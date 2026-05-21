using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using DoipSimulator.Core.Observability.Pcap;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class PcapRecordingTests
{
    [Fact]
    public async Task PcapWriterCreatesReadableGlobalHeaderAndPacketRecord()
    {
        var filePath = CreateTempPath("writer.pcap");
        var timestamp = DateTimeOffset.Parse("2026-05-19T01:02:03.456Z");

        await using (var writer = new PcapWriter(filePath))
        {
            await writer.WritePacketAsync(new PcapPacket(
                PcapTransport.Udp,
                PcapPacketDirection.Inbound,
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13400),
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 55000),
                [0x01, 0x02, 0x03],
                timestamp));
        }

        var bytes = await File.ReadAllBytesAsync(filePath);
        Assert.True(bytes.Length > PcapWriter.GlobalHeaderLength);
        Assert.Equal(0xA1B2C3D4u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4)));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2)));
        Assert.Equal(4, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6, 2)));
        Assert.Equal(65535u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(101u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20, 4)));
        Assert.Equal((uint)timestamp.ToUnixTimeSeconds(), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24, 4)));
        Assert.Equal(456000u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28, 4)));
        var capturedLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(32, 4));
        Assert.Equal(capturedLength, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(36, 4)));
        Assert.Equal((uint)(bytes.Length - PcapWriter.GlobalHeaderLength - PcapWriter.PacketHeaderLength), capturedLength);
        Assert.Equal(0x45, bytes[40]);
        Assert.Equal(17, bytes[49]);
        Assert.Equal([0x01, 0x02, 0x03], bytes[^3..]);
    }

    [Fact]
    public async Task PcapWriterSupportsEmptyTcpPayloadAndClosesReadableFile()
    {
        var filePath = CreateTempPath("empty-tcp.pcap");

        await using (var writer = new PcapWriter(filePath))
        {
            await writer.WritePacketAsync(new PcapPacket(
                PcapTransport.Tcp,
                PcapPacketDirection.Outbound,
                new IPEndPoint(IPAddress.Loopback, 13400),
                new IPEndPoint(IPAddress.Loopback, 55000),
                [],
                DateTimeOffset.UtcNow));
        }

        var bytes = await File.ReadAllBytesAsync(filePath);
        Assert.Equal(PcapWriter.GlobalHeaderLength + PcapWriter.PacketHeaderLength + 40, bytes.Length);
        Assert.Equal(6, bytes[PcapWriter.GlobalHeaderLength + PcapWriter.PacketHeaderLength + 9]);
    }

    [Fact]
    public async Task PcapWriterAssignsTcpSequenceAndAckNumbersPerConversation()
    {
        var filePath = CreateTempPath("tcp-sequence.pcap");
        var local = new IPEndPoint(IPAddress.Loopback, 13400);
        var remote = new IPEndPoint(IPAddress.Loopback, 55000);

        await using (var writer = new PcapWriter(filePath))
        {
            await writer.WritePacketAsync(new PcapPacket(
                PcapTransport.Tcp,
                PcapPacketDirection.Inbound,
                local,
                remote,
                [0x02, 0xFD, 0x00, 0x05],
                DateTimeOffset.UtcNow));
            await writer.WritePacketAsync(new PcapPacket(
                PcapTransport.Tcp,
                PcapPacketDirection.Outbound,
                local,
                remote,
                [0x02, 0xFD, 0x00, 0x06, 0x10],
                DateTimeOffset.UtcNow));
            await writer.WritePacketAsync(new PcapPacket(
                PcapTransport.Tcp,
                PcapPacketDirection.Inbound,
                local,
                remote,
                [0x02, 0xFD, 0x80, 0x01],
                DateTimeOffset.UtcNow));
        }

        var bytes = await File.ReadAllBytesAsync(filePath);
        var firstPacketOffset = PcapWriter.GlobalHeaderLength + PcapWriter.PacketHeaderLength;
        var secondPacketOffset = firstPacketOffset + 40 + 4 + PcapWriter.PacketHeaderLength;
        var thirdPacketOffset = secondPacketOffset + 40 + 5 + PcapWriter.PacketHeaderLength;

        AssertTcpSequence(bytes, firstPacketOffset, sequenceNumber: 1, acknowledgementNumber: 1);
        AssertTcpSequence(bytes, secondPacketOffset, sequenceNumber: 1, acknowledgementNumber: 5);
        AssertTcpSequence(bytes, thirdPacketOffset, sequenceNumber: 5, acknowledgementNumber: 6);
    }

    [Fact]
    public async Task PcapRecorderLifecyclePublishesStartStopAndKeepsStatus()
    {
        var events = new RecordingRuntimeEventPublisher();
        await using var recorder = new PcapRecorder(CreateTempDirectory(), eventPublisher: events);

        var started = await recorder.StartAsync();
        await recorder.RecordAsync(CreateUdpPacket([0x01]));
        var active = recorder.GetStatus();
        var stopped = await recorder.StopAsync();

        Assert.True(started.Recording);
        Assert.True(File.Exists(started.FilePath));
        Assert.True(active.BytesWritten > PcapWriter.GlobalHeaderLength);
        Assert.False(stopped.Recording);
        Assert.Equal(started.FilePath, stopped.FilePath);
        Assert.Contains(events.Events, item => item.Category == RuntimeEventCategory.Pcap && item.Name == "pcap.recording.started");
        Assert.Contains(events.Events, item => item.Category == RuntimeEventCategory.Pcap && item.Name == "pcap.recording.stopped");
    }

    [Fact]
    public async Task PcapRecorderStopsAndPublishesEventWhenSizeLimitWouldBeExceeded()
    {
        var events = new RecordingRuntimeEventPublisher();
        await using var recorder = new PcapRecorder(
            CreateTempDirectory(),
            maxBytes: PcapWriter.GlobalHeaderLength + PcapWriter.PacketHeaderLength + 28,
            eventPublisher: events);

        await recorder.StartAsync();
        await recorder.RecordAsync(CreateUdpPacket([0x01]));
        var status = recorder.GetStatus();

        Assert.False(status.Recording);
        Assert.Equal(PcapWriter.GlobalHeaderLength, status.BytesWritten);
        Assert.Contains(events.Events, item => item.Category == RuntimeEventCategory.Pcap && item.Name == "pcap.recording.size_limit_reached");
    }

    [Fact]
    public async Task PcapApiReturnsStatusContractAndControlsLifecycle()
    {
        await using var recorder = new PcapRecorder(CreateTempDirectory());
        await using var app = CreateApp(out var baseAddress, recorder);

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var initial = await client.GetFromJsonAsync<PcapRecordingStatus>("/api/pcap/status");
            var startedResponse = await client.PostAsync("/api/pcap/start", content: null);
            var started = await startedResponse.Content.ReadFromJsonAsync<PcapRecordingStatus>();
            var repeatedResponse = await client.PostAsync("/api/pcap/start", content: null);
            var repeated = await repeatedResponse.Content.ReadFromJsonAsync<PcapRecordingStatus>();
            var stoppedResponse = await client.PostAsync("/api/pcap/stop", content: null);
            var stopped = await stoppedResponse.Content.ReadFromJsonAsync<PcapRecordingStatus>();

            Assert.NotNull(initial);
            Assert.False(initial!.Recording);
            Assert.Null(initial.FilePath);
            Assert.Equal(PcapRecorder.DefaultMaxBytes, initial.MaxBytes);
            Assert.True(started!.Recording);
            Assert.Equal(started.FilePath, repeated!.FilePath);
            Assert.Equal(started.BytesWritten, repeated.BytesWritten);
            Assert.False(stopped!.Recording);
            Assert.Equal(started.FilePath, stopped.FilePath);
            Assert.True(File.Exists(started.FilePath));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static PcapPacket CreateUdpPacket(byte[] payload)
    {
        return new PcapPacket(
            PcapTransport.Udp,
            PcapPacketDirection.Inbound,
            new IPEndPoint(IPAddress.Loopback, 13400),
            new IPEndPoint(IPAddress.Loopback, 55000),
            payload,
            DateTimeOffset.UtcNow);
    }

    private static WebApplication CreateApp(out Uri baseAddress, IPcapRecorder recorder)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        return WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow),
            pcapRecorder: recorder);
    }

    private static string CreateTempPath(string fileName)
    {
        return Path.Combine(CreateTempDirectory(), fileName);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void AssertTcpSequence(
        byte[] bytes,
        int ipPacketOffset,
        uint sequenceNumber,
        uint acknowledgementNumber)
    {
        Assert.Equal(sequenceNumber, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(ipPacketOffset + 24, 4)));
        Assert.Equal(acknowledgementNumber, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(ipPacketOffset + 28, 4)));
    }

    private sealed class RecordingRuntimeEventPublisher : IRuntimeEventPublisher
    {
        public List<RuntimeEvent> Events { get; } = [];

        public RuntimeEventPublishError? LastError => null;

        public ValueTask PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(runtimeEvent);
            return ValueTask.CompletedTask;
        }
    }
}
