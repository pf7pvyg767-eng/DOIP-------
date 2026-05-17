using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds;

public sealed class RoutineControlService : IUdsService
{
    public const byte Sid = 0x31;
    public const byte StartRoutine = 0x01;
    public const byte StopRoutine = 0x02;
    public const byte RequestRoutineResults = 0x03;

    private readonly Dictionary<ushort, RoutineConfig> routines;
    private readonly EcuRuntimeState ecuState;
    private readonly IRuntimeEventPublisher eventPublisher;

    public RoutineControlService(
        SimulatorConfig config,
        EcuRuntimeState ecuState,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        this.ecuState = ecuState;
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
        routines = config.Uds.Routines
            .Where(item => ConfigValidator.TryParseRoutineIdentifier(item, out _))
            .Select(item =>
            {
                ConfigValidator.TryParseRoutineIdentifier(item, out var routineId);
                return new KeyValuePair<ushort, RoutineConfig>(routineId, item);
            })
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.Last().Value);
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length < 3)
        {
            return await RejectAsync(context, request.OriginalServiceId, null, null, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat, "invalid format", cancellationToken);
        }

        var controlType = request.Payload[0];
        if (controlType is not StartRoutine and not StopRoutine and not RequestRoutineResults)
        {
            return await RejectAsync(context, request.OriginalServiceId, null, controlType, NegativeResponseCode.SubFunctionNotSupported, "unsupported control type", cancellationToken);
        }

        var routineId = (ushort)((request.Payload[1] << 8) | request.Payload[2]);
        if (!routines.TryGetValue(routineId, out var routine))
        {
            return await RejectAsync(context, request.OriginalServiceId, routineId, controlType, NegativeResponseCode.RequestOutOfRange, "unknown routine", cancellationToken);
        }

        if (!IsSessionAllowed(routine, ecuState.CurrentSession))
        {
            return await RejectAsync(context, request.OriginalServiceId, routineId, controlType, NegativeResponseCode.ConditionsNotCorrect, "session not allowed", cancellationToken);
        }

        if (!IsSecurityAllowed(routine, ecuState))
        {
            return await RejectAsync(context, request.OriginalServiceId, routineId, controlType, NegativeResponseCode.SecurityAccessDenied, "security state not allowed", cancellationToken);
        }

        var responsePayload = ResolveFixedResponse(routine, controlType);
        if (!DidRuntimeStore.TryParseHexBytes(responsePayload, out var bytes))
        {
            return await RejectAsync(context, request.OriginalServiceId, routineId, controlType, NegativeResponseCode.RequestOutOfRange, "fixed response not configured", cancellationToken);
        }

        await PublishRoutineEventAsync(context, routineId, controlType, "accepted", null, cancellationToken);
        return [new RawUdsResponse([0x71, controlType, request.Payload[1], request.Payload[2], .. bytes!])];
    }

    private async ValueTask<IReadOnlyList<UdsResponse>> RejectAsync(
        UdsContext context,
        byte originalServiceId,
        ushort? routineId,
        byte? controlType,
        NegativeResponseCode code,
        string reason,
        CancellationToken cancellationToken)
    {
        await PublishRoutineEventAsync(context, routineId, controlType, "rejected", reason, cancellationToken);
        return [new NegativeResponse(originalServiceId, code)];
    }

    private ValueTask PublishRoutineEventAsync(
        UdsContext context,
        ushort? routineId,
        byte? controlType,
        string outcome,
        string? reason,
        CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, object?>
        {
            ["serviceId"] = "0x31",
            ["routineId"] = routineId is null ? null : ConfigValidator.FormatRoutineIdentifier(routineId.Value),
            ["controlType"] = controlType is null ? null : FormatControlType(controlType.Value),
            ["outcome"] = outcome,
            ["reason"] = reason,
        };

        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                outcome == "accepted" ? RuntimeEventLevel.Info : RuntimeEventLevel.Warning,
                RuntimeEventCategory.Uds,
                "uds.routineControl.invoked",
                "RoutineControl MVP request processed.",
                context.ConnectionId,
                data),
            cancellationToken);
    }

    private static string? ResolveFixedResponse(RoutineConfig routine, byte controlType)
    {
        return controlType switch
        {
            StartRoutine => routine.FixedResponses.Start,
            StopRoutine => routine.FixedResponses.Stop,
            RequestRoutineResults => routine.FixedResponses.RequestResults,
            _ => null,
        };
    }

    private static bool IsSessionAllowed(RoutineConfig routine, DiagnosticSession currentSession)
    {
        if (routine.AllowedSessions.Count == 0)
        {
            return true;
        }

        var current = FormatSession(currentSession);
        return routine.AllowedSessions.Any(item => string.Equals(item, current, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSecurityAllowed(RoutineConfig routine, EcuRuntimeState ecuState)
    {
        if (routine.RequiredSecurityLevel is not null)
        {
            return ecuState.IsSecurityLevelUnlocked(routine.RequiredSecurityLevel.Value);
        }

        return string.IsNullOrWhiteSpace(routine.RequiredSecurityState)
            || (string.Equals(routine.RequiredSecurityState, "unlocked", StringComparison.OrdinalIgnoreCase)
                ? ecuState.IsAnySecurityLevelUnlocked()
                : string.Equals(routine.RequiredSecurityState, ecuState.SecurityStateSummary, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatControlType(byte controlType)
    {
        return controlType switch
        {
            StartRoutine => "startRoutine",
            StopRoutine => "stopRoutine",
            RequestRoutineResults => "requestRoutineResults",
            _ => $"unsupported(0x{controlType:X2})",
        };
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
}
