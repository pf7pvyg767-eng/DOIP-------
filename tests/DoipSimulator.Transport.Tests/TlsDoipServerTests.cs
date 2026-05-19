using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Protocols.Doip;
using DoipSimulator.Protocols.Uds;
using DoipSimulator.Transport.Tcp;

namespace DoipSimulator.Transport.Tests;

public class TlsDoipServerTests
{
    private readonly DoipCodec codec = new();

    [Fact]
    public async Task TrustedClientCertificateCompletesRoutingActivationAndUdsMainPathOverTls()
    {
        using var certificates = TestCertificates.Create();
        var events = new CapturingEventSink();
        var eventPublisher = new RuntimeEventBus([events]);
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(
            certificates,
            registry,
            eventPublisher,
            CreateUdsDispatcher(eventPublisher));
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = await AuthenticateClientAsync(client, certificates.ClientCertificate);

        await stream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
        var activationResponse = await ReadFrameAsync(stream);
        Assert.Equal(DoipPayloadType.RoutingActivationResponse, activationResponse.PayloadType);
        Assert.Equal((byte)RoutingActivationResponseCode.SuccessfullyActivated, activationResponse.Payload[4]);
        Assert.Contains(registry.GetActiveSnapshots(), connection => connection.Transport == "tls" && connection.RoutingActivated);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x10, 0x03]));
        Assert.Equal([0x50, 0x03, 0x00, 0x32, 0x13, 0x88], (await ReadFrameAsync(stream)).Payload[4..]);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x22, 0xF1, 0x90]));
        Assert.Equal([0x62, 0xF1, 0x90, 0x4C, 0x54], (await ReadFrameAsync(stream)).Payload[4..]);

        await stream.WriteAsync(CreateDiagnosticMessageFrame(0x0E80, 0x0E00, [0x3E, 0x00]));
        Assert.Equal([0x7E, 0x00], (await ReadFrameAsync(stream)).Payload[4..]);

        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "tls.handshake.succeeded");
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "doip.tls.routing_activation.succeeded");
        Assert.Contains(events.Events, runtimeEvent => runtimeEvent.Name == "connection.opened" && runtimeEvent.Data!["transport"]?.Equals("tls") == true);
    }

    [Fact]
    public async Task MissingRequiredClientCertificateFailsTlsHandshakeAndPublishesReason()
    {
        using var certificates = TestCertificates.Create();
        var events = new CapturingEventSink();
        var eventPublisher = new RuntimeEventBus([events]);
        var registry = new ConnectionRegistry();
        await using var server = CreateServer(certificates, registry, eventPublisher, CreateUdsDispatcher(eventPublisher));
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.BoundPort);
        await using var stream = new SslStream(client.GetStream(), false);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "localhost",
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            });
            await stream.WriteAsync(CreateRoutingActivationFrame(0x0E80));
            await ReadFrameAsync(stream);
        });

        await WaitUntilAsync(() => events.Events.Any(runtimeEvent => runtimeEvent.Name == "tls.handshake.failed"));
        var failure = Assert.Single(events.Events, runtimeEvent => runtimeEvent.Name == "tls.handshake.failed");
        Assert.Contains("required", failure.Data!["reason"]?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private TlsDoipServer CreateServer(
        TestCertificates certificates,
        ConnectionRegistry registry,
        IRuntimeEventPublisher eventPublisher,
        IUdsDispatcher udsDispatcher,
        bool requireClientCertificate = true)
    {
        return new TlsDoipServer(
            new TlsDoipServerOptions(
                IPAddress.Loopback,
                0,
                0x0E00,
                new HashSet<ushort> { 0x0E80 },
                certificates.ServerCertificate,
                new TlsClientCertificateValidator(requireClientCertificate, certificates.ClientCaCertificate),
                RequireClientCertificate: requireClientCertificate,
                IdleTimeout: TimeSpan.FromSeconds(5)),
            codec,
            registry,
            eventPublisher,
            udsDispatcher);
    }

    private static async Task<SslStream> AuthenticateClientAsync(TcpClient client, X509Certificate2? clientCertificate = null)
    {
        var stream = new SslStream(client.GetStream(), false);
        await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            ClientCertificates = clientCertificate is null ? null : new X509CertificateCollection { clientCertificate },
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
        });
        return stream;
    }

    private static IUdsDispatcher CreateUdsDispatcher(IRuntimeEventPublisher eventPublisher)
    {
        var state = new EcuRuntimeState(0x0E00);
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dids =
        [
            new DidConfig
            {
                Identifier = "0xF190",
                Name = "VIN",
                ValueEncoding = "hex",
                Value = "4C54",
            },
        ];
        var didRuntimeStore = new DidRuntimeStore(config, "unused.json", new ConfigStore(), eventPublisher);
        var dtcStore = new DtcRuntimeStore(config, eventPublisher);
        return new UdsDispatcher(
            [
                new DiagnosticSessionControlService(state, config, eventPublisher),
                new TesterPresentService(state),
                new SecurityAccessService(config, state, eventPublisher),
                new RequestDownloadService(state, config, eventPublisher),
                new TransferDataService(state, eventPublisher),
                new RequestTransferExitService(state, eventPublisher),
                new ReadDataByIdentifierService(didRuntimeStore, state, eventPublisher),
                new ReadDtcInformationService(dtcStore),
                new ClearDiagnosticInformationService(dtcStore),
            ],
            eventPublisher,
            config,
            state);
    }

    private byte[] CreateRoutingActivationFrame(ushort testerLogicalAddress)
    {
        var payload = new byte[RoutingActivationHandler.RequestPayloadLength];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), testerLogicalAddress);
        var encoded = codec.Encode(DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.RoutingActivationRequest,
            payload));
        Assert.True(encoded.IsSuccess);
        return encoded.Value!;
    }

    private byte[] CreateDiagnosticMessageFrame(ushort sourceAddress, ushort targetAddress, byte[] udsPayload)
    {
        var payload = new byte[4 + udsPayload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(0, 2), sourceAddress);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(2, 2), targetAddress);
        udsPayload.CopyTo(payload.AsSpan(4));
        var encoded = codec.Encode(DoipFrame.Create(
            DoipCodec.Iso13400ProtocolVersion,
            DoipPayloadType.DiagnosticMessage,
            payload));
        Assert.True(encoded.IsSuccess);
        return encoded.Value!;
    }

    private async Task<DoipFrame> ReadFrameAsync(SslStream stream)
    {
        var headerBytes = new byte[DoipCodec.HeaderLength];
        await stream.ReadExactlyAsync(headerBytes);
        var header = codec.DecodeHeader(headerBytes);
        Assert.True(header.IsSuccess);

        var payload = new byte[header.Value!.PayloadLength];
        await stream.ReadExactlyAsync(payload);
        var decoded = codec.Decode(headerBytes.Concat(payload).ToArray());
        Assert.True(decoded.IsSuccess);
        return decoded.Value!;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(3));
        while (!condition())
        {
            await Task.Delay(50, cancellation.Token);
        }
    }

    private sealed class TestCertificates : IDisposable
    {
        private TestCertificates(
            X509Certificate2 serverCertificate,
            X509Certificate2 clientCaCertificate,
            X509Certificate2 clientCertificate)
        {
            ServerCertificate = serverCertificate;
            ClientCaCertificate = clientCaCertificate;
            ClientCertificate = clientCertificate;
        }

        public X509Certificate2 ServerCertificate { get; }

        public X509Certificate2 ClientCaCertificate { get; }

        public X509Certificate2 ClientCertificate { get; }

        public static TestCertificates Create()
        {
            var serverCa = CreateCertificateAuthority("Test Server CA");
            var clientCa = CreateCertificateAuthority("Test Client CA");
            using var rawServer = CreateSignedCertificate("localhost", serverCa, "1.3.6.1.5.5.7.3.1");
            using var rawClient = CreateSignedCertificate("trusted-client", clientCa, "1.3.6.1.5.5.7.3.2");
            var server = ReimportForWindowsSchannel(rawServer);
            var client = ReimportForWindowsSchannel(rawClient);
            serverCa.Dispose();
            return new TestCertificates(server, clientCa, client);
        }

        public void Dispose()
        {
            ServerCertificate.Dispose();
            ClientCaCertificate.Dispose();
            ClientCertificate.Dispose();
        }

        private static X509Certificate2 CreateCertificateAuthority(string subject)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest(
                $"CN={subject}",
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
        }

        private static X509Certificate2 CreateSignedCertificate(string subject, X509Certificate2 issuer, string enhancedKeyUsageOid)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest(
                $"CN={subject}",
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(enhancedKeyUsageOid)], true));
            var serial = RandomNumberGenerator.GetBytes(16);
            using var signed = request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), issuer.NotAfter.AddSeconds(-1), serial);
            return signed.CopyWithPrivateKey(key);
        }

#pragma warning disable SYSLIB0057
        private static X509Certificate2 ReimportForWindowsSchannel(X509Certificate2 certificate)
        {
            return new X509Certificate2(
                certificate.Export(X509ContentType.Pkcs12),
                (string?)null,
                X509KeyStorageFlags.UserKeySet);
        }
#pragma warning restore SYSLIB0057
    }
}
