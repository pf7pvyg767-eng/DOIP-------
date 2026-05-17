using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds.Tests;

public class SecurityAccessServiceTests
{
    [Fact]
    public async Task SeedRequestReturnsNonEmptySeedAndCorrectKeyUnlocksLevel()
    {
        var config = CreateConfig();
        var ecuState = new EcuRuntimeState(0x0E00);
        var service = new SecurityAccessService(config, ecuState);

        var seedResponses = await service.HandleAsync(
            new UdsRequest(0x27, [0x01]),
            new UdsContext());
        var seedResponse = Assert.Single(seedResponses).ToBytes();

        Assert.Equal(0x67, seedResponse[0]);
        Assert.Equal(0x01, seedResponse[1]);
        Assert.NotEmpty(seedResponse[2..]);

        var key = SecurityAccessService.ComputeExpectedKey(config.Uds.SecurityAccess[0], seedResponse[2..]);
        var keyResponses = await service.HandleAsync(
            new UdsRequest(0x27, [0x02, .. key]),
            new UdsContext());

        Assert.Equal([0x67, 0x02], Assert.Single(keyResponses).ToBytes());
        Assert.True(ecuState.IsSecurityLevelUnlocked(1));
    }

    [Fact]
    public async Task WrongKeyReturnsNrcAndIncrementsFailureCount()
    {
        var config = CreateConfig(maxFailedAttempts: 3);
        var ecuState = new EcuRuntimeState(0x0E00);
        var service = new SecurityAccessService(config, ecuState);

        await service.HandleAsync(new UdsRequest(0x27, [0x01]), new UdsContext());
        var responses = await service.HandleAsync(
            new UdsRequest(0x27, [0x02, 0x00, 0x00, 0x00, 0x00]),
            new UdsContext());

        Assert.Equal([0x7F, 0x27, 0x35], Assert.Single(responses).ToBytes());
        Assert.Equal(1, ecuState.GetSecurityLevelSnapshot(1, DateTimeOffset.UtcNow).FailedAttempts);
        Assert.False(ecuState.IsSecurityLevelUnlocked(1));
    }

    [Fact]
    public async Task MaxFailedAttemptsEnterLockoutAndRejectSeedUntilDelayExpires()
    {
        var now = new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero);
        var config = CreateConfig(maxFailedAttempts: 2, lockoutMs: 5000);
        var ecuState = new EcuRuntimeState(0x0E00);
        var service = new SecurityAccessService(config, ecuState, nowProvider: () => now);

        await service.HandleAsync(new UdsRequest(0x27, [0x01]), new UdsContext());
        await service.HandleAsync(new UdsRequest(0x27, [0x02, 0x00]), new UdsContext());
        var lockoutResponse = await service.HandleAsync(new UdsRequest(0x27, [0x02, 0x00]), new UdsContext());
        var rejectedSeed = await service.HandleAsync(new UdsRequest(0x27, [0x01]), new UdsContext());

        Assert.Equal([0x7F, 0x27, 0x36], Assert.Single(lockoutResponse).ToBytes());
        Assert.Equal([0x7F, 0x27, 0x37], Assert.Single(rejectedSeed).ToBytes());

        now = now.AddMilliseconds(5001);
        var acceptedSeed = await service.HandleAsync(new UdsRequest(0x27, [0x01]), new UdsContext());

        Assert.Equal(0x67, Assert.Single(acceptedSeed).ToBytes()[0]);
    }

    [Fact]
    public async Task UnsupportedSubFunctionAndKeyBeforeSeedReturnNrc()
    {
        var service = new SecurityAccessService(CreateConfig(), new EcuRuntimeState(0x0E00));

        var unsupported = await service.HandleAsync(new UdsRequest(0x27, [0x7F]), new UdsContext());
        var keyBeforeSeed = await service.HandleAsync(new UdsRequest(0x27, [0x02, 0x00]), new UdsContext());

        Assert.Equal([0x7F, 0x27, 0x12], Assert.Single(unsupported).ToBytes());
        Assert.Equal([0x7F, 0x27, 0x22], Assert.Single(keyBeforeSeed).ToBytes());
    }

    [Fact]
    public async Task SecurityAccessPublishesEventsWithoutKeyMaterial()
    {
        var sink = new CapturingEventSink();
        var service = new SecurityAccessService(
            CreateConfig(),
            new EcuRuntimeState(0x0E00),
            new RuntimeEventBus([sink]));

        await service.HandleAsync(new UdsRequest(0x27, [0x01]), new UdsContext(ConnectionId: "conn_000001"));

        var securityEvent = Assert.Single(sink.Events, item => item.Name == "uds.securityAccess.processed");
        Assert.Equal("0x27", securityEvent.Data!["serviceId"]);
        Assert.Equal(1, securityEvent.Data["securityLevel"]);
        Assert.Equal("seed-issued", securityEvent.Data["outcome"]);
        Assert.DoesNotContain(securityEvent.Data.Keys, key => key.Contains("key", StringComparison.OrdinalIgnoreCase));
    }

    public static SimulatorConfig CreateConfig(int maxFailedAttempts = 3, int lockoutMs = 1000)
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.SecurityAccess =
        [
            new SecurityAccessConfig
            {
                Level = 1,
                SeedSubFunction = "0x01",
                KeySubFunction = "0x02",
                Algorithm = "builtin-xor",
                AlgorithmParameter = "A5",
                MaxFailedAttempts = maxFailedAttempts,
                LockoutMs = lockoutMs,
            },
        ];
        return config;
    }
}
