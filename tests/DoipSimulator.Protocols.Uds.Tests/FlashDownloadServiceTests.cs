using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds.Tests;

public class FlashDownloadServiceTests
{
    [Fact]
    public async Task DownloadTransferAndExitCompleteMainPath()
    {
        var config = CreateFlashConfig(securityRequired: false);
        var state = new EcuRuntimeState(0x0E00);
        state.SetSession(DiagnosticSession.Programming);
        var download = new RequestDownloadService(state, config);
        var transfer = new TransferDataService(state);
        var exit = new RequestTransferExitService(state);

        var downloadResponse = await download.HandleAsync(
            new UdsRequest(0x34, [0x00, 0x44, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x05]),
            new UdsContext());
        var transferOne = await transfer.HandleAsync(new UdsRequest(0x36, [0x01, 0xAA, 0xBB]), new UdsContext());
        var transferTwo = await transfer.HandleAsync(new UdsRequest(0x36, [0x02, 0xCC, 0xDD, 0xEE]), new UdsContext());
        var exitResponse = await exit.HandleAsync(new UdsRequest(0x37, []), new UdsContext());

        Assert.Equal([0x74, 0x20, 0x00, 0x04], Assert.Single(downloadResponse).ToBytes());
        Assert.Equal([0x76, 0x01], Assert.Single(transferOne).ToBytes());
        Assert.Equal([0x76, 0x02], Assert.Single(transferTwo).ToBytes());
        Assert.Equal([0x77], Assert.Single(exitResponse).ToBytes());
        Assert.False(state.GetFlashDownloadSnapshot().IsActive);
    }

    [Fact]
    public async Task RequestDownloadRequiresProgrammingSession()
    {
        var config = CreateFlashConfig(securityRequired: false);
        var state = new EcuRuntimeState(0x0E00);
        var service = new RequestDownloadService(state, config);

        var responses = await service.HandleAsync(
            new UdsRequest(0x34, [0x00, 0x44, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x01]),
            new UdsContext());

        Assert.Equal([0x7F, 0x34, 0x22], Assert.Single(responses).ToBytes());
        Assert.False(state.GetFlashDownloadSnapshot().IsActive);
    }

    [Fact]
    public async Task RequestDownloadRequiresConfiguredSecurityUnlock()
    {
        var config = CreateFlashConfig(securityRequired: true);
        var state = new EcuRuntimeState(0x0E00);
        state.SetSession(DiagnosticSession.Programming);
        var service = new RequestDownloadService(state, config);

        var responses = await service.HandleAsync(
            new UdsRequest(0x34, [0x00, 0x44, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x01]),
            new UdsContext());

        Assert.Equal([0x7F, 0x34, 0x33], Assert.Single(responses).ToBytes());
        Assert.False(state.GetFlashDownloadSnapshot().IsActive);
    }

    [Fact]
    public async Task WrongBlockSequenceCounterDoesNotAdvanceState()
    {
        var config = CreateFlashConfig(securityRequired: false);
        var state = new EcuRuntimeState(0x0E00);
        state.SetSession(DiagnosticSession.Programming);
        var download = new RequestDownloadService(state, config);
        var transfer = new TransferDataService(state);

        await download.HandleAsync(
            new UdsRequest(0x34, [0x00, 0x44, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x03]),
            new UdsContext());
        var responses = await transfer.HandleAsync(new UdsRequest(0x36, [0x02, 0xAA]), new UdsContext());

        Assert.Equal([0x7F, 0x36, 0x73], Assert.Single(responses).ToBytes());
        var snapshot = state.GetFlashDownloadSnapshot();
        Assert.Equal(0, snapshot.ReceivedSize);
        Assert.Equal(1, snapshot.ExpectedBlockSequenceCounter);
    }

    [Fact]
    public async Task IncompleteTransferExitReturnsNrcAndKeepsStateActive()
    {
        var config = CreateFlashConfig(securityRequired: false);
        var state = new EcuRuntimeState(0x0E00);
        state.SetSession(DiagnosticSession.Programming);
        var download = new RequestDownloadService(state, config);
        var transfer = new TransferDataService(state);
        var exit = new RequestTransferExitService(state);

        await download.HandleAsync(
            new UdsRequest(0x34, [0x00, 0x44, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x03]),
            new UdsContext());
        await transfer.HandleAsync(new UdsRequest(0x36, [0x01, 0xAA]), new UdsContext());
        var responses = await exit.HandleAsync(new UdsRequest(0x37, []), new UdsContext());

        Assert.Equal([0x7F, 0x37, 0x22], Assert.Single(responses).ToBytes());
        Assert.True(state.GetFlashDownloadSnapshot().IsActive);
    }

    [Fact]
    public void FlashConfigValidationReturnsFieldSpecificErrors()
    {
        var config = CreateFlashConfig(securityRequired: true);
        config.Uds.Flash!.MaxMemorySize = 0;
        config.Uds.Flash.MaxBlockLength = 0;
        config.Uds.Flash.AllowedSessions = ["invalid"];
        config.Uds.Flash.RequiredSecurityLevel = null;

        var result = ConfigValidator.Validate(config);

        Assert.Contains(result.Errors, error => error.Field == "uds.flash.maxMemorySize");
        Assert.Contains(result.Errors, error => error.Field == "uds.flash.maxBlockLength");
        Assert.Contains(result.Errors, error => error.Field == "uds.flash.allowedSessions[0]");
        Assert.Contains(result.Errors, error => error.Field == "uds.flash.requiredSecurityLevel");
    }

    [Fact]
    public async Task DispatcherConnectionClosedClearsActiveDownloadAndPublishesEvent()
    {
        var sink = new CapturingEventSink();
        var publisher = new RuntimeEventBus([sink]);
        var config = CreateFlashConfig(securityRequired: false);
        var state = new EcuRuntimeState(0x0E00);
        state.SetSession(DiagnosticSession.Programming);
        var dispatcher = new UdsDispatcher(
            [new RequestDownloadService(state, config, publisher)],
            publisher,
            config,
            state);

        await dispatcher.DispatchAsync(
            new byte[] { 0x34, 0x00, 0x44, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x03 },
            new UdsContext(ConnectionId: "conn_000001"));
        await dispatcher.NotifyConnectionClosedAsync(new UdsContext(ConnectionId: "conn_000001"));

        Assert.False(state.GetFlashDownloadSnapshot().IsActive);
        Assert.Contains(sink.Events, runtimeEvent => runtimeEvent.Name == "uds.flash.download.cancelled");
    }

    public static SimulatorConfig CreateFlashConfig(bool securityRequired)
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.TesterPresentTimeout.Enabled = false;
        config.Uds.Flash = new FlashConfig
        {
            Enabled = true,
            MaxMemorySize = 16,
            MaxBlockLength = 4,
            AllowedSessions = ["programming"],
            SecurityRequired = securityRequired,
            RequiredSecurityLevel = 1,
        };
        return config;
    }
}
