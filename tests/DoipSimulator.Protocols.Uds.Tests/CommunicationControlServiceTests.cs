using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds.Tests;

public class CommunicationControlServiceTests
{
    [Fact]
    public async Task SupportedRequestUpdatesStateAndReturnsPositiveResponse()
    {
        var store = new ControlServiceStateStore(SimulatorConfig.CreateDefault());
        var service = new CommunicationControlService(store);

        var responses = await service.HandleAsync(
            new UdsRequest(0x28, [0x03, 0x03]),
            new UdsContext());
        var snapshot = store.GetSnapshot();

        Assert.Equal([0x68, 0x03], Assert.Single(responses).ToBytes());
        Assert.Equal("disableRxAndTx", snapshot.CommunicationControl.ControlType);
        Assert.Equal("normalAndNetworkManagementCommunication", snapshot.CommunicationControl.CommunicationType);
    }

    [Theory]
    [InlineData(new byte[] { }, 0x13)]
    [InlineData(new byte[] { 0x00 }, 0x13)]
    [InlineData(new byte[] { 0x00, 0x01, 0x00 }, 0x13)]
    [InlineData(new byte[] { 0x7F, 0x01 }, 0x12)]
    [InlineData(new byte[] { 0x00, 0x7F }, 0x31)]
    public async Task UnsupportedCommunicationControlRequestReturnsNrc(byte[] payload, byte expectedNrc)
    {
        var store = new ControlServiceStateStore(SimulatorConfig.CreateDefault());
        var service = new CommunicationControlService(store);

        var responses = await service.HandleAsync(
            new UdsRequest(0x28, payload),
            new UdsContext());

        Assert.Equal([0x7F, 0x28, expectedNrc], Assert.Single(responses).ToBytes());
    }

    [Fact]
    public async Task StateChangePublishesRuntimeEvent()
    {
        var sink = new CapturingEventSink();
        var store = new ControlServiceStateStore(SimulatorConfig.CreateDefault(), new RuntimeEventBus([sink]));
        var service = new CommunicationControlService(store);

        await service.HandleAsync(
            new UdsRequest(0x28, [0x01, 0x01]),
            new UdsContext());

        Assert.Contains(sink.Events, item =>
            item.Name == "uds.communicationControl.changed" &&
            item.Data!["serviceId"]?.Equals("0x28") == true &&
            item.Data!["controlType"]?.Equals("enableRxDisableTx") == true);
    }
}
