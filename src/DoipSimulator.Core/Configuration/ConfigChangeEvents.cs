namespace DoipSimulator.Core.Configuration;

public sealed record ConfigChangedEvent(DateTimeOffset ChangedAt, string ConfigPath);

public interface IConfigChangePublisher
{
    void Publish(ConfigChangedEvent changeEvent);
}

public sealed class NullConfigChangePublisher : IConfigChangePublisher
{
    public static NullConfigChangePublisher Instance { get; } = new();

    private NullConfigChangePublisher()
    {
    }

    public void Publish(ConfigChangedEvent changeEvent)
    {
    }
}
