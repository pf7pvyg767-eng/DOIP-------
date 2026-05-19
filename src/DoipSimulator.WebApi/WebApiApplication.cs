using System.Net.WebSockets;
using System.Text.Json;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.Faults;
using DoipSimulator.Core.Observability.Pcap;
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
    DateTimeOffset? LastTesterPresentAt,
    TesterPresentTimingSnapshot Timing);

public sealed record DidValueUpdateRequest(string? ValueEncoding, string? Value, bool Persist = false);

public sealed record DidValueUpdateResponse(string Did, string ValueEncoding, string Value, bool Persisted);

public sealed record DidErrorResponse(string Code, string Message);

public sealed record DtcActivateRequest(string? Status, string? Description);

public sealed record DtcErrorResponse(string Code, string Message);

public sealed record FaultDisconnectRequest(string ConnectionId);

public sealed record FaultNextNrcRequest(string ServiceId, string Nrc);

public sealed record FaultErrorResponse(string Code, string Message);

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
        EcuRuntimeState? ecuRuntimeState = null,
        DidRuntimeStore? didRuntimeStore = null,
        DtcRuntimeStore? dtcRuntimeStore = null,
        ControlServiceStateStore? controlServiceStateStore = null,
        IPcapRecorder? pcapRecorder = null,
        FaultRuntimeState? faultRuntimeState = null)
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
        var initialConfig = store.LoadAsync(configPath).GetAwaiter().GetResult();
        var faults = faultRuntimeState ?? new FaultRuntimeState(initialConfig.FaultProfile, eventPublisher);
        var ecuState = ecuRuntimeState ?? new EcuRuntimeState(0x0E00);
        var didStore = didRuntimeStore ?? new DidRuntimeStore(
            initialConfig,
            configPath,
            store,
            eventPublisher);
        var dtcStore = dtcRuntimeStore ?? new DtcRuntimeStore(
            initialConfig,
            eventPublisher);
        var controlStore = controlServiceStateStore ?? new ControlServiceStateStore(
            initialConfig,
            eventPublisher);
        var pcap = pcapRecorder ?? NullPcapRecorder.Instance;

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

        app.MapGet("/api/faults", () => Results.Ok(faults.GetSnapshot()));

        app.MapPut("/api/faults", async (HttpRequest request, CancellationToken cancellationToken) =>
        {
            FaultProfile? profile;
            try
            {
                profile = await request.ReadFromJsonAsync<FaultProfile>(JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new FaultErrorResponse("INVALID_JSON", "Request body must be a valid fault profile JSON object."));
            }

            var errors = new List<ConfigValidationError>();
            ConfigValidator.ValidateFaultProfile(profile, errors);
            if (errors.Count > 0)
            {
                return Results.BadRequest(ToErrorResponse(errors));
            }

            return Results.Ok(await faults.UpdateProfileAsync(profile!, "api", cancellationToken));
        });

        app.MapPost("/api/faults/actions/next-nrc", async (
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            FaultNextNrcRequest? body;
            try
            {
                body = await request.ReadFromJsonAsync<FaultNextNrcRequest>(JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new FaultErrorResponse("INVALID_JSON", "Request body must be valid JSON."));
            }

            if (body is null)
            {
                return Results.BadRequest(new FaultErrorResponse("INVALID_FAULT_ACTION", "next-nrc request body is required."));
            }

            var current = faults.GetSnapshot().Profile;
            current.NextNrc = new UdsFaultOverrideConfig
            {
                ServiceId = body.ServiceId,
                Nrc = body.Nrc,
            };

            var errors = new List<ConfigValidationError>();
            ConfigValidator.ValidateFaultProfile(current, errors);
            if (errors.Count > 0)
            {
                return Results.BadRequest(ToErrorResponse(errors));
            }

            return Results.Ok(await faults.UpdateProfileAsync(current, "api.next-nrc", cancellationToken));
        });

        app.MapPost("/api/faults/actions/disconnect", async (
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            FaultDisconnectRequest? body;
            try
            {
                body = await request.ReadFromJsonAsync<FaultDisconnectRequest>(JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new FaultErrorResponse("INVALID_JSON", "Request body must be valid JSON."));
            }

            if (body is null || string.IsNullOrWhiteSpace(body.ConnectionId))
            {
                return Results.BadRequest(new FaultErrorResponse("INVALID_CONNECTION_ID", "connectionId is required."));
            }

            var disconnected = await connections.RequestDisconnectAsync(body.ConnectionId);
            if (!disconnected)
            {
                return Results.NotFound(new FaultErrorResponse("CONNECTION_NOT_FOUND", "No active connection matched the requested connectionId."));
            }

            await faults.PublishFaultEventAsync(
                "fault.connection.disconnected",
                "Manual TCP disconnect fault requested.",
                body.ConnectionId,
                new Dictionary<string, object?>
                {
                    ["connectionId"] = body.ConnectionId,
                },
                cancellationToken);

            return Results.Ok(new { body.ConnectionId, disconnected = true });
        });

        app.MapGet("/api/ecu/state", () => Results.Ok(ToEcuStateSnapshot(ecuState, store.LoadAsync(configPath).GetAwaiter().GetResult())));

        app.MapGet("/api/dids", () => Results.Ok(didStore.List()));

        app.MapGet("/api/dtcs", () => Results.Ok(dtcStore.List()));

        app.MapGet("/api/control-services", () => Results.Ok(controlStore.GetSnapshot()));

        app.MapGet("/api/pcap/status", () => Results.Ok(pcap.GetStatus()));

        app.MapPost("/api/pcap/start", async (CancellationToken cancellationToken) =>
            Results.Ok(await pcap.StartAsync(cancellationToken)));

        app.MapPost("/api/pcap/stop", async (CancellationToken cancellationToken) =>
            Results.Ok(await pcap.StopAsync(cancellationToken)));

        app.MapPost("/api/dtcs/{code}/activate", async (
            string code,
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            DtcActivateRequest? body = null;
            if (request.ContentLength is > 0)
            {
                try
                {
                    body = await request.ReadFromJsonAsync<DtcActivateRequest>(JsonOptions, cancellationToken);
                }
                catch (JsonException)
                {
                    return Results.BadRequest(new DtcErrorResponse("INVALID_JSON", "Request body must be valid JSON."));
                }
            }

            if (!TryParseDtcRouteValue(code, out var dtcCode))
            {
                return Results.BadRequest(new DtcErrorResponse("INVALID_DTC", "DTC route value must be a 24-bit hex identifier."));
            }

            var result = await dtcStore.ActivateAsync(
                dtcCode,
                "api",
                body?.Status,
                body?.Description,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToDtcOperationError(result);
            }

            return Results.Ok(result.Snapshot);
        });

        app.MapPost("/api/dtcs/{code}/clear", async (
            string code,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseDtcRouteValue(code, out var dtcCode))
            {
                return Results.BadRequest(new DtcErrorResponse("INVALID_DTC", "DTC route value must be a 24-bit hex identifier."));
            }

            var result = await dtcStore.ClearAsync(dtcCode, "api", cancellationToken);
            if (!result.Succeeded)
            {
                return ToDtcOperationError(result);
            }

            return Results.Ok(result.Snapshot);
        });

        app.MapPut("/api/dids/{did}/value", async (
            string did,
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            DidValueUpdateRequest? body;
            try
            {
                body = await request.ReadFromJsonAsync<DidValueUpdateRequest>(JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new DidErrorResponse("INVALID_JSON", "Request body must be valid JSON."));
            }

            if (body is null || string.IsNullOrWhiteSpace(body.Value))
            {
                return Results.BadRequest(new DidErrorResponse("INVALID_DID_VALUE", "DID value is required."));
            }

            if (!TryParseDidRouteValue(did, out var didId))
            {
                return Results.BadRequest(new DidErrorResponse("INVALID_DID", "DID route value must be a 16-bit hex identifier."));
            }

            var result = await didStore.WriteHexAsync(
                didId,
                body.ValueEncoding ?? "hex",
                body.Value,
                ecuState,
                "api",
                body.Persist,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToDidWriteError(result);
            }

            return Results.Ok(new DidValueUpdateResponse(
                DidRuntimeStore.FormatDid(didId),
                "hex",
                body.Value.ToUpperInvariant(),
                body.Persist));
        });

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
            await faults.UpdateProfileAsync(config!.FaultProfile, "api.config", cancellationToken);
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

    private static bool TryParseDidRouteValue(string value, out ushort did)
    {
        var normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"0x{value}";
        return ConfigValidator.TryParseDidIdentifier(
            new DidConfig { Identifier = normalized, ValueEncoding = "hex", Value = "00" },
            out did);
    }

    private static bool TryParseDtcRouteValue(string value, out uint code)
    {
        var normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"0x{value}";
        return ConfigValidator.TryParseDtcCode(normalized, out code);
    }

    private static IResult ToDidWriteError(DidWriteResult result)
    {
        var response = new DidErrorResponse(result.Failure.ToString(), result.Message ?? "DID write rejected.");
        return result.Failure switch
        {
            DidWriteFailure.UnknownDid => Results.NotFound(response),
            DidWriteFailure.NotWritable => Results.Json(response, statusCode: StatusCodes.Status403Forbidden),
            DidWriteFailure.ConditionsNotCorrect => Results.Json(response, statusCode: StatusCodes.Status409Conflict),
            DidWriteFailure.SecurityAccessDenied => Results.Json(response, statusCode: StatusCodes.Status403Forbidden),
            _ => Results.BadRequest(response),
        };
    }

    private static IResult ToDtcOperationError(DtcOperationResult result)
    {
        var response = new DtcErrorResponse(result.Failure.ToString(), result.Message ?? "DTC operation rejected.");
        return result.Failure switch
        {
            DtcOperationFailure.UnknownDtc => Results.NotFound(response),
            _ => Results.BadRequest(response),
        };
    }

    private static EcuStateSnapshot ToEcuStateSnapshot(EcuRuntimeState state, SimulatorConfig config)
    {
        var timeout = config.Uds.TesterPresentTimeout;
        return new EcuStateSnapshot(
            $"0x{state.LogicalAddress:X4}",
            FormatSession(state.CurrentSession),
            state.SecurityStateSummary,
            state.LastTesterPresentAt,
            state.GetTesterPresentTimingSnapshot(timeout.Enabled, timeout.TimeoutMs));
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
