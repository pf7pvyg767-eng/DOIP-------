using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
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

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
