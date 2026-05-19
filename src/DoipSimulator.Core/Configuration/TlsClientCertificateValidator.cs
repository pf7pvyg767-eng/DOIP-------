using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace DoipSimulator.Core.Configuration;

public sealed record TlsClientCertificateValidationResult(bool Accepted, string Reason);

public sealed class TlsClientCertificateValidator
{
    private readonly bool requireClientCertificate;
    private readonly X509Certificate2? trustedClientCa;

    public TlsClientCertificateValidator(bool requireClientCertificate, X509Certificate2? trustedClientCa)
    {
        this.requireClientCertificate = requireClientCertificate;
        this.trustedClientCa = trustedClientCa;
    }

    public TlsClientCertificateValidationResult Validate(
        X509Certificate? certificate,
        X509Chain? _,
        SslPolicyErrors sslPolicyErrors)
    {
        if (certificate is null)
        {
            return requireClientCertificate
                ? new TlsClientCertificateValidationResult(false, "Client certificate is required but was not provided.")
                : new TlsClientCertificateValidationResult(true, "Client certificate is not required.");
        }

        if (!requireClientCertificate)
        {
            return new TlsClientCertificateValidationResult(true, "Client certificate accepted; mTLS is not required.");
        }

        if (trustedClientCa is null)
        {
            return new TlsClientCertificateValidationResult(false, "Client certificate CA is required but not configured.");
        }

        using var clientCertificate = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
        using var chain = new X509Chain();
        chain.ChainPolicy.CustomTrustStore.Add(trustedClientCa);
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

        if (chain.Build(clientCertificate))
        {
            return new TlsClientCertificateValidationResult(true, "Client certificate is trusted by configured CA.");
        }

        var reason = chain.ChainStatus.Length == 0
            ? sslPolicyErrors.ToString()
            : string.Join("; ", chain.ChainStatus.Select(status => $"{status.Status}: {status.StatusInformation.Trim()}"));

        return new TlsClientCertificateValidationResult(false, $"Client certificate validation failed: {reason}");
    }
}
