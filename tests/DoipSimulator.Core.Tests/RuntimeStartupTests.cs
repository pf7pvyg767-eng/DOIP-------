using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
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
        var port = GetFreeLoopbackPort();
        var logPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "runtime-events.log");
        var configPath = Path.Combine(Path.GetDirectoryName(logPath)!, "simulator-config.json");
        var config = SimulatorConfig.CreateDefault();
        config.Network.BindAddress = "127.0.0.1";
        config.Network.DoipUdpPort = GetFreeUdpPort();
        config.Network.DoipTcpPort = GetFreeLoopbackPort();
        await new ConfigStore().SaveAsync(configPath, config);
        using var cancellation = new CancellationTokenSource();

        var runTask = CliEntryPoint.RunAsync(
            ["run", "--listen-address", "127.0.0.1", "--port", port.ToString(), "--event-log", logPath, "--config", configPath],
            TextWriter.Null,
            TextWriter.Null,
            cancellation.Token);

        try
        {
            await WaitForLogContentAsync(logPath, "runtime.started");
        }
        finally
        {
            await cancellation.CancelAsync();
        }

        var exitCode = await runTask;
        var content = await File.ReadAllTextAsync(logPath);

        Assert.Equal(0, exitCode);
        Assert.Contains("runtime.started", content);
        Assert.Contains("runtime.stopped", content);
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

    private static async Task WaitForLogContentAsync(string path, string expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (!timeout.IsCancellationRequested)
        {
            if (File.Exists(path))
            {
                var content = await File.ReadAllTextAsync(path, timeout.Token);
                if (content.Contains(expected, StringComparison.Ordinal))
                {
                    return;
                }
            }

            await Task.Delay(50, timeout.Token);
        }

        throw new TimeoutException($"Timed out waiting for '{expected}' in '{path}'.");
    }

}
