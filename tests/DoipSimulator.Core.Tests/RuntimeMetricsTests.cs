using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Observability.Metrics;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Core.Tests;

public class RuntimeMetricsTests
{
    [Fact]
    public async Task SnapshotAggregatesConnectionsRatesQueuesAndMemory()
    {
        var registry = new ConnectionRegistry();
        var hub = new RuntimeEventHub();
        var collector = new RuntimeMetricsCollector(registry, hub);

        registry.AddTcpConnection("127.0.0.1:55000");
        registry.AddTcpConnection("127.0.0.1:55001");
        await hub.WriteAsync(CreateUdsRequestEvent());
        await collector.WriteAsync(CreateUdsRequestEvent());
        await collector.WriteAsync(RuntimeEvent.Create(
            RuntimeEventLevel.Info,
            RuntimeEventCategory.Pcap,
            "pcap.recording.started",
            "PCAP recording started."));
        collector.RecordPcapWrite(128);

        var snapshot = collector.GetSnapshot();

        Assert.Equal(2, snapshot.Connections.Active);
        Assert.Equal(2, snapshot.Connections.TotalAccepted);
        Assert.True(snapshot.Throughput.UdsRequestsPerSecond > 0);
        Assert.True(snapshot.WriteRates.LogEntriesPerSecond > 0);
        Assert.True(snapshot.WriteRates.PcapBytesPerSecond > 0);
        Assert.Equal(1, snapshot.Queues.Event.Length);
        Assert.Equal("available", snapshot.Queues.Event.State);
        Assert.True(snapshot.Memory.WorkingSetBytes > 0);
        Assert.True(snapshot.Memory.ManagedHeapBytes > 0);
    }

    [Fact]
    public void SnapshotUsesUnavailableQueueStateWhenNoEventHubExists()
    {
        var collector = new RuntimeMetricsCollector(new ConnectionRegistry());

        var snapshot = collector.GetSnapshot();

        Assert.Null(snapshot.Queues.Event.Length);
        Assert.Equal("unavailable", snapshot.Queues.Event.State);
        Assert.Equal(0, snapshot.Queues.Pcap.Length);
    }

    private static RuntimeEvent CreateUdsRequestEvent()
    {
        return RuntimeEvent.Create(
            RuntimeEventLevel.Info,
            RuntimeEventCategory.Uds,
            "uds.request.received",
            "UDS request accepted for dispatch.");
    }
}
