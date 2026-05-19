using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Core.Observability.Pcap;

public sealed class PcapRecorder : IPcapRecorder, IAsyncDisposable
{
    public const long DefaultMaxBytes = 524_288_000;

    private readonly string outputDirectory;
    private readonly long maxBytes;
    private readonly IRuntimeEventPublisher eventPublisher;
    private readonly SemaphoreSlim gate = new(1, 1);
    private PcapWriter? writer;
    private string? filePath;
    private long bytesWritten;

    public PcapRecorder(
        string? outputDirectory = null,
        long maxBytes = DefaultMaxBytes,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        this.outputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "logs", "pcap")
            : outputDirectory;
        this.maxBytes = maxBytes;
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
    }

    public PcapRecordingStatus GetStatus()
    {
        return new PcapRecordingStatus(writer is not null, filePath, bytesWritten, maxBytes);
    }

    public async ValueTask<PcapRecordingStatus> StartAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (writer is not null)
            {
                return GetStatus();
            }

            Directory.CreateDirectory(outputDirectory);
            filePath = Path.Combine(outputDirectory, $"session-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.pcap");
            writer = new PcapWriter(filePath);
            bytesWritten = writer.BytesWritten;
            await PublishAsync(RuntimeEventLevel.Info, "pcap.recording.started", "PCAP recording started.", cancellationToken);
            return GetStatus();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await PublishAsync(RuntimeEventLevel.Error, "pcap.recording.error", "PCAP recording could not be started.", CancellationToken.None, exception.Message);
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<PcapRecordingStatus> StopAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (writer is null)
            {
                return GetStatus();
            }

            await StopCurrentSessionAsync("pcap.recording.stopped", "PCAP recording stopped.", RuntimeEventLevel.Info, cancellationToken);
            return GetStatus();
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask RecordAsync(PcapPacket packet, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (writer is null)
            {
                return;
            }

            var recordLength = writer.GetRecordLength(packet);
            if (bytesWritten + recordLength > maxBytes)
            {
                await StopCurrentSessionAsync(
                    "pcap.recording.size_limit_reached",
                    "PCAP recording stopped because the size limit was reached.",
                    RuntimeEventLevel.Warning,
                    cancellationToken);
                return;
            }

            await writer.WritePacketAsync(packet, cancellationToken);
            bytesWritten = writer.BytesWritten;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await PublishAsync(RuntimeEventLevel.Error, "pcap.recording.error", "PCAP packet recording failed.", CancellationToken.None, exception.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (writer is not null)
            {
                await writer.DisposeAsync();
                writer = null;
            }
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }

    private async ValueTask StopCurrentSessionAsync(
        string eventName,
        string message,
        RuntimeEventLevel level,
        CancellationToken cancellationToken)
    {
        var activeWriter = writer;
        writer = null;
        if (activeWriter is not null)
        {
            await activeWriter.DisposeAsync();
        }

        await PublishAsync(level, eventName, message, cancellationToken);
    }

    private ValueTask PublishAsync(
        RuntimeEventLevel level,
        string name,
        string message,
        CancellationToken cancellationToken,
        string? error = null)
    {
        var data = new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["bytesWritten"] = bytesWritten,
            ["maxBytes"] = maxBytes,
        };
        if (!string.IsNullOrWhiteSpace(error))
        {
            data["error"] = error;
        }

        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(level, RuntimeEventCategory.Pcap, name, message, data: data),
            cancellationToken);
    }
}
