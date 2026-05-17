using DoipSimulator.Core.Configuration;

namespace DoipSimulator.Protocols.Uds;

public sealed class ControlDtcSettingService : IUdsService
{
    public const byte Sid = 0x85;

    private readonly ControlServiceStateStore stateStore;

    public ControlDtcSettingService(ControlServiceStateStore stateStore)
    {
        this.stateStore = stateStore;
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length != 1)
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat)];
        }

        var settingType = request.Payload[0];
        if (!ControlServiceStateStore.IsSupportedDtcSettingType(settingType))
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.SubFunctionNotSupported)];
        }

        await stateStore.ApplyDtcSettingAsync(settingType, "uds", cancellationToken);
        return [new RawUdsResponse([0xC5, settingType])];
    }
}
