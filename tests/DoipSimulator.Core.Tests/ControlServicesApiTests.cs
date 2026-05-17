using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Protocols.Uds;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests;

public class ControlServicesApiTests
{
    [Fact]
    public async Task ControlServicesApiReturnsRoutineConfigAndCurrentStates()
    {
        await using var app = CreateApp(out var baseAddress, out var controlStore);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            var communicationService = new CommunicationControlService(controlStore);
            var dtcSettingService = new ControlDtcSettingService(controlStore);

            await communicationService.HandleAsync(new UdsRequest(0x28, [0x03, 0x03]), new UdsContext());
            await dtcSettingService.HandleAsync(new UdsRequest(0x85, [0x02]), new UdsContext());

            var snapshot = await client.GetFromJsonAsync<ControlServicesSnapshot>("/api/control-services");

            var routine = Assert.Single(snapshot!.Routines);
            Assert.Equal("0x0201", routine.RoutineId);
            Assert.True(routine.HasStartResponse);
            Assert.Equal("disableRxAndTx", snapshot.CommunicationControl.ControlType);
            Assert.False(snapshot.DtcSetting.Enabled);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication CreateApp(
        out Uri baseAddress,
        out ControlServiceStateStore controlStore)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        var configPath = CreateTempConfigPath();
        var config = SimulatorConfig.CreateDefault();
        var configStore = new ConfigStore();
        configStore.SaveAsync(configPath, config).GetAwaiter().GetResult();
        controlStore = new ControlServiceStateStore(config);
        return WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow, configPath),
            configStore,
            controlServiceStateStore: controlStore);
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
