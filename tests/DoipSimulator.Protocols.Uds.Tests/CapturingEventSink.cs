using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds.Tests;

internal sealed class CapturingEventSink : IRuntimeEventSink
{
    private readonly Lock gate = new();
    private readonly List<RuntimeEvent> events = [];

    public IReadOnlyList<RuntimeEvent> Events
    {
        get
        {
            lock (gate)
            {
                return events.ToArray();
            }
        }
    }

    public ValueTask WriteAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            events.Add(runtimeEvent);
        }

        return ValueTask.CompletedTask;
    }
}
