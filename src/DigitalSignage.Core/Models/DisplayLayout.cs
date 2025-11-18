namespace DigitalSignage.Core.Models;

/// <summary>
/// Represents a complete display layout with all elements and data sources
/// </summary>
public class DisplayLayout
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string LayoutType { get; set; } = "png"; // png (preferred)
    public string Version { get; set; } = "1.0";
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime Modified { get; set; } = DateTime.UtcNow;
    public Resolution Resolution { get; set; } = new();
    public string? BackgroundImage { get; set; }
    public string? BackgroundColor { get; set; } = "#FFFFFF";
    public string? PngContentBase64 { get; set; }
    public string? FileName { get; set; }
    public List<DisplayElement> Elements { get; set; } = new();
    public List<DataSource> DataSources { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Legacy linked data source IDs for datagrid elements (no longer used)
    /// </summary>
    public List<Guid> LinkedDataSourceIds { get; set; } = new();

    /// <summary>
    /// Category for organizing layouts (e.g., "Marketing", "Operations", "Emergency")
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Tags for better organization and filtering (comma-separated or as list)
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Screen resolution configuration
/// </summary>
public class Resolution
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public string Orientation { get; set; } = "landscape"; // landscape or portrait
}
