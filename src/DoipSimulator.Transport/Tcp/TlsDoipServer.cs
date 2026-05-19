using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Protocols.Doip;
using DoipSimulator.Protocols.Uds;

namespace DoipSimulator.Transport.Tcp;

public sealed record TlsDoipServerOptions(
    IPAddress BindAddress,
    int Port,
    ushort EntityLogicalAddress,
    IReadOnlySet<ushort> SourceAddressWhitelist,
    X509Certificate2 ServerCertificate,
    TlsClientCertificateValidator ClientCertificateValidator,
    bool RequireClientCertificate,
    TimeSpan? IdleTimeout = null);

public sealed class TlsDoipServer : IAsyncDisposable
{
    private static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(30);
    private readonly TlsDoipServerOptions options;
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

    public TlsDoipServer(
        TlsDoipServerOptions options,
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

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (listener is not null)
        {
            return;
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
                $"Failed to bind DoIP TLS endpoint {options.BindAddress}:{options.Port}: {exception.Message}",
                exception);
        }

        await eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Tls,
                "tls.listener.started",
                "DoIP TLS listener started.",
                data: new Dictionary<string, object?>
                {
                    ["bindAddress"] = options.BindAddress.ToString(),
                    ["port"] = BoundPort,
                    ["requireClientCertificate"] = options.RequireClientCertificate,
                }),
            cancellationToken);
        acceptTask = Task.Run(() => AcceptLoopAsync(shutdown.Token), CancellationToken.None);
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
        catch (IOException)
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
                new DiagnosticSessionControlService(state, config, eventPublisher),
                new TesterPresentService(
                    state,
                    timeout: TimeSpan.FromMilliseconds(config.Uds.TesterPresentTimeout.TimeoutMs)),
                new SecurityAccessService(config, state, eventPublisher),
                new RequestDownloadService(state, config, eventPublisher),
                new TransferDataService(state, eventPublisher),
                new RequestTransferExitService(state, eventPublisher),
                new ReadDataByIdentifierService(didRuntimeStore, state, eventPublisher),
                new ReadDtcInformationService(dtcRuntimeStore),
                new ClearDiagnosticInformationService(dtcRuntimeStore),
            ],
            eventPublisher,
            config,
            state);
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
        var lastClientCertificateReason = "Client certificate was not evaluated.";
        await PublishTlsEventAsync(
            RuntimeEventLevel.Info,
            "tls.connection.accepted",
            "DoIP TLS client accepted.",
            null,
            remoteEndpoint,
            null,
            cancellationToken);

        var sslStream = new SslStream(
            client.GetStream(),
            leaveInnerStreamOpen: false,
            (_, certificate, chain, sslPolicyErrors) =>
            {
                var result = options.ClientCertificateValidator.Validate(certificate, chain, sslPolicyErrors);
                lastClientCertificateReason = result.Reason;
                return result.Accepted;
            });

        ConnectionSnapshot? connection = null;
        try
        {
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = options.ServerCertificate,
                ClientCertificateRequired = options.RequireClientCertificate,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                EnabledSslProtocols = SslProtocols.None,
            }, cancellationToken);

            if (options.RequireClientCertificate && sslStream.RemoteCertificate is null)
            {
                await PublishTlsFailureAsync(
                    "tls.handshake.failed",
                    remoteEndpoint,
                    null,
                    "Client certificate is required but was not provided.",
                    cancellationToken);
                return;
            }

            connection = connectionRegistry.AddTlsConnection(remoteEndpoint);
            await PublishTlsEventAsync(
                RuntimeEventLevel.Info,
                "tls.handshake.succeeded",
                "DoIP TLS handshake succeeded.",
                connection.ConnectionId,
                remoteEndpoint,
                connection,
                cancellationToken);
            await PublishConnectionEventAsync("connection.opened", "Connection opened.", connection, cancellationToken);
            await ProcessDoipStreamAsync(connection, sslStream, cancellationToken);
        }
        catch (AuthenticationException exception)
        {
            await PublishTlsFailureAsync(
                "tls.handshake.failed",
                remoteEndpoint,
                connection?.ConnectionId,
                $"{lastClientCertificateReason} {exception.Message}".Trim(),
                cancellationToken);
        }
        catch (IOException exception)
        {
            await PublishTlsFailureAsync(
                "tls.connection.io_failed",
                remoteEndpoint,
                connection?.ConnectionId,
                exception.Message,
                cancellationToken);
        }
        finally
        {
            await sslStream.DisposeAsync();
            if (connection is not null)
            {
                await udsDispatcher.NotifyConnectionClosedAsync(
                    new UdsContext(
                        connection.ConnectionId,
                        remoteEndpoint,
                        connection.TesterLogicalAddress,
                        connection.EcuLogicalAddress),
                    CancellationToken.None);
                connectionRegistry.Remove(connection.ConnectionId);
                await PublishConnectionEventAsync("connection.closed", "Connection closed.", connection with { State = "closed" }, CancellationToken.None);
                await PublishTlsEventAsync(
                    RuntimeEventLevel.Info,
                    "tls.connection.closed",
                    "DoIP TLS connection closed.",
                    connection.ConnectionId,
                    remoteEndpoint,
                    connection with { State = "closed" },
                    CancellationToken.None);
            }
        }
    }

    private async Task ProcessDoipStreamAsync(
        ConnectionSnapshot connection,
        SslStream stream,
        CancellationToken cancellationToken)
    {
        var reader = new DoipStreamReader(codec);
        var buffer = new byte[4096];
        using var idleTimeout = new CancellationTokenSource(options.IdleTimeout ?? DefaultIdleTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, idleTimeout.Token);

        while (!linked.Token.IsCancellationRequested)
        {
            var bytesRead = await stream.ReadAsync(buffer, linked.Token);
            if (bytesRead == 0)
            {
                break;
            }

            idleTimeout.CancelAfter(options.IdleTimeout ?? DefaultIdleTimeout);
            var readResult = reader.Append(buffer.AsSpan(0, bytesRead));
            foreach (var error in readResult.Errors)
            {
                await PublishProtocolErrorAsync(connection.ConnectionId, connection.RemoteEndpoint, error.Code.ToString(), linked.Token);
            }

            foreach (var frame in readResult.Frames)
            {
                await PublishDoipFrameEventAsync(
                    "doip.frame.received",
                    "DoIP frame received.",
                    connection.ConnectionId,
                    connection.RemoteEndpoint,
                    frame,
                    "received",
                    linked.Token);
                await HandleFrameAsync(connection.ConnectionId, connection.RemoteEndpoint, frame, stream, linked.Token);
            }
        }
    }

    private async ValueTask HandleFrameAsync(
        string connectionId,
        string remoteEndpoint,
        DoipFrame frame,
        SslStream stream,
        CancellationToken cancellationToken)
    {
        if (frame.PayloadType == DoipPayloadType.RoutingActivationRequest)
        {
            await HandleRoutingActivationAsync(connectionId, remoteEndpoint, frame, stream, cancellationToken);
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

            await stream.WriteAsync(encoded.Value, cancellationToken);
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
            return;
        }

        if (frame.PayloadType == DoipPayloadType.DiagnosticMessage)
        {
            await HandleDiagnosticMessageAsync(connectionId, remoteEndpoint, frame, stream, cancellationToken);
            return;
        }

        await PublishProtocolErrorAsync(connectionId, remoteEndpoint, "UnsupportedTlsPayloadType", cancellationToken);
    }

    private async ValueTask HandleRoutingActivationAsync(
        string connectionId,
        string remoteEndpoint,
        DoipFrame frame,
        SslStream stream,
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

        await stream.WriteAsync(encoded.Value, cancellationToken);
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
                allowed ? "doip.tls.routing_activation.succeeded" : "doip.tls.routing_activation.denied",
                allowed ? "DoIP TLS Routing Activation succeeded." : "DoIP TLS Routing Activation denied by source address whitelist.",
                connectionId,
                new Dictionary<string, object?>
                {
                    ["connectionId"] = connectionId,
                    ["transport"] = "tls",
                    ["remoteEndpoint"] = remoteEndpoint,
                    ["testerLogicalAddress"] = ConnectionRegistry.FormatLogicalAddress(request.Value.TesterLogicalAddress),
                    ["ecuLogicalAddress"] = ConnectionRegistry.FormatLogicalAddress(options.EntityLogicalAddress),
                    ["activationSucceeded"] = allowed,
                    ["responseCode"] = $"0x{(byte)responseCode:X2}",
                    ["routingActivated"] = allowed,
                    ["state"] = "open",
                }),
            cancellationToken);
    }

    private async ValueTask HandleDiagnosticMessageAsync(
        string connectionId,
        string remoteEndpoint,
        DoipFrame frame,
        SslStream stream,
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

        var testerLogicalAddress = BinaryPrimitives.ReadUInt16BigEndian(frame.Payload.AsSpan(0, 2));
        var ecuLogicalAddress = BinaryPrimitives.ReadUInt16BigEndian(frame.Payload.AsSpan(2, 2));
        var udsPayload = frame.Payload.AsMemory(4);
        var context = new UdsContext(
            connectionId,
            remoteEndpoint,
            ConnectionRegistry.FormatLogicalAddress(testerLogicalAddress),
            ConnectionRegistry.FormatLogicalAddress(ecuLogicalAddress));

        var responses = await udsDispatcher.DispatchAsync(udsPayload, context, cancellationToken);
        foreach (var response in responses)
        {
            if (response.DelayBeforeSend > TimeSpan.Zero)
            {
                await Task.Delay(response.DelayBeforeSend, cancellationToken);
            }

            var encoded = codec.Encode(DoipFrame.Create(
                DoipCodec.Iso13400ProtocolVersion,
                DoipPayloadType.DiagnosticMessage,
                CreateDiagnosticResponsePayload(ecuLogicalAddress, testerLogicalAddress, response.ToBytes())));

            if (!encoded.IsSuccess || encoded.Value is null)
            {
                await PublishProtocolErrorAsync(connectionId, remoteEndpoint, encoded.Error?.Code.ToString() ?? "EncodeFailed", cancellationToken);
                return;
            }

            await stream.WriteAsync(encoded.Value, cancellationToken);
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

    private ValueTask PublishConnectionEventAsync(
        string name,
        string message,
        ConnectionSnapshot connection,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Connection,
                name,
                message,
                connection.ConnectionId,
                CreateConnectionEventData(connection)),
            cancellationToken);
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
                "doip.tls.protocol_error",
                "DoIP TLS frame rejected.",
                connectionId,
                new Dictionary<string, object?>
                {
                    ["connectionId"] = connectionId,
                    ["transport"] = "tls",
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
                    ["transport"] = "tls",
                    ["remoteEndpoint"] = remoteEndpoint,
                    ["direction"] = direction,
                    ["payloadType"] = $"0x{frame.PayloadType.Value:X4}",
                    ["payloadTypeName"] = frame.PayloadType.KnownName,
                    ["payloadLength"] = frame.Payload.Length,
                    ["payloadSummary"] = ToHex(frame.Payload),
                }),
            cancellationToken);
    }

    private ValueTask PublishTlsEventAsync(
        RuntimeEventLevel level,
        string name,
        string message,
        string? connectionId,
        string remoteEndpoint,
        ConnectionSnapshot? connection,
        CancellationToken cancellationToken)
    {
        var data = connection is null
            ? new Dictionary<string, object?>
            {
                ["transport"] = "tls",
                ["remoteEndpoint"] = remoteEndpoint,
            }
            : CreateConnectionEventData(connection);
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(level, RuntimeEventCategory.Tls, name, message, connectionId, data),
            cancellationToken);
    }

    private ValueTask PublishTlsFailureAsync(
        string name,
        string remoteEndpoint,
        string? connectionId,
        string reason,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Error,
                RuntimeEventCategory.Tls,
                name,
                "DoIP TLS handshake failed.",
                connectionId,
                new Dictionary<string, object?>
                {
                    ["transport"] = "tls",
                    ["remoteEndpoint"] = remoteEndpoint,
                    ["reason"] = reason,
                }),
            cancellationToken);
    }

    private static Dictionary<string, object?> CreateConnectionEventData(ConnectionSnapshot connection)
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
            ["state"] = connection.State,
        };
    }

    private static byte[] CreateDiagnosticResponsePayload(
        ushort sourceAddress,
        ushort targetAddress,
        byte[] udsPayload)
    {
        var payload = new byte[4 + udsPayload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), sourceAddress);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), targetAddress);
        udsPayload.CopyTo(payload.AsSpan(4));
        return payload;
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        return string.Join(' ', bytes.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
