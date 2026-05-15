namespace DoipSimulator.Protocols.Doip;

public sealed record DoipHeader(
    byte ProtocolVersion,
    byte InverseProtocolVersion,
    DoipPayloadType PayloadType,
    uint PayloadLength)
{
    public ushort PayloadTypeRawValue => PayloadType.Value;

    public static DoipHeader Create(byte protocolVersion, DoipPayloadType payloadType, uint payloadLength)
    {
        return new DoipHeader(protocolVersion, (byte)~protocolVersion, payloadType, payloadLength);
    }
}
