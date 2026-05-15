using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class EventStreamApiTests
{
    [Fact]
    public async Task RecentEventsApiReturnsLatestLimitedEvents()
    {
        var hub = new RuntimeEventHub(capacity: 10);
        await using var app = CreateApp(hub, out var baseAddress);
        await hub.WriteAsync(CreateEvent(RuntimeEventCategory.System, "system.1"));
        await hub.WriteAsync(CreateEvent(RuntimeEventCategory.System, "system.2"));
        await hub.WriteAsync(CreateEvent(RuntimeEventCategory.System, "system.3"));

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var events = await client.GetFromJsonAsync<RuntimeEvent[]>("/api/events/recent?limit=2");

            Assert.NotNull(events);
            Assert.Equal(["system.2", "system.3"], events!.Select(item => item.Name).ToArray());
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task RecentEventsApiFiltersByCategory()
    {
        var hub = new RuntimeEventHub(capacity: 10);
        await using var app = CreateApp(hub, out var baseAddress);
        await hub.WriteAsync(CreateEvent(RuntimeEventCategory.System, "system.1"));
        await hub.WriteAsync(CreateEvent(RuntimeEventCategory.Doip, "doip.1"));
        await hub.WriteAsync(CreateEvent(RuntimeEventCategory.Config, "config.1"));
        await hub.WriteAsync(CreateEvent(RuntimeEventCategory.Doip, "doip.2"));

        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var events = await client.GetFromJsonAsync<RuntimeEvent[]>("/api/events/recent?limit=200&category=doip");

            Assert.NotNull(events);
            Assert.Equal(["doip.1", "doip.2"], events!.Select(item => item.Name).ToArray());
            Assert.All(events, item => Assert.Equal(RuntimeEventCategory.Doip, item.Category));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task WebSocketClientReceivesPublishedRuntimeEvent()
    {
        var hub = new RuntimeEventHub();
        var publisher = new RuntimeEventBus([hub]);
        await using var app = CreateApp(hub, out var baseAddress, publisher);

        await app.StartAsync();

        try
        {
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(ToWebSocketUri(baseAddress, "/api/events/stream"), CancellationToken.None);

            await publisher.PublishAsync(CreateEvent(RuntimeEventCategory.Config, "config.saved"));

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await ReceiveEventAsync(socket, timeout.Token);

            Assert.Equal("config.saved", received.Name);
            Assert.Equal(RuntimeEventCategory.Config, received.Category);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task WebSocketDisconnectDoesNotPreventReconnect()
    {
        var hub = new RuntimeEventHub();
        var publisher = new RuntimeEventBus([hub]);
        await using var app = CreateApp(hub, out var baseAddress, publisher);

        await app.StartAsync();

        try
        {
            using (var firstSocket = new ClientWebSocket())
            {
                await firstSocket.ConnectAsync(ToWebSocketUri(baseAddress, "/api/events/stream"), CancellationToken.None);
                await firstSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test disconnect", CancellationToken.None);
            }

            using var secondSocket = new ClientWebSocket();
            await secondSocket.ConnectAsync(ToWebSocketUri(baseAddress, "/api/events/stream"), CancellationToken.None);
            await publisher.PublishAsync(CreateEvent(RuntimeEventCategory.System, "runtime.reconnected"));

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await ReceiveEventAsync(secondSocket, timeout.Token);

            Assert.Equal("runtime.reconnected", received.Name);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication CreateApp(
        RuntimeEventHub hub,
        out Uri baseAddress,
        IRuntimeEventPublisher? publisher = null)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        return WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow),
            runtimeEventPublisher: publisher,
            runtimeEventHub: hub);
    }

    private static RuntimeEvent CreateEvent(RuntimeEventCategory category, string name)
    {
        return RuntimeEvent.Create(RuntimeEventLevel.Info, category, name, $"{name} message.");
    }

    private static async Task<RuntimeEvent> ReceiveEventAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        var json = Encoding.UTF8.GetString(stream.ToArray());
        return JsonSerializer.Deserialize<RuntimeEvent>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static Uri ToWebSocketUri(Uri baseAddress, string path)
    {
        var builder = new UriBuilder(baseAddress)
        {
            Scheme = "ws",
            Path = path,
        };
        return builder.Uri;
    }

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
