namespace DigitalSignage.Data.Entities;

/// <summary>
/// Audit log for tracking all system changes
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty; // e.g., "Created", "Updated", "Deleted", "Login", "Logout"
    public string? EntityType { get; set; } // e.g., "Layout", "Client", "User"
    public string? EntityId { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string? Username { get; set; } // Denormalized for deleted users
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Dictionary<string, object> Changes { get; set; } = new(); // JSON of before/after values
    public string? Description { get; set; } // Human-readable description
    public bool IsSuccessful { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
