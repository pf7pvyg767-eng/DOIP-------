using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Protocols.Uds;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class DtcsApiTests
{
    [Fact]
    public async Task ActivateDtcThenGetDtcsReturnsActiveState()
    {
        await using var app = CreateApp(out var baseAddress, out _, out _);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var activateResponse = await client.PostAsJsonAsync(
                "/api/dtcs/123456/activate",
                new DtcActivateRequest("0x2F", null));
            var dtcs = await client.GetFromJsonAsync<DtcRuntimeSnapshot[]>("/api/dtcs");

            Assert.True(activateResponse.IsSuccessStatusCode);
            var dtc = Assert.Single(dtcs!, item => item.Code == "0x123456");
            Assert.True(dtc.Active);
            Assert.Equal("0x2F", dtc.Status);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task UnknownDtcOperationReturnsNotFound()
    {
        await using var app = CreateApp(out var baseAddress, out _, out _);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };

            var response = await client.PostAsJsonAsync(
                "/api/dtcs/654321/activate",
                new DtcActivateRequest("0x2F", null));
            var dtcs = await client.GetFromJsonAsync<DtcRuntimeSnapshot[]>("/api/dtcs");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.All(dtcs!, item => Assert.False(item.Active));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ApiActivationIsReadableByReadDtcInformation()
    {
        await using var app = CreateApp(out var baseAddress, out _, out var dtcStore);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            var readService = new ReadDtcInformationService(dtcStore);

            var activateResponse = await client.PostAsJsonAsync(
                "/api/dtcs/123456/activate",
                new DtcActivateRequest("0x2F", null));
            var readResponses = await readService.HandleAsync(
                new UdsRequest(0x19, [0x02, 0xFF]),
                new UdsContext());

            Assert.True(activateResponse.IsSuccessStatusCode);
            Assert.Equal([0x59, 0x02, 0xFF, 0x12, 0x34, 0x56, 0x2F], Assert.Single(readResponses).ToBytes());
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task UdsClearIsReflectedByApiAndReadDtcInformation()
    {
        await using var app = CreateApp(out var baseAddress, out _, out var dtcStore);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            var clearService = new ClearDiagnosticInformationService(dtcStore);
            var readService = new ReadDtcInformationService(dtcStore);

            await client.PostAsJsonAsync("/api/dtcs/123456/activate", new DtcActivateRequest("0x2F", null));
            var clearResponses = await clearService.HandleAsync(
                new UdsRequest(0x14, [0x12, 0x34, 0x56]),
                new UdsContext());
            var dtcs = await client.GetFromJsonAsync<DtcRuntimeSnapshot[]>("/api/dtcs");
            var readResponses = await readService.HandleAsync(
                new UdsRequest(0x19, [0x02, 0xFF]),
                new UdsContext());

            Assert.Equal([0x54], Assert.Single(clearResponses).ToBytes());
            Assert.False(Assert.Single(dtcs!, item => item.Code == "0x123456").Active);
            Assert.Equal([0x59, 0x02, 0xFF], Assert.Single(readResponses).ToBytes());
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication CreateApp(
        out Uri baseAddress,
        out string configPath,
        out DtcRuntimeStore dtcStore)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        configPath = CreateTempConfigPath();
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dtcs =
        [
            new DtcConfig
            {
                Code = "0x123456",
                Name = "Injectable",
                Description = "API test DTC",
                Status = "0x00",
            },
        ];
        var configStore = new ConfigStore();
        configStore.SaveAsync(configPath, config).GetAwaiter().GetResult();
        dtcStore = new DtcRuntimeStore(config);
        return WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow, configPath),
            configStore,
            dtcRuntimeStore: dtcStore);
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
}
