using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DoipSimulator.Core.Configuration;

public sealed class TlsCertificateLoadException : Exception
{
    public TlsCertificateLoadException(string field, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Field = field;
    }

    public string Field { get; }
}

public static class TlsCertificateLoader
{
    public static X509Certificate2 LoadServerCertificate(TlsConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ServerCertificatePath))
        {
            throw new TlsCertificateLoadException(
                "tls.serverCertificatePath",
                "TLS server certificate path is required.");
        }

        if (!File.Exists(config.ServerCertificatePath))
        {
            throw new TlsCertificateLoadException(
                "tls.serverCertificatePath",
                $"TLS server certificate file was not found: {config.ServerCertificatePath}");
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(config.ServerPrivateKeyPath))
            {
                if (!File.Exists(config.ServerPrivateKeyPath))
                {
                    throw new TlsCertificateLoadException(
                        "tls.serverPrivateKeyPath",
                        $"TLS server private key file was not found: {config.ServerPrivateKeyPath}");
                }

                return X509Certificate2.CreateFromPemFile(
                    config.ServerCertificatePath,
                    config.ServerPrivateKeyPath);
            }

            return X509CertificateLoader.LoadPkcs12FromFile(
                config.ServerCertificatePath,
                config.ServerCertificatePassword,
                X509KeyStorageFlags.UserKeySet);
        }
        catch (TlsCertificateLoadException)
        {
            throw;
        }
        catch (CryptographicException exception)
        {
            throw new TlsCertificateLoadException(
                "tls.serverCertificatePath",
                $"TLS server certificate could not be loaded from {config.ServerCertificatePath}. Check certificate format, private key, or password.",
                exception);
        }
    }

    public static X509Certificate2? LoadClientCertificateAuthority(TlsConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ClientCaPath))
        {
            if (config.RequireClientCertificate)
            {
                throw new TlsCertificateLoadException(
                    "tls.clientCaPath",
                    "TLS client CA path is required when client certificates are required.");
            }

            return null;
        }

        if (!File.Exists(config.ClientCaPath))
        {
            throw new TlsCertificateLoadException(
                "tls.clientCaPath",
                $"TLS client CA certificate file was not found: {config.ClientCaPath}");
        }

        try
        {
            return X509CertificateLoader.LoadCertificateFromFile(config.ClientCaPath);
        }
        catch (CryptographicException exception)
        {
            throw new TlsCertificateLoadException(
                "tls.clientCaPath",
                $"TLS client CA certificate could not be loaded from {config.ClientCaPath}.",
                exception);
        }
    }
}
