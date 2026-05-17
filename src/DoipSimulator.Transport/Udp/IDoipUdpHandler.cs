namespace DoipSimulator.Transport.Udp;

public interface IDoipUdpHandler
{
    ValueTask<IReadOnlyList<OutboundDatagram>> HandleAsync(
        InboundDatagram datagram,
        CancellationToken cancellationToken = default);
}
