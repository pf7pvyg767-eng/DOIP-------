namespace DoipSimulator.Core.Faults;

public sealed class FaultProfile
{
    public bool Enabled { get; set; }

    public int ResponseDelayMs { get; set; }

    public bool PauseResponses { get; set; }

    public bool RoutingActivationFailure { get; set; }

    public DoipHeaderFaultConfig CorruptNextDoipHeader { get; set; } = new();

    public UdsFaultOverrideConfig? NextNrc { get; set; }

    public UdsFaultOverrideConfig? NextCustomResponse { get; set; }

    public FaultProfile Clone()
    {
        return new FaultProfile
        {
            Enabled = Enabled,
            ResponseDelayMs = ResponseDelayMs,
            PauseResponses = PauseResponses,
            RoutingActivationFailure = RoutingActivationFailure,
            CorruptNextDoipHeader = CorruptNextDoipHeader.Clone(),
            NextNrc = NextNrc?.Clone(),
            NextCustomResponse = NextCustomResponse?.Clone(),
        };
    }

    public static FaultProfile Disabled() => new();
}

public sealed class DoipHeaderFaultConfig
{
    public bool InverseVersion { get; set; }

    public int PayloadLengthDelta { get; set; }

    public bool IsActive => InverseVersion || PayloadLengthDelta != 0;

    public DoipHeaderFaultConfig Clone()
    {
        return new DoipHeaderFaultConfig
        {
            InverseVersion = InverseVersion,
            PayloadLengthDelta = PayloadLengthDelta,
        };
    }
}

public sealed class UdsFaultOverrideConfig
{
    public string ServiceId { get; set; } = string.Empty;

    public string? Nrc { get; set; }

    public string? ResponseBytes { get; set; }

    public UdsFaultOverrideConfig Clone()
    {
        return new UdsFaultOverrideConfig
        {
            ServiceId = ServiceId,
            Nrc = Nrc,
            ResponseBytes = ResponseBytes,
        };
    }
}

public sealed record FaultRuntimeSnapshot(
    FaultProfile Profile,
    bool PauseResponses,
    bool HasPendingDoipHeaderFault,
    UdsFaultOverrideConfig? NextNrc,
    UdsFaultOverrideConfig? NextCustomResponse);
