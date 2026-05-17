using DoipSimulator.Core.Ecu;

namespace DoipSimulator.Protocols.Uds.Tests;

public class EcuRuntimeStateTests
{
    [Fact]
    public void InitialStateUsesDefaultSessionAndLockedSecuritySummary()
    {
        var state = new EcuRuntimeState(0x0E00);

        Assert.Equal(0x0E00, state.LogicalAddress);
        Assert.Equal(DiagnosticSession.Default, state.CurrentSession);
        Assert.Equal("locked", state.SecurityStateSummary);
        Assert.Null(state.LastTesterPresentAt);
    }

    [Fact]
    public void StateUpdatesSessionAndTesterPresentTimestamp()
    {
        var state = new EcuRuntimeState(0x0E00);
        var acceptedAt = DateTimeOffset.Parse("2026-05-17T00:00:00Z");

        var previous = state.SetSession(DiagnosticSession.Extended);
        state.RecordTesterPresent(acceptedAt);

        Assert.Equal(DiagnosticSession.Default, previous);
        Assert.Equal(DiagnosticSession.Extended, state.CurrentSession);
        Assert.Equal(acceptedAt, state.LastTesterPresentAt);
    }
}
