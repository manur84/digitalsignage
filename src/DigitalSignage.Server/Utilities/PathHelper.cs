using System.IO;

namespace DigitalSignage.Server.Utilities;

/// <summary>
/// Shared utility class for path validation and security
/// Eliminates code duplication across MediaService and EnhancedMediaService
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Validates that a filename is safe and doesn't contain path traversal attempts
    /// </summary>
    /// <param name="fileName">Filename to validate</param>
    /// <returns>True if filename is safe, false if it contains path traversal or invalid characters</returns>
    /// <remarks>
    /// Checks for:
    /// - Path traversal attempts (containing "..")
    /// - Directory separators in filename
    /// - Invalid characters that could be used for path manipulation
    /// </remarks>
    public static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        // Check for path traversal attempts
        if (fileName.Contains(".."))
        {
            return false;
        }

        // Ensure filename doesn't contain directory separators
        // This prevents paths like "subdir/file.txt" or "C:\file.txt"
        if (Path.GetFileName(fileName) != fileName)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates filename and returns appropriate error message if invalid
    /// </summary>
    /// <param name="fileName">Filename to validate</param>
    /// <param name="errorMessage">Error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool TryValidateFileName(string fileName, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            errorMessage = "Filename cannot be empty";
            return false;
        }

        if (fileName.Contains(".."))
        {
            errorMessage = "Invalid filename: path traversal detected";
            return false;
        }

        if (Path.GetFileName(fileName) != fileName)
        {
            errorMessage = "Invalid filename: directory separators not allowed";
            return false;
        }

        return true;
    }
}
