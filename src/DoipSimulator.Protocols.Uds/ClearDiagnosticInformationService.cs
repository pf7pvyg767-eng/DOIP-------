using DoipSimulator.Core.Configuration;

namespace DoipSimulator.Protocols.Uds;

public sealed class ClearDiagnosticInformationService : IUdsService
{
    public const byte Sid = 0x14;
    public const uint GroupOfAllDtcs = 0xFFFFFF;

    private readonly DtcRuntimeStore dtcRuntimeStore;

    public ClearDiagnosticInformationService(DtcRuntimeStore dtcRuntimeStore)
    {
        this.dtcRuntimeStore = dtcRuntimeStore;
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length != 3)
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat)];
        }

        var code = (uint)((request.Payload[0] << 16) | (request.Payload[1] << 8) | request.Payload[2]);
        if (code == GroupOfAllDtcs)
        {
            await dtcRuntimeStore.ClearAllAsync("uds", cancellationToken);
            return [new RawUdsResponse([0x54])];
        }

        var result = await dtcRuntimeStore.ClearAsync(code, "uds", cancellationToken);
        if (!result.Succeeded)
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.RequestOutOfRange)];
        }

        return [new RawUdsResponse([0x54])];
    }
}
