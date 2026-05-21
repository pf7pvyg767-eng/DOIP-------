using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Configuration;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class RuntimeSummaryApiTests
{
    [Fact]
    public async Task GetRuntimeSummaryReturnsConnectionGuideFields()
    {
        await using var app = CreateApp(
            out var baseAddress,
            out var options,
            out var config,
            out _,
            out var connectionRegistry);
        connectionRegistry.AddTcpConnection("127.0.0.1:54321");
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var summary = await client.GetFromJsonAsync<RuntimeSummaryResponse>("/api/runtime/summary");

            Assert.NotNull(summary);
            Assert.Equal(options.ListenAddress, summary.WebApiListenAddress);
            Assert.Equal(options.Port, summary.WebApiPort);
            Assert.Equal($"http://{options.ListenAddress}:{options.Port}", summary.WebApiEndpoint);
            Assert.Equal(config.Network.DoipUdpPort, summary.DoipUdpPort);
            Assert.Equal(config.Network.DoipTcpPort, summary.DoipTcpPort);
            Assert.Equal(config.Network.DoipTlsPort, summary.DoipTlsPort);
            Assert.False(summary.TlsEnabled);
            Assert.Equal(config.Entity.Vin, summary.Vin);
            Assert.Equal(config.Entity.LogicalAddress, summary.EcuLogicalAddress);
            Assert.Equal(config.Network.SourceAddressWhitelist, summary.TesterSourceAddressWhitelist);
            Assert.Equal(options.StartedAt, summary.StartedAt);
            Assert.Equal(Environment.ProcessId, summary.ProcessId);
            Assert.Equal(1, summary.ActiveConnectionCount);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task GetRuntimeSummaryUsesNonDefaultPortsAndConfigPath()
    {
        await using var app = CreateApp(
            out var baseAddress,
            out var options,
            out var config,
            out var configPath,
            out _);
        config.Network.DoipUdpPort = 13411;
        config.Network.DoipTcpPort = 13412;
        config.Network.DoipTlsPort = 3497;
        config.Network.SourceAddressWhitelist = ["0x0E80", "0x0E81"];
        await new ConfigStore().SaveAsync(configPath, config);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var summary = await client.GetFromJsonAsync<RuntimeSummaryResponse>("/api/runtime/summary");

            Assert.NotNull(summary);
            Assert.Equal(options.Port, summary.WebApiPort);
            Assert.Equal(13411, summary.DoipUdpPort);
            Assert.Equal(13412, summary.DoipTcpPort);
            Assert.Equal(3497, summary.DoipTlsPort);
            Assert.Equal(["0x0E80", "0x0E81"], summary.TesterSourceAddressWhitelist);
            Assert.Equal(configPath, summary.ConfigPath);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task GetRuntimeSummaryWorksWithDefaultConfigPath()
    {
        var port = GetFreeLoopbackPort();
        var options = new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow);
        await using var app = WebApiApplication.Create([], options);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            var response = await client.GetAsync("/api/runtime/summary");
            var summary = await response.Content.ReadFromJsonAsync<RuntimeSummaryResponse>();

            Assert.True(response.IsSuccessStatusCode);
            Assert.NotNull(summary);
            Assert.False(string.IsNullOrWhiteSpace(summary.ConfigPath));
            Assert.Equal(13400, summary.DoipUdpPort);
            Assert.Equal(13400, summary.DoipTcpPort);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task GetRuntimeSummaryIsReadOnly()
    {
        await using var app = CreateApp(
            out var baseAddress,
            out _,
            out _,
            out _,
            out var connectionRegistry);
        var connection = connectionRegistry.AddTcpConnection("127.0.0.1:54321");
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var response = await client.GetAsync("/api/runtime/summary");
            var after = connectionRegistry.Get(connection.ConnectionId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(after);
            Assert.False(after.RoutingActivated);
            Assert.Equal(1, connectionRegistry.ActiveCount);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication CreateApp(
        out Uri baseAddress,
        out WebApiRuntimeOptions options,
        out SimulatorConfig config,
        out string configPath,
        out ConnectionRegistry connectionRegistry)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        options = new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow, CreateTempConfigPath());
        configPath = options.ConfigPath!;
        config = SimulatorConfig.CreateDefault();
        var configStore = new ConfigStore();
        configStore.SaveAsync(configPath, config).GetAwaiter().GetResult();
        connectionRegistry = new ConnectionRegistry();

        return WebApiApplication.Create(
            [],
            options,
            configStore,
            connectionRegistry: connectionRegistry);
    }

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string CreateTempConfigPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "simulator.json");
    }
}
