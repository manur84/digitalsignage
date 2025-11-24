namespace DigitalSignage.Core.Security;

/// <summary>
/// Account lockout policy configuration
/// </summary>
public class AccountLockoutPolicy
{
    /// <summary>
    /// Enable account lockout mechanism
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of failed login attempts before lockout
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Lockout duration in minutes
    /// </summary>
    public int LockoutDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Time window in minutes for counting failed attempts
    /// </summary>
    public int FailedAttemptsWindowMinutes { get; set; } = 15;

    /// <summary>
    /// Default lockout policy (secure settings)
    /// </summary>
    public static AccountLockoutPolicy Default => new()
    {
        Enabled = true,
        MaxFailedAttempts = 5,
        LockoutDurationMinutes = 15,
        FailedAttemptsWindowMinutes = 15
    };

    /// <summary>
    /// Disabled lockout policy (for development/testing)
    /// </summary>
    public static AccountLockoutPolicy Disabled => new()
    {
        Enabled = false,
        MaxFailedAttempts = int.MaxValue,
        LockoutDurationMinutes = 0,
        FailedAttemptsWindowMinutes = 60
    };
}
