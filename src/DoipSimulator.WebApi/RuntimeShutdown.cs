using DoipSimulator.Core.Observability.Pcap;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.WebApi;

public sealed record RuntimeShutdownResponse(
    bool Accepted,
    bool AlreadyRequested,
    DateTimeOffset RequestedAt);

public interface IRuntimeShutdownSignal
{
    void RequestShutdown();
}

public sealed class CancellationTokenSourceRuntimeShutdownSignal : IRuntimeShutdownSignal
{
    private readonly CancellationTokenSource shutdown;

    public CancellationTokenSourceRuntimeShutdownSignal(CancellationTokenSource shutdown)
    {
        this.shutdown = shutdown;
    }

    public void RequestShutdown()
    {
        try
        {
            if (!shutdown.IsCancellationRequested)
            {
                shutdown.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }
}

internal sealed class NullRuntimeShutdownSignal : IRuntimeShutdownSignal
{
    public static NullRuntimeShutdownSignal Instance { get; } = new();

    private NullRuntimeShutdownSignal()
    {
    }

    public void RequestShutdown()
    {
    }
}

internal sealed class RuntimeShutdownRequestHandler
{
    private readonly IRuntimeShutdownSignal shutdownSignal;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset? requestedAt;

    public RuntimeShutdownRequestHandler(IRuntimeShutdownSignal shutdownSignal)
    {
        this.shutdownSignal = shutdownSignal;
    }

    public async ValueTask<RuntimeShutdownResponse> RequestAsync(
        IRuntimeEventPublisher eventPublisher,
        IPcapRecorder pcapRecorder,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (requestedAt is { } existingRequest)
            {
                return new RuntimeShutdownResponse(true, true, existingRequest);
            }

            var currentRequest = DateTimeOffset.UtcNow;
            requestedAt = currentRequest;

            await eventPublisher.PublishAsync(
                RuntimeEvent.Create(
                    RuntimeEventLevel.Info,
                    RuntimeEventCategory.System,
                    "system.shutdown.requested",
                    "Runtime shutdown requested from Web API.",
                    data: new Dictionary<string, object?>
                    {
                        ["requestedAt"] = currentRequest,
                    },
                    timestamp: currentRequest),
                cancellationToken);

            await StopPcapIfActiveAsync(eventPublisher, pcapRecorder, cancellationToken);
            shutdownSignal.RequestShutdown();

            return new RuntimeShutdownResponse(true, false, currentRequest);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async ValueTask StopPcapIfActiveAsync(
        IRuntimeEventPublisher eventPublisher,
        IPcapRecorder pcapRecorder,
        CancellationToken cancellationToken)
    {
        if (!pcapRecorder.GetStatus().Recording)
        {
            return;
        }

        try
        {
            await pcapRecorder.StopAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await eventPublisher.PublishAsync(
                RuntimeEvent.Create(
                    RuntimeEventLevel.Warning,
                    RuntimeEventCategory.System,
                    "system.shutdown.pcap_stop_failed",
                    "PCAP recording could not be stopped before shutdown.",
                    data: new Dictionary<string, object?>
                    {
                        ["error"] = exception.Message,
                    }),
                cancellationToken);
        }
    }
}
