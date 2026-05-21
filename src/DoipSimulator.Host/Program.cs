using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.Faults;
using DoipSimulator.Core.Observability.Logging;
using DoipSimulator.Core.Observability.Metrics;
using DoipSimulator.Core.Observability.Pcap;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Host;
using DoipSimulator.Protocols.Doip;
using DoipSimulator.Protocols.Uds;
using DoipSimulator.Transport.Tcp;
using DoipSimulator.Transport.Udp;
using DoipSimulator.WebApi;
using Microsoft.Extensions.Hosting;

return await CliEntryPoint.RunAsync(args, Console.Out, Console.Error);

namespace DoipSimulator.Host
{
    public sealed record HostRuntimeOptions(
        string ListenAddress,
        int Port,
        string? EventLogPath = null,
        string? ConfigPath = null)
    {
        public const string DefaultListenAddress = "127.0.0.1";
        public const int DefaultPort = 5080;

        public static HostRuntimeOptions Default { get; } = new(DefaultListenAddress, DefaultPort);

        public string ResolveEventLogPath()
        {
            return string.IsNullOrWhiteSpace(EventLogPath)
                ? Path.Combine(AppContext.BaseDirectory, "runtime-events.log")
                : EventLogPath;
        }

        public string ResolveConfigPath()
        {
            return string.IsNullOrWhiteSpace(ConfigPath)
                ? Path.Combine(AppContext.BaseDirectory, "simulator-config.json")
                : ConfigPath;
        }
    }

    public sealed record ParseResult(HostRuntimeOptions? Options, string? Error)
    {
        public bool IsSuccess => Error is null && Options is not null;
    }

    public static class CliEntryPoint
    {
        private const string CommandName = "doip-simulator";

        public static async Task<int> RunAsync(
            string[] args,
            TextWriter output,
            TextWriter error,
            CancellationToken cancellationToken = default)
        {
            if (args.Length == 0 || IsHelp(args))
            {
                WriteHelp(output);
                return 0;
            }

            if (args[0] == "run")
            {
                return await RunHostAsync(args[1..], output, error, cancellationToken);
            }

            error.WriteLine($"Unknown command: {string.Join(' ', args)}");
            error.WriteLine();
            WriteHelp(error);
            return 1;
        }

