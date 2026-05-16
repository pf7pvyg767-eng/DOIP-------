using DoipSimulator.Core.Ecu;

namespace DoipSimulator.Protocols.Uds;

public sealed class TesterPresentService : IUdsService
{
    public const byte Sid = 0x3E;

    private readonly EcuRuntimeState state;
    private readonly TimeProvider timeProvider;

    public TesterPresentService(EcuRuntimeState state, TimeProvider? timeProvider = null)
    {
        this.state = state;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public byte ServiceId => Sid;

    public ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length != 1)
        {
            return ValueTask.FromResult<IReadOnlyList<UdsResponse>>(
                [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat)]);
        }

        if (request.Payload[0] != 0x00)
        {
            return ValueTask.FromResult<IReadOnlyList<UdsResponse>>(
                [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.SubFunctionNotSupported)]);
        }

        state.RecordTesterPresent(timeProvider.GetUtcNow());
        return ValueTask.FromResult<IReadOnlyList<UdsResponse>>([new RawUdsResponse([0x7E, 0x00])]);
    }
}
