using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Protocols.Doip;
using DoipSimulator.Protocols.Uds;
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
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "connection.opened");
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.frame.received");
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.frame.sent");
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.tcp.routing_activation.succeeded");
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.tcp.alive_check.responded");
    }

    [Fact]
    public async Task DiagnosticMessageAfterRoutingActivationReturnsUnsupportedSidNrc()
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

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x99]));
        var diagnosticResponse = await ReadFrameAsync(stream);

        Assert.Equal(DoipPayloadType.DiagnosticMessage, diagnosticResponse.PayloadType);
        Assert.Equal(0x0E00, BinaryPrimitives.ReadUInt16BigEndian(diagnosticResponse.Payload.AsSpan(0, 2)));
        Assert.Equal(0x0E80, BinaryPrimitives.ReadUInt16BigEndian(diagnosticResponse.Payload.AsSpan(2, 2)));
        Assert.Equal([0x7F, 0x99, 0x11], diagnosticResponse.Payload[4..]);
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Category == RuntimeEventCategory.Uds && runtimeEvent.Name == "uds.request.received");
        Assert.Contains(
            events.Events,
            runtimeEvent => runtimeEvent.Category == RuntimeEventCategory.Uds &&
                runtimeEvent.Name == "uds.request.received" &&
                runtimeEvent.Data!["byteSummary"]?.Equals("99") == true);
        Assert.Contains(
            events.Events,
            runtimeEvent => runtimeEvent.Category == RuntimeEventCategory.Uds &&
                runtimeEvent.Name == "uds.response.sent" &&
                runtimeEvent.Data!["byteSummary"]?.Equals("7F 99 11") == true);
    }

    [Fact]
    public async Task EmptyDiagnosticUdsPayloadReturnsLengthNrcAndKeepsConnectionOpen()
    {
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(registry, NullRuntimeEventPublisher.Instance, new HashSet<ushort> { 0x0E80 });
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = client.GetStream();

        await stream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
        var activationResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.RoutingActivationResponse, activationResponse.PayloadType);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, []));
        var diagnosticResponse = await ReadFrameAsync(stream);

        Assert.Equal(DoipPayloadType.DiagnosticMessage, diagnosticResponse.PayloadType);
        Assert.Equal([0x7F, 0x00, 0x13], diagnosticResponse.Payload[4..]);

        await stream.WriteAsync(CreateAliveCheckFrame());
        var aliveCheckResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.AliveCheckResponse, aliveCheckResponse.PayloadType);
    }

    [Fact]
    public async Task DiagnosticSessionControlAfterRoutingActivationReturnsPositiveResponse()
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

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x10, 0x03]));
        var diagnosticResponse = await ReadFrameAsync(stream);

        Assert.Equal(DoipPayloadType.DiagnosticMessage, diagnosticResponse.PayloadType);
        Assert.Equal(0x0E00, BinaryPrimitives.ReadUInt16BigEndian(diagnosticResponse.Payload.AsSpan(0, 2)));
        Assert.Equal(0x0E80, BinaryPrimitives.ReadUInt16BigEndian(diagnosticResponse.Payload.AsSpan(2, 2)));
        Assert.Equal([0x50, 0x03, 0x00, 0x32, 0x13, 0x88], diagnosticResponse.Payload[4..]);
        Assert.Contains(
            events.Events,
            runtimeEvent => runtimeEvent.Category == RuntimeEventCategory.Uds &&
                runtimeEvent.Name == "uds.session.changed" &&
                runtimeEvent.Data!["newSession"]?.Equals("extended") == true);
        Assert.Contains(
            events.Events,
            runtimeEvent => runtimeEvent.Category == RuntimeEventCategory.State &&
                runtimeEvent.Name == "state.session.changed" &&
                runtimeEvent.Data!["currentSession"]?.Equals("extended") == true);
    }

    [Fact]
    public async Task TesterPresentAfterRoutingActivationReturnsPositiveResponseAndKeepsConnectionOpen()
    {
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(registry, NullRuntimeEventPublisher.Instance, new HashSet<ushort> { 0x0E80 });
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = client.GetStream();

        await stream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
        var activationResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.RoutingActivationResponse, activationResponse.PayloadType);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x3E, 0x00]));
        var diagnosticResponse = await ReadFrameAsync(stream);

        Assert.Equal(DoipPayloadType.DiagnosticMessage, diagnosticResponse.PayloadType);
        Assert.Equal([0x7E, 0x00], diagnosticResponse.Payload[4..]);

        await stream.WriteAsync(CreateAliveCheckFrame());
        var aliveCheckResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.AliveCheckResponse, aliveCheckResponse.PayloadType);
    }

    [Fact]
    public async Task ReadDataByIdentifierAfterRoutingActivationReturnsConfiguredDid()
    {
        var events = new CapturingEventSink();
        var eventPublisher = new RuntimeEventBus([events]);
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(
            registry,
            eventPublisher,
            new HashSet<ushort> { 0x0E80 },
            udsDispatcher: CreateUdsDispatcher(eventPublisher));
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = client.GetStream();

        await stream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
        var activationResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.RoutingActivationResponse, activationResponse.PayloadType);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x22, 0xF1, 0x90]));
        var diagnosticResponse = await ReadFrameAsync(stream);

        Assert.Equal(DoipPayloadType.DiagnosticMessage, diagnosticResponse.PayloadType);
        Assert.Equal(0x0E00, BinaryPrimitives.ReadUInt16BigEndian(diagnosticResponse.Payload.AsSpan(0, 2)));
        Assert.Equal(0x0E80, BinaryPrimitives.ReadUInt16BigEndian(diagnosticResponse.Payload.AsSpan(2, 2)));
        Assert.Equal([0x62, 0xF1, 0x90, 0x4C, 0x54], diagnosticResponse.Payload[4..]);
        Assert.Contains(
            events.Events,
            runtimeEvent => runtimeEvent.Category == RuntimeEventCategory.Uds &&
                runtimeEvent.Name == "uds.did.read" &&
                runtimeEvent.Data!["did"]?.Equals("0xF190") == true &&
            runtimeEvent.Data!["responseLength"]?.Equals(4) == true);
    }

    [Fact]
    public async Task ResponsePendingConfiguredServiceSendsPendingThenFinalDiagnosticResponse()
    {
        var eventPublisher = NullRuntimeEventPublisher.Instance;
        var registry = new ConnectionRegistry();
        var config = SimulatorConfig.CreateDefault();
        config.Uds.ResponseDelays =
        [
            new ServiceResponseDelayConfig
            {
                ServiceId = "0x22",
                ResponsePending = new ResponsePendingConfig { Enabled = true },
                InitialDelayMs = 0,
                FinalDelayMs = 0,
            },
        ];
        await using var server = CreateServer(
            registry,
            eventPublisher,
            new HashSet<ushort> { 0x0E80 },
            udsDispatcher: CreateUdsDispatcher(eventPublisher, config: config));
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = client.GetStream();

        await stream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
        var activationResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.RoutingActivationResponse, activationResponse.PayloadType);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x22, 0xF1, 0x90]));
        var pendingResponse = await ReadFrameAsync(stream);
        var finalResponse = await ReadFrameAsync(stream);

        Assert.Equal([0x7F, 0x22, 0x78], pendingResponse.Payload[4..]);
        Assert.Equal([0x62, 0xF1, 0x90, 0x4C, 0x54], finalResponse.Payload[4..]);
    }

    [Fact]
    public async Task DelayedDiagnosticResponseDoesNotBlockOtherConnectionAliveCheck()
    {
        var eventPublisher = NullRuntimeEventPublisher.Instance;
        var registry = new ConnectionRegistry();
        var config = SimulatorConfig.CreateDefault();
        config.Uds.ResponseDelays =
        [
            new ServiceResponseDelayConfig
            {
                ServiceId = "0x22",
                ResponsePending = new ResponsePendingConfig { Enabled = true },
                InitialDelayMs = 0,
                FinalDelayMs = 250,
            },
        ];
        await using var server = CreateServer(
            registry,
            eventPublisher,
            new HashSet<ushort> { 0x0E80, 0x0E81 },
            udsDispatcher: CreateUdsDispatcher(eventPublisher, config: config));
        await server.StartAsync();

        using var delayedClient = new TcpClient();
        await delayedClient.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var delayedStream = delayedClient.GetStream();
        await delayedStream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
        await ReadFrameAsync(delayedStream);

        using var otherClient = new TcpClient();
        await otherClient.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var otherStream = otherClient.GetStream();

        var delayedRequest = Task.Run(async () =>
        {
            await delayedStream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x22, 0xF1, 0x90]));
            return await ReadFrameAsync(delayedStream);
        });

        await otherStream.WriteAsync(CreateAliveCheckFrame());
        var aliveCheckResponse = await ReadFrameAsync(otherStream);
        var pendingResponse = await delayedRequest;

        Assert.Equal(DoipPayloadType.AliveCheckResponse, aliveCheckResponse.PayloadType);
        Assert.Equal([0x7F, 0x22, 0x78], pendingResponse.Payload[4..]);
    }

    [Fact]
    public async Task SecurityAccessAfterRoutingActivationReturnsSeedResponse()
    {
        var events = new CapturingEventSink();
        var eventPublisher = new RuntimeEventBus([events]);
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(
            registry,
            eventPublisher,
            new HashSet<ushort> { 0x0E80 },
            udsDispatcher: CreateUdsDispatcher(eventPublisher));
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = client.GetStream();

        await stream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
        var activationResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.RoutingActivationResponse, activationResponse.PayloadType);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x27, 0x01]));
        var diagnosticResponse = await ReadFrameAsync(stream);

        Assert.Equal(DoipPayloadType.DiagnosticMessage, diagnosticResponse.PayloadType);
        Assert.Equal(0x0E00, BinaryPrimitives.ReadUInt16BigEndian(diagnosticResponse.Payload.AsSpan(0, 2)));
        Assert.Equal(0x0E80, BinaryPrimitives.ReadUInt16BigEndian(diagnosticResponse.Payload.AsSpan(2, 2)));
        Assert.Equal(0x67, diagnosticResponse.Payload[4]);
        Assert.Equal(0x01, diagnosticResponse.Payload[5]);
        Assert.NotEmpty(diagnosticResponse.Payload[6..]);
        Assert.Contains(
            events.Events,
            runtimeEvent => runtimeEvent.Category == RuntimeEventCategory.Uds &&
                runtimeEvent.Name == "uds.securityAccess.processed" &&
                runtimeEvent.Data!["outcome"]?.Equals("seed-issued") == true);
    }

    [Fact]
    public async Task SecurityAccessKeyAfterRoutingActivationUnlocksLevel()
    {
        var eventPublisher = NullRuntimeEventPublisher.Instance;
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(
            registry,
            eventPublisher,
            new HashSet<ushort> { 0x0E80 },
            udsDispatcher: CreateUdsDispatcher(eventPublisher));
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = client.GetStream();

        await stream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
        var activationResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.RoutingActivationResponse, activationResponse.PayloadType);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x27, 0x01]));
        var seedResponse = await ReadFrameAsync(stream);
        var key = SecurityAccessService.ComputeExpectedKey(
            SimulatorConfig.CreateDefault().Uds.SecurityAccess[0],
            seedResponse.Payload[6..]);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x27, 0x02, .. key]));
        var keyResponse = await ReadFrameAsync(stream);

        Assert.Equal(DoipPayloadType.DiagnosticMessage, keyResponse.PayloadType);
        Assert.Equal([0x67, 0x02], keyResponse.Payload[4..]);
    }

    [Fact]
    public async Task ReadDtcInformationAfterRoutingActivationReturnsActiveDtc()
    {
        var events = new CapturingEventSink();
        var eventPublisher = new RuntimeEventBus([events]);
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(
            registry,
            eventPublisher,
            new HashSet<ushort> { 0x0E80 },
            udsDispatcher: CreateUdsDispatcher(eventPublisher, dtcActive: true));
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = client.GetStream();

        await stream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
        var activationResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.RoutingActivationResponse, activationResponse.PayloadType);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x19, 0x02, 0xFF]));
        var diagnosticResponse = await ReadFrameAsync(stream);

        Assert.Equal(DoipPayloadType.DiagnosticMessage, diagnosticResponse.PayloadType);
        Assert.Equal([0x59, 0x02, 0xFF, 0x12, 0x34, 0x56, 0x2F], diagnosticResponse.Payload[4..]);
        Assert.Contains(
            events.Events,
            runtimeEvent => runtimeEvent.Category == RuntimeEventCategory.Uds &&
                runtimeEvent.Name == "uds.dtc.read" &&
                runtimeEvent.Data!["returnedCount"]?.Equals(1) == true);
    }

    [Fact]
    public async Task ClearDiagnosticInformationAfterRoutingActivationClearsActiveDtc()
    {
        var eventPublisher = NullRuntimeEventPublisher.Instance;
        var registry = new ConnectionRegistry();
        var dtcStore = CreateDtcRuntimeStore(eventPublisher, active: true);
        await using var server = CreateServer(
            registry,
            eventPublisher,
            new HashSet<ushort> { 0x0E80 },
            udsDispatcher: CreateUdsDispatcher(eventPublisher, dtcRuntimeStore: dtcStore));
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = client.GetStream();

        await stream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
        var activationResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.RoutingActivationResponse, activationResponse.PayloadType);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x14, 0x12, 0x34, 0x56]));
        var clearResponse = await ReadFrameAsync(stream);
        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x19, 0x02, 0xFF]));
        var readResponse = await ReadFrameAsync(stream);

        Assert.Equal([0x54], clearResponse.Payload[4..]);
        Assert.Equal([0x59, 0x02, 0xFF], readResponse.Payload[4..]);
        Assert.False(Assert.Single(dtcStore.List()).Active);
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
        Assert.Contains(
            events.Events,
            runtimeEvent => runtimeEvent.Name == "connection.closed" &&
                runtimeEvent.Data!["state"]?.Equals("closed") == true);
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
        TimeSpan? idleTimeout = null,
        IUdsDispatcher? udsDispatcher = null)
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
            eventPublisher,
            udsDispatcher);
    }

    private static IUdsDispatcher CreateUdsDispatcher(
        IRuntimeEventPublisher eventPublisher,
        bool dtcActive = false,
        DtcRuntimeStore? dtcRuntimeStore = null,
        SimulatorConfig? config = null)
    {
        var state = new EcuRuntimeState(0x0E00);
        config ??= SimulatorConfig.CreateDefault();
        config.Uds.Dids =
        [
            new DidConfig
            {
                Identifier = "0xF190",
                Name = "VIN",
                ValueEncoding = "hex",
                Value = "4C54",
            },
        ];
        var didRuntimeStore = new DidRuntimeStore(config, "unused.json", new ConfigStore(), eventPublisher);
        var dtcStore = dtcRuntimeStore ?? CreateDtcRuntimeStore(eventPublisher, dtcActive);
        return new UdsDispatcher(
            [
                new DiagnosticSessionControlService(state, config, eventPublisher),
                new TesterPresentService(state),
                new SecurityAccessService(config, state, eventPublisher),
                new ReadDataByIdentifierService(didRuntimeStore, state, eventPublisher),
                new ReadDtcInformationService(dtcStore),
                new ClearDiagnosticInformationService(dtcStore),
            ],
            eventPublisher,
            config,
            state);
    }

    private static DtcRuntimeStore CreateDtcRuntimeStore(IRuntimeEventPublisher eventPublisher, bool active)
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dtcs =
        [
            new DtcConfig
            {
                Code = "0x123456",
                Name = "DoIP DTC",
                Status = "0x2F",
                Active = active,
            },
        ];
        return new DtcRuntimeStore(config, eventPublisher);
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

    private byte[] CreateDiagnosticMessageFrame(ushort sourceAddress, ushort targetAddress, byte[] udsPayload)
    {
        var payload = new byte[4 + udsPayload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), sourceAddress);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), targetAddress);
        udsPayload.CopyTo(payload.AsSpan(4));
        var encoded = codec.Encode(DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.DiagnosticMessage,
            payload));
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
