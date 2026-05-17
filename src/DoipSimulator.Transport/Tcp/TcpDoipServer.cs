using System.Globalization;
using System.Net;
using System.Net.Sockets;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Protocols.Doip;
using DoipSimulator.Protocols.Uds;

namespace DoipSimulator.Transport.Tcp;

public sealed record TcpDoipServerOptions(
    IPAddress BindAddress,
    int Port,
    ushort EntityLogicalAddress,
    IReadOnlySet<ushort> SourceAddressWhitelist,
    TimeSpan? IdleTimeout = null);

public sealed class TcpDoipServer : IAsyncDisposable
{
    private static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(30);
    private readonly TcpDoipServerOptions options;
    private readonly IDoipCodec codec;
    private readonly ConnectionRegistry connectionRegistry;
    private readonly IRuntimeEventPublisher eventPublisher;
    private readonly IUdsDispatcher udsDispatcher;
    private readonly RoutingActivationHandler routingActivationHandler;
    private readonly AliveCheckHandler aliveCheckHandler;
    private readonly List<Task> connectionTasks = [];
    private readonly object connectionTasksGate = new();
    private CancellationTokenSource? shutdown;
    private TcpListener? listener;
    private Task? acceptTask;

    public TcpDoipServer(
        TcpDoipServerOptions options,
        IDoipCodec codec,
        ConnectionRegistry connectionRegistry,
        IRuntimeEventPublisher? eventPublisher = null,
        IUdsDispatcher? udsDispatcher = null)
    {
        this.options = options;
        this.codec = codec;
        this.connectionRegistry = connectionRegistry;
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
        this.udsDispatcher = udsDispatcher ?? CreateDefaultUdsDispatcher(options.EntityLogicalAddress, this.eventPublisher);
        routingActivationHandler = new RoutingActivationHandler(codec);
        aliveCheckHandler = new AliveCheckHandler(codec);
    }

    private static UdsDispatcher CreateDefaultUdsDispatcher(
        ushort entityLogicalAddress,
        IRuntimeEventPublisher eventPublisher)
    {
        var state = new EcuRuntimeState(entityLogicalAddress);
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dids = [];
        var didRuntimeStore = new DidRuntimeStore(config, "unused.json", new ConfigStore(), eventPublisher);
        var dtcRuntimeStore = new DtcRuntimeStore(config, eventPublisher);
        return new UdsDispatcher(
            [
                new DiagnosticSessionControlService(state, eventPublisher),
                new TesterPresentService(state),
                new ReadDataByIdentifierService(didRuntimeStore, eventPublisher),
                new ReadDtcInformationService(dtcRuntimeStore),
                new ClearDiagnosticInformationService(dtcRuntimeStore),
            ],
            eventPublisher);
    }

