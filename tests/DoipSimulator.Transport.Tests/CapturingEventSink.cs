using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Transport.Tests;

internal sealed class CapturingEventSink : IRuntimeEventSink
{
    private readonly List<RuntimeEvent> events = [];

    public IReadOnlyList<RuntimeEvent> Events => events;

    public ValueTask WriteAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
    {
        events.Add(runtimeEvent);
        return ValueTask.CompletedTask;
    }
}
