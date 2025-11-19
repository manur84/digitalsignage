using System.Security.Cryptography;
using System.Text;

namespace DigitalSignage.Server.Utilities;

/// <summary>
/// Shared utility class for SHA256 hash generation
/// Eliminates code duplication across QueryCacheService, DataSourceManager, and EnhancedMediaService
/// </summary>
public static class HashingHelper
{
    /// <summary>
    /// Computes SHA256 hash of a string and returns it as uppercase hexadecimal
    /// Used for cache key generation
    /// </summary>
    /// <param name="input">String to hash</param>
    /// <returns>SHA256 hash as uppercase hex string (64 characters)</returns>
    public static string ComputeSha256Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            throw new ArgumentException("Input cannot be null or empty", nameof(input));
        }

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes); // Uppercase hex (e.g., "A1B2C3...")
    }

    /// <summary>
    /// Computes SHA256 hash of a string and returns it as Base64
    /// Used for data change detection
    /// </summary>
    /// <param name="input">String to hash</param>
    /// <returns>SHA256 hash as Base64 string (44 characters)</returns>
    public static string ComputeSha256HashBase64(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            throw new ArgumentException("Input cannot be null or empty", nameof(input));
        }

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Computes SHA256 hash of byte array and returns it as lowercase hexadecimal
    /// Used for file deduplication
    /// </summary>
    /// <param name="data">Byte array to hash</param>
    /// <returns>SHA256 hash as lowercase hex string (64 characters)</returns>
    public static string ComputeSha256HashFromBytes(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Data cannot be null or empty", nameof(data));
        }

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
