using System.Text.Json;
using DoipSimulator.Core.Configuration;

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

public static class WebApiApplication
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WebApplication Create(
        string[] args,
        WebApiRuntimeOptions options,
        ConfigStore? configStore = null,
        IConfigChangePublisher? configChangePublisher = null)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        builder.WebHost.UseUrls($"http://{options.ListenAddress}:{options.Port}");

        var app = builder.Build();
        var store = configStore ?? new ConfigStore();
        var publisher = configChangePublisher ?? NullConfigChangePublisher.Instance;
        var configPath = ResolveConfigPath(options.ConfigPath);

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

        return app;
    }

    private static string ResolveConfigPath(string? configuredPath)
    {
        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "simulator.json")
            : configuredPath;
    }

    private static ConfigValidationErrorResponse ToErrorResponse(IReadOnlyList<ConfigValidationError> errors)
    {
        return new ConfigValidationErrorResponse(
            "CONFIG_VALIDATION_FAILED",
            "Simulator configuration validation failed.",
            errors.Select(error => new ConfigValidationErrorDetail(error.Field, error.Message)).ToArray());
    }
}
