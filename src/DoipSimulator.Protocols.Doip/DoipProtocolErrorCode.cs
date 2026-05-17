namespace DoipSimulator.Protocols.Doip;

public enum DoipProtocolErrorCode
{
    HeaderTooShort,
    UnsupportedProtocolVersion,
    InverseVersionMismatch,
    PayloadLengthMismatch,
    InvalidEncodeInput
}
