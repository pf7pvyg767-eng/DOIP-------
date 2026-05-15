using System.Buffers.Binary;

namespace DoipSimulator.Protocols.Doip;

public sealed class DoipCodec : IDoipCodec
{
    public const int HeaderLength = 8;
    public const byte Iso13400ProtocolVersion = 0x02;

    public DecodeResult<DoipHeader> DecodeHeader(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderLength)
        {
            return DecodeResult<DoipHeader>.Failure(new DoipProtocolError(
                DoipProtocolErrorCode.HeaderTooShort,
                $"DoIP header requires {HeaderLength} bytes but received {bytes.Length}.",
                ExpectedHeaderLength: HeaderLength,
                ActualHeaderLength: bytes.Length));
        }

        var protocolVersion = bytes[0];
        var inverseProtocolVersion = bytes[1];
        var payloadType = BinaryPrimitives.ReadUInt16BigEndian(bytes[2..4]);
        var payloadLength = BinaryPrimitives.ReadUInt32BigEndian(bytes[4..8]);
        var header = new DoipHeader(
            protocolVersion,
            inverseProtocolVersion,
            DoipPayloadType.FromRawValue(payloadType),
            payloadLength);

        return DecodeResult<DoipHeader>.Success(header);
    }

    public DecodeResult<DoipFrame> Decode(ReadOnlySpan<byte> bytes)
    {
        var headerResult = DecodeHeader(bytes);
        if (!headerResult.IsSuccess || headerResult.Value is null)
        {
            return DecodeResult<DoipFrame>.Failure(headerResult.Error!);
        }

        var header = headerResult.Value;
        var validationError = ValidateHeader(header, checked((uint)(bytes.Length - HeaderLength)));
        if (validationError is not null)
        {
            return DecodeResult<DoipFrame>.Failure(validationError);
        }

        var payload = bytes[HeaderLength..].ToArray();
        return DecodeResult<DoipFrame>.Success(new DoipFrame(header, payload));
    }

    public DecodeResult<byte[]> Encode(DoipFrame frame)
    {
        if (frame is null)
        {
            return DecodeResult<byte[]>.Failure(new DoipProtocolError(
                DoipProtocolErrorCode.InvalidEncodeInput,
                "DoIP frame cannot be null."));
        }

        var payload = frame.Payload ?? [];
        var actualPayloadLength = checked((uint)payload.Length);
        var validationError = ValidateHeader(frame.Header, actualPayloadLength);
        if (validationError is not null)
        {
            return DecodeResult<byte[]>.Failure(validationError.Code == DoipProtocolErrorCode.PayloadLengthMismatch
                ? validationError with
                {
                    Code = DoipProtocolErrorCode.InvalidEncodeInput,
                    Message = "DoIP frame payload length metadata does not match the payload byte count."
                }
                : validationError);
        }

        var bytes = new byte[HeaderLength + payload.Length];
        bytes[0] = frame.Header.ProtocolVersion;
        bytes[1] = frame.Header.InverseProtocolVersion;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2, 2), frame.Header.PayloadTypeRawValue);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4, 4), actualPayloadLength);
        payload.CopyTo(bytes.AsSpan(HeaderLength));

        return DecodeResult<byte[]>.Success(bytes);
    }

    private static DoipProtocolError? ValidateHeader(DoipHeader header, uint actualPayloadLength)
    {
        if (header.ProtocolVersion != Iso13400ProtocolVersion)
        {
            return new DoipProtocolError(
                DoipProtocolErrorCode.UnsupportedProtocolVersion,
                $"Unsupported DoIP protocol version 0x{header.ProtocolVersion:X2}.",
                ProtocolVersion: header.ProtocolVersion);
        }

        var expectedInverse = (byte)~header.ProtocolVersion;
        if (header.InverseProtocolVersion != expectedInverse)
        {
            return new DoipProtocolError(
                DoipProtocolErrorCode.InverseVersionMismatch,
                $"DoIP inverse protocol version 0x{header.InverseProtocolVersion:X2} does not match protocol version 0x{header.ProtocolVersion:X2}.",
                ProtocolVersion: header.ProtocolVersion,
                InverseProtocolVersion: header.InverseProtocolVersion);
        }

        if (header.PayloadLength != actualPayloadLength)
        {
            return new DoipProtocolError(
                DoipProtocolErrorCode.PayloadLengthMismatch,
                $"DoIP payload length mismatch. Declared {header.PayloadLength} bytes but received {actualPayloadLength} bytes.",
                DeclaredPayloadLength: header.PayloadLength,
                ActualPayloadLength: actualPayloadLength);
        }

        return null;
    }
}
