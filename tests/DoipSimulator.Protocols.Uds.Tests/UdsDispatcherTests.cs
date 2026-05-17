using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds.Tests;

public class UdsDispatcherTests
{
    [Fact]
    public void NegativeResponseEncodesStandardBytes()
    {
        var response = new NegativeResponse(0x22, NegativeResponseCode.ServiceNotSupported);

        Assert.Equal([0x7F, 0x22, 0x11], response.ToBytes());
    }

    [Fact]
    public void UdsRequestSplitsServiceIdAndPayload()
    {
        var created = UdsRequest.TryCreate([0x22, 0xF1, 0x90], out var request);

        Assert.True(created);
        Assert.Equal(0x22, request!.ServiceId);
        Assert.Equal([0xF1, 0x90], request.Payload);
    }

    [Fact]
    public async Task UnknownServiceReturnsServiceNotSupported()
    {
        var dispatcher = new UdsDispatcher();

        var responses = await dispatcher.DispatchAsync(new byte[] { 0x99 }, new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x7F, 0x99, 0x11], response.ToBytes());
    }

    [Fact]
    public async Task EmptyPayloadReturnsIncorrectLengthOrInvalidFormat()
    {
        var dispatcher = new UdsDispatcher();

        var responses = await dispatcher.DispatchAsync(Array.Empty<byte>(), new UdsContext(ConnectionId: "conn_000001"));

        var response = Assert.Single(responses);
        Assert.Equal([0x7F, 0x00, 0x13], response.ToBytes());
    }

    [Fact]
    public async Task RegisteredServiceReceivesRequestAndContext()
    {
        var service = new EchoService();
        var dispatcher = new UdsDispatcher([service]);
        var context = new UdsContext("conn_000001", "127.0.0.1:50000", "0x0E80", "0x0E00");

        var responses = await dispatcher.DispatchAsync(new byte[] { 0x22, 0xF1, 0x90 }, context);

        Assert.Same(context, service.Context);
        Assert.Equal(0x22, service.Request!.ServiceId);
        var response = Assert.Single(responses);
        Assert.Equal([0x62, 0xF1, 0x90], response.ToBytes());
    }

    [Fact]
    public async Task DispatcherPublishesRequestResponseAndErrorEvents()
    {
        var sink = new CapturingEventSink();
        var dispatcher = new UdsDispatcher(eventPublisher: new RuntimeEventBus([sink]));

        await dispatcher.DispatchAsync(new byte[] { 0x99 }, new UdsContext(ConnectionId: "conn_000001"));

        Assert.Contains(sink.Events, item => item.Category == RuntimeEventCategory.Uds && item.Name == "uds.request.received");
        Assert.Contains(sink.Events, item => item.Category == RuntimeEventCategory.Uds && item.Name == "uds.dispatch.unsupported_service");
        Assert.Contains(sink.Events, item => item.Category == RuntimeEventCategory.Uds && item.Name == "uds.response.sent");
    }

    private sealed class EchoService : IUdsService
    {
        public byte ServiceId => 0x22;

        public UdsRequest? Request { get; private set; }

        public UdsContext? Context { get; private set; }

        public ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
            UdsRequest request,
            UdsContext context,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            Context = context;
            return ValueTask.FromResult<IReadOnlyList<UdsResponse>>([new RawUdsResponse([0x62, .. request.Payload])]);
        }
    }
}
