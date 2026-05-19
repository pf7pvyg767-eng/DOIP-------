using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Faults;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class FaultsApiTests
{
    [Fact]
    public async Task FaultApiReturnsAndUpdatesRuntimeProfile()
    {
        var configPath = CreateTempConfigPath();
        await using var app = CreateApp(configPath, out var baseAddress);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var initial = await client.GetFromJsonAsync<FaultRuntimeSnapshot>("/api/faults");
            Assert.NotNull(initial);
            Assert.False(initial!.Profile.Enabled);

            var profile = initial.Profile;
            profile.Enabled = true;
            profile.ResponseDelayMs = 15;
            profile.PauseResponses = true;

            var response = await client.PutAsJsonAsync("/api/faults", profile);
            var updated = await response.Content.ReadFromJsonAsync<FaultRuntimeSnapshot>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(updated);
            Assert.True(updated!.Profile.Enabled);
            Assert.Equal(15, updated.Profile.ResponseDelayMs);
            Assert.True(updated.PauseResponses);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task FaultApiRejectsInvalidProfileWithoutMutatingState()
    {
        var configPath = CreateTempConfigPath();
        await using var app = CreateApp(configPath, out var baseAddress);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            var invalid = new FaultProfile
            {
                Enabled = true,
                ResponseDelayMs = -1,
            };

            var response = await client.PutAsJsonAsync("/api/faults", invalid);
            var current = await client.GetFromJsonAsync<FaultRuntimeSnapshot>("/api/faults");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(current);
            Assert.False(current!.Profile.Enabled);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task NextNrcActionConfiguresOneShotOverride()
    {
        var configPath = CreateTempConfigPath();
        await using var app = CreateApp(configPath, out var baseAddress);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            var response = await client.PostAsJsonAsync(
                "/api/faults/actions/next-nrc",
                new FaultNextNrcRequest("0x22", "0x31"));
            var snapshot = await response.Content.ReadFromJsonAsync<FaultRuntimeSnapshot>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(snapshot?.Profile.NextNrc);
            Assert.Equal("0x22", snapshot!.Profile.NextNrc!.ServiceId);
            Assert.Equal("0x31", snapshot.Profile.NextNrc.Nrc);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task DisconnectActionClosesRegisteredConnection()
    {
        var configPath = CreateTempConfigPath();
        var registry = new ConnectionRegistry();
        var disconnected = false;
        var connection = registry.AddTcpConnection(
            "127.0.0.1:12345",
            disconnectAction: () =>
            {
                disconnected = true;
                return ValueTask.CompletedTask;
            });
        await using var app = CreateApp(configPath, out var baseAddress, registry);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            var response = await client.PostAsJsonAsync(
                "/api/faults/actions/disconnect",
                new FaultDisconnectRequest(connection.ConnectionId));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(disconnected);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication CreateApp(
        string configPath,
        out Uri baseAddress,
        ConnectionRegistry? connectionRegistry = null)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        return WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow, configPath),
            connectionRegistry: connectionRegistry);
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
}
