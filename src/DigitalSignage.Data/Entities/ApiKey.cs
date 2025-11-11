namespace DigitalSignage.Data.Entities;

/// <summary>
/// API Key for programmatic access to the Digital Signage system
/// </summary>
public class ApiKey
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty; // SHA256 hash of the actual key
    public string? Description { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? LastUsedIpAddress { get; set; }
    public int UsageCount { get; set; } = 0;
}
