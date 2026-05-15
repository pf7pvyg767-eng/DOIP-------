using DoipSimulator.Protocols.Doip;

namespace DoipSimulator.Protocols.Doip.Tests;

public class DoipCodecTests
{
    private readonly DoipCodec _codec = new();

    [Fact]
    public void EncodeDecodeRoundTripPreservesHeaderAndPayload()
    {
        var payload = new byte[] { 0x0E, 0x00, 0x10, 0x22, 0xF1, 0x90 };
        var frame = DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.DiagnosticMessage,
            payload);

        var encodeResult = _codec.Encode(frame);
        Assert.True(encodeResult.IsSuccess);
        Assert.NotNull(encodeResult.Value);

        var bytes = encodeResult.Value;
        Assert.Equal(DoipCodec.Iso13400ProtocolVersion, bytes[0]);
        Assert.Equal(0xFD, bytes[1]);
        Assert.Equal(0x80, bytes[2]);
        Assert.Equal(0x01, bytes[3]);
        Assert.Equal(0x00, bytes[4]);
        Assert.Equal(0x00, bytes[5]);
        Assert.Equal(0x00, bytes[6]);
        Assert.Equal(0x06, bytes[7]);

        var decodeResult = _codec.Decode(bytes);
        Assert.True(decodeResult.IsSuccess);
        Assert.NotNull(decodeResult.Value);

        var decoded = decodeResult.Value;
        Assert.Equal(DoipCodec.Iso13400ProtocolVersion, decoded.ProtocolVersion);
        Assert.Equal(0xFD, decoded.InverseProtocolVersion);
        Assert.Equal(DoipPayloadType.DiagnosticMessage.Value, decoded.PayloadTypeRawValue);
        Assert.True(decoded.PayloadType.IsKnown);
        Assert.Equal((uint)payload.Length, decoded.PayloadLength);
        Assert.Equal(payload, decoded.Payload);
    }

    [Fact]
    public void DecodeReturnsExplicitErrorWhenHeaderIsTooShort()
    {
        var result = _codec.Decode([0x02, 0xFD, 0x80]);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(DoipProtocolErrorCode.HeaderTooShort, result.Error.Code);
        Assert.Equal(DoipCodec.HeaderLength, result.Error.ExpectedHeaderLength);
        Assert.Equal(3, result.Error.ActualHeaderLength);
    }

    [Fact]
    public void DecodeReturnsExplicitErrorWhenProtocolVersionIsUnsupported()
    {
        var bytes = new byte[] { 0x03, 0xFC, 0x80, 0x01, 0x00, 0x00, 0x00, 0x00 };

        var result = _codec.Decode(bytes);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(DoipProtocolErrorCode.UnsupportedProtocolVersion, result.Error.Code);
        Assert.Equal((byte)0x03, result.Error.ProtocolVersion);
    }

    [Fact]
    public void DecodeReturnsExplicitErrorWhenInverseVersionDoesNotMatch()
    {
        var bytes = new byte[] { 0x02, 0xFC, 0x80, 0x01, 0x00, 0x00, 0x00, 0x00 };

        var result = _codec.Decode(bytes);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(DoipProtocolErrorCode.InverseVersionMismatch, result.Error.Code);
        Assert.Equal((byte)0x02, result.Error.ProtocolVersion);
        Assert.Equal((byte)0xFC, result.Error.InverseProtocolVersion);
    }

    [Fact]
    public void DecodeReturnsExplicitErrorWhenPayloadLengthIsSmallerThanActualPayload()
    {
        var bytes = new byte[] { 0x02, 0xFD, 0x80, 0x01, 0x00, 0x00, 0x00, 0x01, 0xAA, 0xBB };

        var result = _codec.Decode(bytes);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(DoipProtocolErrorCode.PayloadLengthMismatch, result.Error.Code);
        Assert.Equal((uint)1, result.Error.DeclaredPayloadLength);
        Assert.Equal((uint)2, result.Error.ActualPayloadLength);
    }

    [Fact]
    public void DecodeReturnsExplicitErrorWhenPayloadLengthIsGreaterThanActualPayload()
    {
        var bytes = new byte[] { 0x02, 0xFD, 0x80, 0x01, 0x00, 0x00, 0x00, 0x03, 0xAA, 0xBB };

        var result = _codec.Decode(bytes);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(DoipProtocolErrorCode.PayloadLengthMismatch, result.Error.Code);
        Assert.Equal((uint)3, result.Error.DeclaredPayloadLength);
        Assert.Equal((uint)2, result.Error.ActualPayloadLength);
    }

    [Fact]
    public void DecodePreservesUnknownPayloadTypeWithoutFailing()
    {
        var bytes = new byte[] { 0x02, 0xFD, 0x12, 0x34, 0x00, 0x00, 0x00, 0x01, 0xAA };

        var result = _codec.Decode(bytes);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(0x1234, result.Value.PayloadTypeRawValue);
        Assert.False(result.Value.PayloadType.IsKnown);
        Assert.Null(result.Value.PayloadType.KnownName);
        Assert.Equal([0xAA], result.Value.Payload);
    }

    [Fact]
    public void EncodeReturnsExplicitErrorWhenPayloadLengthMetadataDoesNotMatchPayload()
    {
        var header = DoipHeader.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.DiagnosticMessage,
            1);
        var frame = new DoipFrame(header, [0xAA, 0xBB]);

        var result = _codec.Encode(frame);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(DoipProtocolErrorCode.InvalidEncodeInput, result.Error.Code);
        Assert.Equal((uint)1, result.Error.DeclaredPayloadLength);
        Assert.Equal((uint)2, result.Error.ActualPayloadLength);
    }

    [Fact]
    public void CodecTestsUseOnlyInMemoryBytes()
    {
        Assert.Equal(DoipCodec.HeaderLength, _codec.Encode(DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.VehicleIdentificationRequest,
            [])).Value!.Length);
    }
}
