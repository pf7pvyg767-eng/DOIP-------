using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds.Tests;

public class ReadDtcInformationServiceTests
{
    public static IEnumerable<object[]> InvalidReadPayloads()
    {
        yield return [Array.Empty<byte>()];
        yield return [new byte[] { 0x02 }];
        yield return [new byte[] { 0x02, 0xFF, 0x00 }];
    }

    [Fact]
    public async Task ReportDtcByStatusMaskReturnsActiveDtc()
    {
        var store = CreateStore();
        await store.ActivateAsync(0x123456, "test", "0x2F");
        var service = new ReadDtcInformationService(store);

        var responses = await service.HandleAsync(
            new UdsRequest(0x19, [0x02, 0xFF]),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.Equal([0x59, 0x02, 0xFF, 0x12, 0x34, 0x56, 0x2F], Assert.Single(responses).ToBytes());
    }

    [Fact]
    public async Task ClearedDtcIsNotReportedAsActive()
    {
        var store = CreateStore();
        await store.ActivateAsync(0x123456, "test", "0x2F");
        await store.ClearAsync(0x123456, "test");
        var service = new ReadDtcInformationService(store);

        var responses = await service.HandleAsync(
            new UdsRequest(0x19, [0x02, 0xFF]),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.Equal([0x59, 0x02, 0xFF], Assert.Single(responses).ToBytes());
    }

    [Fact]
    public async Task UnsupportedSubfunctionReturnsNrc()
    {
        var service = new ReadDtcInformationService(CreateStore());

        var responses = await service.HandleAsync(
            new UdsRequest(0x19, [0x0A, 0xFF]),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.Equal([0x7F, 0x19, 0x12], Assert.Single(responses).ToBytes());
    }

    [Theory]
    [MemberData(nameof(InvalidReadPayloads))]
    public async Task InvalidLengthReturnsFormatNrc(params byte[] payload)
    {
        var service = new ReadDtcInformationService(CreateStore());

        var responses = await service.HandleAsync(
            new UdsRequest(0x19, payload),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.Equal([0x7F, 0x19, 0x13], Assert.Single(responses).ToBytes());
    }

    [Fact]
    public async Task SuccessfulReadPublishesDtcReadEvent()
    {
        var sink = new CapturingEventSink();
        var store = CreateStore(new RuntimeEventBus([sink]));
        await store.ActivateAsync(0x123456, "test", "0x2F");
        var service = new ReadDtcInformationService(store);

        await service.HandleAsync(
            new UdsRequest(0x19, [0x02, 0xFF]),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.Contains(sink.Events, item =>
            item.Name == "uds.dtc.read" &&
            item.Data!["returnedCount"]?.Equals(1) == true &&
            item.Data!["statusMask"]?.Equals("0xFF") == true);
    }

    private static DtcRuntimeStore CreateStore(IRuntimeEventPublisher? eventPublisher = null)
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dtcs =
        [
            new DtcConfig
            {
                Code = "0x123456",
                Name = "Configured DTC",
                Status = "0x00",
            },
        ];
        return new DtcRuntimeStore(config, eventPublisher);
    }
}