        public static ParseResult ParseRunOptions(IReadOnlyList<string> args)
        {
            var options = HostRuntimeOptions.Default;

            for (var index = 0; index < args.Count; index++)
            {
                var option = args[index];
                if (option is "--listen-address")
                {
                    if (!TryReadValue(args, ref index, option, out var listenAddress, out var error))
                    {
                        return new ParseResult(null, error);
                    }

                    if (!IPAddress.TryParse(listenAddress, out _))
                    {
                        return new ParseResult(null, $"Invalid listen address: {listenAddress}");
                    }

                    options = options with { ListenAddress = listenAddress };
                    continue;
                }

                if (option is "--port")
                {
                    if (!TryReadValue(args, ref index, option, out var portValue, out var error))
                    {
                        return new ParseResult(null, error);
                    }

                    if (!int.TryParse(portValue, NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
                        port is < 1 or > 65535)
                    {
                        return new ParseResult(null, $"Invalid port: {portValue}. Port must be between 1 and 65535.");
                    }

                    options = options with { Port = port };
                    continue;
                }

                if (option is "--event-log")
                {
                    if (!TryReadValue(args, ref index, option, out var eventLogPath, out var error))
                    {
                        return new ParseResult(null, error);
                    }

                    options = options with { EventLogPath = eventLogPath };
                    continue;
                }

                if (option is "--config")
                {
                    if (!TryReadValue(args, ref index, option, out var configPath, out var error))
                    {
                        return new ParseResult(null, error);
                    }

                    options = options with { ConfigPath = configPath };
                    continue;
                }

                return new ParseResult(null, $"Unknown run option: {option}");
            }

            return new ParseResult(options, null);
        }

        private static async Task<int> RunHostAsync(
            IReadOnlyList<string> args,
            TextWriter output,
            TextWriter error,
            CancellationToken cancellationToken)
        {
            var parsed = ParseRunOptions(args);
            if (!parsed.IsSuccess)
            {
                error.WriteLine(parsed.Error);
                error.WriteLine();
                WriteHelp(error);
                return 1;
            }

            var options = parsed.Options!;
            if (!RuntimePortChecker.IsPortAvailable(options.ListenAddress, options.Port, out var portError))
            {
                error.WriteLine($"Port {options.Port} is not available on {options.ListenAddress}: {portError}");
                return 1;
            }

            using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ConsoleCancelEventHandler? cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                shutdown.Cancel();
            };

            Console.CancelKeyPress += cancelHandler;

            try
            {
                var logPath = options.ResolveEventLogPath();
                await using var eventSink = new FileRuntimeEventSink(logPath);
                var eventHub = new RuntimeEventHub();
                var connectionRegistry = new ConnectionRegistry();
                var metricsCollector = new RuntimeMetricsCollector(connectionRegistry, eventHub);
                var eventPublisher = new RuntimeEventBus([metricsCollector, eventSink, eventHub]);
                var configPath = options.ResolveConfigPath();
                var configStore = new ConfigStore(eventPublisher);
                var config = await configStore.LoadAsync(configPath, shutdown.Token);
                var didRuntimeStore = new DidRuntimeStore(config, configPath, configStore, eventPublisher);
                var dtcRuntimeStore = new DtcRuntimeStore(config, eventPublisher);
                var controlServiceStateStore = new ControlServiceStateStore(config, eventPublisher);
                var ecuRuntimeState = new EcuRuntimeState(TcpDoipServer.ParseLogicalAddress(config.Entity.LogicalAddress));
                var faultRuntimeState = new FaultRuntimeState(config.FaultProfile, eventPublisher);
                await using var pcapRecorder = new PcapRecorder(eventPublisher: eventPublisher, metricsSink: metricsCollector);
                await using var udpServer = CreateUdpServer(config, eventPublisher, pcapRecorder);
                await using var tcpServer = CreateTcpServer(config, eventPublisher, connectionRegistry, ecuRuntimeState, didRuntimeStore, dtcRuntimeStore, controlServiceStateStore, pcapRecorder, faultRuntimeState);
                await using var tlsServer = await CreateTlsServerAsync(config, eventPublisher, connectionRegistry, ecuRuntimeState, didRuntimeStore, dtcRuntimeStore, controlServiceStateStore, faultRuntimeState, shutdown.Token);
                var startedAt = DateTimeOffset.UtcNow;
                await using var app = WebApiApplication.Create(
                    [],
                    new WebApiRuntimeOptions(options.ListenAddress, options.Port, startedAt, configPath),
                    configStore,
                    runtimeEventPublisher: eventPublisher,
                    runtimeEventHub: eventHub,
                    connectionRegistry: connectionRegistry,
                    ecuRuntimeState: ecuRuntimeState,
                    didRuntimeStore: didRuntimeStore,
                    dtcRuntimeStore: dtcRuntimeStore,
                    controlServiceStateStore: controlServiceStateStore,
                    pcapRecorder: pcapRecorder,
                    metricsCollector: metricsCollector,
                    faultRuntimeState: faultRuntimeState,
                    runtimeShutdownSignal: new CancellationTokenSourceRuntimeShutdownSignal(shutdown));

                await app.StartAsync(shutdown.Token);
                await udpServer.StartAsync(shutdown.Token);
                await tcpServer.StartAsync(shutdown.Token);
                if (tlsServer is not null)
                {
                    await tlsServer.StartAsync(shutdown.Token);
                }

                await eventPublisher.PublishAsync(
                    RuntimeEvent.Create(
                        RuntimeEventLevel.Info,
                        RuntimeEventCategory.System,
                        "runtime.started",
                        "Simulator runtime started.",
                        data: new Dictionary<string, object?>
                        {
                            ["listenAddress"] = options.ListenAddress,
                            ["port"] = options.Port,
                            ["doipUdpPort"] = udpServer.BoundPort,
                            ["doipTcpPort"] = tcpServer.BoundPort,
                            ["doipTlsPort"] = tlsServer?.BoundPort,
                            ["startedAt"] = startedAt,
                        }),
                    shutdown.Token);
                output.WriteLine($"Web console/API listening at http://127.0.0.1:{options.Port}");
                output.WriteLine("Press Ctrl+C to stop.");
                await app.WaitForShutdownAsync(shutdown.Token);
                await PublishStoppedAsync(eventPublisher, options);
                return 0;
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
                await using var stoppedSink = new FileRuntimeEventSink(options.ResolveEventLogPath());
                await PublishStoppedAsync(new RuntimeEventBus([stoppedSink]), options);
                return 0;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        private static async ValueTask PublishStoppedAsync(
            IRuntimeEventPublisher eventPublisher,
            HostRuntimeOptions options)
        {
            await eventPublisher.PublishAsync(
                RuntimeEvent.Create(
                    RuntimeEventLevel.Info,
                    RuntimeEventCategory.System,
                    "runtime.stopped",
                    "Simulator runtime stopped.",
                    data: new Dictionary<string, object?>
                    {
                        ["listenAddress"] = options.ListenAddress,
                        ["port"] = options.Port,
                    }));
        }

        private static bool TryReadValue(
            IReadOnlyList<string> args,
            ref int index,
            string option,
            out string value,
            out string? error)
        {
            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = "";
                error = $"Missing value for {option}.";
                return false;
            }

            index++;
            value = args[index];
            error = null;
            return true;
        }

        private static bool IsHelp(string[] args)
        {
            return args.Length == 1 && args[0] is "--help" or "-h" or "help";
        }

        private static void WriteHelp(TextWriter writer)
        {
            writer.WriteLine("DOIP Simulator");
            writer.WriteLine();
            writer.WriteLine($"Usage: {CommandName} [command]");
            writer.WriteLine();
            writer.WriteLine("Commands:");
            writer.WriteLine("  run       Start the WebApi runtime.");
            writer.WriteLine("  --help    Show this help text.");
            writer.WriteLine();
            writer.WriteLine("Run options:");
            writer.WriteLine("  --listen-address <address>  WebApi listen address. Default: 127.0.0.1");
            writer.WriteLine("  --port <port>               WebApi listen port. Default: 5080");
            writer.WriteLine("  --event-log <path>          Runtime event log path. Default: runtime-events.log beside the host assembly.");
            writer.WriteLine("  --config <path>             Simulator JSON config path. Missing file uses the validated default configuration.");
            writer.WriteLine();
            writer.WriteLine("The runtime starts the WebApi, UDP DoIP vehicle discovery, TCP DoIP routing activation, optional TLS DoIP transport, the UDS dispatcher, minimal session services, SecurityAccess 0x27 MVP with optional DLL seed/key plugin loading, fixed-byte DID reads/writes, DTC 0x19/0x14 MVP services, control-service 0x31/0x28/0x85 MVP state, Flash download 0x34/0x36/0x37 MVP state, and PCAP recording MVP; it does not start complex Routine scripts, real file flashing, certificate generation UI, database, OEM security algorithms, or external services.");
        }

        private static UdpDoipServer CreateUdpServer(
            SimulatorConfig config,
            IRuntimeEventPublisher eventPublisher,
            IPcapRecorder pcapRecorder)
        {
            var bindAddress = IPAddress.Parse(config.Network.BindAddress);
            var targetAddress = IPAddress.Parse(config.Network.VehicleAnnouncementTargetAddress);
            var options = new UdpDoipServerOptions(
                bindAddress,
                config.Network.DoipUdpPort,
                config.Network.VehicleAnnouncementEnabled,
                TimeSpan.FromMilliseconds(config.Network.VehicleAnnouncementIntervalMilliseconds),
                new IPEndPoint(targetAddress, config.Network.VehicleAnnouncementTargetPort));
            var entityInfo = DoipEntityInfo.Create(
                config.Entity.Vin,
                config.Entity.Eid,
                config.Entity.Gid,
                config.Entity.LogicalAddress);
            var handler = new VehicleIdentificationUdpHandler(
                entityInfo,
                new DoipCodec(),
                eventPublisher);

            return new UdpDoipServer(options, handler, pcapRecorder);
        }

        private static TcpDoipServer CreateTcpServer(
            SimulatorConfig config,
            IRuntimeEventPublisher eventPublisher,
            ConnectionRegistry connectionRegistry,
            EcuRuntimeState ecuRuntimeState,
            DidRuntimeStore didRuntimeStore,
            DtcRuntimeStore dtcRuntimeStore,
            ControlServiceStateStore controlServiceStateStore,
            IPcapRecorder pcapRecorder,
            FaultRuntimeState faultRuntimeState)
        {
            var bindAddress = IPAddress.Parse(config.Network.BindAddress);
            var entityLogicalAddress = TcpDoipServer.ParseLogicalAddress(config.Entity.LogicalAddress);
            var sourceAddressWhitelist = TcpDoipServer.ParseSourceAddressWhitelist(config.Network.SourceAddressWhitelist);
            var options = new TcpDoipServerOptions(
                bindAddress,
                config.Network.DoipTcpPort,
                entityLogicalAddress,
                sourceAddressWhitelist,
                TimeSpan.FromMilliseconds(config.Network.TcpConnectionIdleTimeoutMilliseconds));

            return new TcpDoipServer(
                options,
                new DoipCodec(),
                connectionRegistry,
                eventPublisher,
                new UdsDispatcher(
                    [
                        new DiagnosticSessionControlService(ecuRuntimeState, config, eventPublisher),
                        new TesterPresentService(
                            ecuRuntimeState,
                            timeout: TimeSpan.FromMilliseconds(config.Uds.TesterPresentTimeout.TimeoutMs)),
                        new SecurityAccessService(config, ecuRuntimeState, eventPublisher),
                        new RequestDownloadService(ecuRuntimeState, config, eventPublisher),
                        new TransferDataService(ecuRuntimeState, eventPublisher),
                        new RequestTransferExitService(ecuRuntimeState, eventPublisher),
                        new ReadDataByIdentifierService(didRuntimeStore, ecuRuntimeState, eventPublisher),
                        new WriteDataByIdentifierService(didRuntimeStore, ecuRuntimeState),
                        new ReadDtcInformationService(dtcRuntimeStore),
                        new ClearDiagnosticInformationService(dtcRuntimeStore),
                        new RoutineControlService(config, ecuRuntimeState, eventPublisher),
                        new CommunicationControlService(controlServiceStateStore),
                        new ControlDtcSettingService(controlServiceStateStore),
                    ],
                    eventPublisher,
                    config,
                    ecuRuntimeState,
                    faultRuntimeState: faultRuntimeState),
                pcapRecorder,
                faultRuntimeState);
        }

        private static async Task<TlsDoipServer?> CreateTlsServerAsync(
            SimulatorConfig config,
            IRuntimeEventPublisher eventPublisher,
            ConnectionRegistry connectionRegistry,
            EcuRuntimeState ecuRuntimeState,
            DidRuntimeStore didRuntimeStore,
            DtcRuntimeStore dtcRuntimeStore,
            ControlServiceStateStore controlServiceStateStore,
            FaultRuntimeState faultRuntimeState,
            CancellationToken cancellationToken)
        {
            if (!config.Tls.Enabled)
            {
                return null;
            }

            X509Certificate2 serverCertificate;
            X509Certificate2? clientCa;
            try
            {
                serverCertificate = TlsCertificateLoader.LoadServerCertificate(config.Tls);
                clientCa = TlsCertificateLoader.LoadClientCertificateAuthority(config.Tls);
            }
            catch (TlsCertificateLoadException exception)
            {
                await eventPublisher.PublishAsync(
                    RuntimeEvent.Create(
                        RuntimeEventLevel.Error,
                        RuntimeEventCategory.Tls,
                        "tls.certificate.load_failed",
                        "TLS certificate loading failed.",
                        data: new Dictionary<string, object?>
                        {
                            ["field"] = exception.Field,
                            ["reason"] = exception.Message,
                        }),
                    cancellationToken);
                throw;
            }

            var bindAddress = IPAddress.Parse(config.Network.BindAddress);
            var entityLogicalAddress = TcpDoipServer.ParseLogicalAddress(config.Entity.LogicalAddress);
            var sourceAddressWhitelist = TcpDoipServer.ParseSourceAddressWhitelist(config.Network.SourceAddressWhitelist);
            var options = new TlsDoipServerOptions(
                bindAddress,
                config.Network.DoipTlsPort,
                entityLogicalAddress,
                sourceAddressWhitelist,
                serverCertificate,
                new TlsClientCertificateValidator(config.Tls.RequireClientCertificate, clientCa),
                config.Tls.RequireClientCertificate,
                TimeSpan.FromMilliseconds(config.Network.TcpConnectionIdleTimeoutMilliseconds));

            return new TlsDoipServer(
                options,
                new DoipCodec(),
                connectionRegistry,
                eventPublisher,
                new UdsDispatcher(
                    [
                        new DiagnosticSessionControlService(ecuRuntimeState, config, eventPublisher),
                        new TesterPresentService(
                            ecuRuntimeState,
                            timeout: TimeSpan.FromMilliseconds(config.Uds.TesterPresentTimeout.TimeoutMs)),
                        new SecurityAccessService(config, ecuRuntimeState, eventPublisher),
                        new RequestDownloadService(ecuRuntimeState, config, eventPublisher),
                        new TransferDataService(ecuRuntimeState, eventPublisher),
                        new RequestTransferExitService(ecuRuntimeState, eventPublisher),
                        new ReadDataByIdentifierService(didRuntimeStore, ecuRuntimeState, eventPublisher),
                        new WriteDataByIdentifierService(didRuntimeStore, ecuRuntimeState),
                        new ReadDtcInformationService(dtcRuntimeStore),
                        new ClearDiagnosticInformationService(dtcRuntimeStore),
                        new RoutineControlService(config, ecuRuntimeState, eventPublisher),
                        new CommunicationControlService(controlServiceStateStore),
                        new ControlDtcSettingService(controlServiceStateStore),
                    ],
                    eventPublisher,
                    config,
                    ecuRuntimeState,
                    faultRuntimeState: faultRuntimeState));
        }
    }

    public static class RuntimePortChecker
    {
        public static bool IsPortAvailable(string listenAddress, int port, out string? error)
        {
            TcpListener? listener = null;

            try
            {
                listener = new TcpListener(IPAddress.Parse(listenAddress), port);
                listener.Start();
                error = null;
                return true;
            }
            catch (SocketException exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                listener?.Stop();
            }
        }
    }
}
