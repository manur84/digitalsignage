using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// Control for displaying and manipulating a design element on the canvas
/// Rendering only - drag/drop handled by MainWindow.xaml.cs
/// </summary>
public class DesignerItemControl : ContentControl
{
    // REMOVED: Drag-related fields - dragging now handled by MainWindow.xaml.cs
    // private Point _dragStartPosition;
    // private bool _isDragging;
    // private AlignmentGuidesAdorner? _alignmentAdorner;

    public static readonly DependencyProperty DisplayElementProperty =
        DependencyProperty.Register(
            nameof(DisplayElement),
            typeof(DisplayElement),
            typeof(DesignerItemControl),
            new PropertyMetadata(null, OnDisplayElementChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(DesignerItemControl),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public DisplayElement? DisplayElement
    {
        get => (DisplayElement?)GetValue(DisplayElementProperty);
        set => SetValue(DisplayElementProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    static DesignerItemControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DesignerItemControl),
            new FrameworkPropertyMetadata(typeof(DesignerItemControl)));
    }

    public DesignerItemControl()
    {
        Focusable = true;
        // REMOVED: Mouse event handlers - these are now handled by MainWindow.xaml.cs
        // to avoid conflicts and enable proper multi-element dragging
        // MouseLeftButtonDown += OnMouseLeftButtonDown;
        // MouseMove += OnMouseMove;
        // MouseLeftButtonUp += OnMouseLeftButtonUp;

        // CRITICAL: The control itself must stretch to fill its container
        // The container's size is bound to Size.Width/Height by DesignerItemsControl.PrepareContainerForItemOverride
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        // Content alignment must also stretch to ensure child elements fill the control
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        MinWidth = 10;
        MinHeight = 10;

        // FIXED: Removed debug background/border - elements now render with proper appearance
        Background = Brushes.Transparent;
        BorderBrush = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Opacity = 1.0;

        // Canvas position (Canvas.Left/Top/ZIndex) is now handled by XAML data binding
        // in MainWindow.xaml ItemContainerStyle. This is the correct WPF approach.
        System.Diagnostics.Debug.WriteLine("DesignerItemControl: Constructor called");

        SizeChanged += (s, e) =>
        {
            var canvasLeft = Canvas.GetLeft(this);
            var canvasTop = Canvas.GetTop(this);
            var zIndex = Panel.GetZIndex(this);

            System.Diagnostics.Debug.WriteLine(
                $"DesignerItemControl.SizeChanged: Element='{DisplayElement?.Name}', " +
                $"NewSize={e.NewSize.Width}x{e.NewSize.Height}, " +
                $"ActualWidth={ActualWidth}, ActualHeight={ActualHeight}, " +
                $"Canvas.Left={canvasLeft}, Canvas.Top={canvasTop}, " +
                $"Panel.ZIndex={zIndex}, " +
                $"IsVisible={IsVisible}, Visibility={Visibility}");
        };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Get Canvas attached properties for debugging
        var canvasLeft = Canvas.GetLeft(this);
        var canvasTop = Canvas.GetTop(this);
        var zIndex = Panel.GetZIndex(this);

        System.Diagnostics.Debug.WriteLine(
            $"DesignerItemControl.OnApplyTemplate: " +
            $"Element='{DisplayElement?.Name}', " +
            $"Width={Width}, Height={Height}, " +
            $"ActualWidth={ActualWidth}, ActualHeight={ActualHeight}, " +
            $"Canvas.Left={canvasLeft}, Canvas.Top={canvasTop}, " +
            $"Panel.ZIndex={zIndex}, " +
            $"HorizontalAlignment={HorizontalAlignment}, VerticalAlignment={VerticalAlignment}");
    }

    #region Safe Parsing Helper Methods

    /// <summary>
    /// Safely parses a string/object to double with fallback to default value
    /// Prevents FormatException when property values are empty strings, null, or invalid
    /// </summary>
    private static double TryParseDouble(object? value, double defaultValue)
    {
        if (value == null)
            return defaultValue;

        // If already a double, return it directly
        if (value is double d)
            return d;

        // If it's an int or other numeric type, convert safely
        if (value is int i)
            return i;
        if (value is float f)
            return f;
        if (value is decimal dec)
            return (double)dec;

        // Try parsing string representation
        var str = value.ToString();
        if (string.IsNullOrWhiteSpace(str))
            return defaultValue;

        if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return defaultValue;
    }

    /// <summary>
    /// Safely parses a string/object to int with fallback to default value
    /// </summary>
    private static int TryParseInt(object? value, int defaultValue)
    {
        if (value == null)
            return defaultValue;

        // If already an int, return it directly
        if (value is int i)
            return i;

        // If it's a double, convert safely
        if (value is double d)
            return (int)Math.Round(d);
        if (value is float f)
            return (int)Math.Round(f);

        // Try parsing string representation
        var str = value.ToString();
        if (string.IsNullOrWhiteSpace(str))
            return defaultValue;

        if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        // Try parsing as double first, then convert to int
        if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleResult))
            return (int)Math.Round(doubleResult);

