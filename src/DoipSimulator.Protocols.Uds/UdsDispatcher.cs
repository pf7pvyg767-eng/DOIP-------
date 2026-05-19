using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using System.Globalization;

namespace DoipSimulator.Protocols.Uds;

public interface IUdsDispatcher
{
    ValueTask<IReadOnlyList<UdsResponse>> DispatchAsync(
        ReadOnlyMemory<byte> payload,
        UdsContext context,
        CancellationToken cancellationToken = default);

    ValueTask NotifyConnectionClosedAsync(
        UdsContext context,
        CancellationToken cancellationToken = default);
}

public sealed class UdsDispatcher : IUdsDispatcher
{
    private readonly Dictionary<byte, IUdsService> services;
    private readonly IRuntimeEventPublisher eventPublisher;
    private readonly IReadOnlyDictionary<byte, ServiceResponseDelayConfig> responseDelays;
    private readonly EcuRuntimeState? ecuRuntimeState;
    private readonly TesterPresentTimeoutConfig testerPresentTimeout;
    private readonly TimeProvider timeProvider;

    public UdsDispatcher(
        IEnumerable<IUdsService>? services = null,
        IRuntimeEventPublisher? eventPublisher = null,
        SimulatorConfig? config = null,
        EcuRuntimeState? ecuRuntimeState = null,
        TimeProvider? timeProvider = null)
    {
        this.services = [];
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
        responseDelays = BuildResponseDelays(config);
        this.ecuRuntimeState = ecuRuntimeState;
        testerPresentTimeout = config?.Uds?.TesterPresentTimeout ?? new TesterPresentTimeoutConfig();
        this.timeProvider = timeProvider ?? TimeProvider.System;

        foreach (var service in services ?? [])
        {
            Register(service);
        }
    }

    public void Register(IUdsService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (!services.TryAdd(service.ServiceId, service))
        {
            throw new InvalidOperationException($"UDS service 0x{service.ServiceId:X2} is already registered.");
        }
    }

    public async ValueTask<IReadOnlyList<UdsResponse>> DispatchAsync(
        ReadOnlyMemory<byte> payload,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        await EvaluateTesterPresentTimeoutAsync(context, cancellationToken);

        if (!UdsRequest.TryCreate(payload.Span, out var request) || request is null)
        {
            var response = new NegativeResponse(0x00, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat);
            await PublishErrorAsync(context, "uds.dispatch.invalid_format", "UDS request payload is empty or invalid.", 0x00, response.Code, cancellationToken);
            await PublishResponseAsync(context, response, cancellationToken);
            return [response];
        }

        await PublishRequestAsync(context, request, cancellationToken);

        if (!services.TryGetValue(request.ServiceId, out var service))
        {
            var response = new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.ServiceNotSupported);
            await PublishErrorAsync(context, "uds.dispatch.unsupported_service", "UDS service is not registered.", request.ServiceId, response.Code, cancellationToken);
            await PublishResponseAsync(context, response, cancellationToken);
            return [response];
        }

        var serviceResponses = await service.HandleAsync(request, context, cancellationToken);
        var responses = await ApplyResponseDelayAsync(request.ServiceId, serviceResponses, context, cancellationToken);
        foreach (var response in responses)
        {
            await PublishResponseAsync(context, response, cancellationToken);
        }

