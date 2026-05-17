using DoipSimulator.Core.Configuration;

namespace DoipSimulator.Protocols.Uds.Tests;

public class ClearDiagnosticInformationServiceTests
{
    public static IEnumerable<object[]> InvalidClearPayloads()
    {
        yield return [Array.Empty<byte>()];
        yield return [new byte[] { 0x12, 0x34 }];
        yield return [new byte[] { 0x12, 0x34, 0x56, 0x78 }];
    }

    [Fact]
    public async Task ClearConfiguredDtcReturnsPositiveAndUpdatesStore()
    {
        var store = CreateStore();
        await store.ActivateAsync(0x123456, "test", "0x2F");
        var clearService = new ClearDiagnosticInformationService(store);
        var readService = new ReadDtcInformationService(store);

        var clearResponses = await clearService.HandleAsync(
            new UdsRequest(0x14, [0x12, 0x34, 0x56]),
            new UdsContext(ConnectionId: "conn_000001"));
        var readResponses = await readService.HandleAsync(
            new UdsRequest(0x19, [0x02, 0xFF]),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.Equal([0x54], Assert.Single(clearResponses).ToBytes());
        Assert.Equal([0x59, 0x02, 0xFF], Assert.Single(readResponses).ToBytes());
        Assert.False(Assert.Single(store.List()).Active);
    }

    [Fact]
    public async Task ClearUnknownDtcReturnsRequestOutOfRangeWithoutChangingStore()
    {
        var store = CreateStore();
        await store.ActivateAsync(0x123456, "test", "0x2F");
        var service = new ClearDiagnosticInformationService(store);

        var responses = await service.HandleAsync(
            new UdsRequest(0x14, [0x65, 0x43, 0x21]),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.Equal([0x7F, 0x14, 0x31], Assert.Single(responses).ToBytes());
        Assert.True(Assert.Single(store.List()).Active);
    }

    [Theory]
    [MemberData(nameof(InvalidClearPayloads))]
    public async Task InvalidLengthReturnsFormatNrc(params byte[] payload)
    {
        var service = new ClearDiagnosticInformationService(CreateStore());

        var responses = await service.HandleAsync(
            new UdsRequest(0x14, payload),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.Equal([0x7F, 0x14, 0x13], Assert.Single(responses).ToBytes());
    }

    private static DtcRuntimeStore CreateStore()
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
        return new DtcRuntimeStore(config);
    }
}
