using CommunityToolkit.Mvvm.ComponentModel;

namespace DigitalSignage.Core.Models;

/// <summary>
/// Base class for all display elements
/// </summary>
public partial class DisplayElement : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _type = "unknown"; // text, image, shape, qrcode, table, datetime

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Position _position = new();

    [ObservableProperty]
    private Size _size = new();

    [ObservableProperty]
    private int _zIndex = 0;

    [ObservableProperty]
    private double _rotation = 0; // 0-360 degrees

    [ObservableProperty]
    private double _opacity = 1.0; // 0.0-1.0

    [ObservableProperty]
    private bool _visible = true;

    [ObservableProperty]
    private string? _dataBinding; // {{variable.name}} syntax

    [ObservableProperty]
    private Dictionary<string, object> _properties = new();

    [ObservableProperty]
    private Animation? _animation;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Initializes default properties for all element types
    /// This prevents KeyNotFoundException when binding in XAML
    /// </summary>
    public void InitializeDefaultProperties()
    {
        if (Properties == null)
            Properties = new Dictionary<string, object>();

        // ============================================
        // COMMON PROPERTIES (ALL ELEMENT TYPES)
        // ============================================
        EnsureProperty("Rotation", 0.0);
        EnsureProperty("IsVisible", true);
        EnsureProperty("IsLocked", false);

        // Shadow properties
        EnsureProperty("EnableShadow", false);
        EnsureProperty("ShadowBlur", 5.0);
        EnsureProperty("ShadowColor", "#000000");
        EnsureProperty("ShadowOffsetX", 2.0);
        EnsureProperty("ShadowOffsetY", 2.0);

        // Border/Shape properties (add to ALL types for consistency)
        EnsureProperty("BorderRadius", 0.0);       // Used in Properties panel and DesignerItemControl
        EnsureProperty("CornerRadius", 0.0);       // Used in XAML for Rectangle elements
        EnsureProperty("Source", "");              // Used in XAML for Image elements
        EnsureProperty("FillColor", "#FFFFFF");
        EnsureProperty("BorderColor", "#000000");
        EnsureProperty("BorderThickness", 0.0);

        // ============================================
        // TYPE-SPECIFIC PROPERTIES
        // ============================================
        string elementType = Type?.ToLower() ?? "text";

        switch (elementType)
        {
            case "text":
                // TEXT PROPERTIES - CRITICAL!
                EnsureProperty("Content", "Sample Text");
                EnsureProperty("FontFamily", "Arial");
                EnsureProperty("FontSize", 24.0);  // MUST be Double!
                EnsureProperty("FontWeight", "Normal");
                EnsureProperty("FontStyle", "Normal");
                EnsureProperty("Color", "#000000");
                EnsureProperty("TextAlign", "Left");
                EnsureProperty("VerticalAlign", "Top");
                EnsureProperty("WordWrap", true);
                EnsureProperty("FillColor", "Transparent");  // Background
                EnsureProperty("BorderThickness", 0.0);  // No border by default
                break;

            case "image":
                EnsureProperty("Source", "");
                EnsureProperty("Stretch", "Uniform");
                EnsureProperty("AltText", "Image");
                break;

            case "rectangle":
            case "shape":
                EnsureProperty("FillColor", "#ADD8E6");  // Light blue
                EnsureProperty("BorderColor", "#0000FF");  // Blue
                EnsureProperty("BorderThickness", 2.0);
                EnsureProperty("CornerRadius", 0.0);
                break;

            case "circle":
                EnsureProperty("FillColor", "#FFD700");  // Gold
                EnsureProperty("BorderColor", "#FFA500");  // Orange
                EnsureProperty("BorderThickness", 2.0);
                break;

            case "qrcode":
                EnsureProperty("Data", "https://example.com");
                EnsureProperty("ErrorCorrection", "M");
                EnsureProperty("ForegroundColor", "#000000");
                EnsureProperty("BackgroundColor", "#FFFFFF");
                break;

            case "table":
                EnsureProperty("HeaderBackground", "#4CAF50");
                EnsureProperty("HeaderForeground", "#FFFFFF");
                EnsureProperty("RowBackground", "#FFFFFF");
                EnsureProperty("AlternateRowBackground", "#F5F5F5");
                EnsureProperty("BorderColor", "#DDDDDD");
                EnsureProperty("BorderWidth", 1.0);
                EnsureProperty("FontSize", 14.0);
                EnsureProperty("ShowBorder", true);
                break;

            case "datetime":
                EnsureProperty("Format", "dddd, dd MMMM yyyy HH:mm:ss");
                EnsureProperty("FontFamily", "Arial");
                EnsureProperty("FontSize", 24.0);
                EnsureProperty("Color", "#000000");
                EnsureProperty("UpdateInterval", 1000);  // Update every second
                break;
        }
    }

    /// <summary>
    /// Ensures a property exists with a default value if not already set
    /// </summary>
    private void EnsureProperty(string key, object defaultValue)
    {
        if (!Properties.ContainsKey(key))
        {
            Properties[key] = defaultValue;
        }
    }
}

public partial class Position : ObservableObject
{
    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private string _unit = "px"; // px or %
}

public partial class Size : ObservableObject
{
    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    [ObservableProperty]
    private string _unit = "px"; // px or %
}

public class Animation
{
    public string Type { get; set; } = "none"; // fade, slide, none
    public int Duration { get; set; } = 300; // milliseconds
    public string Easing { get; set; } = "ease-in-out";
}

/// <summary>
/// Text element properties
/// </summary>
public class TextElementProperties
{
    public string Content { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Arial";
    public int FontSize { get; set; } = 16;
    public string FontWeight { get; set; } = "normal"; // normal, bold
    public string FontStyle { get; set; } = "normal"; // normal, italic
    public string Color { get; set; } = "#000000";
    public string TextAlign { get; set; } = "left"; // left, center, right
    public string VerticalAlign { get; set; } = "top"; // top, middle, bottom
    public bool WordWrap { get; set; } = true;
}

/// <summary>
/// Image element properties
/// </summary>
public class ImageElementProperties
{
    public string Source { get; set; } = string.Empty; // URL or base64
    public string Fit { get; set; } = "contain"; // contain, cover, fill, none
    public string? AltText { get; set; }
}

/// <summary>
/// Shape element properties
/// </summary>
public class ShapeElementProperties
{
    public string ShapeType { get; set; } = "rectangle"; // rectangle, circle, line
    public string FillColor { get; set; } = "#CCCCCC";
    public string StrokeColor { get; set; } = "#000000";
    public int StrokeWidth { get; set; } = 1;
    public int CornerRadius { get; set; } = 0;
}

/// <summary>
/// QR Code element properties
/// </summary>
public class QRCodeElementProperties
{
    public string Data { get; set; } = string.Empty;
    public string ErrorCorrection { get; set; } = "M"; // L, M, Q, H
    public string ForegroundColor { get; set; } = "#000000";
    public string BackgroundColor { get; set; } = "#FFFFFF";
}

/// <summary>
/// Table element properties
/// </summary>
public class TableElementProperties
{
    public List<string> Columns { get; set; } = new();
    public string HeaderBackground { get; set; } = "#EEEEEE";
    public string RowBackground { get; set; } = "#FFFFFF";
    public string AlternateRowBackground { get; set; } = "#F9F9F9";
    public string BorderColor { get; set; } = "#CCCCCC";
    public int BorderWidth { get; set; } = 1;
}