        return responses;
    }

    public ValueTask NotifyConnectionClosedAsync(
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (ecuRuntimeState is null || !ecuRuntimeState.ClearFlashDownload())
        {
            return ValueTask.CompletedTask;
        }

        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Warning,
                RuntimeEventCategory.Uds,
                "uds.flash.download.cancelled",
                "Flash download state cleared because the diagnostic connection closed.",
                context.ConnectionId,
                CreateEventData(context, new Dictionary<string, object?>
                {
                    ["outcome"] = "cancelled",
                    ["reason"] = "connection-closed",
                    ["active"] = false,
                })),
            cancellationToken);
    }

    private async ValueTask<IReadOnlyList<UdsResponse>> ApplyResponseDelayAsync(
        byte serviceId,
        IReadOnlyList<UdsResponse> serviceResponses,
        UdsContext context,
        CancellationToken cancellationToken)
    {
        if (!responseDelays.TryGetValue(serviceId, out var delay))
        {
            return serviceResponses;
        }

        var responses = new List<UdsResponse>();
        if (delay.ResponsePending.Enabled)
        {
            var pending = AddDelay(
                new NegativeResponse(serviceId, NegativeResponseCode.ResponsePending),
                delay.InitialDelayMs);
            responses.Add(pending);
            await PublishTimingEventAsync(
                context,
                "uds.response_pending.sent",
                "UDS ResponsePending produced before delayed final response.",
                serviceId,
                delay,
                cancellationToken);
        }

        responses.AddRange(serviceResponses.Select(response => AddDelay(response, delay.FinalDelayMs)));
        return responses;
    }

    private async ValueTask EvaluateTesterPresentTimeoutAsync(
        UdsContext context,
        CancellationToken cancellationToken)
    {
        if (ecuRuntimeState is null || !testerPresentTimeout.Enabled)
        {
            return;
        }

        var result = ecuRuntimeState.EvaluateTesterPresentTimeout(
            true,
            TimeSpan.FromMilliseconds(testerPresentTimeout.TimeoutMs),
            timeProvider.GetUtcNow());
        if (!result.FellBack)
        {
            return;
        }

        var data = CreateEventData(context, new Dictionary<string, object?>
        {
            ["previousSession"] = FormatSession(result.PreviousSession),
            ["newSession"] = FormatSession(result.CurrentSession),
            ["currentSession"] = FormatSession(result.CurrentSession),
            ["reason"] = result.Reason,
            ["lastTesterPresentAt"] = result.LastAcceptedAt,
            ["timeoutDeadline"] = result.TimeoutDeadline,
        });

        await eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Warning,
                RuntimeEventCategory.Uds,
                "uds.tester_present.timeout",
                "TesterPresent timeout caused diagnostic session fallback.",
                context.ConnectionId,
                data),
            cancellationToken);

        await eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Warning,
                RuntimeEventCategory.State,
                "state.session.changed",
                "ECU diagnostic session changed because TesterPresent timed out.",
                context.ConnectionId,
                data),
            cancellationToken);
    }

    private ValueTask PublishTimingEventAsync(
        UdsContext context,
        string name,
        string message,
        byte serviceId,
        ServiceResponseDelayConfig delay,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Uds,
                name,
                message,
                context.ConnectionId,
                CreateEventData(context, new Dictionary<string, object?>
                {
                    ["serviceId"] = FormatByte(serviceId),
                    ["responsePendingEnabled"] = delay.ResponsePending.Enabled,
                    ["initialDelayMs"] = delay.InitialDelayMs,
                    ["finalDelayMs"] = delay.FinalDelayMs,
                })),
            cancellationToken);
    }

    private ValueTask PublishRequestAsync(UdsContext context, UdsRequest request, CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Uds,
                "uds.request.received",
                "UDS request accepted for dispatch.",
                context.ConnectionId,
                CreateEventData(context, new Dictionary<string, object?>
                {
                    ["serviceId"] = FormatByte(request.ServiceId),
                    ["byteSummary"] = ToHex(ToBytes(request)),
                })),
            cancellationToken);
    }

    private ValueTask PublishResponseAsync(UdsContext context, UdsResponse response, CancellationToken cancellationToken)
    {
        var data = CreateEventData(context, new Dictionary<string, object?>
        {
            ["responseType"] = response.IsNegative ? "negative" : "positive",
            ["responseSid"] = FormatByte(response.ToBytes()[0]),
            ["byteSummary"] = ToHex(response.ToBytes()),
        });

        if (response is NegativeResponse negativeResponse)
        {
            data["originalServiceId"] = FormatByte(negativeResponse.OriginalServiceId);
            data["nrc"] = FormatByte((byte)negativeResponse.Code);
        }

        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Uds,
                "uds.response.sent",
                "UDS response produced by dispatcher.",
                context.ConnectionId,
                data),
            cancellationToken);
    }

    private ValueTask PublishErrorAsync(
        UdsContext context,
        string name,
        string message,
        byte serviceId,
        NegativeResponseCode code,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Warning,
                RuntimeEventCategory.Uds,
                name,
                message,
                context.ConnectionId,
                CreateEventData(context, new Dictionary<string, object?>
                {
                    ["serviceId"] = FormatByte(serviceId),
                    ["nrc"] = FormatByte((byte)code),
                })),
            cancellationToken);
    }

    private static Dictionary<string, object?> CreateEventData(UdsContext context, Dictionary<string, object?> data)
    {
        data["connectionId"] = context.ConnectionId;
        data["remoteEndpoint"] = context.RemoteEndpoint;
        data["testerLogicalAddress"] = context.TesterLogicalAddress;
        data["ecuLogicalAddress"] = context.EcuLogicalAddress;
        return data;
    }

    private static string FormatByte(byte value) => $"0x{value:X2}";

    private static IReadOnlyDictionary<byte, ServiceResponseDelayConfig> BuildResponseDelays(SimulatorConfig? config)
    {
        var delays = new Dictionary<byte, ServiceResponseDelayConfig>();
        foreach (var delay in config?.Uds?.ResponseDelays ?? [])
        {
            if (ConfigValidator.TryParseByteHex(delay.ServiceId, out var serviceId))
            {
                delays[serviceId] = delay;
            }
        }

        return delays;
    }

    private static UdsResponse AddDelay(UdsResponse response, int delayMilliseconds)
    {
        return delayMilliseconds <= 0
            ? response
            : new DelayedUdsResponse(response, TimeSpan.FromMilliseconds(delayMilliseconds));
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

    private static byte[] ToBytes(UdsRequest request) => [request.ServiceId, .. request.Payload];

    private static string ToHex(IReadOnlyList<byte> bytes)
    {
        return string.Join(' ', bytes.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
