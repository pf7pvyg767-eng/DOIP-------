using System.Threading.Channels;

namespace DoipSimulator.Core.RuntimeEvents;

public sealed class RuntimeEventHub : IRuntimeEventSink
{
    public const int DefaultCapacity = 1000;
    public const int DefaultRecentLimit = 200;
    public const int MaxRecentLimit = 1000;

    private readonly object gate = new();
    private readonly int capacity;
    private readonly Queue<RuntimeEvent> recentEvents;
    private readonly Dictionary<Guid, Channel<RuntimeEvent>> subscribers = [];

    public RuntimeEventHub(int capacity = DefaultCapacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        this.capacity = capacity;
        recentEvents = new Queue<RuntimeEvent>(capacity);
    }

    public int Capacity => capacity;

    public ValueTask WriteAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
    {
        Channel<RuntimeEvent>[] currentSubscribers;
        lock (gate)
        {
            recentEvents.Enqueue(runtimeEvent);
            while (recentEvents.Count > capacity)
            {
                recentEvents.Dequeue();
            }

            currentSubscribers = subscribers.Values.ToArray();
        }

        foreach (var subscriber in currentSubscribers)
        {
            subscriber.Writer.TryWrite(runtimeEvent);
        }

        return ValueTask.CompletedTask;
    }

    public IReadOnlyList<RuntimeEvent> GetRecent(int? limit = null, RuntimeEventCategory? category = null)
    {
        var boundedLimit = Math.Clamp(limit ?? DefaultRecentLimit, 1, Math.Min(MaxRecentLimit, capacity));

        lock (gate)
        {
            IEnumerable<RuntimeEvent> query = recentEvents;
            if (category is not null)
            {
                query = query.Where(item => item.Category == category);
            }

            return query.TakeLast(boundedLimit).ToArray();
        }
    }

    public RuntimeEventSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<RuntimeEvent>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        lock (gate)
        {
            subscribers[id] = channel;
        }

        return new RuntimeEventSubscription(channel.Reader, () => Unsubscribe(id));
    }

    private void Unsubscribe(Guid id)
    {
        Channel<RuntimeEvent>? channel;
        lock (gate)
        {
            if (!subscribers.Remove(id, out channel))
            {
                return;
            }
        }

        channel.Writer.TryComplete();
    }
}

public sealed class RuntimeEventSubscription : IDisposable
{
    private readonly Action dispose;
    private bool isDisposed;

    public RuntimeEventSubscription(ChannelReader<RuntimeEvent> events, Action dispose)
    {
        Events = events;
        this.dispose = dispose;
    }

    public ChannelReader<RuntimeEvent> Events { get; }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        dispose();
    }
}
