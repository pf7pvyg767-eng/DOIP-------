using System.Net;
using System.Text;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Protocols.Doip;

namespace DoipSimulator.Transport.Udp;

public sealed class VehicleIdentificationUdpHandler : IDoipUdpHandler
{
    private readonly DoipEntityInfo entityInfo;
    private readonly IDoipCodec codec;
    private readonly IRuntimeEventPublisher eventPublisher;

    public VehicleIdentificationUdpHandler(
        DoipEntityInfo entityInfo,
        IDoipCodec codec,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        this.entityInfo = entityInfo;
        this.codec = codec;
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
    }

    public async ValueTask<IReadOnlyList<OutboundDatagram>> HandleAsync(
        InboundDatagram datagram,
        CancellationToken cancellationToken = default)
    {
        var decoded = codec.Decode(datagram.Payload);
        if (!decoded.IsSuccess || decoded.Value is null)
        {
            await PublishProtocolErrorAsync(datagram.RemoteEndpoint, decoded.Error?.Code.ToString() ?? "DecodeFailed", cancellationToken);
            return [];
        }

        var frame = decoded.Value;
        if (!IsVehicleIdentificationRequest(frame.PayloadType))
        {
            return [];
        }

        await PublishRequestAsync(frame, datagram.RemoteEndpoint, cancellationToken);
        if (!HasValidRequestPayload(frame))
        {
            await PublishProtocolErrorAsync(datagram.RemoteEndpoint, "InvalidVehicleIdentificationPayload", cancellationToken);
            return [];
        }

        if (!IsMatch(frame))
        {
            return [];
        }

        var responsePayload = entityInfo.EncodeVehicleIdentificationPayload();
        var responseFrame = DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.VehicleAnnouncementMessage,
            responsePayload);
        var encoded = codec.Encode(responseFrame);
        if (!encoded.IsSuccess || encoded.Value is null)
        {
            await PublishProtocolErrorAsync(datagram.RemoteEndpoint, encoded.Error?.Code.ToString() ?? "EncodeFailed", cancellationToken);
            return [];
        }

        await PublishResponseAsync(datagram.RemoteEndpoint, cancellationToken);
        return [new OutboundDatagram(encoded.Value, datagram.RemoteEndpoint)];
    }

    public byte[] CreateAnnouncementDatagram()
    {
        var frame = DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.VehicleAnnouncementMessage,
            entityInfo.EncodeVehicleIdentificationPayload());
        var encoded = codec.Encode(frame);

        if (!encoded.IsSuccess || encoded.Value is null)
        {
            throw new InvalidOperationException(encoded.Error?.Message ?? "Failed to encode vehicle announcement.");
        }

        return encoded.Value;
    }

    public ValueTask PublishAnnouncementAsync(IPEndPoint targetEndpoint, CancellationToken cancellationToken = default)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Doip,
                "doip.udp.vehicle_announcement.sent",
                "DoIP UDP vehicle announcement sent.",
                data: IdentityData(targetEndpoint)),
            cancellationToken);
    }

    private static bool IsVehicleIdentificationRequest(DoipPayloadType payloadType)
    {
        return payloadType == DoipPayloadType.VehicleIdentificationRequest
            || payloadType == DoipPayloadType.VehicleIdentificationRequestWithEid
            || payloadType == DoipPayloadType.VehicleIdentificationRequestWithVin;
    }

    private bool IsMatch(DoipFrame frame)
    {
        if (frame.PayloadType == DoipPayloadType.VehicleIdentificationRequest)
        {
            return true;
        }

        if (frame.PayloadType == DoipPayloadType.VehicleIdentificationRequestWithEid)
        {
            return frame.Payload.AsSpan().SequenceEqual(entityInfo.Eid);
        }

        if (frame.PayloadType == DoipPayloadType.VehicleIdentificationRequestWithVin)
        {
            return Encoding.ASCII.GetString(frame.Payload) == entityInfo.Vin;
        }

        return false;
    }

    private static bool HasValidRequestPayload(DoipFrame frame)
    {
        if (frame.PayloadType == DoipPayloadType.VehicleIdentificationRequest)
        {
            return frame.Payload.Length == 0;
        }

        if (frame.PayloadType == DoipPayloadType.VehicleIdentificationRequestWithEid)
        {
            return frame.Payload.Length == 6;
        }

        if (frame.PayloadType == DoipPayloadType.VehicleIdentificationRequestWithVin)
        {
            return frame.Payload.Length == 17;
        }

        return false;
    }

    private async ValueTask PublishRequestAsync(
        DoipFrame frame,
        IPEndPoint remoteEndpoint,
        CancellationToken cancellationToken)
    {
        await eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Doip,
                "doip.udp.vehicle_identification.requested",
                "DoIP UDP vehicle identification request received.",
                data: new Dictionary<string, object?>
                {
                    ["remoteEndpoint"] = remoteEndpoint.ToString(),
                    ["payloadType"] = frame.PayloadType.KnownName ?? $"0x{frame.PayloadTypeRawValue:X4}",
                }),
            cancellationToken);
    }

    private ValueTask PublishResponseAsync(IPEndPoint remoteEndpoint, CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Doip,
                "doip.udp.vehicle_identification.responded",
                "DoIP UDP vehicle identification response sent.",
                data: IdentityData(remoteEndpoint)),
            cancellationToken);
    }

    private ValueTask PublishProtocolErrorAsync(
        IPEndPoint remoteEndpoint,
        string errorCode,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Warning,
                RuntimeEventCategory.Doip,
                "doip.udp.protocol_error",
                "DoIP UDP datagram rejected.",
                data: new Dictionary<string, object?>
                {
                    ["remoteEndpoint"] = remoteEndpoint.ToString(),
                    ["errorCode"] = errorCode,
                }),
            cancellationToken);
    }

    private Dictionary<string, object?> IdentityData(IPEndPoint endpoint)
    {
        return new Dictionary<string, object?>
        {
            ["vin"] = entityInfo.Vin,
            ["logicalAddress"] = $"0x{entityInfo.LogicalAddress:X4}",
            ["eid"] = Convert.ToHexString(entityInfo.Eid),
            ["gid"] = Convert.ToHexString(entityInfo.Gid),
            ["remoteEndpoint"] = endpoint.ToString(),
        };
    }
}
