using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Host;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class RuntimeStartupTests
{
    [Fact]
    public void RunOptionsAcceptListenAddressAndPort()
    {
        var result = CliEntryPoint.ParseRunOptions(["--listen-address", "127.0.0.1", "--port", "5188"]);

        Assert.True(result.IsSuccess);
        Assert.Equal("127.0.0.1", result.Options!.ListenAddress);
        Assert.Equal(5188, result.Options.Port);
    }

    [Theory]
    [InlineData("--listen-address", "not-an-address")]
    [InlineData("--port", "0")]
    [InlineData("--port", "70000")]
    public void RunOptionsRejectInvalidInputs(string option, string value)
    {
        var result = CliEntryPoint.ParseRunOptions([option, value]);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void PortCheckerDetectsOccupiedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var isAvailable = RuntimePortChecker.IsPortAvailable("127.0.0.1", port, out var error);

        Assert.False(isAvailable);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task HealthEndpointReturnsMinimalHealthInformation()
    {
        var port = GetFreeLoopbackPort();
        var startedAt = DateTimeOffset.Parse("2026-05-15T00:00:00Z");
        await using var app = WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, startedAt));

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            var response = await client.GetAsync("/api/health");
            var health = await response.Content.ReadFromJsonAsync<HealthResponse>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(health);
            Assert.Equal("ok", health!.Status);
            Assert.False(string.IsNullOrWhiteSpace(health.Version));
            Assert.Equal(startedAt, health.StartedAt);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task HostRunWritesStartupAndStopEventsToRuntimeLog()
    {
        var port = GetDistinctFreeLoopbackPorts(2);
        var logPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "runtime-events.log");
        var configPath = Path.Combine(Path.GetDirectoryName(logPath)!, "simulator-config.json");
        var config = SimulatorConfig.CreateDefault();
        config.Network.BindAddress = "127.0.0.1";
        config.Network.DoipUdpPort = GetFreeUdpPort();
        config.Network.DoipTcpPort = port[1];
        await new ConfigStore().SaveAsync(configPath, config);
        using var cancellation = new CancellationTokenSource();
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();

        var runTask = CliEntryPoint.RunAsync(
            ["run", "--listen-address", "127.0.0.1", "--port", port[0].ToString(), "--event-log", logPath, "--config", configPath],
            standardOutput,
            standardError,
            cancellation.Token);

        try
        {
            await WaitForStartupLogOrExitAsync(logPath, "runtime.started", runTask);
        }
        finally
        {
            await cancellation.CancelAsync();
        }

        var exitCode = await runTask;
        var content = await File.ReadAllTextAsync(logPath);

        Assert.True(
            exitCode == 0,
            $"Expected exit code 0 but got {exitCode}.{Environment.NewLine}stdout:{Environment.NewLine}{standardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{standardError}");
        Assert.Contains("runtime.started", content);
        Assert.Contains("runtime.stopped", content);
    }

    [Fact]
    public async Task HostRunStopsAndReleasesPortsWhenShutdownApiIsRequested()
    {
        var ports = GetDistinctFreeLoopbackPorts(2);
        var webApiPort = ports[0];
        var doipTcpPort = ports[1];
        var doipUdpPort = GetFreeUdpPort();
        var logPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "runtime-events.log");
        var configPath = Path.Combine(Path.GetDirectoryName(logPath)!, "simulator-config.json");
        var config = SimulatorConfig.CreateDefault();
        config.Network.BindAddress = "127.0.0.1";
        config.Network.DoipUdpPort = doipUdpPort;
        config.Network.DoipTcpPort = doipTcpPort;
        await new ConfigStore().SaveAsync(configPath, config);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();

        var runTask = CliEntryPoint.RunAsync(
            ["run", "--listen-address", "127.0.0.1", "--port", webApiPort.ToString(), "--event-log", logPath, "--config", configPath],
            standardOutput,
            standardError,
            cancellation.Token);

        await WaitForStartupLogOrExitAsync(logPath, "runtime.started", runTask);

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{webApiPort}") };
        var response = await client.PostAsync("/api/runtime/shutdown", content: null, cancellation.Token);
        var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(10), cancellation.Token);
        var content = await File.ReadAllTextAsync(logPath, cancellation.Token);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(
            exitCode == 0,
            $"Expected exit code 0 but got {exitCode}.{Environment.NewLine}stdout:{Environment.NewLine}{standardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{standardError}");
        Assert.Contains("system.shutdown.requested", content);
        Assert.Contains("runtime.stopped", content);
        Assert.True(RuntimePortChecker.IsPortAvailable("127.0.0.1", webApiPort, out _));
        Assert.True(RuntimePortChecker.IsPortAvailable("127.0.0.1", doipTcpPort, out _));
        Assert.True(IsUdpPortAvailable("127.0.0.1", doipUdpPort));
    }

    [Fact]
    public async Task HostRunStopsWhenShutdownApiIsRequestedWithEventStreamConnected()
    {
        var ports = GetDistinctFreeLoopbackPorts(2);
        var webApiPort = ports[0];
        var logPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "runtime-events.log");
        var configPath = Path.Combine(Path.GetDirectoryName(logPath)!, "simulator-config.json");
        var config = SimulatorConfig.CreateDefault();
        config.Network.BindAddress = "127.0.0.1";
        config.Network.DoipUdpPort = GetFreeUdpPort();
        config.Network.DoipTcpPort = ports[1];
        await new ConfigStore().SaveAsync(configPath, config);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();

        var runTask = CliEntryPoint.RunAsync(
            ["run", "--listen-address", "127.0.0.1", "--port", webApiPort.ToString(), "--event-log", logPath, "--config", configPath],
            standardOutput,
            standardError,
            cancellation.Token);

        await WaitForStartupLogOrExitAsync(logPath, "runtime.started", runTask);
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{webApiPort}/api/events/stream"), cancellation.Token);

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{webApiPort}") };
        var response = await client.PostAsync("/api/runtime/shutdown", content: null, cancellation.Token);
        var exitCode = await runTask.WaitAsync(TimeSpan.FromSeconds(3), cancellation.Token);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(0, exitCode);
    }

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static int GetFreeUdpPort()
    {
        using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
    }

    private static int[] GetDistinctFreeLoopbackPorts(int count)
    {
        var ports = new HashSet<int>();
        while (ports.Count < count)
        {
            ports.Add(GetFreeLoopbackPort());
        }

        return [.. ports];
    }

    private static bool IsUdpPortAvailable(string address, int port)
    {
        try
        {
            using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Parse(address), port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static async Task WaitForStartupLogOrExitAsync(string path, string expected, Task<int> runTask)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (!timeout.IsCancellationRequested)
        {
            if (runTask.IsCompleted)
            {
                break;
            }

            if (File.Exists(path))
            {
                var content = await ReadSharedTextAsync(path, timeout.Token);
                if (content.Contains(expected, StringComparison.Ordinal))
                {
                    return;
                }
            }

            await Task.Delay(50, timeout.Token);
        }

        if (runTask.IsCompleted)
        {
            await runTask;
        }

        throw new TimeoutException($"Timed out waiting for '{expected}' in '{path}' or runtime exited before startup log was written.");
    }

    private static async Task<string> ReadSharedTextAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            useAsync: true);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

}
