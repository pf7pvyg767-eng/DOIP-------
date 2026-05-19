using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Observability.Metrics;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class MetricsApiTests
{
    [Fact]
    public async Task GetMetricsReturnsRuntimeMetricsSnapshot()
    {
        var registry = new ConnectionRegistry();
        registry.AddTcpConnection("127.0.0.1:55000");
        var hub = new RuntimeEventHub();
        var collector = new RuntimeMetricsCollector(registry, hub);
        await collector.WriteAsync(RuntimeEvent.Create(
            RuntimeEventLevel.Info,
            RuntimeEventCategory.Uds,
            "uds.request.received",
            "UDS request accepted for dispatch."));
        await using var app = CreateApp(out var baseAddress, registry, hub, collector);

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var snapshot = await client.GetFromJsonAsync<RuntimeMetricsSnapshot>("/api/metrics");

            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot!.Connections.Active);
            Assert.Equal(1, snapshot.Connections.TotalAccepted);
            Assert.True(snapshot.Throughput.UdsRequestsPerSecond > 0);
            Assert.Equal("available", snapshot.Queues.Event.State);
            Assert.True(snapshot.Memory.WorkingSetBytes > 0);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task GetMetricsIsReadOnlyForConnectionsAndEvents()
    {
        var registry = new ConnectionRegistry();
        registry.AddTcpConnection("127.0.0.1:55000");
        var hub = new RuntimeEventHub();
        var collector = new RuntimeMetricsCollector(registry, hub);
        await hub.WriteAsync(RuntimeEvent.Create(
            RuntimeEventLevel.Info,
            RuntimeEventCategory.System,
            "runtime.started",
            "Simulator runtime started."));
        await using var app = CreateApp(out var baseAddress, registry, hub, collector);

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            var eventCountBeforeMetricsReads = hub.RecentCount;

            _ = await client.GetFromJsonAsync<RuntimeMetricsSnapshot>("/api/metrics");
            _ = await client.GetFromJsonAsync<RuntimeMetricsSnapshot>("/api/metrics");

            Assert.Equal(1, registry.ActiveCount);
            Assert.Equal(1, registry.TotalAccepted);
            Assert.Equal(eventCountBeforeMetricsReads, hub.RecentCount);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication CreateApp(
        out Uri baseAddress,
        ConnectionRegistry registry,
        RuntimeEventHub hub,
        RuntimeMetricsCollector collector)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        return WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow),
            runtimeEventHub: hub,
            connectionRegistry: registry,
            metricsCollector: collector);
    }

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
