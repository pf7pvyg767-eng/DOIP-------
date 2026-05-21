using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Protocols.Uds;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class DidsApiTests
{
    [Fact]
    public async Task PutDidValueThenGetDidsReturnsUpdatedValue()
    {
        await using var app = CreateApp(out var baseAddress, out _);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var putResponse = await client.PutAsJsonAsync(
                "/api/dids/F190/value",
                new DidValueUpdateRequest("hex", "AABBCC", false));
            var dids = await client.GetFromJsonAsync<DidRuntimeSnapshot[]>("/api/dids");

            Assert.True(putResponse.IsSuccessStatusCode);
            var did = Assert.Single(dids!, item => item.Did == "0xF190");
            Assert.Equal("AABBCC", did.Value);
            Assert.True(did.Writable);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task PutReadOnlyDidReturnsForbidden()
    {
        await using var app = CreateApp(out var baseAddress, out _);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var response = await client.PutAsJsonAsync(
                "/api/dids/F191/value",
                new DidValueUpdateRequest("hex", "AABB", false));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task PutInvalidValueReturnsBadRequest()
    {
        await using var app = CreateApp(out var baseAddress, out _);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var response = await client.PutAsJsonAsync(
                "/api/dids/F190/value",
                new DidValueUpdateRequest("hex", "ABC", false));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task PersistTrueSurvivesReload()
    {
        await using var app = CreateApp(out var baseAddress, out var configPath);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var response = await client.PutAsJsonAsync(
                "/api/dids/F190/value",
                new DidValueUpdateRequest("hex", "AABBCC", true));
            var reloaded = await new ConfigStore().LoadAsync(configPath);

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal("AABBCC", Assert.Single(reloaded.Uds.Dids, item => item.Identifier == "0xF190").Value);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ApiWriteIsImmediatelyReadableByReadDataByIdentifier()
    {
        await using var app = CreateApp(out var baseAddress, out _, out var didStore, out _);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            var readService = new ReadDataByIdentifierService(didStore);

            var putResponse = await client.PutAsJsonAsync(
                "/api/dids/F190/value",
                new DidValueUpdateRequest("hex", "AABBCC", false));
            var readResponses = await readService.HandleAsync(
                new UdsRequest(0x22, [0xF1, 0x90]),
                new UdsContext());

            Assert.True(putResponse.IsSuccessStatusCode);
            Assert.Equal([0x62, 0xF1, 0x90, 0xAA, 0xBB, 0xCC], Assert.Single(readResponses).ToBytes());
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task UdsWriteIsImmediatelyVisibleThroughGetDids()
    {
        await using var app = CreateApp(out var baseAddress, out _, out var didStore, out var ecuState);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            var writeService = new WriteDataByIdentifierService(didStore, ecuState);

            var writeResponses = await writeService.HandleAsync(
                new UdsRequest(0x2E, [0xF1, 0x90, 0xAA, 0xBB, 0xCC]),
                new UdsContext());
            var dids = await client.GetFromJsonAsync<DidRuntimeSnapshot[]>("/api/dids");

            Assert.Equal([0x6E, 0xF1, 0x90], Assert.Single(writeResponses).ToBytes());
            Assert.Equal("AABBCC", Assert.Single(dids!, item => item.Did == "0xF190").Value);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task DynamicDidIsVisibleThroughGetDidsAndReadDataByIdentifier()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-21T00:00:00Z"));
        await using var app = CreateApp(out var baseAddress, out _, out var didStore, out _, config =>
        {
            config.Uds.Dids.Add(new DidConfig
            {
                Identifier = "0xF192",
                Name = "Dynamic linear",
                ValueProvider = new DidValueProviderConfig
                {
                    Type = "linear",
                    NumericType = "uint16",
                    Offset = 100,
                    SlopePerSecond = 2,
                },
            });
        }, timeProvider);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            var readService = new ReadDataByIdentifierService(didStore);
            timeProvider.Advance(TimeSpan.FromSeconds(5));

            var dids = await client.GetFromJsonAsync<DidRuntimeSnapshot[]>("/api/dids");
            var readResponses = await readService.HandleAsync(
                new UdsRequest(0x22, [0xF1, 0x92]),
                new UdsContext());

            Assert.Equal("006E", Assert.Single(dids!, item => item.Did == "0xF192").Value);
            Assert.Equal([0x62, 0xF1, 0x92, 0x00, 0x6E], Assert.Single(readResponses).ToBytes());
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task GetDidSampleReturnsCurrentDynamicSample()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-21T00:00:00Z"));
        await using var app = CreateApp(out var baseAddress, out _, out _, out _, config =>
        {
            config.Uds.Dids.Add(new DidConfig
            {
                Identifier = "0xF192",
                Name = "Dynamic linear",
                ValueProvider = new DidValueProviderConfig
                {
                    Type = "linear",
                    NumericType = "uint16",
                    Offset = 100,
                    SlopePerSecond = 2,
                },
            });
        }, timeProvider);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            timeProvider.Advance(TimeSpan.FromSeconds(5));

            var sample = await client.GetFromJsonAsync<DidRuntimeSample>("/api/dids/0xF192/sample");

            Assert.NotNull(sample);
            Assert.Equal("0xF192", sample.Did);
            Assert.Equal("Dynamic linear", sample.Name);
            Assert.Equal("006E", sample.RawValue);
            Assert.Equal(110, sample.NumericValue);
            Assert.Equal("linear", sample.ProviderType);
            Assert.Equal(DateTimeOffset.Parse("2026-05-21T00:00:05Z"), sample.SampledAt);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task GetDidSampleReturnsNotFoundForUnknownDidAndBadRequestForInvalidDid()
    {
        await using var app = CreateApp(out var baseAddress, out _);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var unknown = await client.GetAsync("/api/dids/0xF199/sample");
            var invalid = await client.GetAsync("/api/dids/not-a-did/sample");

            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task GetDidSamplesReturnsStaticAndDynamicSamplesWithoutDiagnosticTraffic()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-21T00:00:00Z"));
        await using var app = CreateApp(out var baseAddress, out _, out _, out _, config =>
        {
            config.Uds.Dids.Add(new DidConfig
            {
                Identifier = "0xF192",
                Name = "Dynamic linear",
                ValueProvider = new DidValueProviderConfig
                {
                    Type = "linear",
                    NumericType = "uint16",
                    Offset = 100,
                    SlopePerSecond = 2,
                },
            });
        }, timeProvider);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            timeProvider.Advance(TimeSpan.FromSeconds(5));

            var samples = await client.GetFromJsonAsync<DidRuntimeSample[]>("/api/dids/samples");

            Assert.NotNull(samples);
            var staticSample = Assert.Single(samples, item => item.Did == "0xF190");
            var dynamicSample = Assert.Single(samples, item => item.Did == "0xF192");
            Assert.Equal("010203", staticSample.RawValue);
            Assert.Null(staticSample.NumericValue);
            Assert.Equal("static", staticSample.ProviderType);
            Assert.Equal("006E", dynamicSample.RawValue);
            Assert.Equal(110, dynamicSample.NumericValue);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task PutDidProviderUpdatesRuntimeReadsWithoutRestart()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-21T00:00:00Z"));
        await using var app = CreateApp(out var baseAddress, out _, out var didStore, out _, timeProvider: timeProvider);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            var readService = new ReadDataByIdentifierService(didStore);

            var response = await client.PutAsJsonAsync(
                "/api/dids/F190/provider",
                new DidProviderUpdateRequest(
                    new DidValueProviderConfig
                    {
                        Type = "sine",
                        NumericType = "uint16",
                        Amplitude = 10,
                        Offset = 100,
                        PeriodMs = 1000,
                    },
                    Persist: false));
            timeProvider.Advance(TimeSpan.FromMilliseconds(250));
            var readResponses = await readService.HandleAsync(
                new UdsRequest(0x22, [0xF1, 0x90]),
                new UdsContext());
            var dids = await client.GetFromJsonAsync<DidRuntimeSnapshot[]>("/api/dids");

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal([0x62, 0xF1, 0x90, 0x00, 0x6E], Assert.Single(readResponses).ToBytes());
            var did = Assert.Single(dids!, item => item.Did == "0xF190");
            Assert.False(did.Writable);
            Assert.Equal("sine", did.ValueProvider!.Type);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task PutDidProviderRejectsInvalidProviderAndUnknownDid()
    {
        await using var app = CreateApp(out var baseAddress, out _);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var invalid = await client.PutAsJsonAsync(
                "/api/dids/F190/provider",
                new DidProviderUpdateRequest(
                    new DidValueProviderConfig
                    {
                        Type = "sine",
                        NumericType = "uint16",
                        Amplitude = 10,
                        Offset = 100,
                        PeriodMs = 0,
                    },
                    Persist: false));
            var unknown = await client.PutAsJsonAsync(
                "/api/dids/F199/provider",
                new DidProviderUpdateRequest(
                    new DidValueProviderConfig
                    {
                        Type = "linear",
                        NumericType = "uint16",
                        Offset = 100,
                        SlopePerSecond = 1,
                    },
                    Persist: false));

            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication CreateApp(out Uri baseAddress, out string configPath)
    {
        return CreateApp(out baseAddress, out configPath, out _, out _);
    }

    private static WebApplication CreateApp(
        out Uri baseAddress,
        out string configPath,
        out DidRuntimeStore didStore,
        out EcuRuntimeState ecuState,
        Action<SimulatorConfig>? configure = null,
        TimeProvider? timeProvider = null)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        configPath = CreateTempConfigPath();
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
        configure?.Invoke(config);
        var configStore = new ConfigStore();
        configStore.SaveAsync(configPath, config).GetAwaiter().GetResult();
        didStore = new DidRuntimeStore(config, configPath, configStore, timeProvider: timeProvider);
        ecuState = new EcuRuntimeState(0x0E00);
        return WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow, configPath),
            configStore,
            ecuRuntimeState: ecuState,
            didRuntimeStore: didStore);
    }

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
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
