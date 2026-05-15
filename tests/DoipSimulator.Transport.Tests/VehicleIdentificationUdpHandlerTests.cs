using System.Net;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Protocols.Doip;
using DoipSimulator.Transport.Udp;

namespace DoipSimulator.Transport.Tests;

public class VehicleIdentificationUdpHandlerTests
{
    private static readonly DoipEntityInfo Entity = DoipEntityInfo.Create(
        "LTEST123456789012",
        "010203040506",
        "A1A2A3A4A5A6",
        "0x0E01");

    [Fact]
    public async Task BasicVehicleIdentificationRequestReturnsConfiguredEntityResponse()
    {
        var events = new CapturingEventSink();
        var codec = new DoipCodec();
        var handler = new VehicleIdentificationUdpHandler(Entity, codec, new RuntimeEventBus([events]));
        var request = CreateRequest(codec, DoipPayloadType.VehicleIdentificationRequest, []);

        var outbound = await handler.HandleAsync(new InboundDatagram(request, LoopbackEndpoint()));

        Assert.Single(outbound);
        var decoded = codec.Decode(outbound[0].Payload);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(DoipPayloadType.VehicleAnnouncementMessage, decoded.Value!.PayloadType);
        var payload = VehicleIdentificationPayload.Decode(decoded.Value.Payload);
        Assert.Equal(Entity.Vin, payload.Vin);
        Assert.Equal(Entity.LogicalAddress, payload.LogicalAddress);
        Assert.Equal(Entity.Eid, payload.Eid);
        Assert.Equal(Entity.Gid, payload.Gid);
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.udp.vehicle_identification.requested");
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.udp.vehicle_identification.responded");
    }

    [Fact]
    public async Task DirectedEidAndVinRequestsOnlyRespondWhenTheyMatch()
    {
        var codec = new DoipCodec();
        var handler = new VehicleIdentificationUdpHandler(Entity, codec);

        var eidMatch = await handler.HandleAsync(new InboundDatagram(
            CreateRequest(codec, DoipPayloadType.VehicleIdentificationRequestWithEid, Entity.Eid),
            LoopbackEndpoint()));
        var vinMatch = await handler.HandleAsync(new InboundDatagram(
            CreateRequest(codec, DoipPayloadType.VehicleIdentificationRequestWithVin, System.Text.Encoding.ASCII.GetBytes(Entity.Vin)),
            LoopbackEndpoint()));
        var eidMiss = await handler.HandleAsync(new InboundDatagram(
            CreateRequest(codec, DoipPayloadType.VehicleIdentificationRequestWithEid, [0, 0, 0, 0, 0, 1]),
            LoopbackEndpoint()));

        Assert.Single(eidMatch);
        Assert.Single(vinMatch);
        Assert.Empty(eidMiss);
    }

    [Fact]
    public async Task CodecValidationErrorPublishesDoipWarningAndDoesNotThrow()
    {
        var events = new CapturingEventSink();
        var handler = new VehicleIdentificationUdpHandler(
            Entity,
            new DoipCodec(),
            new RuntimeEventBus([events]));

        var outbound = await handler.HandleAsync(new InboundDatagram([0x02], LoopbackEndpoint()));

        Assert.Empty(outbound);
        var error = Assert.Single(events.Events);
        Assert.Equal(RuntimeEventCategory.Doip, error.Category);
        Assert.Equal(RuntimeEventLevel.Warning, error.Level);
        Assert.Equal("doip.udp.protocol_error", error.Name);
    }

    [Fact]
    public async Task InvalidVehicleIdentificationPayloadPublishesDoipWarningAndDoesNotRespond()
    {
        var events = new CapturingEventSink();
        var codec = new DoipCodec();
        var handler = new VehicleIdentificationUdpHandler(
            Entity,
            codec,
            new RuntimeEventBus([events]));

        var outbound = await handler.HandleAsync(new InboundDatagram(
            CreateRequest(codec, DoipPayloadType.VehicleIdentificationRequestWithEid, [0x01]),
            LoopbackEndpoint()));

        Assert.Empty(outbound);
        Assert.Contains(events.Events, runtimeEvent =>
            runtimeEvent.Name == "doip.udp.protocol_error"
            && Equals(runtimeEvent.Data?["errorCode"], "InvalidVehicleIdentificationPayload"));
    }

    private static byte[] CreateRequest(DoipCodec codec, DoipPayloadType payloadType, byte[] payload)
    {
        var encoded = codec.Encode(DoipFrame.Create(DoipCodec.Iso13400ProtocolVersion, payloadType, payload));
        Assert.True(encoded.IsSuccess);
        return encoded.Value!;
    }

    private static IPEndPoint LoopbackEndpoint() => new(IPAddress.Loopback, 50000);
}
