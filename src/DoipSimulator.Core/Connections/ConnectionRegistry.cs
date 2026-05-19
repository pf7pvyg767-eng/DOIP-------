namespace DoipSimulator.Core.Connections;

public sealed record ConnectionSnapshot(
    string ConnectionId,
    string Transport,
    string RemoteEndpoint,
    bool RoutingActivated,
    string? TesterLogicalAddress,
    string? EcuLogicalAddress,
    DateTimeOffset ConnectedAt,
    string State = "open");

public sealed class ConnectionRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<string, ConnectionSnapshot> connections = [];
    private long nextConnectionNumber;

    public ConnectionSnapshot AddTcpConnection(string remoteEndpoint, DateTimeOffset? connectedAt = null)
    {
        return AddConnection("tcp", remoteEndpoint, connectedAt);
    }

    public ConnectionSnapshot AddTlsConnection(string remoteEndpoint, DateTimeOffset? connectedAt = null)
    {
        return AddConnection("tls", remoteEndpoint, connectedAt);
    }

    private ConnectionSnapshot AddConnection(string transport, string remoteEndpoint, DateTimeOffset? connectedAt = null)
    {
        var id = $"conn_{Interlocked.Increment(ref nextConnectionNumber):D6}";
        var snapshot = new ConnectionSnapshot(
            id,
            transport,
            remoteEndpoint,
            RoutingActivated: false,
            TesterLogicalAddress: null,
            EcuLogicalAddress: null,
            connectedAt ?? DateTimeOffset.UtcNow);

        lock (gate)
        {
            connections[id] = snapshot;
        }

        return snapshot;
    }

    public ConnectionSnapshot? MarkRoutingActivated(
        string connectionId,
        ushort testerLogicalAddress,
        ushort ecuLogicalAddress)
    {
        lock (gate)
        {
            if (!connections.TryGetValue(connectionId, out var snapshot))
            {
                return null;
            }

            var updated = snapshot with
            {
                RoutingActivated = true,
                TesterLogicalAddress = FormatLogicalAddress(testerLogicalAddress),
                EcuLogicalAddress = FormatLogicalAddress(ecuLogicalAddress),
            };
            connections[connectionId] = updated;
            return updated;
        }
    }

    public bool Remove(string connectionId)
    {
        lock (gate)
        {
            return connections.Remove(connectionId);
        }
    }

    public ConnectionSnapshot? Get(string connectionId)
    {
        lock (gate)
        {
            return connections.GetValueOrDefault(connectionId);
        }
    }

    public IReadOnlyList<ConnectionSnapshot> GetActiveSnapshots()
    {
        lock (gate)
        {
            return connections.Values
                .OrderBy(connection => connection.ConnectedAt)
                .ToArray();
        }
    }

    public static string FormatLogicalAddress(ushort logicalAddress) => $"0x{logicalAddress:X4}";
}
