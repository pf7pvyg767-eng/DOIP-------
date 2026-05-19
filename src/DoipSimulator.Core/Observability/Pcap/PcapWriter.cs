using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace DoipSimulator.Core.Observability.Pcap;

public sealed class PcapWriter : IAsyncDisposable
{
    public const int GlobalHeaderLength = 24;
    public const int PacketHeaderLength = 16;

    private const uint MagicNumber = 0xA1B2C3D4;
    private const ushort VersionMajor = 2;
    private const ushort VersionMinor = 4;
    private const uint SnapLength = 65535;
    private const uint LinkTypeRaw = 101;
    private const byte TcpProtocol = 6;
    private const byte UdpProtocol = 17;
    private readonly FileStream stream;
    private bool disposed;

    public PcapWriter(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        WriteGlobalHeader();
    }

    public long BytesWritten => stream.Length;

    public int GetRecordLength(PcapPacket packet)
    {
        return PacketHeaderLength + BuildIpPacket(packet).Length;
    }

    public async ValueTask WritePacketAsync(PcapPacket packet, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var bytes = BuildIpPacket(packet);
        var header = new byte[PacketHeaderLength];
        var unixMicroseconds = packet.Timestamp.ToUnixTimeMilliseconds() * 1000;
        var seconds = unixMicroseconds / 1_000_000;
        var microseconds = unixMicroseconds % 1_000_000;

        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, 4), checked((uint)seconds));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), checked((uint)microseconds));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), checked((uint)bytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12, 4), checked((uint)bytes.Length));

        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await stream.FlushAsync();
        await stream.DisposeAsync();
    }

    private void WriteGlobalHeader()
    {
        var header = new byte[GlobalHeaderLength];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, 4), MagicNumber);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4, 2), VersionMajor);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6, 2), VersionMinor);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16, 4), SnapLength);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20, 4), LinkTypeRaw);
        stream.Write(header);
        stream.Flush();
    }

    private static byte[] BuildIpPacket(PcapPacket packet)
    {
        return packet.Transport switch
        {
            PcapTransport.Udp => BuildUdpIpPacket(packet),
            PcapTransport.Tcp => BuildTcpIpPacket(packet),
            _ => throw new ArgumentOutOfRangeException(nameof(packet), "Unsupported pcap transport."),
        };
    }

    private static byte[] BuildUdpIpPacket(PcapPacket packet)
    {
        var payload = packet.Payload;
        var totalLength = 20 + 8 + payload.Length;
        var bytes = new byte[totalLength];
        WriteIpv4Header(bytes.AsSpan(0, 20), packet, UdpProtocol, totalLength);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(20, 2), GetSourcePort(packet));
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(22, 2), GetDestinationPort(packet));
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(24, 2), checked((ushort)(8 + payload.Length)));
        payload.CopyTo(bytes.AsSpan(28));
        return bytes;
    }

    private static byte[] BuildTcpIpPacket(PcapPacket packet)
    {
        var payload = packet.Payload;
        var totalLength = 20 + 20 + payload.Length;
        var bytes = new byte[totalLength];
        WriteIpv4Header(bytes.AsSpan(0, 20), packet, TcpProtocol, totalLength);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(20, 2), GetSourcePort(packet));
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(22, 2), GetDestinationPort(packet));
        bytes[32] = 0x50;
        bytes[33] = 0x18;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(34, 2), 8192);
        payload.CopyTo(bytes.AsSpan(40));
        return bytes;
    }

    private static void WriteIpv4Header(Span<byte> header, PcapPacket packet, byte protocol, int totalLength)
    {
        header[0] = 0x45;
        BinaryPrimitives.WriteUInt16BigEndian(header.Slice(2, 2), checked((ushort)totalLength));
        header[8] = 64;
        header[9] = protocol;
        GetSourceAddress(packet).GetAddressBytes().CopyTo(header.Slice(12, 4));
        GetDestinationAddress(packet).GetAddressBytes().CopyTo(header.Slice(16, 4));
        BinaryPrimitives.WriteUInt16BigEndian(header.Slice(10, 2), ComputeIpv4HeaderChecksum(header));
    }

    private static ushort ComputeIpv4HeaderChecksum(ReadOnlySpan<byte> header)
    {
        uint sum = 0;
        for (var index = 0; index < header.Length; index += 2)
        {
            sum += BinaryPrimitives.ReadUInt16BigEndian(header.Slice(index, 2));
        }

        while ((sum >> 16) != 0)
        {
            sum = (sum & 0xFFFF) + (sum >> 16);
        }

        return (ushort)~sum;
    }

    private static IPAddress GetSourceAddress(PcapPacket packet)
    {
        return ToIpv4(packet.Direction == PcapPacketDirection.Inbound
            ? packet.RemoteEndpoint.Address
            : packet.LocalEndpoint.Address);
    }

    private static IPAddress GetDestinationAddress(PcapPacket packet)
    {
        return ToIpv4(packet.Direction == PcapPacketDirection.Inbound
            ? packet.LocalEndpoint.Address
            : packet.RemoteEndpoint.Address);
    }

    private static ushort GetSourcePort(PcapPacket packet)
    {
        return checked((ushort)(packet.Direction == PcapPacketDirection.Inbound
            ? packet.RemoteEndpoint.Port
            : packet.LocalEndpoint.Port));
    }

    private static ushort GetDestinationPort(PcapPacket packet)
    {
        return checked((ushort)(packet.Direction == PcapPacketDirection.Inbound
            ? packet.LocalEndpoint.Port
            : packet.RemoteEndpoint.Port));
    }

    private static IPAddress ToIpv4(IPAddress address)
    {
        return address.AddressFamily == AddressFamily.InterNetwork
            ? address
            : IPAddress.Loopback;
    }
}
