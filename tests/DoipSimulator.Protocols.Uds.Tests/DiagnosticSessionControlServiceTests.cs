using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds.Tests;

public class DiagnosticSessionControlServiceTests
{
    public static IEnumerable<object[]> SupportedSessions()
    {
        yield return [0x01, DiagnosticSession.Default];
        yield return [0x03, DiagnosticSession.Extended];
        yield return [0x02, DiagnosticSession.Programming];
    }

    [Theory]
    [MemberData(nameof(SupportedSessions))]
    public async Task SupportedSubFunctionsChangeSessionAndReturnP2Parameters(byte subFunction, DiagnosticSession expectedSession)
    {
        var state = new EcuRuntimeState(0x0E00);
        var service = new DiagnosticSessionControlService(state);

        var responses = await service.HandleAsync(
            new UdsRequest(0x10, [subFunction]),
            new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x50, subFunction, 0x00, 0x32, 0x13, 0x88], response.ToBytes());
        Assert.Equal(expectedSession, state.CurrentSession);
    }

    [Fact]
    public async Task UnknownSubFunctionReturnsNrcAndKeepsSession()
    {
        var state = new EcuRuntimeState(0x0E00);
        var service = new DiagnosticSessionControlService(state);

        var responses = await service.HandleAsync(
            new UdsRequest(0x10, [0x7F]),
            new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x7F, 0x10, 0x12], response.ToBytes());
        Assert.Equal(DiagnosticSession.Default, state.CurrentSession);
    }

    [Fact]
    public async Task InvalidLengthReturnsNrcAndKeepsSession()
    {
        var state = new EcuRuntimeState(0x0E00);
        var service = new DiagnosticSessionControlService(state);

        var responses = await service.HandleAsync(
            new UdsRequest(0x10, []),
            new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x7F, 0x10, 0x13], response.ToBytes());
        Assert.Equal(DiagnosticSession.Default, state.CurrentSession);
    }

    [Fact]
    public async Task AcceptedSessionPublishesSessionChangeEvent()
    {
        var sink = new CapturingEventSink();
        var state = new EcuRuntimeState(0x0E00);
        var service = new DiagnosticSessionControlService(state, new RuntimeEventBus([sink]));

        await service.HandleAsync(
            new UdsRequest(0x10, [0x03]),
            new UdsContext("conn_000001", "127.0.0.1:50000", "0x0E80", "0x0E00"));

        var runtimeEvent = Assert.Single(sink.Events, item => item.Name == "uds.session.changed");
        Assert.Equal(RuntimeEventCategory.Uds, runtimeEvent.Category);
        Assert.Equal("default", runtimeEvent.Data!["previousSession"]);
        Assert.Equal("extended", runtimeEvent.Data!["newSession"]);
        Assert.Equal("0x0E00", runtimeEvent.Data!["ecuLogicalAddress"]);
    }
}
