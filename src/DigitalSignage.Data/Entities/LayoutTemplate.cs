using DigitalSignage.Core.Models;

namespace DigitalSignage.Data.Entities;

/// <summary>
/// Predefined layout template for quick creation of common layouts
/// </summary>
public class LayoutTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LayoutTemplateCategory Category { get; set; }
    public string? ThumbnailPath { get; set; }
    public Resolution Resolution { get; set; } = new() { Width = 1920, Height = 1080 };
    public string? BackgroundColor { get; set; } = "#FFFFFF";
    public string? BackgroundImage { get; set; }
    public string ElementsJson { get; set; } = "[]"; // JSON serialized DisplayElement list
    public bool IsBuiltIn { get; set; } = false; // Built-in templates cannot be deleted
    public bool IsPublic { get; set; } = true;
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; } = 0;
}

public enum LayoutTemplateCategory
{
    /// <summary>
    /// Room occupancy displays
    /// </summary>
    RoomOccupancy,

    /// <summary>
    /// Information boards
    /// </summary>
    InformationBoard,

    /// <summary>
    /// Wayfinding and directory signs
    /// </summary>
    Wayfinding,

    /// <summary>
    /// Digital menu boards
    /// </summary>
    MenuBoard,

    /// <summary>
    /// Welcome screens
    /// </summary>
    WelcomeScreen,

    /// <summary>
    /// Emergency information
    /// </summary>
    Emergency,

    /// <summary>
    /// Blank templates
    /// </summary>
    Blank,

    /// <summary>
    /// Custom user templates
    /// </summary>
    Custom
}
