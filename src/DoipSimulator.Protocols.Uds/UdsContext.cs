namespace DoipSimulator.Protocols.Uds;

public sealed record UdsContext(
    string? ConnectionId = null,
    string? RemoteEndpoint = null,
    string? TesterLogicalAddress = null,
    string? EcuLogicalAddress = null);
