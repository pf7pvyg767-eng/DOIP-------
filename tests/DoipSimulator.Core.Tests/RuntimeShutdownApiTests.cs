using System.Net;
using System.Net.Http.Json;
using DoipSimulator.Core.Observability.Pcap;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class RuntimeShutdownApiTests
{
    [Fact]
    public async Task ShutdownEndpointSignalsRuntimeAndPublishesEvent()
    {
        var signal = new RecordingRuntimeShutdownSignal();
        var hub = new RuntimeEventHub();
        await using var app = CreateApp(out var baseAddress, signal, runtimeEventHub: hub);

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var response = await client.PostAsync("/api/runtime/shutdown", content: null);
            var shutdown = await response.Content.ReadFromJsonAsync<RuntimeShutdownResponse>();

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.NotNull(shutdown);
            Assert.True(shutdown!.Accepted);
            Assert.False(shutdown.AlreadyRequested);
            Assert.Equal(1, signal.RequestCount);
            Assert.Contains(
                hub.GetRecent(),
                item => item.Category == RuntimeEventCategory.System && item.Name == "system.shutdown.requested");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ShutdownEndpointIsIdempotentWhileRuntimeStillResponds()
    {
        var signal = new RecordingRuntimeShutdownSignal();
        var hub = new RuntimeEventHub();
        await using var app = CreateApp(out var baseAddress, signal, runtimeEventHub: hub);

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var firstResponse = await client.PostAsync("/api/runtime/shutdown", content: null);
            var secondResponse = await client.PostAsync("/api/runtime/shutdown", content: null);
            var secondShutdown = await secondResponse.Content.ReadFromJsonAsync<RuntimeShutdownResponse>();

            Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, secondResponse.StatusCode);
            Assert.NotNull(secondShutdown);
            Assert.True(secondShutdown!.AlreadyRequested);
            Assert.Equal(1, signal.RequestCount);
            Assert.Single(hub.GetRecent(), item => item.Name == "system.shutdown.requested");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ShutdownEndpointStopsActivePcapBeforeSignalingRuntime()
    {
        var pcap = new RecordingPcapRecorder(recording: true);
        var signal = new RecordingRuntimeShutdownSignal(() => pcap.StopCount == 1);
        await using var app = CreateApp(out var baseAddress, signal, pcapRecorder: pcap);

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var response = await client.PostAsync("/api/runtime/shutdown", content: null);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.Equal(1, pcap.StopCount);
            Assert.Equal(1, signal.RequestCount);
            Assert.True(signal.InvariantHeldAtRequest);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication CreateApp(
        out Uri baseAddress,
        IRuntimeShutdownSignal shutdownSignal,
        RuntimeEventHub? runtimeEventHub = null,
        IPcapRecorder? pcapRecorder = null)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        return WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow),
            runtimeEventHub: runtimeEventHub,
            pcapRecorder: pcapRecorder,
            runtimeShutdownSignal: shutdownSignal);
    }

    private static int GetFreeLoopbackPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class RecordingRuntimeShutdownSignal : IRuntimeShutdownSignal
    {
        private readonly Func<bool>? invariant;

        public RecordingRuntimeShutdownSignal(Func<bool>? invariant = null)
        {
            this.invariant = invariant;
        }

        public int RequestCount { get; private set; }

        public bool InvariantHeldAtRequest { get; private set; } = true;

        public void RequestShutdown()
        {
            InvariantHeldAtRequest &= invariant?.Invoke() ?? true;
            RequestCount++;
        }
    }

    private sealed class RecordingPcapRecorder : IPcapRecorder
    {
        private bool recording;

        public RecordingPcapRecorder(bool recording)
        {
            this.recording = recording;
        }

        public int StopCount { get; private set; }

        public PcapRecordingStatus GetStatus()
        {
            return new PcapRecordingStatus(recording, recording ? "active.pcap" : null, 24, PcapRecorder.DefaultMaxBytes);
        }

        public ValueTask<PcapRecordingStatus> StartAsync(CancellationToken cancellationToken = default)
        {
            recording = true;
            return ValueTask.FromResult(GetStatus());
        }

        public ValueTask<PcapRecordingStatus> StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            recording = false;
            return ValueTask.FromResult(GetStatus());
        }

        public ValueTask RecordAsync(PcapPacket packet, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
