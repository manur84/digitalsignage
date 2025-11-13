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

    /// <summary>
    /// Initializes default properties for all element types
    /// This prevents KeyNotFoundException when binding in XAML
    /// </summary>
    public void InitializeDefaultProperties()
    {
        // Common properties for all elements
        EnsureProperty("Rotation", 0.0);
        EnsureProperty("IsVisible", true);
        EnsureProperty("IsLocked", false);

        // Initialize type-specific properties based on element type
        switch (Type.ToLower())
        {
            case "text":
                EnsureProperty("Content", string.Empty);
                EnsureProperty("FontFamily", "Arial");
                EnsureProperty("FontSize", 24.0);
                EnsureProperty("FontWeight", "Normal");
                EnsureProperty("FontStyle", "Normal");
                EnsureProperty("Color", "#000000");
                EnsureProperty("TextAlign", "Left");
                EnsureProperty("VerticalAlign", "Top");
                EnsureProperty("WordWrap", true);
                break;

            case "image":
                EnsureProperty("Source", string.Empty);
                EnsureProperty("Stretch", "Uniform");
                EnsureProperty("AltText", string.Empty);
                break;

            case "rectangle":
            case "shape":
            case "circle":
                EnsureProperty("FillColor", "#FFFFFF");
                EnsureProperty("BorderColor", "#000000");
                EnsureProperty("BorderThickness", 1.0);
                EnsureProperty("CornerRadius", 0.0);
                break;

            case "qrcode":
                EnsureProperty("Data", string.Empty);
                EnsureProperty("ErrorCorrection", "M");
                EnsureProperty("ForegroundColor", "#000000");
                EnsureProperty("BackgroundColor", "#FFFFFF");
                break;

            case "table":
                EnsureProperty("HeaderBackground", "#EEEEEE");
                EnsureProperty("RowBackground", "#FFFFFF");
                EnsureProperty("AlternateRowBackground", "#F9F9F9");
                EnsureProperty("BorderColor", "#CCCCCC");
                EnsureProperty("BorderWidth", 1.0);
                break;

            case "datetime":
                EnsureProperty("Format", "yyyy-MM-dd HH:mm:ss");
                EnsureProperty("FontFamily", "Arial");
                EnsureProperty("FontSize", 24.0);
                EnsureProperty("Color", "#000000");
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
