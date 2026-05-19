using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Observability.Pcap;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Core.Observability.Metrics;

public sealed record RuntimeMetricsSnapshot(
    DateTimeOffset CollectedAt,
    ConnectionMetrics Connections,
    ThroughputMetrics Throughput,
    QueueMetrics Queues,
    WriteRateMetrics WriteRates,
    ProcessMemoryMetrics Memory);

public sealed record ConnectionMetrics(int Active, long TotalAccepted);

public sealed record ThroughputMetrics(double UdsRequestsPerSecond);

public sealed record QueueMetrics(QueueMetric Event, QueueMetric Pcap);

public sealed record QueueMetric(int? Length, string State);

public sealed record WriteRateMetrics(double LogEntriesPerSecond, double PcapBytesPerSecond);

public sealed record ProcessMemoryMetrics(long WorkingSetBytes, long ManagedHeapBytes);

public interface IPcapMetricsSink
{
    void RecordPcapWrite(long bytesWritten, DateTimeOffset? timestamp = null);
}

public sealed class RuntimeMetricsCollector : IRuntimeEventSink, IPcapMetricsSink
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(10);

    private readonly ConnectionRegistry connections;
    private readonly RuntimeEventHub? eventHub;
    private readonly RateCounter udsRequestRate;
    private readonly RateCounter logEntryRate;
    private readonly RateCounter pcapByteRate;
    private readonly TimeProvider timeProvider;

    public RuntimeMetricsCollector(
        ConnectionRegistry connections,
        RuntimeEventHub? eventHub = null,
        TimeSpan? rateWindow = null,
        TimeProvider? timeProvider = null)
    {
        this.connections = connections;
        this.eventHub = eventHub;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        var window = rateWindow ?? DefaultWindow;
        udsRequestRate = new RateCounter(window);
        logEntryRate = new RateCounter(window);
        pcapByteRate = new RateCounter(window);
    }

    public ValueTask WriteAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
    {
        logEntryRate.Record(1, runtimeEvent.Timestamp);
        if (runtimeEvent.Category == RuntimeEventCategory.Uds &&
            string.Equals(runtimeEvent.Name, "uds.request.received", StringComparison.Ordinal))
        {
            udsRequestRate.Record(1, runtimeEvent.Timestamp);
        }

        return ValueTask.CompletedTask;
    }

    public void RecordPcapWrite(long bytesWritten, DateTimeOffset? timestamp = null)
    {
        if (bytesWritten <= 0)
        {
            return;
        }

        pcapByteRate.Record(bytesWritten, timestamp ?? timeProvider.GetUtcNow());
    }

    public RuntimeMetricsSnapshot GetSnapshot(IPcapRecorder? pcapRecorder = null)
    {
        var now = timeProvider.GetUtcNow();
        var pcapStatus = pcapRecorder?.GetStatus();

        return new RuntimeMetricsSnapshot(
            now,
            new ConnectionMetrics(connections.ActiveCount, connections.TotalAccepted),
            new ThroughputMetrics(Math.Round(udsRequestRate.GetPerSecond(now), 3)),
            new QueueMetrics(
                new QueueMetric(eventHub?.RecentCount, eventHub is null ? "unavailable" : "available"),
                new QueueMetric(0, pcapStatus?.Recording == true ? "synchronous-writer" : "idle")),
            new WriteRateMetrics(
                Math.Round(logEntryRate.GetPerSecond(now), 3),
                Math.Round(pcapByteRate.GetPerSecond(now), 3)),
            new ProcessMemoryMetrics(
                Environment.WorkingSet,
                GC.GetTotalMemory(forceFullCollection: false)));
    }

    private sealed class RateCounter
    {
        private readonly object gate = new();
        private readonly TimeSpan window;
        private readonly Queue<(DateTimeOffset Timestamp, long Units)> samples = [];

        public RateCounter(TimeSpan window)
        {
            if (window <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(window), "Rate window must be greater than zero.");
            }

            this.window = window;
        }

        public void Record(long units, DateTimeOffset timestamp)
        {
            lock (gate)
            {
                samples.Enqueue((timestamp, units));
                Trim(timestamp);
            }
        }

        public double GetPerSecond(DateTimeOffset now)
        {
            lock (gate)
            {
                Trim(now);
                if (samples.Count == 0)
                {
                    return 0;
                }

                return samples.Sum(sample => sample.Units) / window.TotalSeconds;
            }
        }

        private void Trim(DateTimeOffset now)
        {
            var cutoff = now - window;
            while (samples.Count > 0 && samples.Peek().Timestamp < cutoff)
            {
                samples.Dequeue();
            }
        }
    }
}
