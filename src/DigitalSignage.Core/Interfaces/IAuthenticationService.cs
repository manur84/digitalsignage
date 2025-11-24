namespace DigitalSignage.Core.Interfaces;

/// <summary>
/// Service for authentication and authorization
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticate user with username and password
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate API key
    /// </summary>
    Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new registration token for client devices
    /// </summary>
    Task<string> CreateRegistrationTokenAsync(
        int createdByUserId,
        string? description = null,
        DateTime? expiresAt = null,
        int? maxUses = null,
        string? allowedMacAddress = null,
        string? allowedGroup = null,
        string? allowedLocation = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate and consume a registration token
    /// </summary>
    Task<RegistrationTokenValidationResult> ValidateRegistrationTokenAsync(
        string token,
        string clientMacAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a registration token as used
    /// </summary>
    Task<bool> ConsumeRegistrationTokenAsync(
        string token,
        string clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new API key for a user
    /// </summary>
    Task<string> CreateApiKeyAsync(
        int userId,
        string name,
        string? description = null,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke an API key
    /// </summary>
    Task<bool> RevokeApiKeyAsync(int apiKeyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Change user password
    /// </summary>
    Task<bool> ChangePasswordAsync(
        int userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hash a password using secure algorithm
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verify password against hash
    /// </summary>
    bool VerifyPassword(string password, string hash);
    
    /// <summary>
    /// Validate password against current password policy
    /// </summary>
    bool ValidatePasswordPolicy(string password, out string? errorMessage);
}

/// <summary>
/// Result of authentication attempt
/// </summary>
public class AuthenticationResult
{
    public bool Success { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}

/// <summary>
/// Result of API key validation
/// </summary>
public class ApiKeyValidationResult
{
    public bool IsValid { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public int? ApiKeyId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of registration token validation
/// </summary>
public class RegistrationTokenValidationResult
{
    public bool IsValid { get; set; }
    public int? TokenId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AutoAssignGroup { get; set; }
    public string? AutoAssignLocation { get; set; }
}
