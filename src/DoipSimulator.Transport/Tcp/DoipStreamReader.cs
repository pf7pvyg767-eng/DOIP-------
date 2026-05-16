using DoipSimulator.Protocols.Doip;

namespace DoipSimulator.Transport.Tcp;

public sealed record DoipStreamReadResult(
    IReadOnlyList<DoipFrame> Frames,
    IReadOnlyList<DoipProtocolError> Errors);

public sealed class DoipStreamReader
{
    private const int DefaultMaxFrameLength = 1024 * 1024;
    private readonly IDoipCodec codec;
    private readonly int maxFrameLength;
    private readonly List<byte> buffer = [];

    public DoipStreamReader(IDoipCodec codec, int maxFrameLength = DefaultMaxFrameLength)
    {
        this.codec = codec;
        this.maxFrameLength = maxFrameLength;
    }

    public DoipStreamReadResult Append(ReadOnlySpan<byte> bytes)
    {
        buffer.AddRange(bytes.ToArray());

        var frames = new List<DoipFrame>();
        var errors = new List<DoipProtocolError>();

        while (buffer.Count >= DoipCodec.HeaderLength)
        {
            var headerResult = codec.DecodeHeader(buffer.Take(DoipCodec.HeaderLength).ToArray());
            if (!headerResult.IsSuccess || headerResult.Value is null)
            {
                errors.Add(headerResult.Error!);
                buffer.RemoveRange(0, DoipCodec.HeaderLength);
                continue;
            }

            var totalLength = checked(DoipCodec.HeaderLength + (int)headerResult.Value.PayloadLength);
            if (totalLength > maxFrameLength)
            {
                errors.Add(new DoipProtocolError(
                    DoipProtocolErrorCode.PayloadLengthMismatch,
                    $"DoIP frame length {totalLength} exceeds maximum stream frame length {maxFrameLength}.",
                    DeclaredPayloadLength: headerResult.Value.PayloadLength));
                buffer.Clear();
                break;
            }

            if (buffer.Count < totalLength)
            {
                break;
            }

            var candidate = buffer.Take(totalLength).ToArray();
            var decoded = codec.Decode(candidate);
            if (decoded.IsSuccess && decoded.Value is not null)
            {
                frames.Add(decoded.Value);
            }
            else if (decoded.Error is not null)
            {
                errors.Add(decoded.Error);
            }

            buffer.RemoveRange(0, totalLength);
        }

        return new DoipStreamReadResult(frames, errors);
    }
}