        return defaultValue;
    }

    /// <summary>
    /// Safely parses a hex color string to Color with fallback to default
    /// </summary>
    private static Color TryParseColor(object? value, Color defaultColor)
    {
        if (value == null)
            return defaultColor;

        var str = value.ToString();
        if (string.IsNullOrWhiteSpace(str))
            return defaultColor;

        try
        {
            return (Color)ColorConverter.ConvertFromString(str);
        }
        catch
        {
            return defaultColor;
        }
    }

    /// <summary>
    /// Safely gets a property value from the Properties dictionary
    /// Returns null if key doesn't exist or element is null
    /// </summary>
    private string? GetProperty(string key)
    {
        if (DisplayElement?.Properties == null)
            return null;

        if (!DisplayElement.Properties.ContainsKey(key))
            return null;

        var value = DisplayElement.Properties[key];
        return value?.ToString();
    }

    #endregion

    private static void OnDisplayElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DesignerItemControl control)
        {
            // Unsubscribe from old element
            if (e.OldValue is DisplayElement oldElement)
            {
                oldElement.PropertyChanged -= control.OnElementPropertyChanged;
                if (oldElement.Position != null)
                    oldElement.Position.PropertyChanged -= control.OnPositionChanged;
                if (oldElement.Size != null)
                    oldElement.Size.PropertyChanged -= control.OnSizeChanged;
            }

            // Subscribe to new element
            if (e.NewValue is DisplayElement newElement)
            {
                newElement.PropertyChanged += control.OnElementPropertyChanged;
                if (newElement.Position != null)
                    newElement.Position.PropertyChanged += control.OnPositionChanged;
                if (newElement.Size != null)
                    newElement.Size.PropertyChanged += control.OnSizeChanged;

                control.UpdateFromElement();
            }
        }
    }

    private void OnElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Handle ALL property changes to ensure immediate visual updates
        // This includes Properties dictionary changes (Color, FontSize, etc.)
        if (e.PropertyName == nameof(DisplayElement.Position) ||
            e.PropertyName == nameof(DisplayElement.Size) ||
            e.PropertyName == nameof(DisplayElement.ZIndex) ||
            e.PropertyName == nameof(DisplayElement.Rotation) ||
            e.PropertyName == nameof(DisplayElement.Opacity) ||
            e.PropertyName == nameof(DisplayElement.Visible) ||
            e.PropertyName == nameof(DisplayElement.Properties) ||
            e.PropertyName == "Item[]") // Indexer property change (e.g., element["Color"] = value)
        {
            // Check if already on UI thread to avoid unnecessary context switch
            if (Dispatcher.CheckAccess())
            {
                UpdateFromElement();
            }
            else
            {
                Dispatcher.InvokeAsync(() => UpdateFromElement());
            }
        }
    }

    private void OnPositionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateCanvasPosition();
    }

    private void OnSizeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // CRITICAL FIX: DO NOT set Width/Height manually!
        // Size changes are handled automatically by XAML binding in ItemContainerStyle
        // Width and Height properties are bound to Size.Width and Size.Height
        // PropertyChanged notifications automatically trigger binding updates
        // Manual updates would conflict with binding and cause size explosion

        // REMOVED: Width = DisplayElement.Size.Width;
        // REMOVED: Height = DisplayElement.Size.Height;

        // Just log for debugging
        if (DisplayElement != null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"DesignerItemControl.OnSizeChanged: Element '{DisplayElement.Name}' size changed to " +
                $"{DisplayElement.Size.Width}x{DisplayElement.Size.Height}");

            Width = DisplayElement.Size.Width;
            Height = DisplayElement.Size.Height;
        }
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DesignerItemControl control)
        {
            control.UpdateSelectionVisual();
        }
    }

    private void UpdateFromElement()
    {
        if (DisplayElement == null)
        {
            System.Diagnostics.Debug.WriteLine("DesignerItemControl: UpdateFromElement called but DisplayElement is null");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"DesignerItemControl: Updating element '{DisplayElement.Name}' " +
            $"at ({DisplayElement.Position.X}, {DisplayElement.Position.Y}) " +
            $"size {DisplayElement.Size.Width}x{DisplayElement.Size.Height} " +
            $"ZIndex={DisplayElement.ZIndex}");

        // Ensure width/height are explicitly set so template doesn't inherit NaN
        if (DisplayElement.Size != null)
        {
            Width = DisplayElement.Size.Width;
            Height = DisplayElement.Size.Height;
        }

        UpdateCanvasPosition();

        // Apply visual effects (rotation, shadow, opacity)
        ApplyVisualEffects();

        // Render content based on element type
        Content = CreateContentForElement();

        // Force immediate layout update
        UpdateLayout();

        System.Diagnostics.Debug.WriteLine($"DesignerItemControl: Element '{DisplayElement.Name}' updated successfully. " +
            $"Width={Width}, Height={Height}, " +
            $"ActualWidth={ActualWidth}, ActualHeight={ActualHeight}, " +
            $"IsVisible={IsVisible}, Visibility={Visibility}, Opacity={Opacity}, " +
            $"Background={Background}, BorderBrush={BorderBrush}, BorderThickness={BorderThickness}, " +
            $"Content={Content?.GetType().Name}");
    }

    private void UpdateCanvasPosition()
    {
        if (DisplayElement?.Position == null)
            return;

        Canvas.SetLeft(this, DisplayElement.Position.X);
        Canvas.SetTop(this, DisplayElement.Position.Y);
        Panel.SetZIndex(this, DisplayElement.ZIndex);
    }

    private UIElement CreateContentForElement()
    {
        if (DisplayElement == null) return new TextBlock { Text = "Empty" };

        return DisplayElement.Type.ToLowerInvariant() switch
        {
            "text" => CreateTextElement(),
            "image" => CreateImageElement(),
            "shape" => CreateShapeElement(),
            "rectangle" => CreateRectangleElement(),
            "circle" => CreateCircleElement(),
            "ellipse" => CreateCircleElement(),
            "datetime" => CreateDateTimeElement(),
            "table" => CreateTableElement(),
            "qrcode" => CreateQRCodeElement(),
            "group" => CreateGroupElement(),
            _ => new TextBlock { Text = $"Unsupported: {DisplayElement.Type}" }
        };
    }

    private UIElement CreateTextElement()
    {
        // Match Pi Client rendering exactly
        // Pi Client: display_renderer.py:create_text_element() lines 487-596

        // Wrap TextBlock in a Border for proper sizing and background support
        var border = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent
        };

        var textBlock = new TextBlock
        {
            Margin = new Thickness(5), // Match Pi padding
            TextTrimming = TextTrimming.None
        };

        if (DisplayElement?.Properties != null)
        {
            // MATCH PI: Get text content - default empty string (Pi line 500)
            if (DisplayElement.Properties.TryGetValue("Content", out var content))
                textBlock.Text = content?.ToString() ?? "";

            // MATCH PI: FontSize - default 16 (Pi line 517)
            if (DisplayElement.Properties.TryGetValue("FontSize", out var fontSize))
                textBlock.FontSize = TryParseDouble(fontSize, 16.0); // Pi default is 16, not 24

            // MATCH PI: FontFamily - default Arial (Pi line 516)
            if (DisplayElement.Properties.TryGetValue("FontFamily", out var fontFamily))
                textBlock.FontFamily = new FontFamily(fontFamily?.ToString() ?? "Arial");

            // MATCH PI: Color - default #000000 (Pi line 555)
            if (DisplayElement.Properties.TryGetValue("Color", out var color))
            {
                try
                {
                    textBlock.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(color?.ToString() ?? "#000000"));
                }
                catch
                {
                    textBlock.Foreground = Brushes.Black;
                }
            }

            // MATCH PI: FontWeight checking 'bold' string (Pi line 532-534)
            if (DisplayElement.Properties.TryGetValue("FontWeight", out var fontWeight))
            {
                var weightStr = fontWeight?.ToString()?.ToLower() ?? "normal";
                textBlock.FontWeight = weightStr == "bold" ? FontWeights.Bold : FontWeights.Normal;
            }

            // MATCH PI: FontStyle checking 'italic' string (Pi line 536-538)
            if (DisplayElement.Properties.TryGetValue("FontStyle", out var fontStyle))
            {
                var styleStr = fontStyle?.ToString()?.ToLower() ?? "normal";
                textBlock.FontStyle = styleStr == "italic" ? FontStyles.Italic : FontStyles.Normal;
            }

            // MATCH PI: Text decorations - check both individual properties (Pi lines 541-548)
            var underline = false;
            var strikethrough = false;

            // Check TextDecoration_Underline property (Pi way)
            if (DisplayElement.Properties.TryGetValue("TextDecoration_Underline", out var underlineProp))
            {
                underline = underlineProp as bool? == true ||
                           underlineProp?.ToString()?.ToLower() == "true";
            }

            // Check TextDecoration_Strikethrough property (Pi way)
            if (DisplayElement.Properties.TryGetValue("TextDecoration_Strikethrough", out var strikethroughProp))
            {
                strikethrough = strikethroughProp as bool? == true ||
                               strikethroughProp?.ToString()?.ToLower() == "true";
            }

            if (underline)
                textBlock.TextDecorations = TextDecorations.Underline;
            else if (strikethrough)
                textBlock.TextDecorations = TextDecorations.Strikethrough;

            // MATCH PI: Text alignment (Pi lines 561-577)
            if (DisplayElement.Properties.TryGetValue("TextAlign", out var textAlign))
            {
                var align = textAlign?.ToString()?.ToLower() ?? "left";
                textBlock.TextAlignment = align switch
                {
                    "center" => TextAlignment.Center,
                    "right" => TextAlignment.Right,
                    "justify" => TextAlignment.Justify,
                    _ => TextAlignment.Left
                };
            }

            // MATCH PI: Vertical alignment (Pi lines 569-576)
            if (DisplayElement.Properties.TryGetValue("VerticalAlign", out var verticalAlign))
            {
                var valign = verticalAlign?.ToString()?.ToLower() ?? "top";
                textBlock.VerticalAlignment = valign switch
                {
                    "middle" or "center" => VerticalAlignment.Center,
                    "bottom" => VerticalAlignment.Bottom,
                    _ => VerticalAlignment.Top
                };
            }
            else
            {
                textBlock.VerticalAlignment = VerticalAlignment.Top; // Pi default
            }

            // MATCH PI: Word wrap - default true (Pi line 583-584)
            if (DisplayElement.Properties.TryGetValue("WordWrap", out var wordWrap))
            {
                var wrap = wordWrap as bool? ?? true;
                textBlock.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            }
            else
            {
                textBlock.TextWrapping = TextWrapping.Wrap; // Pi default is true
            }

            // Apply border properties if present
            if (DisplayElement.Properties.TryGetValue("FillColor", out var fillColor))
            {
                try
                {
                    border.Background = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(fillColor?.ToString() ?? "#FFFFFF"));
                }
                catch
                {
                    border.Background = Brushes.White;
                }
            }

            if (DisplayElement.Properties.TryGetValue("BorderColor", out var borderColor) &&
                DisplayElement.Properties.TryGetValue("BorderThickness", out var borderThickness))
            {
                var thickness = TryParseDouble(borderThickness, 0.0);
                if (thickness > 0)
                {
                    try
                    {
                        border.BorderBrush = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(borderColor?.ToString() ?? "#000000"));
                        border.BorderThickness = new Thickness(thickness);
                    }
                    catch
                    {
                        border.BorderBrush = Brushes.Black;
                    }
                }
            }

            if (DisplayElement.Properties.TryGetValue("BorderRadius", out var borderRadius))
            {
                border.CornerRadius = new CornerRadius(TryParseDouble(borderRadius, 0.0));
            }
        }

        border.Child = textBlock;
        return border;
    }

    private UIElement CreateImageElement()
    {
        // Match Pi Client rendering exactly
        // Pi Client: display_renderer.py:create_image_element() lines 597-677

        var border = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent
        };

        // Try to load real image from properties
        if (DisplayElement?.Properties != null)
        {
            // MATCH PI: Priority 1 - MediaData (Base64 from server) - Pi line 610-627
            string? imagePath = null;
            bool isBase64 = false;

            if (DisplayElement.Properties.TryGetValue("MediaData", out var mediaData))
            {
                // Base64 image data has highest priority like Pi
                imagePath = mediaData?.ToString();
                isBase64 = true;
            }
            // MATCH PI: Priority 2 - Source property (file path) - Pi line 630
            else if (DisplayElement.Properties.TryGetValue("Source", out var source))
            {
                imagePath = source?.ToString();
            }
            // Additional fallbacks for WPF compatibility
            else if (DisplayElement.Properties.TryGetValue("ImageSource", out var imgSource))
            {
                imagePath = imgSource?.ToString();
            }
            else if (DisplayElement.Properties.TryGetValue("MediaId", out var mediaId))
            {
                imagePath = mediaId?.ToString();
            }

            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    var image = new Image
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };

                    // MATCH PI: Fit property mapping (Pi line 651-661)
                    // Pi uses 'Fit' property with values: contain, cover, fill
                    // WPF uses Stretch enum
                    if (DisplayElement.Properties.TryGetValue("Fit", out var fitMode))
                    {
                        var fit = fitMode?.ToString()?.ToLower() ?? "contain";
                        image.Stretch = fit switch
                        {
                            "contain" => Stretch.Uniform,        // Qt.KeepAspectRatio
                            "cover" => Stretch.UniformToFill,    // Qt.KeepAspectRatioByExpanding
                            "fill" => Stretch.Fill,              // Qt.IgnoreAspectRatio
                            _ => Stretch.Uniform                 // Default to contain
                        };
                    }
                    // Fallback to Stretch property if Fit not found
                    else if (DisplayElement.Properties.TryGetValue("Stretch", out var stretchMode))
                    {
                        image.Stretch = stretchMode?.ToString()?.ToLower() switch
                        {
                            "none" => Stretch.None,
                            "fill" => Stretch.Fill,
                            "uniform" => Stretch.Uniform,
                            "uniformtofill" => Stretch.UniformToFill,
                            _ => Stretch.Uniform
                        };
                    }
                    else
                    {
                        // MATCH PI: Default to contain/Uniform (Pi line 660)
                        image.Stretch = Stretch.Uniform;
                    }

                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;

                    // Check if path is absolute or relative
                    if (File.Exists(imagePath))
                    {
                        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                    }
                    else
                    {
                        // Try in media directory
                        var mediaPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "DigitalSignage",
                            "Media",
                            imagePath);

                        if (File.Exists(mediaPath))
                        {
                            bitmap.UriSource = new Uri(mediaPath, UriKind.Absolute);
                        }
                        else
                        {
                            // Path doesn't exist, show placeholder
                            bitmap = null;
                        }
                    }

                    if (bitmap != null)
                    {
                        bitmap.EndInit();
                        image.Source = bitmap;

                        // Apply opacity if specified
                        if (DisplayElement.Properties.TryGetValue("ImageOpacity", out var opacity))
                        {
                            image.Opacity = TryParseDouble(opacity, 1.0);
                        }

                        border.Child = image;
                        return border;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load image from {imagePath}: {ex.Message}");
                    // Fall through to placeholder
                }
            }
        }

        // Fallback: Show placeholder
        var stackPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        stackPanel.Children.Add(new TextBlock
        {
            Text = "🖼",
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        stackPanel.Children.Add(new TextBlock
        {
            Text = "No Image",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = Brushes.Gray
        });

        border.Child = stackPanel;
        return border;
    }

    private UIElement CreateShapeElement()
    {
        return CreateRectangleElement();
    }

    private UIElement CreateRectangleElement()
    {
        // Match Pi Client rendering exactly
        // Pi Client: display_renderer.py:create_shape_element() lines 679-750

        var rectangle = new System.Windows.Shapes.Rectangle
        {
            // MATCH PI: Default colors (Pi lines 694-696, 699-704, 707-712)
            Fill = new SolidColorBrush(Color.FromRgb(204, 204, 204)),  // Pi default: #CCCCCC
            Stroke = new SolidColorBrush(Colors.Black),     // Pi default: #000000
            StrokeThickness = 1,  // Pi default: 1
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Stretch = Stretch.Fill
        };

        if (DisplayElement?.Properties != null)
        {
            // MATCH PI: FillColor with fallback to #CCCCCC (Pi lines 694-696)
            if (DisplayElement.Properties.TryGetValue("FillColor", out var fillColor))
            {
                var colorStr = fillColor?.ToString();
                if (!string.IsNullOrWhiteSpace(colorStr))
                {
                    try
                    {
                        rectangle.Fill = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(colorStr));
                    }
                    catch
                    {
                        rectangle.Fill = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // #CCCCCC
                    }
                }
            }

            // MATCH PI: Check BorderColor first, then StrokeColor as fallback (Pi lines 698-704)
            string? strokeColorStr = null;
            if (DisplayElement.Properties.TryGetValue("BorderColor", out var borderColor))
            {
                strokeColorStr = borderColor?.ToString();
            }
            else if (DisplayElement.Properties.TryGetValue("StrokeColor", out var strokeColor))
            {
                // Fallback to StrokeColor if BorderColor not found (Pi line 701-703)
                strokeColorStr = strokeColor?.ToString();
            }

            if (!string.IsNullOrWhiteSpace(strokeColorStr))
            {
                try
                {
                    rectangle.Stroke = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(strokeColorStr));
                }
                catch
                {
                    rectangle.Stroke = new SolidColorBrush(Colors.Black); // #000000
                }
            }

            // MATCH PI: Check BorderThickness first, then StrokeWidth as fallback (Pi lines 706-712)
            double strokeWidth = 1.0; // Pi default
            if (DisplayElement.Properties.TryGetValue("BorderThickness", out var borderThickness))
            {
                strokeWidth = TryParseDouble(borderThickness, 1.0);
            }
            else if (DisplayElement.Properties.TryGetValue("StrokeWidth", out var strokeWidthProp))
            {
                // Fallback to StrokeWidth if BorderThickness not found (Pi line 709-711)
                strokeWidth = TryParseDouble(strokeWidthProp, 1.0);
            }
            rectangle.StrokeThickness = strokeWidth;

            // MATCH PI: Check CornerRadius first, then BorderRadius as fallback (Pi lines 720-732)
            double radius = 0.0; // Pi default
            if (DisplayElement.Properties.TryGetValue("CornerRadius", out var cornerRadius))
            {
                radius = TryParseDouble(cornerRadius, 0.0);
            }
            else if (DisplayElement.Properties.TryGetValue("BorderRadius", out var borderRadius))
            {
                // Fallback to BorderRadius if CornerRadius not found (Pi line 723-725)
                radius = TryParseDouble(borderRadius, 0.0);
            }

            if (radius > 0)
            {
                rectangle.RadiusX = radius;
                rectangle.RadiusY = radius;
            }
        }

        return rectangle;
    }

    private UIElement CreateCircleElement()
    {
        // Match Pi Client rendering exactly
        // Pi Client uses same shape rendering for circle/ellipse
        // Pi Client: display_renderer.py:create_shape_element() with shape_type='circle'

        var ellipse = new System.Windows.Shapes.Ellipse
        {
            // MATCH PI: Same defaults as rectangle (Pi uses ShapeWidget with same defaults)
            Fill = new SolidColorBrush(Color.FromRgb(204, 204, 204)),  // Pi default: #CCCCCC
            Stroke = new SolidColorBrush(Colors.Black),     // Pi default: #000000
            StrokeThickness = 1,  // Pi default: 1
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Stretch = Stretch.Fill
        };

        if (DisplayElement?.Properties != null)
        {
            // MATCH PI: FillColor with fallback to #CCCCCC (same as rectangle)
            if (DisplayElement.Properties.TryGetValue("FillColor", out var fillColor))
            {
                var colorStr = fillColor?.ToString();
                if (!string.IsNullOrWhiteSpace(colorStr))
                {
                    try
                    {
                        ellipse.Fill = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(colorStr));
                    }
                    catch
                    {
                        ellipse.Fill = new SolidColorBrush(Color.FromRgb(204, 204, 204)); // #CCCCCC
                    }
                }
            }

            // MATCH PI: Check BorderColor first, then StrokeColor as fallback
            string? strokeColorStr = null;
            if (DisplayElement.Properties.TryGetValue("BorderColor", out var borderColor))
            {
                strokeColorStr = borderColor?.ToString();
            }
            else if (DisplayElement.Properties.TryGetValue("StrokeColor", out var strokeColor))
            {
                // Fallback to StrokeColor if BorderColor not found
                strokeColorStr = strokeColor?.ToString();
            }

            if (!string.IsNullOrWhiteSpace(strokeColorStr))
            {
                try
                {
                    ellipse.Stroke = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(strokeColorStr));
                }
                catch
                {
                    ellipse.Stroke = new SolidColorBrush(Colors.Black); // #000000
                }
            }

            // MATCH PI: Check BorderThickness first, then StrokeWidth as fallback
            double strokeWidth = 1.0; // Pi default
            if (DisplayElement.Properties.TryGetValue("BorderThickness", out var borderThickness))
            {
                strokeWidth = TryParseDouble(borderThickness, 1.0);
            }
            else if (DisplayElement.Properties.TryGetValue("StrokeWidth", out var strokeWidthProp))
            {
                // Fallback to StrokeWidth if BorderThickness not found
                strokeWidth = TryParseDouble(strokeWidthProp, 1.0);
            }
            ellipse.StrokeThickness = strokeWidth;
        }

        return ellipse;
    }

    private UIElement CreateDateTimeElement()
    {
        var border = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent
        };

        var textBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(5)
        };

        if (DisplayElement?.Properties != null)
        {
            // Get format string (default: "dddd, dd MMMM yyyy HH:mm:ss")
            var format = DisplayElement.Properties.TryGetValue("Format", out var formatObj)
                ? formatObj?.ToString() ?? "dddd, dd MMMM yyyy HH:mm:ss"
                : "dddd, dd MMMM yyyy HH:mm:ss";

            // Format current date/time
            try
            {
                textBlock.Text = DateTime.Now.ToString(format);
            }
            catch
            {
                textBlock.Text = DateTime.Now.ToString("G"); // Fallback to general date/time
            }

            // Apply font properties
            if (DisplayElement.Properties.TryGetValue("FontSize", out var fontSize))
                textBlock.FontSize = TryParseDouble(fontSize, 24.0);

            if (DisplayElement.Properties.TryGetValue("FontFamily", out var fontFamily))
                textBlock.FontFamily = new FontFamily(fontFamily?.ToString() ?? "Arial");

            if (DisplayElement.Properties.TryGetValue("Color", out var color))
            {
                try
                {
                    textBlock.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(color?.ToString() ?? "#000000"));
                }
                catch
                {
                    textBlock.Foreground = Brushes.Black;
                }
            }
        }
        else
        {
            textBlock.Text = DateTime.Now.ToString("G");
        }

        border.Child = textBlock;
        return border;
    }

    private UIElement CreateTableElement()
    {
        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Create simple preview with header and rows
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Header
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Row 1
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) }); // Row 2
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Rest

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Get colors from properties
        var headerBg = Brushes.CornflowerBlue;
        var headerFg = Brushes.White;
        var rowBg = Brushes.White;
        var altRowBg = Brushes.LightGray;

        if (DisplayElement?.Properties != null)
        {
            if (DisplayElement.Properties.TryGetValue("HeaderBackground", out var hBg))
            {
                try
                {
                    headerBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hBg?.ToString() ?? "#2196F3"));
                }
                catch (FormatException)
                {
                    // Invalid color format - use default blue
                    headerBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
                }
            }

            if (DisplayElement.Properties.TryGetValue("HeaderForeground", out var hFg))
            {
                try
                {
                    headerFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hFg?.ToString() ?? "#FFFFFF"));
                }
                catch (FormatException)
                {
                    // Invalid color format - use default white
                    headerFg = Brushes.White;
                }
            }

            if (DisplayElement.Properties.TryGetValue("RowBackground", out var rBg))
            {
                try
                {
                    rowBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(rBg?.ToString() ?? "#FFFFFF"));
                }
                catch (FormatException)
                {
                    // Invalid color format - use default white
                    rowBg = Brushes.White;
                }
            }

            if (DisplayElement.Properties.TryGetValue("AlternateRowBackground", out var arBg))
            {
                try
                {
                    altRowBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(arBg?.ToString() ?? "#F5F5F5"));
                }
                catch (FormatException)
                {
                    // Invalid color format - use default light gray
                    altRowBg = Brushes.LightGray;
                }
            }
        }

        // Header row
        for (int col = 0; col < 3; col++)
        {
            var headerCell = new Border
            {
                Background = headerBg,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Child = new TextBlock
                {
                    Text = $"Column {col + 1}",
                    Foreground = headerFg,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(4)
                }
            };
            Grid.SetRow(headerCell, 0);
            Grid.SetColumn(headerCell, col);
            grid.Children.Add(headerCell);
        }

        // Data rows
        for (int row = 1; row <= 2; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                var dataCell = new Border
                {
                    Background = row % 2 == 0 ? altRowBg : rowBg,
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Child = new TextBlock
                    {
                        Text = $"Data {row},{col + 1}",
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(4)
                    }
                };
                Grid.SetRow(dataCell, row);
                Grid.SetColumn(dataCell, col);
                grid.Children.Add(dataCell);
            }
        }

        border.Child = grid;
        return border;
    }

    private UIElement CreateQRCodeElement()
    {
        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var stackPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // QR Code placeholder (we would need a QR code library for actual rendering)
        var qrPlaceholder = new Border
        {
            Width = 100,
            Height = 100,
            Background = Brushes.White,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(2),
            Child = new TextBlock
            {
                Text = "QR",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        stackPanel.Children.Add(qrPlaceholder);

        // Show content below
        if (DisplayElement?.Properties != null)
        {
            var content = DisplayElement.Properties.TryGetValue("Content", out var contentObj)
                ? contentObj?.ToString()
                : DisplayElement.Properties.TryGetValue("Data", out var dataObj)
                    ? dataObj?.ToString()
                    : "No data";

            stackPanel.Children.Add(new TextBlock
            {
                Text = content,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 150
            });
        }

        border.Child = stackPanel;
        return border;
    }

    private UIElement CreateGroupElement()
    {
        // Create a canvas to hold child elements
        var groupCanvas = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent
        };

        // Add a visual indicator that this is a group
        var groupBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 100, 100, 255)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(20, 100, 100, 255)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Add group label
        var groupLabel = new TextBlock
        {
            Text = $"Group ({DisplayElement?.Children?.Count ?? 0} items)",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 255)),
            Margin = new Thickness(4, 2, 4, 2),
            Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
            Padding = new Thickness(4, 2, 4, 2)
        };

        groupCanvas.Children.Add(groupBorder);
        groupCanvas.Children.Add(groupLabel);

        // Render child elements (if needed for preview)
        if (DisplayElement?.Children != null)
        {
            foreach (var child in DisplayElement.Children)
            {
                // Create a simple visual representation of child elements
                var childPreview = new Border
                {
                    Width = child.Size.Width,
                    Height = child.Size.Height,
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                    CornerRadius = new CornerRadius(2)
                };

                Canvas.SetLeft(childPreview, child.Position.X);
                Canvas.SetTop(childPreview, child.Position.Y);

                groupCanvas.Children.Add(childPreview);
            }
        }

        return groupCanvas;
    }

    private void UpdateSelectionVisual()
    {
        if (IsSelected)
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
            BorderThickness = new Thickness(2);
        }
        else
        {
            BorderBrush = Brushes.Transparent;
            BorderThickness = new Thickness(0);
        }
    }

    /// <summary>
    /// Applies visual effects like rotation, shadow, and opacity to the element
    /// </summary>
    private void ApplyVisualEffects()
    {
        if (DisplayElement == null) return;

        // Apply rotation
        var rotation = DisplayElement.Rotation;
        if (rotation != 0)
        {
            this.RenderTransform = new RotateTransform(rotation);
            this.RenderTransformOrigin = new Point(0.5, 0.5);
        }
        else
        {
            this.RenderTransform = null;
        }

        // Apply opacity
        this.Opacity = DisplayElement.Opacity;

        // Apply shadow if enabled
        if (DisplayElement["EnableShadow"] as bool? == true)
        {
            try
            {
                var shadowColor = ColorFromHex(DisplayElement["ShadowColor"] as string ?? "#000000");
                var shadowBlur = DisplayElement["ShadowBlur"] as double? ?? 5.0;
                var shadowOffsetX = DisplayElement["ShadowOffsetX"] as double? ?? 2.0;
                var shadowOffsetY = DisplayElement["ShadowOffsetY"] as double? ?? 2.0;

                var effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = shadowColor,
                    BlurRadius = shadowBlur,
                    ShadowDepth = Math.Sqrt(shadowOffsetX * shadowOffsetX + shadowOffsetY * shadowOffsetY),
                    Direction = Math.Atan2(shadowOffsetY, shadowOffsetX) * (180.0 / Math.PI),
                    Opacity = 0.7
                };
                this.Effect = effect;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply shadow effect: {ex.Message}");
                this.Effect = null;
            }
        }
        else
        {
            this.Effect = null;
        }

        // Apply scale transformations (for flip effects)
        var scaleX = DisplayElement["ScaleX"] as double? ?? 1.0;
        var scaleY = DisplayElement["ScaleY"] as double? ?? 1.0;

        if (scaleX != 1.0 || scaleY != 1.0 || rotation != 0)
        {
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(scaleX, scaleY));
            transformGroup.Children.Add(new RotateTransform(rotation));
            this.RenderTransform = transformGroup;
            this.RenderTransformOrigin = new Point(0.5, 0.5);
        }
    }

    /// <summary>
    /// Converts a hex color string to a Color object
    /// </summary>
    private Color ColorFromHex(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Black;
        }
    }

    // REMOVED: Mouse event handlers - now handled by MainWindow.xaml.cs
    // This eliminates conflicts and enables proper multi-element dragging
    // The old mouse handlers are no longer needed
}
