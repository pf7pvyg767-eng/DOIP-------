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

    [Fact]
    public void RandomProviderGeneratesValuesInRangeAndSeededSequenceRepeats()
    {
        var first = CreateStoreWithDynamicDid(new DidValueProviderConfig
        {
            Type = "random",
            NumericType = "uint8",
            Min = 10,
            Max = 20,
            Seed = 42,
        });
        var second = CreateStoreWithDynamicDid(new DidValueProviderConfig
        {
            Type = "random",
            NumericType = "uint8",
            Min = 10,
            Max = 20,
            Seed = 42,
        });

        var firstSequence = ReadDidValues(first, 0xF192, 6).Select(value => value[0]).ToArray();
        var secondSequence = ReadDidValues(second, 0xF192, 6).Select(value => value[0]).ToArray();

        Assert.Equal(firstSequence, secondSequence);
        Assert.All(firstSequence, value => Assert.InRange(value, 10, 20));
    }

    [Fact]
    public void RandomProviderEncodesEveryReadInsideConfiguredRange()
    {
        var store = CreateStoreWithDynamicDid(new DidValueProviderConfig
        {
            Type = "random",
            NumericType = "uint16",
            Min = 300,
            Max = 320,
            Seed = 24,
        });

        var values = ReadDidValues(store, 0xF192, 10);

        Assert.All(values, value =>
        {
            Assert.Equal(2, value.Length);
            var decoded = (value[0] << 8) | value[1];
            Assert.InRange(decoded, 300, 320);
        });
    }

    [Fact]
    public void StaticDidSampleContainsRawHexWithoutNumericValue()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-21T00:00:00Z"));
        var store = CreateStore(out _, timeProvider);

        Assert.True(store.TrySample(0xF190, out var sample));
        Assert.Equal("0xF190", sample.Did);
        Assert.Equal("Writable", sample.Name);
        Assert.Equal("010203", sample.RawValue);
        Assert.Null(sample.NumericValue);
        Assert.Equal("static", sample.ProviderType);
        Assert.Equal(DateTimeOffset.Parse("2026-05-21T00:00:00Z"), sample.SampledAt);
    }

    [Fact]
    public void DynamicDidSampleContainsRawHexNumericValueProviderTypeAndTimestamp()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-21T00:00:00Z"));
        var store = CreateStoreWithDynamicDid(
            new DidValueProviderConfig
            {
                Type = "linear",
                NumericType = "uint16",
                Offset = 100,
                SlopePerSecond = 2,
            },
            timeProvider);

        timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.True(store.TrySample(0xF192, out var sample));
        Assert.Equal("0xF192", sample.Did);
        Assert.Equal("Dynamic", sample.Name);
        Assert.Equal("006E", sample.RawValue);
        Assert.Equal(110, sample.NumericValue);
        Assert.Equal("linear", sample.ProviderType);
        Assert.Equal(DateTimeOffset.Parse("2026-05-21T00:00:05Z"), sample.SampledAt);
        Assert.Contains(store.ListSamples(), item => item.Did == "0xF192" && item.RawValue == "006E");
    }

    [Fact]
    public void SineProviderEncodesCurrentValueAsBigEndianNumericType()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-21T00:00:00Z"));
        var store = CreateStoreWithDynamicDid(
            new DidValueProviderConfig
            {
                Type = "sine",
                NumericType = "uint16",
                Amplitude = 10,
                Offset = 100,
                PeriodMs = 1000,
            },
            timeProvider);

        Assert.True(store.TryRead(0xF192, out var startValue));
        timeProvider.Advance(TimeSpan.FromMilliseconds(250));
        Assert.True(store.TryRead(0xF192, out var quarterValue));

        Assert.Equal([0x00, 0x64], startValue);
        Assert.Equal([0x00, 0x6E], quarterValue);
    }

    [Fact]
    public void LinearProviderEncodesElapsedValueAsBigEndianNumericType()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-21T00:00:00Z"));
        var store = CreateStoreWithDynamicDid(
            new DidValueProviderConfig
            {
                Type = "linear",
                NumericType = "int16",
                Offset = -5,
                SlopePerSecond = 10,
            },
            timeProvider);

        Assert.True(store.TryRead(0xF192, out var startValue));
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        Assert.True(store.TryRead(0xF192, out var value));

        Assert.Equal([0xFF, 0xFB], startValue);
        Assert.Equal([0x00, 0x0F], value);
        Assert.NotEqual(startValue, value);
    }

    [Fact]
    public async Task DynamicDidWriteIsRejected()
    {
        var store = CreateStoreWithDynamicDid(new DidValueProviderConfig
        {
            Type = "random",
            NumericType = "uint8",
            Min = 1,
            Max = 5,
            Seed = 7,
        });

        var result = await store.WriteHexAsync(
            0xF192,
            "hex",
            "03",
            new EcuRuntimeState(0x0E00),
            "api",
            persist: false);

        Assert.Equal(DidWriteFailure.NotWritable, result.Failure);
    }

    private static DidRuntimeStore CreateStore(out SimulatorConfig config, TimeProvider? timeProvider = null)
    {
        config = CreateConfig();
        return new DidRuntimeStore(config, "unused.json", new ConfigStore(), timeProvider: timeProvider);
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

    private static DidRuntimeStore CreateStoreWithDynamicDid(
        DidValueProviderConfig provider,
        TimeProvider? timeProvider = null)
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dids =
        [
            new DidConfig
            {
                Identifier = "0xF192",
                Name = "Dynamic",
                ValueProvider = provider,
            },
        ];

        return new DidRuntimeStore(
            config,
            "unused.json",
            new ConfigStore(),
            timeProvider: timeProvider);
    }

    private static IReadOnlyList<byte[]> ReadDidValues(DidRuntimeStore store, ushort did, int count)
    {
        var values = new List<byte[]>();
        for (var index = 0; index < count; index++)
        {
            Assert.True(store.TryRead(did, out var value));
            values.Add(value);
        }

        return values;
    }

    private static string CreateTempConfigPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "simulator.json");
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset now;

        public ManualTimeProvider(DateTimeOffset now)
        {
            this.now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }

        public void Advance(TimeSpan duration)
        {
            now += duration;
        }
    }
}
