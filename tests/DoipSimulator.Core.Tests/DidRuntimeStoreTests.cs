using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;

namespace DoipSimulator.Core.Tests;

public class DidRuntimeStoreTests
{
    [Fact]
    public async Task WritableDidUpdatesRuntimeValue()
    {
        var store = CreateStore(out _);

        var result = await store.WriteHexAsync(
            0xF190,
            "hex",
            "AABBCC",
            new EcuRuntimeState(0x0E00),
            "api",
            persist: false);

        Assert.True(result.Succeeded);
        Assert.True(store.TryRead(0xF190, out var value));
        Assert.Equal([0xAA, 0xBB, 0xCC], value);
    }

    [Fact]
    public async Task ReadOnlyDidIsRejected()
    {
        var store = CreateStore(out _);

        var result = await store.WriteHexAsync(
            0xF191,
            "hex",
            "AABB",
            new EcuRuntimeState(0x0E00),
            "api",
            persist: false);

        Assert.Equal(DidWriteFailure.NotWritable, result.Failure);
    }

    [Theory]
    [InlineData("ABC", DidWriteFailure.InvalidHex)]
    [InlineData("GG", DidWriteFailure.InvalidHex)]
    [InlineData("AABB", DidWriteFailure.LengthMismatch)]
    public async Task InvalidValuesAreRejected(string value, DidWriteFailure expectedFailure)
    {
        var store = CreateStore(out _);

        var result = await store.WriteHexAsync(
            0xF190,
            "hex",
            value,
            new EcuRuntimeState(0x0E00),
            "api",
            persist: false);

        Assert.Equal(expectedFailure, result.Failure);
        Assert.True(store.TryRead(0xF190, out var current));
        Assert.Equal([0x01, 0x02, 0x03], current);
    }

    [Fact]
    public async Task PersistTrueSavesNewDidValueToJson()
    {
        var path = CreateTempConfigPath();
        var config = CreateConfig();
        var configStore = new ConfigStore();
        await configStore.SaveAsync(path, config);
        var store = new DidRuntimeStore(config, path, configStore);

        var result = await store.WriteHexAsync(
            0xF190,
            "hex",
            "AABBCC",
            new EcuRuntimeState(0x0E00),
            "api",
            persist: true);
        var reloaded = await configStore.LoadAsync(path);

        Assert.True(result.Succeeded);
        Assert.Equal("AABBCC", Assert.Single(reloaded.Uds.Dids, did => did.Identifier == "0xF190").Value);
    }

    private static DidRuntimeStore CreateStore(out SimulatorConfig config)
    {
        config = CreateConfig();
        return new DidRuntimeStore(config, "unused.json", new ConfigStore());
    }

    private static SimulatorConfig CreateConfig()
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dids =
        [
            new DidConfig
            {
                Identifier = "0xF190",
                Name = "Writable",
                ValueEncoding = "hex",
                Value = "010203",
                Writable = true,
                WriteLength = 3,
            },
            new DidConfig
            {
                Identifier = "0xF191",
                Name = "Read-only",
                ValueEncoding = "hex",
                Value = "0102",
            },
        ];
        return config;
    }

    private static string CreateTempConfigPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "simulator.json");
    }
}
