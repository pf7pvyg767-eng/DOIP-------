using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds.Tests;

public class ControlDtcSettingServiceTests
{
    [Fact]
    public async Task SupportedDisableAndEnableRequestsUpdateState()
    {
        var store = new ControlServiceStateStore(SimulatorConfig.CreateDefault());
        var service = new ControlDtcSettingService(store);

        var disableResponses = await service.HandleAsync(
            new UdsRequest(0x85, [0x02]),
            new UdsContext());
        var disabledSnapshot = store.GetSnapshot();
        var enableResponses = await service.HandleAsync(
            new UdsRequest(0x85, [0x01]),
            new UdsContext());
        var enabledSnapshot = store.GetSnapshot();

        Assert.Equal([0xC5, 0x02], Assert.Single(disableResponses).ToBytes());
        Assert.False(disabledSnapshot.DtcSetting.Enabled);
        Assert.Equal("off", disabledSnapshot.DtcSetting.SettingType);
        Assert.Equal([0xC5, 0x01], Assert.Single(enableResponses).ToBytes());
        Assert.True(enabledSnapshot.DtcSetting.Enabled);
        Assert.Equal("on", enabledSnapshot.DtcSetting.SettingType);
    }

    [Theory]
    [InlineData(new byte[] { }, 0x13)]
    [InlineData(new byte[] { 0x01, 0x00 }, 0x13)]
    [InlineData(new byte[] { 0x7F }, 0x12)]
    public async Task UnsupportedControlDtcSettingRequestReturnsNrc(byte[] payload, byte expectedNrc)
    {
        var store = new ControlServiceStateStore(SimulatorConfig.CreateDefault());
        var service = new ControlDtcSettingService(store);

        var responses = await service.HandleAsync(
            new UdsRequest(0x85, payload),
            new UdsContext());

        Assert.Equal([0x7F, 0x85, expectedNrc], Assert.Single(responses).ToBytes());
    }

    [Fact]
    public async Task StateChangePublishesRuntimeEvent()
    {
        var sink = new CapturingEventSink();
        var store = new ControlServiceStateStore(SimulatorConfig.CreateDefault(), new RuntimeEventBus([sink]));
        var service = new ControlDtcSettingService(store);

        await service.HandleAsync(
            new UdsRequest(0x85, [0x02]),
            new UdsContext());

        Assert.Contains(sink.Events, item =>
            item.Name == "uds.dtcSetting.changed" &&
            item.Data!["serviceId"]?.Equals("0x85") == true &&
            item.Data!["enabled"]?.Equals(false) == true);
    }
}
