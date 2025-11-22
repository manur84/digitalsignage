using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DigitalSignage.Server.Configuration;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing SSL/TLS certificates
/// Handles self-signed certificate generation for development and certificate loading for production
///
/// TESTING NOTES:
/// - Self-signed certificates will trigger browser warnings - this is expected for development
/// - Clients (like Raspberry Pi) must be configured to accept self-signed certificates
/// - For production: Replace self-signed certificate with a proper CA-signed certificate
///
/// PORTS USED:
/// - Default HTTPS port: Same as configured HTTP port (8080-9000)
/// - WSS connections use the same port as the HTTP listener
///
/// CLIENT CONFIGURATION:
/// - Python clients: Set 'verify_ssl=False' or provide CA cert bundle
/// - Web browsers: Add certificate exception manually
/// - Mobile apps: Implement custom SSL validation
/// </summary>
public class CertificateService : ICertificateService
{
    private readonly ILogger<CertificateService> _logger;
    private readonly ServerSettings _settings;
    private readonly string _certsDirectory;

    public CertificateService(
        ILogger<CertificateService> logger,
        ServerSettings settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        // Default certificate directory: ./certs/
        _certsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "certs");

        // Ensure certs directory exists
        if (!Directory.Exists(_certsDirectory))
        {
            try
            {
                Directory.CreateDirectory(_certsDirectory);
                _logger.LogInformation("Created certificate directory: {Directory}", _certsDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create certificate directory: {Directory}", _certsDirectory);
            }
        }
    }

