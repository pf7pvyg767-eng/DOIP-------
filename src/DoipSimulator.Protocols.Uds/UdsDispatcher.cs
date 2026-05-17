using DoipSimulator.Core.RuntimeEvents;
using System.Globalization;

namespace DoipSimulator.Protocols.Uds;

public interface IUdsDispatcher
{
    ValueTask<IReadOnlyList<UdsResponse>> DispatchAsync(
        ReadOnlyMemory<byte> payload,
        UdsContext context,
        CancellationToken cancellationToken = default);
}

public sealed class UdsDispatcher : IUdsDispatcher
{
    private readonly Dictionary<byte, IUdsService> services;
    private readonly IRuntimeEventPublisher eventPublisher;

    public UdsDispatcher(
        IEnumerable<IUdsService>? services = null,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        this.services = [];
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;

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

        var responses = await service.HandleAsync(request, context, cancellationToken);
        foreach (var response in responses)
        {
            await PublishResponseAsync(context, response, cancellationToken);
        }

        return responses;
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

    private static byte[] ToBytes(UdsRequest request) => [request.ServiceId, .. request.Payload];

    private static string ToHex(IReadOnlyList<byte> bytes)
    {
        return string.Join(' ', bytes.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
