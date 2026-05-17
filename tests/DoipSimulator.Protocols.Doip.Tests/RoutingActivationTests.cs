using System.Buffers.Binary;
using DoipSimulator.Protocols.Doip;

namespace DoipSimulator.Protocols.Doip.Tests;

public class RoutingActivationTests
{
    private readonly DoipCodec codec = new();

    [Fact]
    public void DecodeRequestReadsTesterLogicalAddressAndActivationType()
    {
        var handler = new RoutingActivationHandler(codec);
        var frame = DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.RoutingActivationRequest,
            CreateRoutingActivationPayload(0x0E80, 0x00));

        var request = handler.DecodeRequest(frame);

        Assert.True(request.IsSuccess);
        Assert.Equal(0x0E80, request.Value!.TesterLogicalAddress);
        Assert.Equal(0x00, request.Value.ActivationType);
    }

    [Fact]
    public void DecodeRequestRejectsShortPayload()
    {
        var handler = new RoutingActivationHandler(codec);
        var frame = DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.RoutingActivationRequest,
            [0x0E]);

        var request = handler.DecodeRequest(frame);

        Assert.False(request.IsSuccess);
        Assert.Equal(DoipProtocolErrorCode.InvalidPayloadLength, request.Error!.Code);
    }

    [Fact]
    public void EncodeResponseCreatesRoutingActivationFrame()
    {
        var handler = new RoutingActivationHandler(codec);

        var encoded = handler.EncodeResponse(new RoutingActivationResponse(
            0x0E80,
            0x0E00,
            RoutingActivationResponseCode.SuccessfullyActivated));

        Assert.True(encoded.IsSuccess);
        var decoded = codec.Decode(encoded.Value!);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(DoipPayloadType.RoutingActivationResponse, decoded.Value!.PayloadType);
        Assert.Equal(0x0E80, BinaryPrimitives.ReadUInt16BigEndian(decoded.Value.Payload.AsSpan(0, 2)));
        Assert.Equal(0x0E00, BinaryPrimitives.ReadUInt16BigEndian(decoded.Value.Payload.AsSpan(2, 2)));
        Assert.Equal((byte)RoutingActivationResponseCode.SuccessfullyActivated, decoded.Value.Payload[4]);
    }

    [Fact]
    public void AliveCheckResponseContainsEntityLogicalAddress()
    {
        var handler = new AliveCheckHandler(codec);

        var encoded = handler.EncodeResponse(0x0E00);

        Assert.True(encoded.IsSuccess);
        var decoded = codec.Decode(encoded.Value!);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(DoipPayloadType.AliveCheckResponse, decoded.Value!.PayloadType);
        Assert.Equal(0x0E00, BinaryPrimitives.ReadUInt16BigEndian(decoded.Value.Payload));
    }

    private static byte[] CreateRoutingActivationPayload(ushort testerLogicalAddress, byte activationType)
    {
        var payload = new byte[RoutingActivationHandler.RequestPayloadLength];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), testerLogicalAddress);
        payload[2] = activationType;
        return payload;
    }
}
