using System.Text.Json.Serialization;

namespace DoipSimulator.Core.RuntimeEvents;

[JsonConverter(typeof(JsonStringEnumConverter<RuntimeEventLevel>))]
public enum RuntimeEventLevel
{
    [JsonStringEnumMemberName("info")]
    Info,
    [JsonStringEnumMemberName("warning")]
    Warning,
    [JsonStringEnumMemberName("error")]
    Error,
}

[JsonConverter(typeof(JsonStringEnumConverter<RuntimeEventCategory>))]
public enum RuntimeEventCategory
{
    [JsonStringEnumMemberName("system")]
    System,
    [JsonStringEnumMemberName("config")]
    Config,
    [JsonStringEnumMemberName("connection")]
    Connection,
    [JsonStringEnumMemberName("doip")]
    Doip,
    [JsonStringEnumMemberName("uds")]
    Uds,
    [JsonStringEnumMemberName("state")]
    State,
    [JsonStringEnumMemberName("fault")]
    Fault,
    [JsonStringEnumMemberName("tls")]
    Tls,
    [JsonStringEnumMemberName("pcap")]
    Pcap,
}

public sealed record RuntimeEvent(
    string Id,
    DateTimeOffset Timestamp,
    RuntimeEventLevel Level,
    RuntimeEventCategory Category,
    string Name,
    string Message,
    string? ConnectionId = null,
    IReadOnlyDictionary<string, object?>? Data = null)
{
    public static RuntimeEvent Create(
        RuntimeEventLevel level,
        RuntimeEventCategory category,
        string name,
        string message,
        string? connectionId = null,
        IReadOnlyDictionary<string, object?>? data = null,
        DateTimeOffset? timestamp = null)
    {
        return new RuntimeEvent(
            Guid.NewGuid().ToString("N"),
            timestamp ?? DateTimeOffset.UtcNow,
            level,
            category,
            name,
            message,
            connectionId,
            data ?? new Dictionary<string, object?>());
    }
}
