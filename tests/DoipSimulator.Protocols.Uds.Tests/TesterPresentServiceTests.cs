using DoipSimulator.Core.Ecu;

namespace DoipSimulator.Protocols.Uds.Tests;

public class TesterPresentServiceTests
{
    [Fact]
    public async Task SupportedSubFunctionReturnsPositiveResponseAndUpdatesTimestamp()
    {
        var state = new EcuRuntimeState(0x0E00);
        var service = new TesterPresentService(state);
        var before = DateTimeOffset.UtcNow;

        var responses = await service.HandleAsync(
            new UdsRequest(0x3E, [0x00]),
            new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x7E, 0x00], response.ToBytes());
        Assert.NotNull(state.LastTesterPresentAt);
        Assert.True(state.LastTesterPresentAt >= before);
    }

    [Fact]
    public async Task UnknownSubFunctionReturnsNrcAndDoesNotUpdateTimestamp()
    {
        var state = new EcuRuntimeState(0x0E00);
        var service = new TesterPresentService(state);

        var responses = await service.HandleAsync(
            new UdsRequest(0x3E, [0x01]),
            new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x7F, 0x3E, 0x12], response.ToBytes());
        Assert.Null(state.LastTesterPresentAt);
    }

    [Fact]
    public async Task InvalidLengthReturnsNrcAndDoesNotUpdateTimestamp()
    {
        var state = new EcuRuntimeState(0x0E00);
        var service = new TesterPresentService(state);

        var responses = await service.HandleAsync(
            new UdsRequest(0x3E, []),
            new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x7F, 0x3E, 0x13], response.ToBytes());
        Assert.Null(state.LastTesterPresentAt);
    }
}
