using System.Globalization;
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
    string PermissionSummary);

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

public sealed class DidRuntimeStore
{
    private readonly Lock gate = new();
    private readonly SimulatorConfig config;
    private readonly string configPath;
    private readonly ConfigStore configStore;
    private readonly IRuntimeEventPublisher eventPublisher;
    private readonly Dictionary<ushort, DidRuntimeEntry> entries;

    public DidRuntimeStore(
        SimulatorConfig config,
        string configPath,
        ConfigStore configStore,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        this.config = config;
        this.configPath = configPath;
        this.configStore = configStore;
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
        entries = config.Uds.Dids
            .Where(did => ConfigValidator.TryParseDidIdentifier(did, out _)
                && string.Equals(did.ValueEncoding, "hex", StringComparison.OrdinalIgnoreCase)
                && TryParseHexBytes(did.Value, out _))
            .Select(did =>
            {
                ConfigValidator.TryParseDidIdentifier(did, out var identifier);
                TryParseHexBytes(did.Value, out var value);
                return new DidRuntimeEntry(identifier, did, value!);
            })
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

            value = [.. entry.Value];
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

            if (!entry.Config.Writable)
            {
                return new DidWriteResult(DidWriteFailure.NotWritable, "DID is not writable.");
            }

            var expectedLength = entry.Config.WriteLength ?? entry.Value.Length;
            if (value.Length != expectedLength)
            {
                return new DidWriteResult(DidWriteFailure.LengthMismatch, $"DID value must be {expectedLength} bytes.");
            }

            if (!IsSessionAllowed(entry.Config, ecuState.CurrentSession))
            {
                return new DidWriteResult(DidWriteFailure.ConditionsNotCorrect, "Current diagnostic session does not allow DID writes.");
            }

            if (!IsSecurityStateAllowed(entry.Config, ecuState.SecurityStateSummary))
            {
                return new DidWriteResult(DidWriteFailure.SecurityAccessDenied, "Current security state does not allow DID writes.");
            }

            entry.Value = [.. value];
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
        return new DidRuntimeSnapshot(
            FormatDid(entry.Identifier),
            entry.Config.Name,
            entry.Config.ValueEncoding,
            ToHex(entry.Value),
            entry.Config.Writable,
            entry.Config.WriteLength ?? entry.Value.Length,
            entry.Config.AllowedWriteSessions.ToArray(),
            entry.Config.RequiredSecurityState,
            BuildPermissionSummary(entry.Config));
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

    private static bool IsSecurityStateAllowed(DidConfig config, string currentSecurityState)
    {
        return string.IsNullOrWhiteSpace(config.RequiredSecurityState)
            || string.Equals(config.RequiredSecurityState, currentSecurityState, StringComparison.OrdinalIgnoreCase);
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
            Value = value;
        }

        public ushort Identifier { get; }

        public DidConfig Config { get; }

        public byte[] Value { get; set; }
    }
}
