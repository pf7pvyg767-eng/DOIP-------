using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Protocols.Doip;
using DoipSimulator.Transport.Tcp;

namespace DoipSimulator.Transport.Tests;

public class TcpDoipServerTests
{
    private readonly DoipCodec codec = new();

    [Fact]
    public void StreamReaderBuffersHalfFrameUntilComplete()
    {
        var reader = new DoipStreamReader(codec);
        var frame = CreateRoutingActivationFrame(0x0E80);
        var split = frame.Length / 2;

        var first = reader.Append(frame.AsSpan(0, split));
        var second = reader.Append(frame.AsSpan(split));

        Assert.Empty(first.Frames);
        Assert.Single(second.Frames);
        Assert.Equal(DoipPayloadType.RoutingActivationRequest, second.Frames[0].PayloadType);
    }

    [Fact]
    public void StreamReaderSplitsStickyFramesInOrder()
    {
        var reader = new DoipStreamReader(codec);
        var first = CreateRoutingActivationFrame(0x0E80);
        var second = CreateAliveCheckFrame();

        var result = reader.Append(first.Concat(second).ToArray());

        Assert.Equal(2, result.Frames.Count);
        Assert.Equal(DoipPayloadType.RoutingActivationRequest, result.Frames[0].PayloadType);
        Assert.Equal(DoipPayloadType.AliveCheckRequest, result.Frames[1].PayloadType);
    }

    [Fact]
    public async Task LocalTcpClientCompletesRoutingActivationAndAliveCheck()
    {
        var events = new CapturingEventSink();
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(
            registry,
            new RuntimeEventBus([events]),
            new HashSet<ushort> { 0x0E80 });
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = client.GetStream();

        await stream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
        var activationResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.RoutingActivationResponse, activationResponse.PayloadType);
        Assert.Equal((byte)RoutingActivationResponseCode.SuccessfullyActivated, activationResponse.Payload[4]);
        Assert.Contains(registry.GetActiveSnapshots(), connection => connection.RoutingActivated);

        await stream.WriteAsync(CreateAliveCheckFrame());
        var aliveCheckResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.AliveCheckResponse, aliveCheckResponse.PayloadType);
        Assert.Equal(0x0E00, BinaryPrimitives.ReadUInt16BigEndian(aliveCheckResponse.Payload));

        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.tcp.connection.created");
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.tcp.routing_activation.succeeded");
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.tcp.alive_check.responded");
    }

    [Fact]
    public async Task NonWhitelistedSourceAddressReceivesDeniedActivation()
    {
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(registry, NullRuntimeEventPublisher.Instance, new HashSet<ushort> { 0x0E80 });
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = client.GetStream();

        await stream.WriteAsync(CreateRoutingActivationFrame(0x0E81));
        var activationResponse = await ReadFrameAsync(stream);

        Assert.Equal(DoipPayloadType.RoutingActivationResponse, activationResponse.PayloadType);
        Assert.Equal((byte)RoutingActivationResponseCode.DeniedUnknownSourceAddress, activationResponse.Payload[4]);
        Assert.DoesNotContain(registry.GetActiveSnapshots(), connection => connection.RoutingActivated);
    }

    [Fact]
    public async Task DisconnectRemovesConnectionFromRegistryAndPublishesEvent()
    {
        var events = new CapturingEventSink();
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(registry, new RuntimeEventBus([events]), new HashSet<ushort> { 0x0E80 });
        await server.StartAsync();

        using (var client = new TcpClient())
        {
            await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
            Assert.NotEmpty(registry.GetActiveSnapshots());
        }

        await WaitUntilAsync(() => registry.GetActiveSnapshots().Count == 0);
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.tcp.connection.disconnected");
    }

    [Fact]
    public async Task IdleConnectionTimesOutAndPublishesEvent()
    {
        var events = new CapturingEventSink();
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(
            registry,
            new RuntimeEventBus([events]),
            new HashSet<ushort> { 0x0E80 },
            TimeSpan.FromMilliseconds(1000));
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);

        await WaitUntilAsync(() => registry.GetActiveSnapshots().Count == 0, TimeSpan.FromSeconds(5));
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.tcp.connection.timeout");
    }

    [Fact]
    public async Task StopReleasesTcpPort()
    {
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(registry, NullRuntimeEventPublisher.Instance, new HashSet<ushort> { 0x0E80 });
        await server.StartAsync();
        var port = server.BoundPort;

        await server.StopAsync();

        var rebound = new TcpListener(IPAddress.Loopback, port);
        try
        {
            rebound.Start();
            Assert.Equal(port, ((IPEndPoint)rebound.LocalEndpoint).Port);
        }
        finally
        {
            rebound.Stop();
        }
    }

    private TcpDoipServer CreateServer(
        ConnectionRegistry registry,
        IRuntimeEventPublisher eventPublisher,
        IReadOnlySet<ushort> whitelist,
        TimeSpan? idleTimeout = null)
    {
        return new TcpDoipServer(
            new TcpDoipServerOptions(
                IPAddress.Loopback,
                0,
                0x0E00,
                whitelist,
                idleTimeout),
            codec,
            registry,
            eventPublisher);
    }

    private byte[] CreateRoutingActivationFrame(ushort testerLogicalAddress)
    {
        var payload = new byte[RoutingActivationHandler.RequestPayloadLength];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), testerLogicalAddress);
        var encoded = codec.Encode(DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.RoutingActivationRequest,
            payload));
        Assert.True(encoded.IsSuccess);
        return encoded.Value!;
    }

    private byte[] CreateAliveCheckFrame()
    {
        var encoded = codec.Encode(DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.AliveCheckRequest,
            []));
        Assert.True(encoded.IsSuccess);
        return encoded.Value!;
    }

    private async Task<DoipFrame> ReadFrameAsync(NetworkStream stream)
    {
        var headerBytes = new byte[DoipCodec.HeaderLength];
        await stream.ReadExactlyAsync(headerBytes);
        var header = codec.DecodeHeader(headerBytes);
        Assert.True(header.IsSuccess);

        var payload = new byte[header.Value!.PayloadLength];
        await stream.ReadExactlyAsync(payload);
        var decoded = codec.Decode(headerBytes.Concat(payload).ToArray());
        Assert.True(decoded.IsSuccess);
        return decoded.Value!;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(3));
        while (!condition())
        {
            await Task.Delay(50, cancellation.Token);
        }
    }
}
