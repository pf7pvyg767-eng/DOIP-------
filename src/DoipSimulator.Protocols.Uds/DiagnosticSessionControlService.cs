using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds;

public sealed class DiagnosticSessionControlService : IUdsService
{
    public const byte Sid = 0x10;
    public const ushort BaselineP2Milliseconds = 50;
    public const ushort BaselineP2StarMilliseconds = 5000;

    private readonly EcuRuntimeState state;
    private readonly IReadOnlyDictionary<byte, SessionTiming> sessionTimings;
    private readonly IRuntimeEventPublisher eventPublisher;

    public DiagnosticSessionControlService(
        EcuRuntimeState state,
        SimulatorConfig? config = null,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        this.state = state;
        sessionTimings = BuildSessionTimings(config);
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length != 1)
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat)];
        }

        var subFunction = request.Payload[0];
        if (!TryMapSession(subFunction, out var session))
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.SubFunctionNotSupported)];
        }

        var previous = state.SetSession(session);
        await PublishSessionChangedAsync(context, previous, session, subFunction, cancellationToken);
        var timing = ResolveTiming(subFunction);
        return [new RawUdsResponse([
            0x50,
            subFunction,
            (byte)(timing.P2Milliseconds >> 8),
            (byte)(timing.P2Milliseconds & 0xFF),
            (byte)(timing.P2StarMilliseconds >> 8),
            (byte)(timing.P2StarMilliseconds & 0xFF)])];
    }

    private SessionTiming ResolveTiming(byte subFunction)
    {
        return sessionTimings.TryGetValue(subFunction, out var timing)
            ? timing
            : new SessionTiming(BaselineP2Milliseconds, BaselineP2StarMilliseconds);
    }

    private static IReadOnlyDictionary<byte, SessionTiming> BuildSessionTimings(SimulatorConfig? config)
    {
        var timings = new Dictionary<byte, SessionTiming>();
        foreach (var session in config?.Uds?.Sessions ?? [])
        {
            if (!ConfigValidator.TryParseByteHex(session.Identifier, out var subFunction))
            {
                continue;
            }

            timings[subFunction] = new SessionTiming(
                (ushort)(session.P2Ms ?? BaselineP2Milliseconds),
                (ushort)(session.P2StarMs ?? BaselineP2StarMilliseconds));
        }

        return timings;
    }

    private static bool TryMapSession(byte subFunction, out DiagnosticSession session)
    {
        session = subFunction switch
        {
            0x01 => DiagnosticSession.Default,
            0x02 => DiagnosticSession.Programming,
            0x03 => DiagnosticSession.Extended,
            _ => default,
        };

        return subFunction is 0x01 or 0x02 or 0x03;
    }

    private ValueTask PublishSessionChangedAsync(
        UdsContext context,
        DiagnosticSession previous,
        DiagnosticSession current,
        byte subFunction,
        CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, object?>
        {
            ["connectionId"] = context.ConnectionId,
            ["remoteEndpoint"] = context.RemoteEndpoint,
            ["testerLogicalAddress"] = context.TesterLogicalAddress,
            ["ecuLogicalAddress"] = context.EcuLogicalAddress ?? FormatLogicalAddress(state.LogicalAddress),
            ["previousSession"] = FormatSession(previous),
            ["newSession"] = FormatSession(current),
            ["currentSession"] = FormatSession(current),
            ["subFunction"] = $"0x{subFunction:X2}",
        };

        return PublishSessionChangedEventsAsync(context.ConnectionId, data, cancellationToken);
    }

    private async ValueTask PublishSessionChangedEventsAsync(
        string? connectionId,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken)
    {
        await eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Uds,
                "uds.session.changed",
                "UDS diagnostic session accepted.",
                connectionId,
                data),
            cancellationToken);

        await eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.State,
                "state.session.changed",
                "ECU diagnostic session changed.",
                connectionId,
                data),
            cancellationToken);
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

    private static string FormatLogicalAddress(ushort logicalAddress) => $"0x{logicalAddress:X4}";

    private sealed record SessionTiming(ushort P2Milliseconds, ushort P2StarMilliseconds);
}
