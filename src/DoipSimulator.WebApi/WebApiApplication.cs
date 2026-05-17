using System.Net.WebSockets;
using System.Text.Json;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.WebApi;

public sealed record WebApiRuntimeOptions(
    string ListenAddress,
    int Port,
    DateTimeOffset StartedAt,
    string? ConfigPath = null);

public sealed record HealthResponse(string Status, string Version, DateTimeOffset StartedAt);

public sealed record ConfigValidationErrorResponse(
    string Code,
    string Message,
    IReadOnlyList<ConfigValidationErrorDetail> Errors);

public sealed record ConfigValidationErrorDetail(string Path, string Message);

public sealed record EcuStateSnapshot(
    string LogicalAddress,
    string CurrentSession,
    string SecurityStateSummary,
    DateTimeOffset? LastTesterPresentAt);

public static class WebApiApplication
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WebApplication Create(
        string[] args,
        WebApiRuntimeOptions options,
        ConfigStore? configStore = null,
        IConfigChangePublisher? configChangePublisher = null,
        IRuntimeEventPublisher? runtimeEventPublisher = null,
        RuntimeEventHub? runtimeEventHub = null,
        ConnectionRegistry? connectionRegistry = null,
        EcuRuntimeState? ecuRuntimeState = null)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        builder.WebHost.UseUrls($"http://{options.ListenAddress}:{options.Port}");

        var app = builder.Build();
        var eventHub = runtimeEventHub ?? new RuntimeEventHub();
        var eventPublisher = runtimeEventPublisher ?? new RuntimeEventBus([eventHub]);
        var store = configStore ?? new ConfigStore(eventPublisher);
        var publisher = configChangePublisher ?? NullConfigChangePublisher.Instance;
        var configPath = ResolveConfigPath(options.ConfigPath);
        var connections = connectionRegistry ?? new ConnectionRegistry();
        var ecuState = ecuRuntimeState ?? new EcuRuntimeState(0x0E00);

        app.UseWebSockets();

        app.MapGet("/api/health", () =>
            Results.Ok(new HealthResponse(
                "ok",
                typeof(WebApiApplication).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                options.StartedAt)));

        app.MapGet("/api/config", async (CancellationToken cancellationToken) =>
        {
            var config = await store.LoadAsync(configPath, cancellationToken);
            return Results.Ok(config);
        });

        app.MapGet("/api/connections", () => Results.Ok(connections.GetActiveSnapshots()));

        app.MapGet("/api/ecu/state", () => Results.Ok(ToEcuStateSnapshot(ecuState)));

        app.MapPut("/api/config", async (HttpRequest request, CancellationToken cancellationToken) =>
        {
            SimulatorConfig? config;
            try
            {
                config = await request.ReadFromJsonAsync<SimulatorConfig>(JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return Results.BadRequest(ToErrorResponse([
                    new ConfigValidationError("config", "Request body must be a valid simulator configuration JSON object."),
                ]));
            }

            var validation = ConfigValidator.Validate(config);
            if (!validation.IsValid)
            {
                return Results.BadRequest(ToErrorResponse(validation.Errors));
            }

            await store.SaveAsync(configPath, config!, cancellationToken);
            publisher.Publish(new ConfigChangedEvent(DateTimeOffset.UtcNow, configPath));

            return Results.Ok(config);
        });

        app.MapGet("/api/events/recent", (
            int? limit,
            string? category) =>
        {
            var parsedCategory = ParseCategory(category);
            if (!string.IsNullOrWhiteSpace(category) && parsedCategory is null)
            {
                return Results.BadRequest(new
                {
                    code = "INVALID_EVENT_CATEGORY",
                    message = $"Unknown event category: {category}.",
                });
            }

            return Results.Ok(eventHub.GetRecent(limit, parsedCategory));
        });

        app.Map("/api/events/stream", async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("WebSocket connection required.", context.RequestAborted);
                return;
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            using var subscription = eventHub.Subscribe();
            using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            var cancellationToken = streamCts.Token;
            var receiveTask = WatchClientDisconnectAsync(webSocket, streamCts);

            try
            {
                await foreach (var runtimeEvent in subscription.Events.ReadAllAsync(cancellationToken))
                {
                    if (webSocket.State is not WebSocketState.Open)
                    {
                        break;
                    }

                    var payload = JsonSerializer.SerializeToUtf8Bytes(runtimeEvent, JsonOptions);
                    await webSocket.SendAsync(
                        payload,
                        WebSocketMessageType.Text,
                        WebSocketMessageFlags.EndOfMessage,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (WebSocketException)
            {
            }
            finally
            {
                await streamCts.CancelAsync();
                try
                {
                    await receiveTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch (WebSocketException)
                {
                }
            }

            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Event stream closed.",
                    CancellationToken.None);
            }
        });

        return app;
    }

    private static async Task WatchClientDisconnectAsync(
        WebSocket webSocket,
        CancellationTokenSource streamCts)
    {
        var buffer = new byte[1024];
        try
        {
            while (!streamCts.IsCancellationRequested &&
                   webSocket.State is WebSocketState.Open or WebSocketState.CloseSent)
            {
                var result = await webSocket.ReceiveAsync(buffer, streamCts.Token);
                if (result.MessageType is WebSocketMessageType.Close)
                {
                    await streamCts.CancelAsync();
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (streamCts.IsCancellationRequested)
        {
        }
        catch (WebSocketException)
        {
            await streamCts.CancelAsync();
        }
    }

    private static RuntimeEventCategory? ParseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        foreach (var value in Enum.GetValues<RuntimeEventCategory>())
        {
            var jsonName = JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
            if (string.Equals(jsonName, category, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static string ResolveConfigPath(string? configuredPath)
    {
        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "simulator.json")
            : configuredPath;
    }

    private static EcuStateSnapshot ToEcuStateSnapshot(EcuRuntimeState state)
    {
        return new EcuStateSnapshot(
            $"0x{state.LogicalAddress:X4}",
            FormatSession(state.CurrentSession),
            state.SecurityStateSummary,
            state.LastTesterPresentAt);
    }

    private static string FormatSession(DiagnosticSession session)
    {
        return session switch
        {
            DiagnosticSession.Default => "default",
            DiagnosticSession.Programming => "programming",
            DiagnosticSession.Extended => "extended",
            _ => session.ToString().ToLowerInvariant(),
        };
    }

    private static ConfigValidationErrorResponse ToErrorResponse(IReadOnlyList<ConfigValidationError> errors)
    {
        return new ConfigValidationErrorResponse(
            "CONFIG_VALIDATION_FAILED",
            "Simulator configuration validation failed.",
            errors.Select(error => new ConfigValidationErrorDetail(error.Field, error.Message)).ToArray());
    }
}
