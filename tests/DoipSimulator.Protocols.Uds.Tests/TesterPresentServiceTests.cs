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
    public async Task SupportedSubFunctionRefreshesTimeoutDeadlineWhenConfigured()
    {
        var state = new EcuRuntimeState(0x0E00);
        var service = new TesterPresentService(state, timeout: TimeSpan.FromMilliseconds(250));

        await service.HandleAsync(
            new UdsRequest(0x3E, [0x00]),
            new UdsContext(ConnectionId: "conn_000001"));

        var snapshot = state.GetTesterPresentTimingSnapshot(true, 250);
        Assert.NotNull(snapshot.LastAcceptedAt);
        Assert.NotNull(snapshot.TimeoutDeadline);
        Assert.True(snapshot.TimeoutDeadline > snapshot.LastAcceptedAt);
    }

    [Fact]
    public void TimeoutEvaluationFallsBackNonDefaultSessionAndRecordsState()
    {
        var state = new EcuRuntimeState(0x0E00);
        var acceptedAt = DateTimeOffset.Parse("2026-05-17T00:00:00Z");
        state.SetSession(DiagnosticSession.Extended);
        state.RecordTesterPresent(acceptedAt, TimeSpan.FromMilliseconds(100));

        var result = state.EvaluateTesterPresentTimeout(
            true,
            TimeSpan.FromMilliseconds(100),
            acceptedAt.AddMilliseconds(101));

        Assert.True(result.FellBack);
        Assert.Equal(DiagnosticSession.Extended, result.PreviousSession);
        Assert.Equal(DiagnosticSession.Default, state.CurrentSession);
        var snapshot = state.GetTesterPresentTimingSnapshot(true, 100);
        Assert.Equal("tester-present-timeout", snapshot.LastFallbackReason);
        Assert.Equal("extended", snapshot.LastFallbackPreviousSession);
    }

    [Fact]
    public void TimeoutEvaluationDoesNotEmitFallbackForDefaultSession()
    {
        var state = new EcuRuntimeState(0x0E00);

        var result = state.EvaluateTesterPresentTimeout(
            true,
            TimeSpan.FromMilliseconds(100),
            DateTimeOffset.Parse("2026-05-17T00:00:00Z"));

        Assert.False(result.FellBack);
        Assert.Equal(DiagnosticSession.Default, state.CurrentSession);
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
