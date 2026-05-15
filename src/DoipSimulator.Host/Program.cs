using System.Globalization;
using System.Net;
using System.Net.Sockets;
using DoipSimulator.Host;
using DoipSimulator.WebApi;
using Microsoft.Extensions.Hosting;

return await CliEntryPoint.RunAsync(args, Console.Out, Console.Error);

namespace DoipSimulator.Host
{
    public sealed record HostRuntimeOptions(string ListenAddress, int Port)
    {
        public const string DefaultListenAddress = "127.0.0.1";
        public const int DefaultPort = 5080;

        public static HostRuntimeOptions Default { get; } = new(DefaultListenAddress, DefaultPort);
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
                await using var app = WebApiApplication.Create(
                    [],
                    new WebApiRuntimeOptions(options.ListenAddress, options.Port, DateTimeOffset.UtcNow));

                await app.StartAsync(shutdown.Token);
                output.WriteLine($"Web console/API listening at http://127.0.0.1:{options.Port}");
                output.WriteLine("Press Ctrl+C to stop.");
                await app.WaitForShutdownAsync(shutdown.Token);
                return 0;
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
                return 0;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
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
            writer.WriteLine();
            writer.WriteLine("The runtime does not load full ECU configuration or start DoIP, UDS, DID, DTC, Flash, TLS, PCAP, database, or external services.");
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