    /// <summary>
    /// Get or create a server certificate for SSL/TLS connections
    /// </summary>
    public X509Certificate2? GetOrCreateServerCertificate()
    {
        // SSL disabled - no certificate needed
        if (!_settings.EnableSsl)
        {
            _logger.LogInformation("SSL is disabled in configuration - no certificate loaded");
            return null;
        }

        _logger.LogInformation("SSL is enabled - loading or generating certificate...");

        // Try to load from configured path first
        if (!string.IsNullOrWhiteSpace(_settings.CertificatePath))
        {
            var cert = LoadCertificateFromFile(_settings.CertificatePath, _settings.CertificatePassword);
            if (cert != null)
            {
                if (ValidateServerCertificate(cert))
                {
                    _logger.LogInformation("Successfully loaded certificate from {Path}", _settings.CertificatePath);
                    _logger.LogInformation("Certificate Subject: {Subject}", cert.Subject);
                    _logger.LogInformation("Certificate Issuer: {Issuer}", cert.Issuer);
                    _logger.LogInformation("Certificate Valid: {NotBefore} to {NotAfter}",
                        cert.NotBefore, cert.NotAfter);
                    return cert;
                }
                else
                {
                    _logger.LogWarning("Certificate from {Path} is not valid for server use", _settings.CertificatePath);
                    cert.Dispose();
                }
            }
        }

        // No valid certificate found - generate self-signed for development
        _logger.LogWarning("No valid certificate found - generating self-signed certificate for development");
        _logger.LogWarning("⚠️  Self-signed certificates are NOT secure for production use!");
        _logger.LogWarning("⚠️  Clients will need to accept/trust this certificate manually");

        try
        {
            var defaultCertPath = Path.Combine(_certsDirectory, "server.pfx");
            var defaultPassword = _settings.CertificatePassword ?? "DigitalSignage2024!";

            var certPath = GenerateSelfSignedCertificate(
                "DigitalSignage Server",
                defaultCertPath,
                defaultPassword);

            // Update settings with generated certificate path
            _settings.CertificatePath = certPath;
            _settings.CertificatePassword = defaultPassword;

            // Load the generated certificate
            var generatedCert = LoadCertificateFromFile(certPath, defaultPassword);
            if (generatedCert != null)
            {
                _logger.LogInformation("✅ Self-signed certificate generated and loaded successfully");
                _logger.LogInformation("Certificate saved to: {Path}", certPath);
                _logger.LogInformation("Certificate password: {Password}", defaultPassword);
                return generatedCert;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate self-signed certificate");
        }

        return null;
    }

    /// <summary>
    /// Load certificate from file (.pfx format)
    /// </summary>
    public X509Certificate2? LoadCertificateFromFile(string path, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogWarning("Certificate path is null or empty");
            return null;
        }

        if (!File.Exists(path))
        {
            _logger.LogWarning("Certificate file not found: {Path}", path);
            return null;
        }

        try
        {
            // Load certificate with private key
            // X509KeyStorageFlags.Exportable allows the private key to be exported
            // X509KeyStorageFlags.PersistKeySet stores the private key in the key container
            var cert = new X509Certificate2(
                path,
                password,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

            _logger.LogDebug("Certificate loaded from {Path}", path);
            _logger.LogDebug("Has Private Key: {HasPrivateKey}", cert.HasPrivateKey);
            _logger.LogDebug("Subject: {Subject}", cert.Subject);
            _logger.LogDebug("Issuer: {Issuer}", cert.Issuer);
            _logger.LogDebug("Valid From: {NotBefore} To: {NotAfter}", cert.NotBefore, cert.NotAfter);

            return cert;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to load certificate from {Path} - invalid password or corrupted file", path);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load certificate from {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// Save certificate to file (.pfx format with private key)
    /// </summary>
    public void SaveCertificateToFile(X509Certificate2 certificate, string path, string password)
    {
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty for PFX export", nameof(password));

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Export certificate with private key to PFX format
            var certBytes = certificate.Export(X509ContentType.Pfx, password);
            File.WriteAllBytes(path, certBytes);

            _logger.LogInformation("Certificate saved to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save certificate to {Path}", path);
            throw;
        }
    }

    /// <summary>
    /// Generate a self-signed certificate for development/testing
    /// </summary>
    public string GenerateSelfSignedCertificate(string subjectName, string certPath, string password)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            throw new ArgumentException("Subject name cannot be null or empty", nameof(subjectName));

        if (string.IsNullOrWhiteSpace(certPath))
            throw new ArgumentException("Certificate path cannot be null or empty", nameof(certPath));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        _logger.LogInformation("Generating self-signed certificate: {Subject}", subjectName);

        try
        {
            // Generate RSA key pair (2048-bit for good security/performance balance)
            using var rsa = RSA.Create(2048);

            // Create certificate request
            var certRequest = new CertificateRequest(
                $"CN={subjectName}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add Subject Alternative Name (SAN) extension
            // This is required by modern browsers and clients
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName(Environment.MachineName);
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);

            // Add local network IPs
            try
            {
                var localIPs = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName())
                    .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                foreach (var ip in localIPs)
                {
                    sanBuilder.AddIpAddress(ip);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add local IP addresses to certificate SAN");
            }

            certRequest.CertificateExtensions.Add(sanBuilder.Build());

            // Add Key Usage extension (Digital Signature, Key Encipherment)
            certRequest.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));

            // Add Extended Key Usage extension (Server Authentication)
            certRequest.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection
                    {
                        new Oid("1.3.6.1.5.5.7.3.1") // Server Authentication
                    },
                    critical: true));

            // Add Basic Constraints extension (not a CA)
            certRequest.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    certificateAuthority: false,
                    hasPathLengthConstraint: false,
                    pathLengthConstraint: 0,
                    critical: true));

            // Create self-signed certificate (valid for 1 year)
            var notBefore = DateTimeOffset.UtcNow.AddDays(-1); // Start yesterday to avoid clock skew issues
            var notAfter = DateTimeOffset.UtcNow.AddYears(1);

            using var cert = certRequest.CreateSelfSigned(notBefore, notAfter);

            // Save to file
            SaveCertificateToFile(cert, certPath, password);

            _logger.LogInformation("✅ Self-signed certificate generated successfully");
            _logger.LogInformation("Certificate Details:");
            _logger.LogInformation("  - Subject: {Subject}", cert.Subject);
            _logger.LogInformation("  - Issuer: {Issuer} (self-signed)", cert.Issuer);
            _logger.LogInformation("  - Valid From: {NotBefore}", cert.NotBefore);
            _logger.LogInformation("  - Valid Until: {NotAfter}", cert.NotAfter);
            _logger.LogInformation("  - Thumbprint: {Thumbprint}", cert.Thumbprint);
            _logger.LogInformation("  - File: {Path}", certPath);
            _logger.LogInformation("  - Password: {Password}", password);
            _logger.LogInformation("");
            _logger.LogInformation("⚠️  IMPORTANT SECURITY NOTES:");
            _logger.LogInformation("  - This is a self-signed certificate suitable ONLY for development/testing");
            _logger.LogInformation("  - Clients will show security warnings and must manually trust this certificate");
            _logger.LogInformation("  - For production: Use a certificate signed by a trusted Certificate Authority (CA)");
            _logger.LogInformation("  - Never expose this certificate password in production environments");

            return certPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate self-signed certificate");
            throw;
        }
    }

    /// <summary>
    /// Validate that a certificate is suitable for use as a server certificate
    /// </summary>
    public bool ValidateServerCertificate(X509Certificate2 certificate)
    {
        if (certificate == null)
        {
            _logger.LogWarning("Certificate validation failed: certificate is null");
            return false;
        }

        // Check if expired
        var now = DateTime.Now;
        if (now < certificate.NotBefore || now > certificate.NotAfter)
        {
            _logger.LogWarning("Certificate validation failed: certificate is expired or not yet valid");
            _logger.LogWarning("Certificate valid from {NotBefore} to {NotAfter}, current time: {Now}",
                certificate.NotBefore, certificate.NotAfter, now);
            return false;
        }

        // Check if has private key
        if (!certificate.HasPrivateKey)
        {
            _logger.LogWarning("Certificate validation failed: certificate does not have a private key");
            return false;
        }

        // Check Extended Key Usage if present
        foreach (var extension in certificate.Extensions)
        {
            if (extension is X509EnhancedKeyUsageExtension ekuExtension)
            {
                var hasServerAuth = false;
                foreach (var oid in ekuExtension.EnhancedKeyUsages)
                {
                    // 1.3.6.1.5.5.7.3.1 = Server Authentication
                    if (oid.Value == "1.3.6.1.5.5.7.3.1")
                    {
                        hasServerAuth = true;
                        break;
                    }
                }

                if (!hasServerAuth)
                {
                    _logger.LogWarning("Certificate validation warning: certificate does not have Server Authentication in Extended Key Usage");
                    // Don't fail validation, just warn - some certificates might not have EKU extension
                }
            }
        }

        _logger.LogDebug("Certificate validation passed");
        return true;
    }

    /// <summary>
    /// Get the thumbprint (hash) of a certificate
    /// </summary>
    public string GetCertificateThumbprint(X509Certificate2 certificate)
    {
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));

        return certificate.Thumbprint;
    }

    /// <summary>
    /// Get or generate the Application ID (GUID) for SSL binding
    /// Returns the configured AppId from ServerSettings, or generates a consistent GUID
    /// </summary>
    public string GetCertificateAppId()
    {
        // Use configured SslAppId if available
        if (!string.IsNullOrWhiteSpace(_settings.SslAppId))
        {
            // Validate it's a valid GUID format
            if (Guid.TryParse(_settings.SslAppId, out _))
            {
                return _settings.SslAppId;
            }
            else
            {
                _logger.LogWarning("Configured SslAppId '{SslAppId}' is not a valid GUID, using default", _settings.SslAppId);
            }
        }

        // Default: Use a consistent GUID for DigitalSignage Server
        // This GUID should remain constant to allow multiple instances to share the same binding
        const string defaultAppId = "12345678-1234-1234-1234-123456789ABC";
        return defaultAppId;
    }
}
