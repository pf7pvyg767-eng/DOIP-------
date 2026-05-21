using System.Globalization;
using System.Buffers.Binary;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Core.Configuration;

public sealed record DidRuntimeSnapshot(
    string Did,
    string? Name,
    string ValueEncoding,
    string Value,
    bool Writable,
    int? ExpectedLength,
    IReadOnlyList<string> AllowedWriteSessions,
    string? RequiredSecurityState,
    DidValueProviderConfig? ValueProvider,
    string PermissionSummary);

public sealed record DidRuntimeSample(
    string Did,
    string? Name,
    string RawValue,
    double? NumericValue,
    string ProviderType,
    DateTimeOffset SampledAt);

public enum DidWriteFailure
{
    None,
    UnknownDid,
    NotWritable,
    UnsupportedEncoding,
    InvalidHex,
    LengthMismatch,
    ConditionsNotCorrect,
    SecurityAccessDenied,
}

public sealed record DidWriteResult(DidWriteFailure Failure, string? Message = null)
{
    public bool Succeeded => Failure == DidWriteFailure.None;

    public static DidWriteResult Success { get; } = new(DidWriteFailure.None);
}

public enum DidProviderUpdateFailure
{
    None,
    UnknownDid,
    InvalidProvider,
}

public sealed record DidProviderUpdateResult(
    DidProviderUpdateFailure Failure,
    DidRuntimeSnapshot? Snapshot = null,
    IReadOnlyList<ConfigValidationError>? Errors = null,
    string? Message = null)
{
    public bool Succeeded => Failure == DidProviderUpdateFailure.None;
}

public sealed class DidRuntimeStore
{
    private readonly Lock gate = new();
    private readonly SimulatorConfig config;
    private readonly string configPath;
    private readonly ConfigStore configStore;
    private readonly IRuntimeEventPublisher eventPublisher;
    private readonly TimeProvider timeProvider;
    private readonly Dictionary<ushort, DidRuntimeEntry> entries;

    public DidRuntimeStore(
        SimulatorConfig config,
        string configPath,
        ConfigStore configStore,
        IRuntimeEventPublisher? eventPublisher = null,
        TimeProvider? timeProvider = null)
    {
        this.config = config;
        this.configPath = configPath;
        this.configStore = configStore;
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        entries = config.Uds.Dids
            .Select(CreateEntry)
            .OfType<DidRuntimeEntry>()
            .GroupBy(entry => entry.Identifier)
            .ToDictionary(group => group.Key, group => group.Last());
    }

    public IReadOnlyList<DidRuntimeSnapshot> List()
    {
        lock (gate)
        {
            return entries.Values
                .OrderBy(entry => entry.Identifier)
                .Select(ToSnapshot)
                .ToArray();
        }
    }

