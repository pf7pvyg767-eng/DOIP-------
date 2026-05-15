namespace DoipSimulator.WebApi;

public sealed record WebApiRuntimeOptions(string ListenAddress, int Port, DateTimeOffset StartedAt);

public sealed record HealthResponse(string Status, string Version, DateTimeOffset StartedAt);

public static class WebApiApplication
{
    public static WebApplication Create(string[] args, WebApiRuntimeOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        builder.WebHost.UseUrls($"http://{options.ListenAddress}:{options.Port}");

        var app = builder.Build();

        app.MapGet("/api/health", () =>
            Results.Ok(new HealthResponse(
                "ok",
                typeof(WebApiApplication).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                options.StartedAt)));

        return app;
    }
}
