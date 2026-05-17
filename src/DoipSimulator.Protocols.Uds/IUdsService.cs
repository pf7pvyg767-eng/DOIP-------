namespace DoipSimulator.Protocols.Uds;

public interface IUdsService
{
    byte ServiceId { get; }

    ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default);
}
