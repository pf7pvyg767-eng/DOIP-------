namespace DoipSimulator.Protocols.Doip;

public interface IDoipCodec
{
    DecodeResult<DoipHeader> DecodeHeader(ReadOnlySpan<byte> bytes);

    DecodeResult<DoipFrame> Decode(ReadOnlySpan<byte> bytes);

    DecodeResult<byte[]> Encode(DoipFrame frame);
}