    public bool TryRead(ushort did, out byte[] value)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(did, out var entry))
            {
                value = [];
                return false;
            }

            value = ResolveValue(entry);
            return true;
        }
    }

    public bool TrySample(ushort did, out DidRuntimeSample sample)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(did, out var entry))
            {
                sample = default!;
                return false;
            }

            sample = ToSample(entry, timeProvider.GetUtcNow());
            return true;
        }
    }

    public IReadOnlyList<DidRuntimeSample> ListSamples()
    {
        lock (gate)
        {
            return entries.Values
                .OrderBy(entry => entry.Identifier)
                .Select(entry => ToSample(entry, timeProvider.GetUtcNow()))
                .ToArray();
        }
    }

    public bool TryGetReadSecurityRequirement(ushort did, out int? requiredSecurityLevel, out string? requiredSecurityState)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(did, out var entry))
            {
                requiredSecurityLevel = null;
                requiredSecurityState = null;
                return false;
            }

            requiredSecurityLevel = entry.Config.RequiredSecurityLevel;
            requiredSecurityState = entry.Config.RequiredSecurityState;
            return true;
        }
    }

    public bool Upsert(DidConfig did)
    {
        if (!ConfigValidator.TryParseDidIdentifier(did, out var identifier)
            || !string.Equals(did.ValueEncoding, "hex", StringComparison.OrdinalIgnoreCase)
            || !TryParseHexBytes(did.Value, out var value))
        {
            return false;
        }

        lock (gate)
        {
            entries[identifier] = new DidRuntimeEntry(identifier, did, value!);
            return true;
        }
    }

    public async ValueTask<DidWriteResult> WriteHexAsync(
        ushort did,
        string valueEncoding,
        string value,
        EcuRuntimeState ecuState,
        string source,
        bool persist,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(valueEncoding, "hex", StringComparison.OrdinalIgnoreCase))
        {
            return new DidWriteResult(DidWriteFailure.UnsupportedEncoding, "Only hex DID values are supported.");
        }

        if (!TryParseHexBytes(value, out var bytes))
        {
            return new DidWriteResult(DidWriteFailure.InvalidHex, "DID value must be an even-length hexadecimal byte string.");
        }

        return await WriteBytesAsync(did, bytes!, ecuState, source, persist, cancellationToken);
    }

    public async ValueTask<DidWriteResult> WriteBytesAsync(
        ushort did,
        byte[] value,
        EcuRuntimeState ecuState,
        string source,
        bool persist,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(did, out var entry))
            {
                return new DidWriteResult(DidWriteFailure.UnknownDid, "DID is not configured.");
            }

            if (!entry.Config.Writable || !entry.IsStatic)
            {
                return new DidWriteResult(DidWriteFailure.NotWritable, "DID is not writable.");
            }

            var expectedLength = entry.Config.WriteLength ?? entry.StaticValue!.Length;
            if (value.Length != expectedLength)
            {
                return new DidWriteResult(DidWriteFailure.LengthMismatch, $"DID value must be {expectedLength} bytes.");
            }

            if (!IsSessionAllowed(entry.Config, ecuState.CurrentSession))
            {
                return new DidWriteResult(DidWriteFailure.ConditionsNotCorrect, "Current diagnostic session does not allow DID writes.");
            }

            if (!IsSecurityStateAllowed(entry.Config, ecuState))
            {
                return new DidWriteResult(DidWriteFailure.SecurityAccessDenied, "Current security state does not allow DID writes.");
            }

            entry.StaticValue = [.. value];
            if (persist)
            {
                entry.Config.Value = ToHex(value);
            }
        }

        if (persist)
        {
            await configStore.SaveAsync(configPath, config, cancellationToken);
        }

        await PublishDidWriteAsync(did, value.Length, source, persist, cancellationToken);
        return DidWriteResult.Success;
    }

    private static DidRuntimeSnapshot ToSnapshot(DidRuntimeEntry entry)
    {
        var value = entry.ResolveValue();
        return new DidRuntimeSnapshot(
            FormatDid(entry.Identifier),
            entry.Config.Name,
            entry.Config.ValueEncoding,
            ToHex(value),
            entry.Config.Writable,
            entry.Config.WriteLength ?? value.Length,
            entry.Config.AllowedWriteSessions.ToArray(),
            entry.Config.RequiredSecurityState,
            entry.Config.ValueProvider,
            BuildPermissionSummary(entry.Config));
    }

    public async ValueTask<DidProviderUpdateResult> UpdateProviderAsync(
        ushort did,
        DidValueProviderConfig provider,
        bool persist,
        CancellationToken cancellationToken = default)
    {
        DidRuntimeEntry entry;
        lock (gate)
        {
            if (!entries.TryGetValue(did, out var current))
            {
                return new DidProviderUpdateResult(
                    DidProviderUpdateFailure.UnknownDid,
                    Message: "DID is not configured.");
            }

            var updated = BuildProviderUpdatedConfig(current.Config, provider, current.ResolveValue());
            var validation = ValidateProviderUpdate(updated);
            if (!validation.IsValid)
            {
                return new DidProviderUpdateResult(
                    DidProviderUpdateFailure.InvalidProvider,
                    Errors: validation.Errors,
                    Message: "DID value provider configuration is invalid.");
            }

            var nextEntry = CreateEntry(updated);
            if (nextEntry is null)
            {
                return new DidProviderUpdateResult(
                    DidProviderUpdateFailure.InvalidProvider,
                    Message: "DID value provider configuration is invalid.");
            }

            var index = config.Uds.Dids.FindIndex(item =>
                ConfigValidator.TryParseDidIdentifier(item, out var identifier) && identifier == did);
            if (index >= 0)
            {
                config.Uds.Dids[index] = updated;
            }

            entries[did] = nextEntry;
            entry = nextEntry;
        }

        if (persist)
        {
            await configStore.SaveAsync(configPath, config, cancellationToken);
        }

        return new DidProviderUpdateResult(
            DidProviderUpdateFailure.None,
            ToSnapshot(entry));
    }

    private static DidConfig BuildProviderUpdatedConfig(DidConfig current, DidValueProviderConfig provider, byte[] currentValue)
    {
        var type = provider.Type ?? "static";
        var isStatic = string.Equals(type, "static", StringComparison.OrdinalIgnoreCase);
        return new DidConfig
        {
            Id = current.Id,
            Identifier = current.Identifier,
            Name = current.Name,
            ValueEncoding = "hex",
            Value = isStatic ? current.Value ?? ToHex(currentValue) : null,
            ValueProvider = isStatic ? null : provider,
            Writable = isStatic && current.Writable,
            WriteLength = isStatic ? current.WriteLength : null,
            AllowedWriteSessions = isStatic ? current.AllowedWriteSessions.ToList() : [],
            RequiredSecurityState = current.RequiredSecurityState,
            RequiredSecurityLevel = current.RequiredSecurityLevel,
        };
    }

    private static ConfigValidationResult ValidateProviderUpdate(DidConfig did)
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dids = [did];
        return ConfigValidator.Validate(config);
    }

    private static DidRuntimeSample ToSample(DidRuntimeEntry entry, DateTimeOffset sampledAt)
    {
        var resolved = entry.ResolveSampleValue(sampledAt);
        return new DidRuntimeSample(
            FormatDid(entry.Identifier),
            entry.Config.Name,
            ToHex(resolved.Value),
            resolved.NumericValue,
            resolved.ProviderType,
            sampledAt);
    }

    private DidRuntimeEntry? CreateEntry(DidConfig did)
    {
        if (!ConfigValidator.TryParseDidIdentifier(did, out var identifier))
        {
            return null;
        }

        if (ConfigValidator.IsStaticDid(did))
        {
            return string.Equals(did.ValueEncoding, "hex", StringComparison.OrdinalIgnoreCase)
                && TryParseHexBytes(did.Value, out var value)
                    ? new DidRuntimeEntry(identifier, did, value!)
                    : null;
        }

        var provider = did.ValueProvider;
        if (provider is null || !ConfigValidator.TryParseDidNumericType(provider.NumericType, out var numericType))
        {
            return null;
        }

        return new DidRuntimeEntry(
            identifier,
            did,
            new DynamicDidValueProvider(provider, numericType, timeProvider, timeProvider.GetUtcNow()));
    }

    private static byte[] ResolveValue(DidRuntimeEntry entry)
    {
        return entry.ResolveValue();
    }

    private static bool IsSessionAllowed(DidConfig config, DiagnosticSession currentSession)
    {
        if (config.AllowedWriteSessions.Count == 0)
        {
            return true;
        }

        var current = FormatSession(currentSession);
        return config.AllowedWriteSessions.Any(item => string.Equals(item, current, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSecurityStateAllowed(DidConfig config, EcuRuntimeState ecuState)
    {
        if (config.RequiredSecurityLevel is not null)
        {
            return ecuState.IsSecurityLevelUnlocked(config.RequiredSecurityLevel.Value);
        }

        return string.IsNullOrWhiteSpace(config.RequiredSecurityState)
            || (string.Equals(config.RequiredSecurityState, "unlocked", StringComparison.OrdinalIgnoreCase)
                ? ecuState.IsAnySecurityLevelUnlocked()
                : string.Equals(config.RequiredSecurityState, ecuState.SecurityStateSummary, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildPermissionSummary(DidConfig config)
    {
        if (!config.Writable)
        {
            return "read-only";
        }

        var parts = new List<string> { "writable" };
        if (config.AllowedWriteSessions.Count > 0)
        {
            parts.Add($"sessions: {string.Join(", ", config.AllowedWriteSessions)}");
        }

        if (!string.IsNullOrWhiteSpace(config.RequiredSecurityState))
        {
            parts.Add($"security: {config.RequiredSecurityState}");
        }

        return string.Join("; ", parts);
    }

    private ValueTask PublishDidWriteAsync(
        ushort did,
        int valueLength,
        string source,
        bool persist,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Uds,
                "uds.did.write",
                "DID runtime value updated.",
                data: new Dictionary<string, object?>
                {
                    ["did"] = FormatDid(did),
                    ["didId"] = FormatDid(did),
                    ["valueLength"] = valueLength,
                    ["source"] = source,
                    ["persist"] = persist,
                }),
            cancellationToken);
    }

    public static bool TryParseHexBytes(string? value, out byte[]? bytes)
    {
        bytes = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length % 2 != 0 || !value.All(Uri.IsHexDigit))
        {
            return false;
        }

        bytes = new byte[value.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = byte.Parse(
                value.AsSpan(index * 2, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }

        return true;
    }

    public static string FormatDid(ushort did) => $"0x{did:X4}";

    public static string ToHex(IReadOnlyList<byte> bytes)
    {
        return string.Concat(bytes.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static string FormatSession(DiagnosticSession session)
    {
        return session switch
        {
            DiagnosticSession.Default => "default",
            DiagnosticSession.Programming => "programming",
            DiagnosticSession.Extended => "extended",
            _ => session.ToString().ToLowerInvariant(),
        };
    }

    private sealed class DidRuntimeEntry
    {
        public DidRuntimeEntry(ushort identifier, DidConfig config, byte[] value)
        {
            Identifier = identifier;
            Config = config;
            StaticValue = value;
        }

        public DidRuntimeEntry(ushort identifier, DidConfig config, DynamicDidValueProvider provider)
        {
            Identifier = identifier;
            Config = config;
            Provider = provider;
        }

        public ushort Identifier { get; }

        public DidConfig Config { get; }

        public byte[]? StaticValue { get; set; }

        public DynamicDidValueProvider? Provider { get; }

        public bool IsStatic => Provider is null;

        public byte[] ResolveValue()
        {
            return Provider is null ? [.. StaticValue!] : Provider.ReadValue();
        }

        public DidResolvedSampleValue ResolveSampleValue(DateTimeOffset sampledAt)
        {
            if (Provider is null)
            {
                return new DidResolvedSampleValue([.. StaticValue!], null, "static");
            }

            return Provider.ReadSampleValue(sampledAt);
        }
    }

    private sealed record DidResolvedSampleValue(byte[] Value, double? NumericValue, string ProviderType);

    private sealed class DynamicDidValueProvider
    {
        private readonly DidValueProviderConfig config;
        private readonly DidNumericType numericType;
        private readonly TimeProvider timeProvider;
        private readonly DateTimeOffset startedAt;
        private readonly Random random;

        public DynamicDidValueProvider(
            DidValueProviderConfig config,
            DidNumericType numericType,
            TimeProvider timeProvider,
            DateTimeOffset startedAt)
        {
            this.config = config;
            this.numericType = numericType;
            this.timeProvider = timeProvider;
            this.startedAt = startedAt;
            random = config.Seed is { } seed ? new Random(seed) : new Random();
        }

        public byte[] ReadValue()
        {
            return ReadSampleValue(timeProvider.GetUtcNow()).Value;
        }

        public DidResolvedSampleValue ReadSampleValue(DateTimeOffset sampledAt)
        {
            var numericValue = ResolveNumericValue(sampledAt);
            var encodedNumericValue = ClampAndRoundNumericValue(numericValue, numericType);
            return new DidResolvedSampleValue(
                EncodeNumericValue(encodedNumericValue, numericType),
                encodedNumericValue,
                (config.Type ?? "static").ToLowerInvariant());
        }

        private double ResolveNumericValue(DateTimeOffset sampledAt)
        {
            var type = config.Type ?? "static";
            if (string.Equals(type, "random", StringComparison.OrdinalIgnoreCase))
            {
                var min = (long)Math.Round(config.Min!.Value, MidpointRounding.AwayFromZero);
                var max = (long)Math.Round(config.Max!.Value, MidpointRounding.AwayFromZero);
                return random.NextInt64(min, max + 1);
            }

            if (string.Equals(type, "sine", StringComparison.OrdinalIgnoreCase))
            {
                var elapsedMilliseconds = (sampledAt - startedAt).TotalMilliseconds;
                var angle = 2 * Math.PI * (elapsedMilliseconds / config.PeriodMs!.Value);
                return config.Offset!.Value + config.Amplitude!.Value * Math.Sin(angle);
            }

            if (string.Equals(type, "linear", StringComparison.OrdinalIgnoreCase))
            {
                var elapsedSeconds = (sampledAt - startedAt).TotalSeconds;
                return config.Offset!.Value + config.SlopePerSecond!.Value * elapsedSeconds;
            }

            return 0;
        }
    }

    private static byte[] EncodeNumericValue(double value, DidNumericType numericType)
    {
        var rounded = ClampAndRoundNumericValue(value, numericType);
        var bytes = new byte[ConfigValidator.GetDidNumericTypeByteLength(numericType)];
        switch (numericType)
        {
            case DidNumericType.UInt8:
                bytes[0] = (byte)rounded;
                break;
            case DidNumericType.UInt16:
                BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)rounded);
                break;
            case DidNumericType.Int16:
                BinaryPrimitives.WriteInt16BigEndian(bytes, (short)rounded);
                break;
            case DidNumericType.UInt32:
                BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)rounded);
                break;
            case DidNumericType.Int32:
                BinaryPrimitives.WriteInt32BigEndian(bytes, (int)rounded);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(numericType));
        }

        return bytes;
    }

    private static double ClampAndRoundNumericValue(double value, DidNumericType numericType)
    {
        var range = ConfigValidator.GetDidNumericTypeRange(numericType);
        return Math.Round(Math.Clamp(value, range.Min, range.Max), MidpointRounding.AwayFromZero);
    }
}
