using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;

namespace DoipSimulator.Protocols.Uds.Tests;

public class WriteDataByIdentifierServiceTests
{
    [Fact]
    public async Task WritableDidReturnsPositiveResponseAndReadReturnsNewValue()
    {
        var state = new EcuRuntimeState(0x0E00);
        state.SetSession(DiagnosticSession.Extended);
        var store = CreateStore();
        var writeService = new WriteDataByIdentifierService(store, state);
        var readService = new ReadDataByIdentifierService(store);

        var writeResponses = await writeService.HandleAsync(
            new UdsRequest(0x2E, [0xF1, 0x90, 0xAA, 0xBB, 0xCC]),
            new UdsContext());
        var readResponses = await readService.HandleAsync(
            new UdsRequest(0x22, [0xF1, 0x90]),
            new UdsContext());

        Assert.Equal([0x6E, 0xF1, 0x90], Assert.Single(writeResponses).ToBytes());
        Assert.Equal([0x62, 0xF1, 0x90, 0xAA, 0xBB, 0xCC], Assert.Single(readResponses).ToBytes());
    }

    [Fact]
    public async Task ReadOnlyDidReturnsRequestOutOfRange()
    {
        var service = new WriteDataByIdentifierService(CreateStore(), new EcuRuntimeState(0x0E00));

        var responses = await service.HandleAsync(
            new UdsRequest(0x2E, [0xF1, 0x91, 0xAA, 0xBB]),
            new UdsContext());

        Assert.Equal([0x7F, 0x2E, 0x31], Assert.Single(responses).ToBytes());
    }

    [Fact]
    public async Task LengthMismatchReturnsIncorrectFormat()
    {
        var service = new WriteDataByIdentifierService(CreateStore(), new EcuRuntimeState(0x0E00));

        var responses = await service.HandleAsync(
            new UdsRequest(0x2E, [0xF1, 0x90, 0xAA]),
            new UdsContext());

        Assert.Equal([0x7F, 0x2E, 0x13], Assert.Single(responses).ToBytes());
    }

    [Fact]
    public async Task SessionPreconditionReturnsConditionsNotCorrect()
    {
        var service = new WriteDataByIdentifierService(CreateStore(), new EcuRuntimeState(0x0E00));

        var responses = await service.HandleAsync(
            new UdsRequest(0x2E, [0xF1, 0x92, 0xAA, 0xBB]),
            new UdsContext());

        Assert.Equal([0x7F, 0x2E, 0x22], Assert.Single(responses).ToBytes());
    }

    [Fact]
    public async Task SecurityPreconditionReturnsSecurityAccessDenied()
    {
        var service = new WriteDataByIdentifierService(CreateStore(), new EcuRuntimeState(0x0E00));

        var responses = await service.HandleAsync(
            new UdsRequest(0x2E, [0xF1, 0x93, 0xAA, 0xBB]),
            new UdsContext());

        Assert.Equal([0x7F, 0x2E, 0x33], Assert.Single(responses).ToBytes());
    }

    private static DidRuntimeStore CreateStore()
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
            new DidConfig
            {
                Identifier = "0xF192",
                Name = "Extended only",
                ValueEncoding = "hex",
                Value = "0102",
                Writable = true,
                WriteLength = 2,
                AllowedWriteSessions = ["extended"],
            },
            new DidConfig
            {
                Identifier = "0xF193",
                Name = "Unlocked only",
                ValueEncoding = "hex",
                Value = "0102",
                Writable = true,
                WriteLength = 2,
                RequiredSecurityState = "unlocked",
            },
        ];

        return new DidRuntimeStore(config, "unused.json", new ConfigStore());
    }
}
