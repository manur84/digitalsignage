using Microsoft.EntityFrameworkCore;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Security;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using Serilog;
using System.Security.Cryptography;
using System.Text;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for authentication and authorization
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly DigitalSignageDbContext _dbContext;
    private readonly ILogger _logger;
    private readonly RateLimitingService? _rateLimitingService;
    private readonly PasswordPolicy _passwordPolicy;
    private readonly AccountLockoutPolicy _lockoutPolicy;
    private readonly SecurityEventLogger _securityLogger;

    public AuthenticationService(
        DigitalSignageDbContext dbContext, 
        RateLimitingService? rateLimitingService = null,
        PasswordPolicy? passwordPolicy = null,
        AccountLockoutPolicy? lockoutPolicy = null,
        SecurityEventLogger? securityLogger = null)
    {
        _dbContext = dbContext;
        _logger = Log.ForContext<AuthenticationService>();
        _rateLimitingService = rateLimitingService;
        _passwordPolicy = passwordPolicy ?? PasswordPolicy.Default;
        _lockoutPolicy = lockoutPolicy ?? AccountLockoutPolicy.Default;
        _securityLogger = securityLogger ?? new SecurityEventLogger(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SecurityEventLogger>.Instance);
    }

    public async Task<AuthenticationResult> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check rate limiting for username
            if (_rateLimitingService != null && !_rateLimitingService.IsRequestAllowed($"auth_user:{username}"))
            {
                _securityLogger.LogRateLimitExceeded("authentication", username);
                _logger.Warning("Authentication rate limit exceeded for user {Username}", username);
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "Too many authentication attempts. Please try again later."
                };
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.IsActive, cancellationToken);

            if (user == null)
            {
                _securityLogger.LogAuthenticationFailure(username, "User not found or inactive");
                _logger.Warning("Authentication failed: User {Username} not found or inactive", username);
                // Use constant-time delay to prevent user enumeration
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "Invalid username or password"
                };
            }

            // Check if account is locked
            if (_lockoutPolicy.Enabled && user.LockedUntil.HasValue && DateTime.UtcNow < user.LockedUntil.Value)
            {
                var remainingLockoutTime = user.LockedUntil.Value - DateTime.UtcNow;
                _securityLogger.LogAuthenticationFailure(username, "Account locked");
                _logger.Warning("Authentication failed: Account locked for user {Username} (locked until {LockedUntil})", 
                    username, user.LockedUntil.Value);
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = $"Account is locked. Please try again in {remainingLockoutTime.Minutes + 1} minutes."
                };
            }

            // Clear lockout if lock period has expired
            if (user.LockedUntil.HasValue && DateTime.UtcNow >= user.LockedUntil.Value)
            {
                user.LockedUntil = null;
                user.FailedLoginAttempts = 0;
                _logger.Information("Account lockout expired for user {Username}", username);
            }

            // Verify password
            if (!VerifyPassword(password, user.PasswordHash))
            {
                await HandleFailedLoginAsync(user, cancellationToken);
                _securityLogger.LogAuthenticationFailure(username, $"Invalid password (attempt {user.FailedLoginAttempts}/{_lockoutPolicy.MaxFailedAttempts})");
                _logger.Warning("Authentication failed: Invalid password for user {Username} (attempt {Attempts}/{MaxAttempts})", 
                    username, user.FailedLoginAttempts, _lockoutPolicy.MaxFailedAttempts);
                
                // Use constant-time delay to prevent timing attacks
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "Invalid username or password"
                };
            }

            // Successful authentication - reset failed attempts
            user.FailedLoginAttempts = 0;
            user.LastFailedLoginAt = null;
            user.LockedUntil = null;
            user.LastLoginAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _securityLogger.LogAuthenticationSuccess(username);
            _logger.Information("User {Username} authenticated successfully", username);

            return new AuthenticationResult
            {
                Success = true,
                UserId = user.Id,
                Username = user.Username,
                TokenExpiresAt = DateTime.UtcNow.AddHours(24) // Example: 24-hour session
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during authentication for user {Username}", username);
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = "Authentication error occurred"
            };
        }
    }

    /// <summary>
    /// Handle failed login attempt - increment counter and lock account if needed
    /// </summary>
    private async Task HandleFailedLoginAsync(User user, CancellationToken cancellationToken)
    {
        if (!_lockoutPolicy.Enabled)
            return;

        user.LastFailedLoginAt = DateTime.UtcNow;

        // Check if previous failures are within the time window
        var windowStart = DateTime.UtcNow.AddMinutes(-_lockoutPolicy.FailedAttemptsWindowMinutes);
        if (!user.LastFailedLoginAt.HasValue || user.LastFailedLoginAt < windowStart)
        {
            // Reset counter if outside time window or first failure
            user.FailedLoginAttempts = 1;
        }
        else
        {
            user.FailedLoginAttempts++;
        }

        // Lock account if max attempts exceeded
        if (user.FailedLoginAttempts >= _lockoutPolicy.MaxFailedAttempts)
        {
            user.LockedUntil = DateTime.UtcNow.AddMinutes(_lockoutPolicy.LockoutDurationMinutes);
            _securityLogger.LogAccountLocked(user.Username, user.FailedLoginAttempts, user.LockedUntil.Value);
            _logger.Warning("Account locked for user {Username} due to {Attempts} failed login attempts. Locked until {LockedUntil}",
                user.Username, user.FailedLoginAttempts, user.LockedUntil.Value);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ApiKeyValidationResult> ValidateApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check rate limiting for API key (use hash as identifier to prevent log spam)
            var keyHash = HashApiKey(apiKey);
            if (_rateLimitingService != null && !_rateLimitingService.IsRequestAllowed($"api_key:{keyHash[..12]}"))
            {
                _logger.Warning("API key validation rate limit exceeded");
                return new ApiKeyValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Too many API requests. Please try again later."
                };
            }

            var apiKeyEntity = await _dbContext.ApiKeys
                .Include(ak => ak.User)
                .FirstOrDefaultAsync(ak => ak.KeyHash == keyHash && ak.IsActive, cancellationToken);

            if (apiKeyEntity == null)
            {
                _logger.Warning("API key validation failed: Key not found or inactive");
                return new ApiKeyValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid or inactive API key"
                };
            }

            if (apiKeyEntity.ExpiresAt.HasValue && DateTime.UtcNow > apiKeyEntity.ExpiresAt.Value)
            {
                _logger.Warning("API key validation failed: Key expired (ID: {KeyId})", apiKeyEntity.Id);
                return new ApiKeyValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "API key has expired"
                };
            }

            if (!apiKeyEntity.User.IsActive)
            {
                _logger.Warning("API key validation failed: Associated user is inactive (ID: {UserId})", apiKeyEntity.UserId);
                return new ApiKeyValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Associated user account is inactive"
                };
            }

            // Update usage statistics
            apiKeyEntity.LastUsedAt = DateTime.UtcNow;
            apiKeyEntity.UsageCount++;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.Debug("API key validated successfully (ID: {KeyId}, User: {Username})",
                apiKeyEntity.Id, apiKeyEntity.User.Username);

            return new ApiKeyValidationResult
            {
                IsValid = true,
                UserId = apiKeyEntity.User.Id,
                Username = apiKeyEntity.User.Username,
                ApiKeyId = apiKeyEntity.Id
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during API key validation");
            return new ApiKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "API key validation error occurred"
            };
        }
    }

    public async Task<string> CreateRegistrationTokenAsync(
        int createdByUserId,
        string? description = null,
        DateTime? expiresAt = null,
        int? maxUses = null,
        string? allowedMacAddress = null,
        string? allowedGroup = null,
        string? allowedLocation = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate secure random token
            var token = GenerateSecureToken(32);

            var registrationToken = new ClientRegistrationToken
            {
                Token = token,
                Description = description,
                MaxUses = maxUses,
                ExpiresAt = expiresAt,
                CreatedByUserId = createdByUserId,
                AllowedMacAddress = allowedMacAddress,
                AllowedGroup = allowedGroup,
                AllowedLocation = allowedLocation,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ClientRegistrationTokens.Add(registrationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _securityLogger.LogRegistrationTokenCreated(createdByUserId, description);
            _logger.Information("Registration token created (ID: {TokenId}, CreatedBy: {UserId})",
                registrationToken.Id, createdByUserId);

            return token;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating registration token");
            throw;
        }
    }

    public async Task<RegistrationTokenValidationResult> ValidateRegistrationTokenAsync(
        string token,
        string clientMacAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var registrationToken = await _dbContext.ClientRegistrationTokens
                .Include(t => t.CreatedByUser)
                .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

            if (registrationToken == null)
            {
                _logger.Warning("Registration token validation failed: Token not found");
                return new RegistrationTokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid registration token"
                };
            }

            if (!registrationToken.IsValid())
            {
                _logger.Warning("Registration token validation failed: Token expired or max uses reached (ID: {TokenId})",
                    registrationToken.Id);
                return new RegistrationTokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Registration token is expired or has been fully used"
                };
            }

            // Check MAC address restriction
            if (!string.IsNullOrWhiteSpace(registrationToken.AllowedMacAddress) &&
                !registrationToken.AllowedMacAddress.Equals(clientMacAddress, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning("Registration token validation failed: MAC address mismatch (Expected: {Expected}, Got: {Actual})",
                    registrationToken.AllowedMacAddress, clientMacAddress);
                return new RegistrationTokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "This registration token is restricted to a specific device"
                };
            }

            _logger.Information("Registration token validated successfully (ID: {TokenId})", registrationToken.Id);

            return new RegistrationTokenValidationResult
            {
                IsValid = true,
                TokenId = registrationToken.Id,
                AutoAssignGroup = registrationToken.AllowedGroup,
                AutoAssignLocation = registrationToken.AllowedLocation
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during registration token validation");
            return new RegistrationTokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Token validation error occurred"
            };
        }
    }

    public async Task<bool> ConsumeRegistrationTokenAsync(
        string token,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var registrationToken = await _dbContext.ClientRegistrationTokens
                .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

            if (registrationToken == null)
                return false;

            registrationToken.UsesCount++;
            registrationToken.UsedAt = DateTime.UtcNow;
            registrationToken.UsedByClientId = clientId;

            // Mark as fully used if single-use or max uses reached
            if (!registrationToken.MaxUses.HasValue || registrationToken.UsesCount >= registrationToken.MaxUses.Value)
            {
                registrationToken.IsUsed = true;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.Information("Registration token consumed (ID: {TokenId}, ClientId: {ClientId})",
                registrationToken.Id, clientId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error consuming registration token");
            return false;
        }
    }

    public async Task<string> CreateApiKeyAsync(
        int userId,
        string name,
        string? description = null,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate secure random API key
            var apiKey = GenerateSecureToken(32);
            var keyHash = HashApiKey(apiKey);

            var apiKeyEntity = new ApiKey
            {
                Name = name,
                KeyHash = keyHash,
                Description = description,
                UserId = userId,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _dbContext.ApiKeys.Add(apiKeyEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _securityLogger.LogApiKeyCreated(userId, name);
            _logger.Information("API key created (ID: {KeyId}, Name: {Name}, UserId: {UserId})",
                apiKeyEntity.Id, name, userId);

            // Return the actual key (only shown once!)
            return apiKey;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating API key");
            throw;
        }
    }

    public async Task<bool> RevokeApiKeyAsync(int apiKeyId, CancellationToken cancellationToken = default)
    {
        try
        {
            var apiKey = await _dbContext.ApiKeys.FindAsync(new object[] { apiKeyId }, cancellationToken);
            if (apiKey == null)
                return false;

            apiKey.IsActive = false;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _securityLogger.LogApiKeyRevoked(apiKeyId);
            _logger.Information("API key revoked (ID: {KeyId})", apiKeyId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error revoking API key {KeyId}", apiKeyId);
            return false;
        }
    }

    public async Task<bool> ChangePasswordAsync(
        int userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate new password against policy
            if (!_passwordPolicy.ValidatePassword(newPassword, out var passwordError))
            {
                _logger.Warning("Password change failed: Password policy violation for user {UserId}: {Error}", 
                    userId, passwordError);
                throw new InvalidOperationException(passwordError);
            }

            var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null)
                return false;

            if (!VerifyPassword(currentPassword, user.PasswordHash))
            {
                _logger.Warning("Password change failed: Current password incorrect (UserId: {UserId})", userId);
                return false;
            }

            user.PasswordHash = HashPassword(newPassword);
            user.LastPasswordChangedAt = DateTime.UtcNow;
            
            // Reset failed login attempts on successful password change
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            user.LastFailedLoginAt = null;
            
            await _dbContext.SaveChangesAsync(cancellationToken);

            _securityLogger.LogPasswordChanged(user.Username, userId);
            _logger.Information("Password changed successfully (UserId: {UserId})", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error changing password for user {UserId}", userId);
            return false;
        }
    }

    public string HashPassword(string password)
    {
        // Use BCrypt with work factor 12 for secure password hashing
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }

    public bool ValidatePasswordPolicy(string password, out string? errorMessage)
    {
        return _passwordPolicy.ValidatePassword(password, out errorMessage);
    }

    private static string HashApiKey(string apiKey)
    {
        // ✅ FIX: Use BCrypt instead of SHA256 for API key hashing
        // Even though API keys have high entropy, BCrypt provides additional security
        // and is consistent with password hashing approach
        // Note: API keys are 32-char random strings, so work factor 10 is sufficient
        return BCrypt.Net.BCrypt.HashPassword(apiKey, workFactor: 10);
    }

    private static string GenerateSecureToken(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var random = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(random);
        }

        var token = new StringBuilder(length);
        foreach (var b in random)
        {
            token.Append(chars[b % chars.Length]);
        }

        return token.ToString();
    }
}
