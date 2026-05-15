namespace DoipSimulator.Core.RuntimeEvents;

public sealed record RuntimeEventPublishError(
    DateTimeOffset OccurredAt,
    string SinkName,
    string Message,
    Exception? Exception = null);

public interface IRuntimeEventSink
{
    ValueTask WriteAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default);
}

public interface IRuntimeEventPublisher
{
    RuntimeEventPublishError? LastError { get; }

    ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default);
}

public sealed class NullRuntimeEventPublisher : IRuntimeEventPublisher
{
    public static NullRuntimeEventPublisher Instance { get; } = new();

    private NullRuntimeEventPublisher()
    {
    }

    public RuntimeEventPublishError? LastError => null;

    public ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}

public sealed class RuntimeEventBus : IRuntimeEventPublisher
{
    private readonly IReadOnlyList<IRuntimeEventSink> sinks;

    public RuntimeEventBus(IEnumerable<IRuntimeEventSink> sinks)
    {
        this.sinks = sinks.ToArray();
    }

    public RuntimeEventPublishError? LastError { get; private set; }

    public async ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
    {
        foreach (var sink in sinks)
        {
            try
            {
                await sink.WriteAsync(runtimeEvent, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                LastError = new RuntimeEventPublishError(
                    DateTimeOffset.UtcNow,
                    sink.GetType().Name,
                    exception.Message,
                    exception);
            }
        }
    }
}
