using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DoipSimulator.Core.Configuration;

namespace DoipSimulator.Core.Tests.Configuration;

public class TlsCertificateTests
{
    [Fact]
    public void ValidationRequiresServerCertificatePathWhenTlsEnabled()
    {
        var config = SimulatorConfig.CreateDefault();
        config.Tls.Enabled = true;

        var result = ConfigValidator.Validate(config);

        Assert.Contains(result.Errors, error => error.Field == "tls.serverCertificatePath");
    }

    [Fact]
    public void ValidationRequiresClientCaPathWhenClientCertificatesAreRequired()
    {
        var config = SimulatorConfig.CreateDefault();
        config.Tls.Enabled = true;
        config.Tls.ServerCertificatePath = "server.pfx";
        config.Tls.RequireClientCertificate = true;

        var result = ConfigValidator.Validate(config);

        Assert.Contains(result.Errors, error => error.Field == "tls.clientCaPath");
    }

    [Fact]
    public void ServerCertificateLoaderReportsMissingPath()
    {
        var config = new TlsConfig { ServerCertificatePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.pfx") };

        var exception = Assert.Throws<TlsCertificateLoadException>(() => TlsCertificateLoader.LoadServerCertificate(config));

        Assert.Equal("tls.serverCertificatePath", exception.Field);
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ServerCertificateLoaderRejectsInvalidPasswordWithoutLeakingPassword()
    {
        using var certificate = CreateSelfSignedCertificate("localhost");
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pfx");
        const string correctPassword = "correct-secret";
        const string wrongPassword = "wrong-secret";
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, correctPassword));

        try
        {
            var config = new TlsConfig
            {
                ServerCertificatePath = path,
                ServerCertificatePassword = wrongPassword,
            };

            var exception = Assert.Throws<TlsCertificateLoadException>(() => TlsCertificateLoader.LoadServerCertificate(config));

            Assert.Equal("tls.serverCertificatePath", exception.Field);
            Assert.DoesNotContain(wrongPassword, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ClientCertificateValidatorAcceptsTrustedClientCertificate()
    {
        using var ca = CreateCertificateAuthority();
        using var client = CreateSignedCertificate("trusted-client", ca, clientAuthentication: true);
        var validator = new TlsClientCertificateValidator(requireClientCertificate: true, ca);

        var result = validator.Validate(client, null, System.Net.Security.SslPolicyErrors.None);

        Assert.True(result.Accepted);
    }

    [Fact]
    public void ClientCertificateValidatorRejectsMissingRequiredClientCertificate()
    {
        using var ca = CreateCertificateAuthority();
        var validator = new TlsClientCertificateValidator(requireClientCertificate: true, ca);

        var result = validator.Validate(null, null, System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable);

        Assert.False(result.Accepted);
        Assert.Contains("required", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClientCertificateValidatorRejectsUntrustedClientCertificate()
    {
        using var trustedCa = CreateCertificateAuthority();
        using var untrustedCa = CreateCertificateAuthority();
        using var client = CreateSignedCertificate("untrusted-client", untrustedCa, clientAuthentication: true);
        var validator = new TlsClientCertificateValidator(requireClientCertificate: true, trustedCa);

        var result = validator.Validate(client, null, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.False(result.Accepted);
        Assert.Contains("validation failed", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static X509Certificate2 CreateCertificateAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Client CA",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
    }

    private static X509Certificate2 CreateSelfSignedCertificate(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={subject}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
    }

    private static X509Certificate2 CreateSignedCertificate(
        string subject,
        X509Certificate2 issuer,
        bool clientAuthentication)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={subject}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid(clientAuthentication ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1")],
            true));

        var serial = RandomNumberGenerator.GetBytes(16);
        using var signed = request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), issuer.NotAfter.AddSeconds(-1), serial);
        return signed.CopyWithPrivateKey(key);
    }
}
