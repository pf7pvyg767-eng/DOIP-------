using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;

namespace DoipSimulator.Protocols.Uds;

public sealed class WriteDataByIdentifierService : IUdsService
{
    public const byte Sid = 0x2E;

    private readonly DidRuntimeStore didRuntimeStore;
    private readonly EcuRuntimeState ecuRuntimeState;

    public WriteDataByIdentifierService(
        DidRuntimeStore didRuntimeStore,
        EcuRuntimeState ecuRuntimeState)
    {
        this.didRuntimeStore = didRuntimeStore;
        this.ecuRuntimeState = ecuRuntimeState;
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length < 3)
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat)];
        }

        var did = (ushort)((request.Payload[0] << 8) | request.Payload[1]);
        var value = request.Payload[2..];
        var result = await didRuntimeStore.WriteBytesAsync(
            did,
            value,
            ecuRuntimeState,
            "uds",
            persist: false,
            cancellationToken);

        if (!result.Succeeded)
        {
            return [new NegativeResponse(request.OriginalServiceId, ToNrc(result.Failure))];
        }

        return [new RawUdsResponse([0x6E, request.Payload[0], request.Payload[1]])];
    }

    private static NegativeResponseCode ToNrc(DidWriteFailure failure)
    {
        return failure switch
        {
            DidWriteFailure.UnknownDid or DidWriteFailure.NotWritable => NegativeResponseCode.RequestOutOfRange,
            DidWriteFailure.ConditionsNotCorrect => NegativeResponseCode.ConditionsNotCorrect,
            DidWriteFailure.SecurityAccessDenied => NegativeResponseCode.SecurityAccessDenied,
            _ => NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat,
        };
    }
}
