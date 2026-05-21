using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds.Tests;

public class ReadDataByIdentifierServiceTests
{
    public static IEnumerable<object[]> InvalidDidPayloads()
    {
        yield return [Array.Empty<byte>()];
        yield return [new byte[] { 0xF1 }];
        yield return [new byte[] { 0xF1, 0x90, 0x00 }];
    }

    [Fact]
    public async Task SingleConfiguredDidReturnsPositiveResponse()
    {
        var state = new EcuRuntimeState(0x0E00);
        state.SetSession(DiagnosticSession.Extended);
        var service = CreateService();

        var responses = await service.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x90]),
            new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x62, 0xF1, 0x90, 0x01, 0x02, 0x03], response.ToBytes());
        Assert.Equal(DiagnosticSession.Extended, state.CurrentSession);
    }

    [Fact]
    public async Task MultipleConfiguredDidsPreserveRequestOrder()
    {
        var service = CreateService();

        var responses = await service.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x91, 0xF1, 0x90]),
            new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x62, 0xF1, 0x91, 0xAA, 0xBB, 0xF1, 0x90, 0x01, 0x02, 0x03], response.ToBytes());
    }

    [Fact]
    public async Task DuplicateDidIsReturnedForEachOccurrence()
    {
        var service = CreateService();

        var responses = await service.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x90, 0xF1, 0x90]),
            new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x62, 0xF1, 0x90, 0x01, 0x02, 0x03, 0xF1, 0x90, 0x01, 0x02, 0x03], response.ToBytes());
    }

    [Fact]
    public async Task UnconfiguredDidReturnsRequestOutOfRangeWithoutPartialData()
    {
        var service = CreateService();

        var responses = await service.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x90, 0xF1, 0x99]),
            new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x7F, 0x22, 0x31], response.ToBytes());
    }

    [Theory]
    [MemberData(nameof(InvalidDidPayloads))]
    public async Task InvalidRequestLengthReturnsIncorrectFormat(byte[] payload)
    {
        var service = CreateService();

        var responses = await service.HandleAsync(
            new UdsRequest(0x22, payload),
            new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x7F, 0x22, 0x13], response.ToBytes());
    }

    [Fact]
    public async Task SuccessfulReadPublishesDidReadEventsWithDidAndResponseLength()
    {
        var sink = new CapturingEventSink();
        var service = CreateService(new RuntimeEventBus([sink]));

        await service.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x91, 0xF1, 0x90]),
            new UdsContext("conn_000001", "127.0.0.1:50000", "0x0E80", "0x0E00"));

        var didEvents = sink.Events.Where(item => item.Name == "uds.did.read").ToArray();
        Assert.Equal(2, didEvents.Length);
        Assert.Equal("0xF191", didEvents[0].Data!["did"]);
        Assert.Equal("0xF191", didEvents[0].Data!["didId"]);
        Assert.Equal(4, didEvents[0].Data!["responseLength"]);
        Assert.Equal(0, didEvents[0].Data!["requestIndex"]);
        Assert.Equal("AABB", didEvents[0].Data!["rawValue"]);
        Assert.Null(didEvents[0].Data!["numericValue"]);
        Assert.Equal("static", didEvents[0].Data!["providerType"]);
        Assert.IsType<DateTimeOffset>(didEvents[0].Data!["sampledAt"]);
        Assert.Equal("0xF190", didEvents[1].Data!["did"]);
        Assert.Equal("0xF190", didEvents[1].Data!["didId"]);
        Assert.Equal(5, didEvents[1].Data!["responseLength"]);
        Assert.Equal(1, didEvents[1].Data!["requestIndex"]);
    }

    [Fact]
    public async Task RejectedRequestDoesNotPublishDidReadEvent()
    {
        var sink = new CapturingEventSink();
        var service = CreateService(new RuntimeEventBus([sink]));

        await service.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x99]),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.DoesNotContain(sink.Events, item => item.Name == "uds.did.read");
    }

    [Fact]
    public async Task ProtectedDidRequiresMatchingUnlockedSecurityLevel()
    {
        var state = new EcuRuntimeState(0x0E00);
        var service = CreateService(ecuState: state, protectedDid: true);

        var lockedResponses = await service.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x90]),
            new UdsContext());
        state.MarkSecurityLevelUnlocked(1);
        var unlockedResponses = await service.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x90]),
            new UdsContext());

        Assert.Equal([0x7F, 0x22, 0x33], Assert.Single(lockedResponses).ToBytes());
        Assert.Equal([0x62, 0xF1, 0x90, 0x01, 0x02, 0x03], Assert.Single(unlockedResponses).ToBytes());
    }

    [Fact]
    public async Task DynamicDidResponseUsesRuntimeStoreBytes()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-21T00:00:00Z"));
        var service = CreateDynamicService(timeProvider);

        var startResponses = await service.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x92]),
            new UdsContext(ConnectionId: "conn_000001"));
        timeProvider.Advance(TimeSpan.FromMilliseconds(250));
        var quarterResponses = await service.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x92]),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.Equal([0x62, 0xF1, 0x92, 0x00, 0x64], Assert.Single(startResponses).ToBytes());
        Assert.Equal([0x62, 0xF1, 0x92, 0x00, 0x6E], Assert.Single(quarterResponses).ToBytes());
    }

    [Fact]
    public async Task DynamicDidReadEventIncludesNumericSampleData()
    {
        var sink = new CapturingEventSink();
        var eventPublisher = new RuntimeEventBus([sink]);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-21T00:00:00Z"));
        var service = CreateDynamicService(timeProvider, eventPublisher);

        timeProvider.Advance(TimeSpan.FromMilliseconds(250));
        await service.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x92]),
            new UdsContext(ConnectionId: "conn_000001"));

        var didEvent = Assert.Single(sink.Events, item => item.Name == "uds.did.read");
        Assert.Equal("0xF192", didEvent.Data!["did"]);
        Assert.Equal("006E", didEvent.Data!["rawValue"]);
        Assert.Equal(110d, didEvent.Data!["numericValue"]);
        Assert.Equal("sine", didEvent.Data!["providerType"]);
        Assert.Equal(DateTimeOffset.Parse("2026-05-21T00:00:00.250Z"), didEvent.Data!["sampledAt"]);
        Assert.Equal("conn_000001", didEvent.Data!["connectionId"]);
    }

    private static ReadDataByIdentifierService CreateService(
        IRuntimeEventPublisher? eventPublisher = null,
        EcuRuntimeState? ecuState = null,
        bool protectedDid = false)
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dids =
        [
                new DidConfig
                {
                    Identifier = "0xF190",
                    Name = "VIN",
                    ValueEncoding = "hex",
                    Value = "010203",
                    RequiredSecurityLevel = protectedDid ? 1 : null,
                },
                new DidConfig
                {
                    Identifier = "0xF191",
                    Name = "Second DID",
                    ValueEncoding = "hex",
                    Value = "AABB",
                },
        ];
        var store = new DidRuntimeStore(config, "unused.json", new ConfigStore(), eventPublisher);
        return new ReadDataByIdentifierService(store, ecuState, eventPublisher);
    }

    private static ReadDataByIdentifierService CreateDynamicService(
        TimeProvider timeProvider,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dids =
        [
            new DidConfig
            {
                Identifier = "0xF192",
                Name = "Dynamic",
                ValueProvider = new DidValueProviderConfig
                {
                    Type = "sine",
                    NumericType = "uint16",
                    Amplitude = 10,
                    Offset = 100,
                    PeriodMs = 1000,
                },
            },
        ];
        var store = new DidRuntimeStore(
            config,
            "unused.json",
            new ConfigStore(),
            eventPublisher,
            timeProvider: timeProvider);
        return new ReadDataByIdentifierService(store, eventPublisher);
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
