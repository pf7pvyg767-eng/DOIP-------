namespace DoipSimulator.Protocols.Doip;

public sealed record DoipProtocolError(
    DoipProtocolErrorCode Code,
    string Message,
    byte? ProtocolVersion = null,
    byte? InverseProtocolVersion = null,
    uint? DeclaredPayloadLength = null,
    uint? ActualPayloadLength = null,
    int? ExpectedHeaderLength = null,
    int? ActualHeaderLength = null);
