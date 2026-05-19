using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Protocols.Uds.Plugins;

namespace DoipSimulator.Protocols.Uds;

public sealed class SecurityAccessService : IUdsService
{
    public const byte Sid = 0x27;

    private readonly Dictionary<byte, SecurityAccessConfig> seedSubFunctions;
    private readonly Dictionary<byte, SecurityAccessConfig> keySubFunctions;
    private readonly EcuRuntimeState ecuState;
    private readonly Func<DateTimeOffset> nowProvider;
    private readonly IRuntimeEventPublisher eventPublisher;
    private readonly ISecurityAccessAlgorithm algorithm;
    private readonly string? pluginLoadError;

    public SecurityAccessService(
        SimulatorConfig config,
        EcuRuntimeState ecuState,
        IRuntimeEventPublisher? eventPublisher = null,
        Func<DateTimeOffset>? nowProvider = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        this.ecuState = ecuState;
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
        this.nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
        var pluginLoad = SecurityPluginLoader.Load(config.SecurityPlugin, this.eventPublisher);
        pluginLoadError = config.SecurityPlugin.Enabled ? pluginLoad.Error : null;
        algorithm = pluginLoad.IsAvailable && pluginLoad.Algorithm is not null
            ? pluginLoad.Algorithm
            : new BuiltInSecurityAccessAlgorithm();

        seedSubFunctions = [];
        keySubFunctions = [];
        foreach (var level in config.Uds.SecurityAccess)
        {
            if (!ConfigValidator.TryParseByteHex(level.SeedSubFunction, out var seedSubFunction)
                || !ConfigValidator.TryParseByteHex(level.KeySubFunction, out var keySubFunction))
            {
                continue;
            }

            ecuState.EnsureSecurityLevel(level.Level);
            seedSubFunctions[seedSubFunction] = level;
            keySubFunctions[keySubFunction] = level;
        }
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length < 1)
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat)];
        }

        var subFunction = request.Payload[0];
        if (seedSubFunctions.TryGetValue(subFunction, out var seedLevel))
        {
            return await HandleSeedRequestAsync(request, context, seedLevel, subFunction, cancellationToken);
        }

        if (keySubFunctions.TryGetValue(subFunction, out var keyLevel))
        {
            return await HandleKeyRequestAsync(request, context, keyLevel, subFunction, cancellationToken);
        }

        await PublishSecurityEventAsync(context, subFunction, null, "rejected", "unsupported sub-function", cancellationToken);
        return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.SubFunctionNotSupported)];
    }

    public static byte[] ComputeExpectedKey(SecurityAccessConfig config, IReadOnlyList<byte> seed)
    {
        if (!DidRuntimeStore.TryParseHexBytes(config.AlgorithmParameter, out var parameter) || parameter is null)
        {
            throw new InvalidOperationException("SecurityAccess algorithm parameter is invalid.");
        }

        if (parameter.Length == 0)
        {
            throw new InvalidOperationException("SecurityAccess algorithm parameter must not be empty.");
        }

        if (string.Equals(config.Algorithm, "builtin-xor", StringComparison.OrdinalIgnoreCase))
        {
            return ApplySeedParameter(seed, parameter, (seedByte, parameterByte) => (byte)(seedByte ^ parameterByte));
        }

        if (string.Equals(config.Algorithm, "builtin-add", StringComparison.OrdinalIgnoreCase))
        {
            return ApplySeedParameter(seed, parameter, (seedByte, parameterByte) => unchecked((byte)(seedByte + parameterByte)));
        }

        throw new InvalidOperationException($"Unsupported SecurityAccess algorithm '{config.Algorithm}'.");
    }

    private async ValueTask<IReadOnlyList<UdsResponse>> HandleSeedRequestAsync(
        UdsRequest request,
        UdsContext context,
        SecurityAccessConfig level,
        byte subFunction,
        CancellationToken cancellationToken)
    {
        if (request.Payload.Length != 1)
        {
            return await RejectAsync(context, request.OriginalServiceId, subFunction, level.Level, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat, "invalid seed request format", cancellationToken);
        }

        var now = nowProvider();
        ecuState.ResetSecurityLockoutIfExpired(level.Level, now);
        if (ecuState.IsSecurityLevelLockedOut(level.Level, now))
        {
            return await RejectAsync(context, request.OriginalServiceId, subFunction, level.Level, NegativeResponseCode.RequiredTimeDelayNotExpired, "lockout active", cancellationToken);
        }

        if (pluginLoadError is not null)
        {
            return await RejectAsync(context, request.OriginalServiceId, subFunction, level.Level, NegativeResponseCode.ConditionsNotCorrect, pluginLoadError, cancellationToken);
        }

        var seedResult = algorithm.GenerateSeed(level, subFunction);
        if (seedResult.Status != SecurityPluginStatus.Success || seedResult.Seed.Length == 0)
        {
            return await RejectAsync(
                context,
                request.OriginalServiceId,
                subFunction,
                level.Level,
                NegativeResponseCode.ConditionsNotCorrect,
                seedResult.Reason ?? "security plugin seed generation failed",
                cancellationToken);
        }

        var seed = ecuState.StoreSecuritySeed(level.Level, seedResult.Seed);
        await PublishSecurityEventAsync(context, subFunction, level.Level, "seed-issued", null, cancellationToken);
        return [new RawUdsResponse([0x67, subFunction, .. seed])];
    }

    private async ValueTask<IReadOnlyList<UdsResponse>> HandleKeyRequestAsync(
        UdsRequest request,
        UdsContext context,
        SecurityAccessConfig level,
        byte subFunction,
        CancellationToken cancellationToken)
    {
        if (request.Payload.Length < 2)
        {
            return await RejectAsync(context, request.OriginalServiceId, subFunction, level.Level, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat, "invalid key request format", cancellationToken);
        }

        var now = nowProvider();
        ecuState.ResetSecurityLockoutIfExpired(level.Level, now);
        if (ecuState.IsSecurityLevelLockedOut(level.Level, now))
        {
            return await RejectAsync(context, request.OriginalServiceId, subFunction, level.Level, NegativeResponseCode.RequiredTimeDelayNotExpired, "lockout active", cancellationToken);
        }

        if (!ecuState.TryGetSecuritySeed(level.Level, out var seed))
        {
            return await RejectAsync(context, request.OriginalServiceId, subFunction, level.Level, NegativeResponseCode.ConditionsNotCorrect, "seed request required first", cancellationToken);
        }

        var suppliedKey = request.Payload[1..];
        var verification = algorithm.VerifyKey(level, seed, suppliedKey);
        if (verification.Status == SecurityPluginStatus.Failure)
        {
            return await RejectAsync(
                context,
                request.OriginalServiceId,
                subFunction,
                level.Level,
                NegativeResponseCode.ConditionsNotCorrect,
                verification.Reason ?? "security plugin key verification failed",
                cancellationToken);
        }

        if (verification.Status == SecurityPluginStatus.InvalidKey)
        {
            var attempts = ecuState.RecordSecurityKeyFailure(
                level.Level,
                level.MaxFailedAttempts,
                TimeSpan.FromMilliseconds(level.LockoutMs),
                now);
            var code = attempts >= level.MaxFailedAttempts
                ? NegativeResponseCode.ExceedNumberOfAttempts
                : NegativeResponseCode.InvalidKey;
            var reason = attempts >= level.MaxFailedAttempts ? "lockout entered" : "invalid key";
            return await RejectAsync(context, request.OriginalServiceId, subFunction, level.Level, code, reason, cancellationToken);
        }

        ecuState.MarkSecurityLevelUnlocked(level.Level);
        await PublishSecurityEventAsync(context, subFunction, level.Level, "unlocked", null, cancellationToken);
        return [new RawUdsResponse([0x67, subFunction])];
    }

    private async ValueTask<IReadOnlyList<UdsResponse>> RejectAsync(
        UdsContext context,
        byte originalServiceId,
        byte subFunction,
        int? level,
        NegativeResponseCode code,
        string reason,
        CancellationToken cancellationToken)
    {
        await PublishSecurityEventAsync(context, subFunction, level, "rejected", reason, cancellationToken);
        return [new NegativeResponse(originalServiceId, code)];
    }

    private ValueTask PublishSecurityEventAsync(
        UdsContext context,
        byte subFunction,
        int? level,
        string outcome,
        string? reason,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                outcome == "rejected" ? RuntimeEventLevel.Warning : RuntimeEventLevel.Info,
                RuntimeEventCategory.Uds,
                "uds.securityAccess.processed",
                "SecurityAccess request processed.",
                context.ConnectionId,
                new Dictionary<string, object?>
                {
                    ["serviceId"] = "0x27",
                    ["subFunction"] = $"0x{subFunction:X2}",
                    ["securityLevel"] = level,
                    ["outcome"] = outcome,
                    ["reason"] = reason,
                }),
            cancellationToken);
    }

    private static byte[] ApplySeedParameter(
        IReadOnlyList<byte> seed,
        byte[] parameter,
        Func<byte, byte, byte> operation)
    {
        var key = new byte[seed.Count];
        for (var index = 0; index < seed.Count; index++)
        {
            key[index] = operation(seed[index], parameter[index % parameter.Length]);
        }

        return key;
    }
}
