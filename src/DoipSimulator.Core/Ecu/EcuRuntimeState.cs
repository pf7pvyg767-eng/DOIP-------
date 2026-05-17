namespace DoipSimulator.Core.Ecu;

public enum DiagnosticSession
{
    Default = 0x01,
    Programming = 0x02,
    Extended = 0x03,
}

public sealed class EcuRuntimeState
{
    private readonly Lock gate = new();

    public EcuRuntimeState(ushort logicalAddress)
    {
        LogicalAddress = logicalAddress;
    }

    public ushort LogicalAddress { get; }

    public string SecurityStateSummary => "locked";

    public DiagnosticSession CurrentSession
    {
        get
        {
            lock (gate)
            {
                return currentSession;
            }
        }
    }

    public DateTimeOffset? LastTesterPresentAt
    {
        get
        {
            lock (gate)
            {
                return lastTesterPresentAt;
            }
        }
    }

    private DiagnosticSession currentSession = DiagnosticSession.Default;
    private DateTimeOffset? lastTesterPresentAt;

    public DiagnosticSession SetSession(DiagnosticSession session)
    {
        lock (gate)
        {
            var previous = currentSession;
            currentSession = session;
            return previous;
        }
    }

    public void RecordTesterPresent(DateTimeOffset acceptedAt)
    {
        lock (gate)
        {
            lastTesterPresentAt = acceptedAt;
        }
    }
}
