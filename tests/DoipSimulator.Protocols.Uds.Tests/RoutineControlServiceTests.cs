using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds.Tests;

public class RoutineControlServiceTests
{
    [Theory]
    [InlineData(0x01, "A1B2", new byte[] { 0x71, 0x01, 0x02, 0x01, 0xA1, 0xB2 })]
    [InlineData(0x02, "B1B2", new byte[] { 0x71, 0x02, 0x02, 0x01, 0xB1, 0xB2 })]
    [InlineData(0x03, "C1C2", new byte[] { 0x71, 0x03, 0x02, 0x01, 0xC1, 0xC2 })]
    public async Task ConfiguredRoutineReturnsFixedResponse(byte controlType, string expectedPayload, byte[] expectedResponse)
    {
        var service = CreateService(fixedResponses: new RoutineFixedResponses
        {
            Start = controlType == 0x01 ? expectedPayload : "00",
            Stop = controlType == 0x02 ? expectedPayload : "00",
            RequestResults = controlType == 0x03 ? expectedPayload : "00",
        });

        var responses = await service.HandleAsync(
            new UdsRequest(0x31, [controlType, 0x02, 0x01]),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.Equal(expectedResponse, Assert.Single(responses).ToBytes());
    }

    [Fact]
    public async Task UnknownRoutineReturnsRequestOutOfRange()
    {
        var service = CreateService();

        var responses = await service.HandleAsync(
            new UdsRequest(0x31, [0x01, 0x99, 0x99]),
            new UdsContext());

        Assert.Equal([0x7F, 0x31, 0x31], Assert.Single(responses).ToBytes());
    }

    [Theory]
    [InlineData(new byte[] { }, 0x13)]
    [InlineData(new byte[] { 0x01, 0x02 }, 0x13)]
    [InlineData(new byte[] { 0x7F, 0x02, 0x01 }, 0x12)]
    public async Task InvalidRoutineRequestReturnsNrc(byte[] payload, byte expectedNrc)
    {
        var service = CreateService();

        var responses = await service.HandleAsync(
            new UdsRequest(0x31, payload),
            new UdsContext());

        Assert.Equal([0x7F, 0x31, expectedNrc], Assert.Single(responses).ToBytes());
    }

    [Fact]
    public async Task RoutinePublishesRuntimeEvent()
    {
        var sink = new CapturingEventSink();
        var service = CreateService(eventPublisher: new RuntimeEventBus([sink]));

        await service.HandleAsync(
            new UdsRequest(0x31, [0x01, 0x02, 0x01]),
            new UdsContext(ConnectionId: "conn_000001"));

        Assert.Contains(sink.Events, item =>
            item.Name == "uds.routineControl.invoked" &&
            item.Data!["routineId"]?.Equals("0x0201") == true &&
            item.Data!["outcome"]?.Equals("accepted") == true);
    }

    private static RoutineControlService CreateService(
        RoutineFixedResponses? fixedResponses = null,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Routines =
        [
            new RoutineConfig
            {
                Identifier = "0x0201",
                Name = "Configured routine",
                FixedResponses = fixedResponses ?? new RoutineFixedResponses
                {
                    Start = "0000",
                    Stop = "0000",
                    RequestResults = "0001",
                },
            },
        ];

        return new RoutineControlService(config, new EcuRuntimeState(0x0E00), eventPublisher);
    }
}
