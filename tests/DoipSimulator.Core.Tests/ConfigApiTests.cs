using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using DoipSimulator.Core.Configuration;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class ConfigApiTests
{
    [Fact]
    public async Task GetConfigReturnsDefaultConfigurationWhenFileIsMissing()
    {
        var configPath = CreateTempConfigPath();
        await using var app = CreateApp(configPath, out var baseAddress);

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var response = await client.GetAsync("/api/config");
            var config = await response.Content.ReadFromJsonAsync<SimulatorConfig>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(config);
            Assert.Equal("LTEST000000000001", config!.Entity.Vin);
            Assert.True(ConfigValidator.Validate(config).IsValid);
            Assert.False(File.Exists(configPath));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task PutConfigSavesValidConfigurationAndPublishesChangeEvent()
    {
        var configPath = CreateTempConfigPath();
        var publisher = new RecordingConfigChangePublisher();
        await using var app = CreateApp(configPath, out var baseAddress, publisher);
        var config = CreateValidModifiedConfig();

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var response = await client.PutAsJsonAsync("/api/config", config);
            var returned = await response.Content.ReadFromJsonAsync<SimulatorConfig>();
            var getResponse = await client.GetFromJsonAsync<SimulatorConfig>("/api/config");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(returned);
            Assert.Equal(config.Entity.Vin, returned!.Entity.Vin);
            Assert.NotNull(getResponse);
            Assert.Equal(config.Entity.Vin, getResponse!.Entity.Vin);
            Assert.Single(publisher.Events);
            Assert.Equal(configPath, publisher.Events[0].ConfigPath);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task PutInvalidConfigReturnsBadRequestWithFieldErrorsAndDoesNotSaveOrPublish()
    {
        var configPath = CreateTempConfigPath();
        var original = SimulatorConfig.CreateDefault();
        await new ConfigStore().SaveAsync(configPath, original);
        var publisher = new RecordingConfigChangePublisher();
        await using var app = CreateApp(configPath, out var baseAddress, publisher);
        var invalid = CreateValidModifiedConfig();
        invalid.Entity.Vin = "BAD";
        invalid.Entity.LogicalAddress = "0x10000";
        invalid.Network.DoipTcpPort = 70000;

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var response = await client.PutAsJsonAsync("/api/config", invalid);
            var error = await response.Content.ReadFromJsonAsync<ConfigValidationErrorResponse>();
            var reloaded = await new ConfigStore().LoadAsync(configPath);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(error);
            Assert.Equal("CONFIG_VALIDATION_FAILED", error!.Code);
            Assert.Contains(error.Errors, item => item.Path == "entity.vin");
            Assert.Contains(error.Errors, item => item.Path == "entity.logicalAddress");
            Assert.Contains(error.Errors, item => item.Path == "network.doipTcpPort");
            Assert.Equal(original.Entity.Vin, reloaded.Entity.Vin);
            Assert.Empty(publisher.Events);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task SavedConfigurationCanBeLoadedByRebuiltConfigStore()
    {
        var configPath = CreateTempConfigPath();
        await using var app = CreateApp(configPath, out var baseAddress);
        var config = CreateValidModifiedConfig();
        config.Entity.Vin = "LTEST000000000005";
        config.Entity.LogicalAddress = "0x0E05";

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var response = await client.PutAsJsonAsync("/api/config", config);
            var reloaded = await new ConfigStore().LoadAsync(configPath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("LTEST000000000005", reloaded.Entity.Vin);
            Assert.Equal("0x0E05", reloaded.Entity.LogicalAddress);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication CreateApp(
        string configPath,
        out Uri baseAddress,
        IConfigChangePublisher? publisher = null)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        return WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow, configPath),
            configChangePublisher: publisher);
    }

    private static SimulatorConfig CreateValidModifiedConfig()
    {
        var config = SimulatorConfig.CreateDefault();
        config.Entity.Vin = "LTEST000000000004";
        config.Entity.Eid = "101122334455";
        config.Entity.Gid = "BABBCCDDEEFF";
        config.Entity.LogicalAddress = "0x0E04";
        config.Network.BindAddress = "127.0.0.1";
        config.Network.DoipUdpPort = 13410;
        config.Network.DoipTcpPort = 13411;
        config.Network.DoipTlsPort = 3500;
        config.Network.SourceAddressWhitelist = ["0x0E84"];
        return config;
    }

    private static string CreateTempConfigPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "simulator.json");
    }

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class RecordingConfigChangePublisher : IConfigChangePublisher
    {
        public List<ConfigChangedEvent> Events { get; } = [];

        public void Publish(ConfigChangedEvent changeEvent)
        {
            Events.Add(changeEvent);
        }
    }
}
