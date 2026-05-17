using DoipSimulator.Core.Configuration;

namespace DoipSimulator.Protocols.Uds;

public sealed class CommunicationControlService : IUdsService
{
    public const byte Sid = 0x28;

    private readonly ControlServiceStateStore stateStore;

    public CommunicationControlService(ControlServiceStateStore stateStore)
    {
        this.stateStore = stateStore;
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length != 2)
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat)];
        }

        var controlType = request.Payload[0];
        if (!ControlServiceStateStore.IsSupportedCommunicationControlType(controlType))
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.SubFunctionNotSupported)];
        }

        var communicationType = request.Payload[1];
        if (!ControlServiceStateStore.IsSupportedCommunicationType(communicationType))
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.RequestOutOfRange)];
        }

        await stateStore.ApplyCommunicationControlAsync(controlType, communicationType, "uds", cancellationToken);
        return [new RawUdsResponse([0x68, controlType])];
    }
}
