using System.Net;

namespace DoipSimulator.Transport.Udp;

public sealed record InboundDatagram(byte[] Payload, IPEndPoint RemoteEndpoint);

public sealed record OutboundDatagram(byte[] Payload, IPEndPoint TargetEndpoint);
