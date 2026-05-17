namespace DoipSimulator.Protocols.Uds;

public enum NegativeResponseCode : byte
{
    ServiceNotSupported = 0x11,
    IncorrectMessageLengthOrInvalidFormat = 0x13,
}

public sealed record NegativeResponse(byte OriginalServiceId, NegativeResponseCode Code) : UdsResponse
{
    public const byte NegativeResponseServiceId = 0x7F;

    public override bool IsNegative => true;

    public override byte[] ToBytes() => [NegativeResponseServiceId, OriginalServiceId, (byte)Code];
}
