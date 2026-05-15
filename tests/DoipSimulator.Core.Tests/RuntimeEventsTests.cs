using System.Text;
using System.Text.Json;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Observability.Logging;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Core.Tests;

public class RuntimeEventsTests
{
    [Fact]
    public void RuntimeEventSerializesCoreFields()
    {
        var runtimeEvent = RuntimeEvent.Create(
            RuntimeEventLevel.Warning,
            RuntimeEventCategory.Pcap,
            "event.name",
            "Event message.",
            "connection-1",
            new Dictionary<string, object?>
            {
                ["key"] = "value",
            },
            DateTimeOffset.Parse("2026-05-15T00:00:00Z"));

        var json = JsonSerializer.Serialize(runtimeEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("id").GetString()));
        Assert.Equal("2026-05-15T00:00:00+00:00", root.GetProperty("timestamp").GetString());
        Assert.Equal("warning", root.GetProperty("level").GetString());
        Assert.Equal("pcap", root.GetProperty("category").GetString());
        Assert.Equal("event.name", root.GetProperty("name").GetString());
        Assert.Equal("Event message.", root.GetProperty("message").GetString());
        Assert.Equal("connection-1", root.GetProperty("connectionId").GetString());
        Assert.Equal("value", root.GetProperty("data").GetProperty("key").GetString());
    }

    [Fact]
    public async Task FileSinkWritesMultipleUtf8JsonLines()
    {
        var logPath = CreateTempPath("runtime-events.log");
        await using var sink = new FileRuntimeEventSink(logPath);

        await sink.WriteAsync(RuntimeEvent.Create(
            RuntimeEventLevel.Info,
            RuntimeEventCategory.System,
            "runtime.started",
            "运行时已启动。"));
        await sink.WriteAsync(RuntimeEvent.Create(
            RuntimeEventLevel.Info,
            RuntimeEventCategory.Config,
            "config.loaded",
            "配置已加载。"));

        var bytes = await File.ReadAllBytesAsync(logPath);
        var content = Encoding.UTF8.GetString(bytes);
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.DoesNotContain('\uFFFD', content);
        Assert.Contains("运行时已启动", content);
        Assert.Equal("runtime.started", JsonDocument.Parse(lines[0]).RootElement.GetProperty("name").GetString());
        Assert.Equal("config.loaded", JsonDocument.Parse(lines[1]).RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task FileSinkCapturesWriteFailureAsDegradedError()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        await using var sink = new FileRuntimeEventSink(directoryPath);

        await sink.WriteAsync(RuntimeEvent.Create(
            RuntimeEventLevel.Info,
            RuntimeEventCategory.System,
            "runtime.started",
            "Simulator runtime started."));

        Assert.NotNull(sink.LastError);
    }

    [Fact]
    public async Task EventBusDoesNotPropagateSinkFailures()
    {
        var failingSink = new FailingRuntimeEventSink();
        var recordingSink = new RecordingRuntimeEventSink();
        var bus = new RuntimeEventBus([failingSink, recordingSink]);
        var runtimeEvent = RuntimeEvent.Create(
            RuntimeEventLevel.Info,
            RuntimeEventCategory.System,
            "runtime.started",
            "Simulator runtime started.");

        await bus.PublishAsync(runtimeEvent);

        Assert.NotNull(bus.LastError);
        Assert.Single(recordingSink.Events);
        Assert.Equal("runtime.started", recordingSink.Events[0].Name);
    }

    [Fact]
    public async Task ConfigStorePublishesLoadAndSaveEvents()
    {
        var publisher = new RecordingRuntimeEventPublisher();
        var store = new ConfigStore(publisher);
        var configPath = CreateTempPath("simulator.json");
        var config = SimulatorConfig.CreateDefault();

        await store.LoadAsync(configPath);
        await store.SaveAsync(configPath, config);

        Assert.Contains(publisher.Events, item => item.Name == "config.loaded" && item.Category == RuntimeEventCategory.Config);
        Assert.Contains(publisher.Events, item => item.Name == "config.saved" && item.Category == RuntimeEventCategory.Config);
    }

    private static string CreateTempPath(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }

    private sealed class FailingRuntimeEventSink : IRuntimeEventSink
    {
        public ValueTask WriteAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
        {
            throw new IOException("sink failed");
        }
    }

    private sealed class RecordingRuntimeEventSink : IRuntimeEventSink
    {
        public List<RuntimeEvent> Events { get; } = [];

        public ValueTask WriteAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(runtimeEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingRuntimeEventPublisher : IRuntimeEventPublisher
    {
        public List<RuntimeEvent> Events { get; } = [];

        public RuntimeEventPublishError? LastError => null;

        public ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(runtimeEvent);
            return ValueTask.CompletedTask;
        }
    }
}
