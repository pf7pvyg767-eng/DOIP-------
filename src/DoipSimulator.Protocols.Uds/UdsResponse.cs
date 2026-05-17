namespace DoipSimulator.Protocols.Uds;

public abstract record UdsResponse
{
    public abstract bool IsNegative { get; }

    public virtual TimeSpan DelayBeforeSend => TimeSpan.Zero;

    public abstract byte[] ToBytes();
}

public sealed record RawUdsResponse(byte[] Bytes) : UdsResponse
{
    public override bool IsNegative => Bytes.Length > 0 && Bytes[0] == NegativeResponse.NegativeResponseServiceId;

    public override byte[] ToBytes() => [.. Bytes];
}

public sealed record DelayedUdsResponse(UdsResponse InnerResponse, TimeSpan Delay) : UdsResponse
{
    public override bool IsNegative => InnerResponse.IsNegative;

    public override TimeSpan DelayBeforeSend => Delay;

    public override byte[] ToBytes() => InnerResponse.ToBytes();
}
