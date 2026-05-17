namespace DoipSimulator.Protocols.Uds;

public enum NegativeResponseCode : byte
{
    ServiceNotSupported = 0x11,
    SubFunctionNotSupported = 0x12,
    IncorrectMessageLengthOrInvalidFormat = 0x13,
    ConditionsNotCorrect = 0x22,
    RequestOutOfRange = 0x31,
    SecurityAccessDenied = 0x33,
}

public sealed record NegativeResponse(byte OriginalServiceId, NegativeResponseCode Code) : UdsResponse
{
    public const byte NegativeResponseServiceId = 0x7F;

    public override bool IsNegative => true;

    public override byte[] ToBytes() => [NegativeResponseServiceId, OriginalServiceId, (byte)Code];
}
