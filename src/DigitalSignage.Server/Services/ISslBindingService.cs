using System.Security.Cryptography.X509Certificates;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing Windows SSL/TLS port bindings via netsh
/// WINDOWS-ONLY: Uses netsh http commands to configure SSL certificate bindings
/// </summary>
/// <remarks>
/// This service automates the SSL certificate binding process on Windows:
/// - Checks if SSL binding exists for a port
/// - Adds SSL binding using netsh http add sslcert
/// - Removes SSL binding using netsh http delete sslcert
/// - Creates URL ACL reservations using netsh http add urlacl
///
/// REQUIRES: Administrator privileges for most operations
///
/// netsh http add sslcert ipport=0.0.0.0:8080 certhash={thumbprint} appid={guid}
/// netsh http add urlacl url=https://+:8080/ws/ user=Everyone
/// </remarks>
public interface ISslBindingService
{
    /// <summary>
    /// Ensure SSL binding exists for the certificate and port
    /// If binding exists with different certificate, it will be updated
    /// </summary>
    /// <param name="certificate">SSL certificate to bind</param>
    /// <param name="port">Port number to bind to</param>
    /// <returns>True if binding was successfully configured, false otherwise</returns>
    Task<bool> EnsureSslBindingAsync(X509Certificate2 certificate, int port);

    /// <summary>
    /// Remove SSL binding from a port
    /// </summary>
    /// <param name="port">Port number to unbind</param>
    /// <returns>True if binding was removed or didn't exist, false on error</returns>
    Task<bool> RemoveSslBindingAsync(int port);

    /// <summary>
    /// Check if the application is running with Administrator privileges
    /// Required for netsh SSL binding operations
    /// </summary>
    /// <returns>True if running as administrator</returns>
    bool IsRunningAsAdministrator();

    /// <summary>
    /// Add URL ACL reservation to allow non-admin users to listen on a URL
    /// </summary>
    /// <param name="url">URL to reserve (e.g., "https://+:8080/ws/")</param>
    /// <returns>True if ACL was added or already exists, false on error</returns>
    Task<bool> AddUrlAclAsync(string url);

    /// <summary>
    /// Check if SSL binding exists for a port
    /// </summary>
    /// <param name="port">Port number to check</param>
    /// <returns>True if binding exists</returns>
    Task<bool> SslBindingExistsAsync(int port);

    /// <summary>
    /// Get the certificate thumbprint for an existing SSL binding
    /// </summary>
    /// <param name="port">Port number to check</param>
    /// <returns>Certificate thumbprint if binding exists, null otherwise</returns>
    Task<string?> GetBoundCertificateThumbprintAsync(int port);
}
