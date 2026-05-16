namespace DoipSimulator.Protocols.Uds;

public abstract record UdsResponse
{
    public abstract bool IsNegative { get; }

    public abstract byte[] ToBytes();
}

public sealed record RawUdsResponse(byte[] Bytes) : UdsResponse
{
    public override bool IsNegative => Bytes.Length > 0 && Bytes[0] == NegativeResponse.NegativeResponseServiceId;

    public override byte[] ToBytes() => [.. Bytes];
}
