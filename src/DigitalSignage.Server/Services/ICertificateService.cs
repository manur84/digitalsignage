using System.Security.Cryptography.X509Certificates;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing SSL/TLS certificates
/// Supports self-signed certificates for development and custom certificates for production
/// </summary>
public interface ICertificateService
{
    /// <summary>
    /// Get or create a server certificate for SSL/TLS connections
    /// - First tries to load from configured path (CertificatePath in appsettings.json)
    /// - If not found or EnableSsl is false, returns null
    /// - For development: generates self-signed certificate if EnableSsl is true but no certificate exists
    /// </summary>
    /// <returns>X509Certificate2 instance or null if SSL is disabled or certificate not available</returns>
    X509Certificate2? GetOrCreateServerCertificate();

    /// <summary>
    /// Load certificate from file (.pfx format)
    /// </summary>
    /// <param name="path">Path to .pfx certificate file</param>
    /// <param name="password">Certificate password (optional)</param>
    /// <returns>X509Certificate2 instance or null if file not found or invalid</returns>
    X509Certificate2? LoadCertificateFromFile(string path, string? password = null);

    /// <summary>
    /// Save certificate to file (.pfx format with private key)
    /// </summary>
    /// <param name="certificate">Certificate to save</param>
    /// <param name="path">Path where to save the certificate</param>
    /// <param name="password">Password to protect the certificate</param>
    void SaveCertificateToFile(X509Certificate2 certificate, string path, string password);

    /// <summary>
    /// Generate a self-signed certificate for development/testing
    /// - 2048-bit RSA key
    /// - SHA256 signature
    /// - Valid for 1 year
    /// - Subject: CN={subjectName}
    /// - Extended Key Usage: Server Authentication
    /// </summary>
    /// <param name="subjectName">Certificate subject name (e.g., "DigitalSignage Server")</param>
    /// <param name="certPath">Path where to save the generated certificate (optional)</param>
    /// <param name="password">Password to protect the saved certificate (optional)</param>
    /// <returns>Path to the saved certificate file</returns>
    string GenerateSelfSignedCertificate(string subjectName, string certPath, string password);

    /// <summary>
    /// Validate that a certificate is suitable for use as a server certificate
    /// Checks:
    /// - Not expired
    /// - Has private key
    /// - Has Server Authentication extended key usage (if present)
    /// </summary>
    /// <param name="certificate">Certificate to validate</param>
    /// <returns>True if certificate is valid for server use</returns>
    bool ValidateServerCertificate(X509Certificate2 certificate);

    /// <summary>
    /// Get the thumbprint (hash) of a certificate
    /// </summary>
    /// <param name="certificate">Certificate to get thumbprint from</param>
    /// <returns>Certificate thumbprint in hexadecimal format</returns>
    string GetCertificateThumbprint(X509Certificate2 certificate);
}