    public int BoundPort
    {
        get
        {
            if (listener?.LocalEndpoint is IPEndPoint endpoint)
            {
                return endpoint.Port;
            }

            return options.Port;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (listener is not null)
        {
            return Task.CompletedTask;
        }

        shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        listener = new TcpListener(options.BindAddress, options.Port);
        try
        {
            listener.Start();
        }
        catch (SocketException exception)
        {
            throw new InvalidOperationException(
                $"Failed to bind DoIP TCP endpoint {options.BindAddress}:{options.Port}: {exception.Message}",
                exception);
        }

        acceptTask = Task.Run(() => AcceptLoopAsync(shutdown.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (shutdown is null)
        {
            return;
        }

        await shutdown.CancelAsync();
        listener?.Stop();

        Task[] activeConnectionTasks;
        lock (connectionTasksGate)
        {
            activeConnectionTasks = connectionTasks.ToArray();
        }

        try
        {
            await Task.WhenAll(activeConnectionTasks.Append(acceptTask).Where(task => task is not null)!);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }

        shutdown.Dispose();
        shutdown = null;
        listener = null;
        acceptTask = null;
        lock (connectionTasksGate)
        {
            connectionTasks.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && listener is not null)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var task = Task.Run(() => HandleClientAsync(client, cancellationToken), CancellationToken.None);
            lock (connectionTasksGate)
            {
                connectionTasks.Add(task);
            }

            _ = task.ContinueWith(
                completed =>
                {
                    lock (connectionTasksGate)
                    {
                        connectionTasks.Remove(completed);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        var remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        var connection = connectionRegistry.AddTcpConnection(remoteEndpoint);
        await PublishConnectionEventAsync("doip.tcp.connection.created", "DoIP TCP connection created.", connection, cancellationToken);

        var streamReader = new DoipStreamReader(codec);
        var buffer = new byte[4096];

        try
        {
            using var idleTimeout = new CancellationTokenSource(options.IdleTimeout ?? DefaultIdleTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, idleTimeout.Token);
            await using var networkStream = client.GetStream();

            while (!linked.Token.IsCancellationRequested)
            {
                var bytesRead = await networkStream.ReadAsync(buffer, linked.Token);
                if (bytesRead == 0)
                {
                    break;
                }

                idleTimeout.CancelAfter(options.IdleTimeout ?? DefaultIdleTimeout);
                var readResult = streamReader.Append(buffer.AsSpan(0, bytesRead));
                foreach (var error in readResult.Errors)
                {
                    await PublishProtocolErrorAsync(connection, error.Code.ToString(), cancellationToken);
                }

                foreach (var frame in readResult.Frames)
                {
                    await PublishDoipFrameEventAsync(
                        "doip.frame.received",
                        "DoIP frame received.",
                        connection.ConnectionId,
                        remoteEndpoint,
                        frame,
                        "received",
                        cancellationToken);
                    await HandleFrameAsync(connection.ConnectionId, remoteEndpoint, frame, networkStream, cancellationToken);
                }
            }

            if (idleTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                await PublishConnectionEventAsync(
                    "doip.tcp.connection.timeout",
                    "DoIP TCP connection timed out.",
                    connection,
                    cancellationToken);
            }
            else
            {
                await PublishConnectionEventAsync(
                    "doip.tcp.connection.disconnected",
                    "DoIP TCP connection disconnected.",
                    connection,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await PublishConnectionEventAsync(
                "doip.tcp.connection.disconnected",
                "DoIP TCP connection disconnected.",
                connection,
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            await PublishConnectionEventAsync(
                "doip.tcp.connection.timeout",
                "DoIP TCP connection timed out.",
                connection,
                CancellationToken.None);
        }
        finally
        {
            connectionRegistry.Remove(connection.ConnectionId);
        }
    }

    private async ValueTask HandleFrameAsync(
        string connectionId,
        string remoteEndpoint,
        DoipFrame frame,
        NetworkStream networkStream,
        CancellationToken cancellationToken)
    {
        if (frame.PayloadType == DoipPayloadType.RoutingActivationRequest)
        {
            await HandleRoutingActivationAsync(connectionId, remoteEndpoint, frame, networkStream, cancellationToken);
            return;
        }

        if (aliveCheckHandler.IsAliveCheckRequest(frame))
        {
            var encoded = aliveCheckHandler.EncodeResponse(options.EntityLogicalAddress);
            if (!encoded.IsSuccess || encoded.Value is null)
            {
                await PublishProtocolErrorAsync(connectionId, remoteEndpoint, encoded.Error?.Code.ToString() ?? "EncodeFailed", cancellationToken);
                return;
            }

            await networkStream.WriteAsync(encoded.Value, cancellationToken);
            await PublishDoipFrameEventAsync(
                "doip.frame.sent",
                "DoIP frame sent.",
                connectionId,
                remoteEndpoint,
                DoipFrame.Create(
                    DoipCodec.Iso13400ProtocolVersion,
                    DoipPayloadType.AliveCheckResponse,
                    encoded.Value.AsSpan(DoipCodec.HeaderLength).ToArray()),
                "sent",
                cancellationToken);
            await eventPublisher.PublishAsync(
                RuntimeEvent.Create(
                    RuntimeEventLevel.Info,
                    RuntimeEventCategory.Doip,
                    "doip.tcp.alive_check.responded",
                    "DoIP TCP Alive Check request received and response sent.",
                    connectionId,
                    new Dictionary<string, object?>
                    {
                        ["remoteEndpoint"] = remoteEndpoint,
                        ["entityLogicalAddress"] = ConnectionRegistry.FormatLogicalAddress(options.EntityLogicalAddress),
                    }),
                cancellationToken);
            return;
        }

        if (frame.PayloadType == DoipPayloadType.DiagnosticMessage)
        {
            await HandleDiagnosticMessageAsync(connectionId, remoteEndpoint, frame, networkStream, cancellationToken);
            return;
        }

        await PublishProtocolErrorAsync(connectionId, remoteEndpoint, "UnsupportedTcpPayloadType", cancellationToken);
    }

    private async ValueTask HandleDiagnosticMessageAsync(
        string connectionId,
        string remoteEndpoint,
        DoipFrame frame,
        NetworkStream networkStream,
        CancellationToken cancellationToken)
    {
        var connection = connectionRegistry.Get(connectionId);
        if (connection?.RoutingActivated != true)
        {
            await PublishProtocolErrorAsync(connectionId, remoteEndpoint, "RoutingActivationRequired", cancellationToken);
            return;
        }

        if (frame.Payload.Length < 4)
        {
            await PublishProtocolErrorAsync(connectionId, remoteEndpoint, "InvalidDiagnosticMessageLength", cancellationToken);
            return;
        }

        var testerLogicalAddress = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(frame.Payload.AsSpan(0, 2));
        var ecuLogicalAddress = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(frame.Payload.AsSpan(2, 2));
        var udsPayload = frame.Payload.AsMemory(4);
        var context = new UdsContext(
            connectionId,
            remoteEndpoint,
            ConnectionRegistry.FormatLogicalAddress(testerLogicalAddress),
            ConnectionRegistry.FormatLogicalAddress(ecuLogicalAddress));

        var responses = await udsDispatcher.DispatchAsync(udsPayload, context, cancellationToken);
        foreach (var response in responses)
        {
            var encoded = codec.Encode(DoipFrame.Create(
                DoipCodec.Iso13400ProtocolVersion,
                DoipPayloadType.DiagnosticMessage,
                CreateDiagnosticResponsePayload(ecuLogicalAddress, testerLogicalAddress, response.ToBytes())));

            if (!encoded.IsSuccess || encoded.Value is null)
            {
                await PublishProtocolErrorAsync(connectionId, remoteEndpoint, encoded.Error?.Code.ToString() ?? "EncodeFailed", cancellationToken);
                return;
            }

            await networkStream.WriteAsync(encoded.Value, cancellationToken);
            await PublishDoipFrameEventAsync(
                "doip.frame.sent",
                "DoIP frame sent.",
                connectionId,
                remoteEndpoint,
                DoipFrame.Create(
                    DoipCodec.Iso13400ProtocolVersion,
                    DoipPayloadType.DiagnosticMessage,
                    CreateDiagnosticResponsePayload(ecuLogicalAddress, testerLogicalAddress, response.ToBytes())),
                "sent",
                cancellationToken);
        }
    }

    private async ValueTask HandleRoutingActivationAsync(
        string connectionId,
        string remoteEndpoint,
        DoipFrame frame,
        NetworkStream networkStream,
        CancellationToken cancellationToken)
    {
        var request = routingActivationHandler.DecodeRequest(frame);
        if (!request.IsSuccess || request.Value is null)
        {
            await PublishProtocolErrorAsync(connectionId, remoteEndpoint, request.Error?.Code.ToString() ?? "InvalidRoutingActivation", cancellationToken);
            return;
        }

        var allowed = options.SourceAddressWhitelist.Contains(request.Value.TesterLogicalAddress);
        var responseCode = allowed
            ? RoutingActivationResponseCode.SuccessfullyActivated
            : RoutingActivationResponseCode.DeniedUnknownSourceAddress;
        var response = new RoutingActivationResponse(
            request.Value.TesterLogicalAddress,
            options.EntityLogicalAddress,
            responseCode);
        var encoded = routingActivationHandler.EncodeResponse(response);
        if (!encoded.IsSuccess || encoded.Value is null)
        {
            await PublishProtocolErrorAsync(connectionId, remoteEndpoint, encoded.Error?.Code.ToString() ?? "EncodeFailed", cancellationToken);
            return;
        }

        if (allowed)
        {
            connectionRegistry.MarkRoutingActivated(
                connectionId,
                request.Value.TesterLogicalAddress,
                options.EntityLogicalAddress);
        }

        await networkStream.WriteAsync(encoded.Value, cancellationToken);
        await PublishDoipFrameEventAsync(
            "doip.frame.sent",
            "DoIP frame sent.",
            connectionId,
            remoteEndpoint,
            DoipFrame.Create(
                DoipCodec.Iso13400ProtocolVersion,
                DoipPayloadType.RoutingActivationResponse,
                encoded.Value.AsSpan(DoipCodec.HeaderLength).ToArray()),
            "sent",
            cancellationToken);
        await eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                allowed ? RuntimeEventLevel.Info : RuntimeEventLevel.Warning,
                RuntimeEventCategory.Doip,
                allowed ? "doip.tcp.routing_activation.succeeded" : "doip.tcp.routing_activation.denied",
                allowed ? "DoIP TCP Routing Activation succeeded." : "DoIP TCP Routing Activation denied by source address whitelist.",
                connectionId,
                new Dictionary<string, object?>
                {
                    ["remoteEndpoint"] = remoteEndpoint,
                    ["testerLogicalAddress"] = ConnectionRegistry.FormatLogicalAddress(request.Value.TesterLogicalAddress),
                    ["ecuLogicalAddress"] = ConnectionRegistry.FormatLogicalAddress(options.EntityLogicalAddress),
                    ["activationSucceeded"] = allowed,
                    ["responseCode"] = $"0x{(byte)responseCode:X2}",
                }),
            cancellationToken);
    }

    private async ValueTask PublishConnectionEventAsync(
        string name,
        string message,
        ConnectionSnapshot connection,
        CancellationToken cancellationToken)
    {
        var isClosed = name.Contains("disconnected", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        var data = CreateConnectionEventData(connection, isClosed ? "closed" : connection.State);

        await eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Doip,
                name,
                message,
                connection.ConnectionId,
                data),
            cancellationToken);

        if (name is "doip.tcp.connection.created" or "doip.tcp.connection.disconnected" or "doip.tcp.connection.timeout")
        {
            await eventPublisher.PublishAsync(
                RuntimeEvent.Create(
                    RuntimeEventLevel.Info,
                    RuntimeEventCategory.Connection,
                    isClosed ? "connection.closed" : "connection.opened",
                    isClosed ? "Connection closed." : "Connection opened.",
                    connection.ConnectionId,
                    data),
                cancellationToken);
        }
    }

    private ValueTask PublishProtocolErrorAsync(
        ConnectionSnapshot connection,
        string errorCode,
        CancellationToken cancellationToken)
    {
        return PublishProtocolErrorAsync(connection.ConnectionId, connection.RemoteEndpoint, errorCode, cancellationToken);
    }

    private ValueTask PublishProtocolErrorAsync(
        string connectionId,
        string remoteEndpoint,
        string errorCode,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Warning,
                RuntimeEventCategory.Doip,
                "doip.tcp.protocol_error",
                "DoIP TCP frame rejected.",
                connectionId,
                new Dictionary<string, object?>
                {
                    ["connectionId"] = connectionId,
                    ["remoteEndpoint"] = remoteEndpoint,
                    ["errorCode"] = errorCode,
                }),
            cancellationToken);
    }

    private ValueTask PublishDoipFrameEventAsync(
        string name,
        string message,
        string connectionId,
        string remoteEndpoint,
        DoipFrame frame,
        string direction,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Doip,
                name,
                message,
                connectionId,
                new Dictionary<string, object?>
                {
                    ["connectionId"] = connectionId,
                    ["remoteEndpoint"] = remoteEndpoint,
                    ["direction"] = direction,
                    ["payloadType"] = $"0x{frame.PayloadType.Value:X4}",
                    ["payloadTypeName"] = frame.PayloadType.KnownName,
                    ["payloadLength"] = frame.Payload.Length,
                    ["payloadSummary"] = ToHex(frame.Payload),
                }),
            cancellationToken);
    }

    private static Dictionary<string, object?> CreateConnectionEventData(
        ConnectionSnapshot connection,
        string state)
    {
        return new Dictionary<string, object?>
        {
            ["connectionId"] = connection.ConnectionId,
            ["transport"] = connection.Transport,
            ["remoteEndpoint"] = connection.RemoteEndpoint,
            ["routingActivated"] = connection.RoutingActivated,
            ["testerLogicalAddress"] = connection.TesterLogicalAddress,
            ["ecuLogicalAddress"] = connection.EcuLogicalAddress,
            ["connectedAt"] = connection.ConnectedAt,
            ["state"] = state,
        };
    }

    public static IReadOnlySet<ushort> ParseSourceAddressWhitelist(IEnumerable<string> values)
    {
        return values
            .Select(ParseLogicalAddress)
            .ToHashSet();
    }

    public static ushort ParseLogicalAddress(string value)
    {
        return ushort.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static byte[] CreateDiagnosticResponsePayload(
        ushort sourceAddress,
        ushort targetAddress,
        byte[] udsPayload)
    {
        var payload = new byte[4 + udsPayload.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), sourceAddress);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), targetAddress);
        udsPayload.CopyTo(payload.AsSpan(4));
        return payload;
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        return string.Join(' ', bytes.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
