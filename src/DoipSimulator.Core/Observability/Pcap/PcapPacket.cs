using System.Net;

namespace DoipSimulator.Core.Observability.Pcap;

public enum PcapTransport
{
    Udp,
    Tcp,
}

public enum PcapPacketDirection
{
    Inbound,
    Outbound,
}

public sealed record PcapPacket(
    PcapTransport Transport,
    PcapPacketDirection Direction,
    IPEndPoint LocalEndpoint,
    IPEndPoint RemoteEndpoint,
    byte[] Payload,
    DateTimeOffset Timestamp);

public sealed record PcapRecordingStatus(
    bool Recording,
    string? FilePath,
    long BytesWritten,
    long MaxBytes);

public interface IPcapRecorder
{
    PcapRecordingStatus GetStatus();

    ValueTask<PcapRecordingStatus> StartAsync(CancellationToken cancellationToken = default);

    ValueTask<PcapRecordingStatus> StopAsync(CancellationToken cancellationToken = default);

    ValueTask RecordAsync(PcapPacket packet, CancellationToken cancellationToken = default);
}

public sealed class NullPcapRecorder : IPcapRecorder
{
    public static NullPcapRecorder Instance { get; } = new();

    private NullPcapRecorder()
    {
    }

    public PcapRecordingStatus GetStatus()
    {
        return new PcapRecordingStatus(false, null, 0, PcapRecorder.DefaultMaxBytes);
    }

    public ValueTask<PcapRecordingStatus> StartAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(GetStatus());
    }

    public ValueTask<PcapRecordingStatus> StopAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(GetStatus());
    }

    public ValueTask RecordAsync(PcapPacket packet, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
