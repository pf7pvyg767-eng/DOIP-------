using System.Net;
using System.Net.Sockets;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Protocols.Doip;
using DoipSimulator.Transport.Udp;

namespace DoipSimulator.Transport.Tests;

public class UdpDoipServerTests
{
    private static readonly DoipEntityInfo Entity = DoipEntityInfo.Create(
        "LTEST123456789012",
        "010203040506",
        "A1A2A3A4A5A6",
        "0x0E01");

    [Fact]
    public async Task LocalUdpClientReceivesVehicleIdentificationResponse()
    {
        var codec = new DoipCodec();
        await using var server = CreateServer(codec, announcementTarget: null);
        await server.StartAsync();

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var request = CreateRequest(codec, DoipPayloadType.VehicleIdentificationRequest, []);
        await client.SendAsync(request, new IPEndPoint(IPAddress.Loopback, server.BoundPort));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.ReceiveAsync(timeout.Token);
        var decoded = codec.Decode(response.Buffer);

        Assert.True(decoded.IsSuccess);
        Assert.Equal(DoipPayloadType.VehicleAnnouncementMessage, decoded.Value!.PayloadType);
        Assert.Equal(Entity.Vin, VehicleIdentificationPayload.Decode(decoded.Value.Payload).Vin);
    }

    [Fact]
    public async Task StopReleasesUdpPort()
    {
        var codec = new DoipCodec();
        await using var server = CreateServer(codec, announcementTarget: null);
        await server.StartAsync();
        var port = server.BoundPort;

        await server.StopAsync();

        using var rebound = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        Assert.Equal(port, ((IPEndPoint)rebound.Client.LocalEndPoint!).Port);
    }

    [Fact]
    public async Task AnnouncementCanBeSentToConfiguredTargetAndLogged()
    {
        var codec = new DoipCodec();
        var events = new CapturingEventSink();
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var receiverEndpoint = (IPEndPoint)receiver.Client.LocalEndPoint!;
        await using var server = CreateServer(
            codec,
            receiverEndpoint,
            announcementEnabled: true,
            eventPublisher: new RuntimeEventBus([events]));

        await server.StartAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var datagram = await receiver.ReceiveAsync(timeout.Token);
        var decoded = codec.Decode(datagram.Buffer);

        Assert.True(decoded.IsSuccess);
        Assert.Equal(DoipPayloadType.VehicleAnnouncementMessage, decoded.Value!.PayloadType);
        Assert.Equal(Entity.Vin, VehicleIdentificationPayload.Decode(decoded.Value.Payload).Vin);
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.udp.vehicle_announcement.sent");
    }

    private static UdpDoipServer CreateServer(
        DoipCodec codec,
        IPEndPoint? announcementTarget,
        bool announcementEnabled = false,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        var handler = new VehicleIdentificationUdpHandler(Entity, codec, eventPublisher);
        return new UdpDoipServer(
            new UdpDoipServerOptions(
                IPAddress.Loopback,
                0,
                announcementEnabled,
                TimeSpan.FromMilliseconds(100),
                announcementTarget),
            handler);
    }

    private static byte[] CreateRequest(DoipCodec codec, DoipPayloadType payloadType, byte[] payload)
    {
        var encoded = codec.Encode(DoipFrame.Create(DoipCodec.Iso13400ProtocolVersion, payloadType, payload));
        Assert.True(encoded.IsSuccess);
        return encoded.Value!;
    }
}
