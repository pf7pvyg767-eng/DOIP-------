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
    private readonly Dictionary<string, Func<ValueTask>> disconnectActions = [];
    private long nextConnectionNumber;

    public ConnectionSnapshot AddTcpConnection(
        string remoteEndpoint,
        DateTimeOffset? connectedAt = null,
        Func<ValueTask>? disconnectAction = null)
    {
        return AddConnection("tcp", remoteEndpoint, connectedAt, disconnectAction);
    }

    public ConnectionSnapshot AddTlsConnection(
        string remoteEndpoint,
        DateTimeOffset? connectedAt = null,
        Func<ValueTask>? disconnectAction = null)
    {
        return AddConnection("tls", remoteEndpoint, connectedAt, disconnectAction);
    }

    private ConnectionSnapshot AddConnection(
        string transport,
        string remoteEndpoint,
        DateTimeOffset? connectedAt = null,
        Func<ValueTask>? disconnectAction = null)
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
            if (disconnectAction is not null)
            {
                disconnectActions[id] = disconnectAction;
            }
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
            disconnectActions.Remove(connectionId);
            return connections.Remove(connectionId);
        }
    }

    public async ValueTask<bool> RequestDisconnectAsync(string connectionId)
    {
        Func<ValueTask>? disconnectAction;
        lock (gate)
        {
            if (!connections.ContainsKey(connectionId))
            {
                return false;
            }

            disconnectActions.TryGetValue(connectionId, out disconnectAction);
        }

        if (disconnectAction is null)
        {
            return false;
        }

        await disconnectAction();
        return true;
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

    public int ActiveCount
    {
        get
        {
            lock (gate)
            {
                return connections.Count;
            }
        }
    }

    public long TotalAccepted => Interlocked.Read(ref nextConnectionNumber);

    public static string FormatLogicalAddress(ushort logicalAddress) => $"0x{logicalAddress:X4}";
}
