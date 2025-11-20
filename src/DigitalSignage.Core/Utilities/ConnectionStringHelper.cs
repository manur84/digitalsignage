using Microsoft.Data.SqlClient;

namespace DigitalSignage.Core.Utilities;

/// <summary>
/// Shared utility class for SQL connection string sanitization and security
/// Eliminates code duplication between SqlDataService and SqlDataSourceService
/// </summary>
public static class ConnectionStringHelper
{
    /// <summary>
    /// Sanitizes a SQL Server connection string by whitelisting safe properties only.
    /// This prevents SQL injection attacks and malicious connection string properties.
    /// </summary>
    /// <param name="connectionString">Raw connection string to sanitize</param>
    /// <returns>Sanitized connection string with only whitelisted properties</returns>
    /// <exception cref="InvalidOperationException">Thrown if connection string format is invalid</exception>
    /// <remarks>
    /// Whitelisted properties:
    /// - DataSource (server address)
    /// - InitialCatalog (database name)
    /// - IntegratedSecurity (Windows authentication)
    /// - UserID/Password (SQL authentication, only if not using IntegratedSecurity)
    /// - Encrypt/TrustServerCertificate (encryption settings)
    /// - ConnectTimeout
    /// - ApplicationIntent (ReadOnly/ReadWrite)
    /// - MultipleActiveResultSets (MARS)
    ///
    /// Security notes:
    /// - PersistSecurityInfo is ALWAYS set to false
    /// - Invalid connection strings are rejected (no fallback to unsafe strings)
    /// - Only essential properties are copied to prevent injection attacks
    /// </remarks>
    public static string SanitizeConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        try
        {
            var src = new SqlConnectionStringBuilder(connectionString);
            var dst = new SqlConnectionStringBuilder
            {
                DataSource = src.DataSource,
                InitialCatalog = src.InitialCatalog,
                IntegratedSecurity = src.IntegratedSecurity,
                Encrypt = src.Encrypt,
                TrustServerCertificate = src.TrustServerCertificate,
                ConnectTimeout = src.ConnectTimeout,
                PersistSecurityInfo = false, // SECURITY: Never persist credentials
            };

            // Only copy credentials when not using Integrated Security
            if (!dst.IntegratedSecurity)
            {
                dst.UserID = src.UserID;
                dst.Password = src.Password;
            }

            // Optional: application intent and MARS can be safely copied if set
            if (src.ContainsKey("ApplicationIntent"))
            {
                dst["ApplicationIntent"] = src["ApplicationIntent"];
            }
            if (src.ContainsKey("MultipleActiveResultSets"))
            {
                dst["MultipleActiveResultSets"] = src["MultipleActiveResultSets"];
            }

            return dst.ConnectionString;
        }
        catch (ArgumentException ex)
        {
            // ✅ SECURITY: Never fallback to unsanitized connection string
            // Invalid connection strings are rejected to prevent SQL injection
            throw new InvalidOperationException(
                "Invalid connection string format. Connection rejected for security reasons.", ex);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Invalid connection string format. Connection rejected for security reasons.", ex);
        }
    }

    /// <summary>
    /// Validates that a connection string can be parsed and sanitized
    /// </summary>
    /// <param name="connectionString">Connection string to validate</param>
    /// <param name="errorMessage">Error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool TryValidateConnectionString(string connectionString, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errorMessage = "Connection string cannot be empty";
            return false;
        }

        try
        {
            _ = SanitizeConnectionString(connectionString);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid connection string: {ex.Message}";
            return false;
        }
    }
}
