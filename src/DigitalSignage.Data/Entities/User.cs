namespace DigitalSignage.Data.Entities;

/// <summary>
/// User entity for authentication and authorization
/// </summary>
public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Operator;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastPasswordChangedAt { get; set; }
}

public enum UserRole
{
    /// <summary>
    /// Read-only access
    /// </summary>
    Viewer,

    /// <summary>
    /// Can manage layouts, devices, and data sources
    /// </summary>
    Operator,

    /// <summary>
    /// Full system access including user management
    /// </summary>
    Admin
}
