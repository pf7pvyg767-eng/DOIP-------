using System.Buffers.Binary;

namespace DoipSimulator.Protocols.Doip;

public enum RoutingActivationResponseCode : byte
{
    DeniedUnknownSourceAddress = 0x00,
    SuccessfullyActivated = 0x10,
}

public sealed record RoutingActivationRequest(ushort TesterLogicalAddress, byte ActivationType);

public sealed record RoutingActivationResponse(
    ushort TesterLogicalAddress,
    ushort EntityLogicalAddress,
    RoutingActivationResponseCode ResponseCode);

public sealed class RoutingActivationHandler
{
    public const int RequestPayloadLength = 7;
    public const int ResponsePayloadLength = 9;

    private readonly IDoipCodec codec;

    public RoutingActivationHandler(IDoipCodec codec)
    {
        this.codec = codec;
    }

    public DecodeResult<RoutingActivationRequest> DecodeRequest(DoipFrame frame)
    {
        if (frame.PayloadType != DoipPayloadType.RoutingActivationRequest)
        {
            return DecodeResult<RoutingActivationRequest>.Failure(new DoipProtocolError(
                DoipProtocolErrorCode.InvalidPayloadLength,
                $"Expected Routing Activation Request payload type but received 0x{frame.PayloadTypeRawValue:X4}."));
        }

        if (frame.Payload.Length < RequestPayloadLength)
        {
            return DecodeResult<RoutingActivationRequest>.Failure(new DoipProtocolError(
                DoipProtocolErrorCode.InvalidPayloadLength,
                $"Routing Activation Request requires at least {RequestPayloadLength} payload bytes but received {frame.Payload.Length}.",
                DeclaredPayloadLength: (uint)RequestPayloadLength,
                ActualPayloadLength: (uint)frame.Payload.Length));
        }

        var testerLogicalAddress = BinaryPrimitives.ReadUInt16BigEndian(frame.Payload.AsSpan(0, 2));
        var activationType = frame.Payload[2];
        return DecodeResult<RoutingActivationRequest>.Success(new RoutingActivationRequest(
            testerLogicalAddress,
            activationType));
    }

    public DecodeResult<byte[]> EncodeResponse(RoutingActivationResponse response)
    {
        var payload = new byte[ResponsePayloadLength];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), response.TesterLogicalAddress);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), response.EntityLogicalAddress);
        payload[4] = (byte)response.ResponseCode;

        return codec.Encode(DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.RoutingActivationResponse,
            payload));
    }
}
