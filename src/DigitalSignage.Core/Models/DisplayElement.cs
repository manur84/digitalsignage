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
