using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Ecu;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class RealtimeObservationApiTests
{
    [Fact]
    public async Task GetConnectionsReturnsCurrentConnectionSnapshots()
    {
        var registry = new ConnectionRegistry();
        var connection = registry.AddTcpConnection("127.0.0.1:55000", DateTimeOffset.Parse("2026-05-17T00:00:00Z"));
        registry.MarkRoutingActivated(connection.ConnectionId, 0x0E80, 0x0E00);
        await using var app = CreateApp(out var baseAddress, connectionRegistry: registry);

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var snapshots = await client.GetFromJsonAsync<ConnectionSnapshot[]>("/api/connections");

            var snapshot = Assert.Single(snapshots!);
            Assert.Equal(connection.ConnectionId, snapshot.ConnectionId);
            Assert.Equal("tcp", snapshot.Transport);
            Assert.Equal("127.0.0.1:55000", snapshot.RemoteEndpoint);
            Assert.True(snapshot.RoutingActivated);
            Assert.Equal("0x0E80", snapshot.TesterLogicalAddress);
            Assert.Equal("0x0E00", snapshot.EcuLogicalAddress);
            Assert.Equal("open", snapshot.State);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task GetConnectionsDistinguishesTcpAndTlsTransports()
    {
        var registry = new ConnectionRegistry();
        registry.AddTcpConnection("127.0.0.1:55000", DateTimeOffset.Parse("2026-05-17T00:00:00Z"));
        registry.AddTlsConnection("127.0.0.1:55001", DateTimeOffset.Parse("2026-05-17T00:00:01Z"));
        await using var app = CreateApp(out var baseAddress, connectionRegistry: registry);

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var snapshots = await client.GetFromJsonAsync<ConnectionSnapshot[]>("/api/connections");

            Assert.Collection(
                snapshots!,
                tcp => Assert.Equal("tcp", tcp.Transport),
                tls => Assert.Equal("tls", tls.Transport));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task GetConnectionsReturnsEmptyArrayWhenNoConnectionsExist()
    {
        await using var app = CreateApp(out var baseAddress);

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var snapshots = await client.GetFromJsonAsync<ConnectionSnapshot[]>("/api/connections");

            Assert.NotNull(snapshots);
            Assert.Empty(snapshots!);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task GetEcuStateReturnsCurrentRuntimeState()
    {
        var state = new EcuRuntimeState(0x0E00);
        state.SetSession(DiagnosticSession.Extended);
        state.RecordTesterPresent(DateTimeOffset.Parse("2026-05-17T00:01:00Z"));
        await using var app = CreateApp(out var baseAddress, ecuRuntimeState: state);

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var snapshot = await client.GetFromJsonAsync<EcuStateSnapshot>("/api/ecu/state");

            Assert.NotNull(snapshot);
            Assert.Equal("0x0E00", snapshot!.LogicalAddress);
            Assert.Equal("extended", snapshot.CurrentSession);
            Assert.Equal("locked", snapshot.SecurityStateSummary);
            Assert.Equal(DateTimeOffset.Parse("2026-05-17T00:01:00Z"), snapshot.LastTesterPresentAt);
            Assert.True(snapshot.Timing.TimeoutEnabled);
            Assert.Equal(5000, snapshot.Timing.TimeoutMs);
            Assert.Equal("extended", snapshot.Timing.CurrentSession);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication CreateApp(
        out Uri baseAddress,
        ConnectionRegistry? connectionRegistry = null,
        EcuRuntimeState? ecuRuntimeState = null)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        return WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow),
            connectionRegistry: connectionRegistry,
            ecuRuntimeState: ecuRuntimeState);
    }

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
