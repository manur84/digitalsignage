using System.Runtime.CompilerServices;

namespace DigitalSignage.Server.Utilities;

/// <summary>
/// Utility class for common validation patterns
/// Consolidates duplicate validation logic across services and ViewModels
/// </summary>
public static class ValidationHelpers
{
    /// <summary>
    /// Throws ArgumentException if string is null, empty, or whitespace
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="paramName">Parameter name for exception message</param>
    /// <exception cref="ArgumentException">Thrown when value is null, empty, or whitespace</exception>
    public static void ThrowIfNullOrWhiteSpace(
        string? value, 
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} cannot be null or empty", paramName);
        }
    }

    /// <summary>
    /// Throws ArgumentNullException if value is null
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="paramName">Parameter name for exception message</param>
    /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
    public static void ThrowIfNull(
        object? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName);
        }
    }

    /// <summary>
    /// Throws ArgumentOutOfRangeException if value is less than minimum
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="minimum">Minimum allowed value (inclusive)</param>
    /// <param name="paramName">Parameter name for exception message</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than minimum</exception>
    public static void ThrowIfLessThan(
        int value,
        int minimum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < minimum)
        {
            throw new ArgumentOutOfRangeException(paramName, value, 
                $"{paramName} must be greater than or equal to {minimum}");
        }
    }

    /// <summary>
    /// Throws ArgumentOutOfRangeException if value is out of range
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="minimum">Minimum allowed value (inclusive)</param>
    /// <param name="maximum">Maximum allowed value (inclusive)</param>
    /// <param name="paramName">Parameter name for exception message</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is out of range</exception>
    public static void ThrowIfOutOfRange(
        int value,
        int minimum,
        int maximum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(paramName, value,
                $"{paramName} must be between {minimum} and {maximum}");
        }
    }

    /// <summary>
    /// Returns true if string is null, empty, or whitespace (non-throwing variant)
    /// </summary>
    public static bool IsNullOrWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }
}
