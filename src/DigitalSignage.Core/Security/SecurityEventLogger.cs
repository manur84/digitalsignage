using Microsoft.Extensions.Logging;

namespace DigitalSignage.Core.Security;

/// <summary>
/// Centralized security event logging
/// </summary>
public class SecurityEventLogger
{
    private readonly ILogger<SecurityEventLogger> _logger;

    public SecurityEventLogger(ILogger<SecurityEventLogger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Security event types
    /// </summary>
    public enum EventType
    {
        AuthenticationSuccess,
        AuthenticationFailure,
        AccountLocked,
        AccountUnlocked,
        PasswordChanged,
        PasswordResetRequested,
        ApiKeyCreated,
        ApiKeyRevoked,
        ApiKeyValidationFailure,
        RegistrationTokenCreated,
        RegistrationTokenUsed,
        RegistrationTokenValidationFailure,
        UnauthorizedAccessAttempt,
        SuspiciousActivity,
        RateLimitExceeded
    }

    /// <summary>
    /// Log a security event
    /// </summary>
    public void LogEvent(EventType eventType, string message, params object[] args)
    {
        _logger.LogInformation("[SECURITY] {EventType}: " + message, new object[] { eventType }.Concat(args).ToArray());
    }

    /// <summary>
    /// Log a security warning
    /// </summary>
    public void LogWarning(EventType eventType, string message, params object[] args)
    {
        _logger.LogWarning("[SECURITY] {EventType}: " + message, new object[] { eventType }.Concat(args).ToArray());
    }

    /// <summary>
    /// Log a security error
    /// </summary>
    public void LogError(EventType eventType, Exception? ex, string message, params object[] args)
    {
        _logger.LogError(ex, "[SECURITY] {EventType}: " + message, new object[] { eventType }.Concat(args).ToArray());
    }

    /// <summary>
    /// Log authentication success
    /// </summary>
    public void LogAuthenticationSuccess(string username, string? ipAddress = null)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            LogEvent(EventType.AuthenticationSuccess, "User {Username} authenticated successfully", username);
        }
        else
        {
            LogEvent(EventType.AuthenticationSuccess, "User {Username} authenticated successfully from {IpAddress}", username, ipAddress);
        }
    }

    /// <summary>
    /// Log authentication failure
    /// </summary>
    public void LogAuthenticationFailure(string username, string reason, string? ipAddress = null)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            LogWarning(EventType.AuthenticationFailure, "Authentication failed for user {Username}: {Reason}", username, reason);
        }
        else
        {
            LogWarning(EventType.AuthenticationFailure, "Authentication failed for user {Username} from {IpAddress}: {Reason}", username, ipAddress, reason);
        }
    }

    /// <summary>
    /// Log account locked
    /// </summary>
    public void LogAccountLocked(string username, int failedAttempts, DateTime lockedUntil)
    {
        LogWarning(EventType.AccountLocked, "Account {Username} locked due to {FailedAttempts} failed login attempts. Locked until {LockedUntil}", 
            username, failedAttempts, lockedUntil);
    }

    /// <summary>
    /// Log password changed
    /// </summary>
    public void LogPasswordChanged(string username, int userId)
    {
        LogEvent(EventType.PasswordChanged, "Password changed for user {Username} (ID: {UserId})", username, userId);
    }

    /// <summary>
    /// Log API key created
    /// </summary>
    public void LogApiKeyCreated(int userId, string keyName)
    {
        LogEvent(EventType.ApiKeyCreated, "API key '{KeyName}' created for user ID {UserId}", keyName, userId);
    }

    /// <summary>
    /// Log API key revoked
    /// </summary>
    public void LogApiKeyRevoked(int keyId, string? reason = null)
    {
        if (string.IsNullOrEmpty(reason))
        {
            LogEvent(EventType.ApiKeyRevoked, "API key {KeyId} revoked", keyId);
        }
        else
        {
            LogEvent(EventType.ApiKeyRevoked, "API key {KeyId} revoked: {Reason}", keyId, reason);
        }
    }

    /// <summary>
    /// Log registration token created
    /// </summary>
    public void LogRegistrationTokenCreated(int userId, string? description = null)
    {
        if (string.IsNullOrEmpty(description))
        {
            LogEvent(EventType.RegistrationTokenCreated, "Registration token created by user ID {UserId}", userId);
        }
        else
        {
            LogEvent(EventType.RegistrationTokenCreated, "Registration token created by user ID {UserId}: {Description}", userId, description);
        }
    }

    /// <summary>
    /// Log suspicious activity
    /// </summary>
    public void LogSuspiciousActivity(string description, string? ipAddress = null, string? additionalInfo = null)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            LogWarning(EventType.SuspiciousActivity, "Suspicious activity detected: {Description} - {Info}", description, additionalInfo ?? "N/A");
        }
        else
        {
            LogWarning(EventType.SuspiciousActivity, "Suspicious activity detected from {IpAddress}: {Description} - {Info}", ipAddress, description, additionalInfo ?? "N/A");
        }
    }

    /// <summary>
    /// Log rate limit exceeded
    /// </summary>
    public void LogRateLimitExceeded(string resource, string? identifier = null)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            LogWarning(EventType.RateLimitExceeded, "Rate limit exceeded for {Resource}", resource);
        }
        else
        {
            LogWarning(EventType.RateLimitExceeded, "Rate limit exceeded for {Resource} by {Identifier}", resource, identifier);
        }
    }
}
