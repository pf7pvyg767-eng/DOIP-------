using System.Text.Json;

namespace DoipSimulator.Core.Configuration;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task<SimulatorConfig> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            var defaultConfig = SimulatorConfig.CreateDefault();
            ConfigValidator.ThrowIfInvalid(defaultConfig);
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
    }
}
