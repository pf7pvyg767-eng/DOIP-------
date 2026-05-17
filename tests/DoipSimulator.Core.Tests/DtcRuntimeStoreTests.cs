using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Core.Tests;

public class DtcRuntimeStoreTests
{
    [Fact]
    public async Task ActivateAndClearConfiguredDtcChangesSnapshot()
    {
        var store = CreateStore();

        var activated = await store.ActivateAsync(0x123456, "test", "0x2F");
        var cleared = await store.ClearAsync(0x123456, "test");

        Assert.True(activated.Succeeded);
        Assert.True(activated.Snapshot!.Active);
        Assert.Equal("0x2F", activated.Snapshot.Status);
        Assert.True(cleared.Succeeded);
        Assert.False(cleared.Snapshot!.Active);
        Assert.Equal("0x00", cleared.Snapshot.Status);
    }

    [Fact]
    public async Task UnknownDtcReturnsErrorWithoutChangingConfiguredState()
    {
        var store = CreateStore();

        var result = await store.ActivateAsync(0x654321, "test", "0x2F");

        Assert.False(result.Succeeded);
        Assert.Equal(DtcOperationFailure.UnknownDtc, result.Failure);
        Assert.All(store.List(), item => Assert.False(item.Active));
    }

    [Fact]
    public async Task DtcOperationsPublishEventsWithCodeOperationSourceAndStatus()
    {
        var sink = new CapturingEventSink();
        var store = CreateStore(new RuntimeEventBus([sink]));

        await store.ActivateAsync(0x123456, "api", "0x2F");
        await store.ClearAsync(0x123456, "uds");
        await store.ActivateAsync(0x654321, "api", "0x2F");

        Assert.Contains(sink.Events, item =>
            item.Name == "uds.dtc.changed" &&
            item.Data!["dtc"]?.Equals("0x123456") == true &&
            item.Data!["operation"]?.Equals("activate") == true &&
            item.Data!["source"]?.Equals("api") == true &&
            item.Data!["status"]?.Equals("0x2F") == true);
        Assert.Contains(sink.Events, item =>
            item.Name == "uds.dtc.changed" &&
            item.Data!["operation"]?.Equals("clear") == true &&
            item.Data!["source"]?.Equals("uds") == true);
        Assert.Contains(sink.Events, item =>
            item.Name == "uds.dtc.rejected" &&
            item.Data!["dtc"]?.Equals("0x654321") == true &&
            item.Data!["operation"]?.Equals("activate") == true);
    }

    private static DtcRuntimeStore CreateStore(IRuntimeEventPublisher? eventPublisher = null)
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dtcs =
        [
            new DtcConfig
            {
                Code = "0x123456",
                Name = "Configured DTC",
                Description = "Test DTC",
                Status = "0x00",
            },
        ];
        return new DtcRuntimeStore(config, eventPublisher);
    }

    private sealed class CapturingEventSink : IRuntimeEventSink
    {
        private readonly List<RuntimeEvent> events = [];

        public IReadOnlyList<RuntimeEvent> Events => events;

        public ValueTask WriteAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
        {
            events.Add(runtimeEvent);
            return ValueTask.CompletedTask;
        }
    }
}
