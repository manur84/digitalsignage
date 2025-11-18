namespace DigitalSignage.Data.Entities;

/// <summary>
/// Token for one-time client device registration
/// </summary>
public class ClientRegistrationToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty; // Unique registration token
    public string? Description { get; set; }
    public int? MaxUses { get; set; } // null = unlimited uses
    public int UsesCount { get; set; } = 0;
    public bool IsUsed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedByClientId { get; set; } // Reference to RaspberryPiClient.Id
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    /// <summary>
    /// Optional restrictions for this token
    /// </summary>
    public string? AllowedMacAddress { get; set; } // Restrict to specific MAC address
    public string? AllowedGroup { get; set; } // Auto-assign to group
    public string? AllowedLocation { get; set; } // Auto-assign to location

    /// <summary>
    /// Check if token is valid for use
    /// </summary>
    public bool IsValid()
    {
        if (ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value)
            return false;

        if (MaxUses.HasValue && UsesCount >= MaxUses.Value)
            return false;

        return !IsUsed || MaxUses.HasValue; // Allow reuse if MaxUses is set
    }
}
