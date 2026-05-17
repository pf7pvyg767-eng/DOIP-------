using DoipSimulator.Core.Configuration;

namespace DoipSimulator.Protocols.Uds;

public sealed class ReadDtcInformationService : IUdsService
{
    public const byte Sid = 0x19;
    public const byte ReportDtcByStatusMask = 0x02;
    public const byte StatusAvailabilityMask = 0xFF;

    private readonly DtcRuntimeStore dtcRuntimeStore;

    public ReadDtcInformationService(DtcRuntimeStore dtcRuntimeStore)
    {
        this.dtcRuntimeStore = dtcRuntimeStore;
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length == 0)
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat)];
        }

        var subFunction = request.Payload[0];
        if (subFunction != ReportDtcByStatusMask)
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.SubFunctionNotSupported)];
        }

        if (request.Payload.Length != 2)
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat)];
        }

        var statusMask = request.Payload[1];
        var activeDtcs = dtcRuntimeStore.ListActive(statusMask);
        var responseBytes = new List<byte>
        {
            0x59,
            ReportDtcByStatusMask,
            StatusAvailabilityMask,
        };

        foreach (var dtc in activeDtcs)
        {
            ConfigValidator.TryParseDtcCode(dtc.Code, out var code);
            ConfigValidator.TryParseStatusByte(dtc.Status, out var status);
            responseBytes.Add((byte)((code >> 16) & 0xFF));
            responseBytes.Add((byte)((code >> 8) & 0xFF));
            responseBytes.Add((byte)(code & 0xFF));
            responseBytes.Add(status);
        }

        await dtcRuntimeStore.PublishReadAsync("uds", activeDtcs.Count, statusMask, cancellationToken);
        return [new RawUdsResponse([.. responseBytes])];
    }
}
