using System.Text.Json;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Core.Configuration;

public sealed class ConfigStore
{
    private readonly IRuntimeEventPublisher eventPublisher;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public ConfigStore(IRuntimeEventPublisher? eventPublisher = null)
    {
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
    }

    public async Task<SimulatorConfig> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            var defaultConfig = SimulatorConfig.CreateDefault();
            ConfigValidator.ThrowIfInvalid(defaultConfig);
            await PublishConfigEventAsync("config.loaded", "Default simulator configuration loaded.", path, cancellationToken);
            return defaultConfig;
        }

        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<SimulatorConfig>(
            stream,
            JsonOptions,
            cancellationToken);

        if (config is null)
        {
            throw new InvalidOperationException($"Configuration file '{path}' did not contain a JSON object.");
        }

        ConfigValidator.ThrowIfInvalid(config);
        await PublishConfigEventAsync("config.loaded", "Simulator configuration loaded.", path, cancellationToken);
        return config;
    }

    public async Task SaveAsync(
        string path,
        SimulatorConfig config,
        CancellationToken cancellationToken = default)
    {
        ConfigValidator.ThrowIfInvalid(config);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
        await PublishConfigEventAsync("config.saved", "Simulator configuration saved.", path, cancellationToken);
    }

    private async ValueTask PublishConfigEventAsync(
        string name,
        string message,
        string path,
        CancellationToken cancellationToken)
    {
        await eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Config,
                name,
                message,
                data: new Dictionary<string, object?>
                {
                    ["path"] = path,
                }),
            cancellationToken);
    }
}
