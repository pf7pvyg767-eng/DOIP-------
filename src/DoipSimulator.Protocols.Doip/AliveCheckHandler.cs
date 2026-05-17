using System.Buffers.Binary;

namespace DoipSimulator.Protocols.Doip;

public sealed class AliveCheckHandler
{
    private readonly IDoipCodec codec;

    public AliveCheckHandler(IDoipCodec codec)
    {
        this.codec = codec;
    }

    public bool IsAliveCheckRequest(DoipFrame frame)
    {
        return frame.PayloadType == DoipPayloadType.AliveCheckRequest && frame.Payload.Length == 0;
    }

    public DecodeResult<byte[]> EncodeResponse(ushort entityLogicalAddress)
    {
        var payload = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(payload, entityLogicalAddress);

        return codec.Encode(DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.AliveCheckResponse,
            payload));
    }
}
