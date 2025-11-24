using System.Text.RegularExpressions;

namespace DigitalSignage.Core.Security;

/// <summary>
/// Password policy configuration and validation
/// </summary>
public class PasswordPolicy
{
    /// <summary>
    /// Minimum password length
    /// </summary>
    public int MinimumLength { get; set; } = 8;

    /// <summary>
    /// Require at least one uppercase letter
    /// </summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>
    /// Require at least one lowercase letter
    /// </summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>
    /// Require at least one digit
    /// </summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>
    /// Require at least one special character
    /// </summary>
    public bool RequireSpecialCharacter { get; set; } = true;

    /// <summary>
    /// Maximum password length (to prevent DoS)
    /// </summary>
    public int MaximumLength { get; set; } = 128;

    /// <summary>
    /// Validates a password against the policy
    /// </summary>
    /// <param name="password">Password to validate</param>
    /// <param name="errorMessage">Error message if validation fails</param>
    /// <returns>True if password meets policy requirements</returns>
    public bool ValidatePassword(string password, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Password cannot be empty";
            return false;
        }

        if (password.Length < MinimumLength)
        {
            errorMessage = $"Password must be at least {MinimumLength} characters long";
            return false;
        }

        if (password.Length > MaximumLength)
        {
            errorMessage = $"Password must not exceed {MaximumLength} characters";
            return false;
        }

        if (RequireUppercase && !password.Any(char.IsUpper))
        {
            errorMessage = "Password must contain at least one uppercase letter";
            return false;
        }

        if (RequireLowercase && !password.Any(char.IsLower))
        {
            errorMessage = "Password must contain at least one lowercase letter";
            return false;
        }

        if (RequireDigit && !password.Any(char.IsDigit))
        {
            errorMessage = "Password must contain at least one digit";
            return false;
        }

        if (RequireSpecialCharacter && !ContainsSpecialCharacter(password))
        {
            errorMessage = "Password must contain at least one special character (!@#$%^&*()_+-=[]{}|;:,.<>?)";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check if password contains at least one special character
    /// </summary>
    private static bool ContainsSpecialCharacter(string password)
    {
        // Common special characters
        const string specialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?/~`'\"\\";
        return password.Any(c => specialChars.Contains(c));
    }

    /// <summary>
    /// Default password policy (strong security)
    /// </summary>
    public static PasswordPolicy Default => new()
    {
        MinimumLength = 8,
        RequireUppercase = true,
        RequireLowercase = true,
        RequireDigit = true,
        RequireSpecialCharacter = true,
        MaximumLength = 128
    };

    /// <summary>
    /// Lenient password policy (for backward compatibility or testing)
    /// </summary>
    public static PasswordPolicy Lenient => new()
    {
        MinimumLength = 6,
        RequireUppercase = false,
        RequireLowercase = true,
        RequireDigit = false,
        RequireSpecialCharacter = false,
        MaximumLength = 128
    };
}
