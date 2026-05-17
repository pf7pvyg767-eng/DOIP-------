namespace DoipSimulator.Protocols.Doip;

public sealed record DoipFrame(DoipHeader Header, byte[] Payload)
{
    public byte ProtocolVersion => Header.ProtocolVersion;

    public byte InverseProtocolVersion => Header.InverseProtocolVersion;

    public DoipPayloadType PayloadType => Header.PayloadType;

    public ushort PayloadTypeRawValue => Header.PayloadTypeRawValue;

    public uint PayloadLength => Header.PayloadLength;

    public static DoipFrame Create(byte protocolVersion, DoipPayloadType payloadType, byte[]? payload)
    {
        var payloadBytes = payload ?? [];
        var header = DoipHeader.Create(protocolVersion, payloadType, checked((uint)payloadBytes.Length));

        return new DoipFrame(header, payloadBytes);
    }
}
