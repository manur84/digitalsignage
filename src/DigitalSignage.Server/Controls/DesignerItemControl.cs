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

        // CRITICAL: Set alignment and minimum size to ensure control is visible
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        MinWidth = 10;
        MinHeight = 10;

        // Make control transparent by default so content is visible
        Background = Brushes.Transparent;

        // Canvas position (Canvas.Left/Top/ZIndex) is now handled by XAML data binding
        // in MainWindow.xaml ItemContainerStyle. This is the correct WPF approach.
        System.Diagnostics.Debug.WriteLine("DesignerItemControl: Constructor called");

        SizeChanged += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine(
                $"DesignerItemControl.SizeChanged: Element='{DisplayElement?.Name}', " +
                $"NewSize={e.NewSize.Width}x{e.NewSize.Height}, " +
                $"ActualWidth={ActualWidth}, ActualHeight={ActualHeight}");
        };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        System.Diagnostics.Debug.WriteLine(
            $"DesignerItemControl.OnApplyTemplate: " +
            $"Element='{DisplayElement?.Name}', " +
            $"Width={Width}, Height={Height}, " +
            $"ActualWidth={ActualWidth}, ActualHeight={ActualHeight}");
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
        // Position updates are handled automatically by XAML binding in ItemContainerStyle
        // Canvas.Left and Canvas.Top are bound to Position.X and Position.Y
        // No manual updates needed - PropertyChanged notifications trigger binding updates
    }

    private void OnSizeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DisplayElement != null)
        {
            // Check if already on UI thread to avoid unnecessary context switch
            if (Dispatcher.CheckAccess())
            {
                Width = DisplayElement.Size.Width;
                Height = DisplayElement.Size.Height;
            }
            else
            {
                Dispatcher.InvokeAsync(() =>
                {
                    Width = DisplayElement.Size.Width;
                    Height = DisplayElement.Size.Height;
                });
            }
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

        // CRITICAL: Set Width and Height BEFORE creating content
        Width = DisplayElement.Size.Width;
        Height = DisplayElement.Size.Height;

        // Position and ZIndex are handled by XAML data binding in MainWindow.xaml ItemContainerStyle

        // Apply visual effects (rotation, shadow, opacity)
        ApplyVisualEffects();

        // Render content based on element type
        Content = CreateContentForElement();

        // Force immediate layout update
        UpdateLayout();

        System.Diagnostics.Debug.WriteLine($"DesignerItemControl: Element '{DisplayElement.Name}' updated successfully. " +
            $"Width={Width}, Height={Height}, " +
            $"ActualWidth={ActualWidth}, ActualHeight={ActualHeight}, " +
            $"IsVisible={IsVisible}, Content={Content?.GetType().Name}");
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
        // Wrap TextBlock in a Border for proper sizing and background support
        var border = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent
        };

        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(5), // Add some padding
            TextTrimming = TextTrimming.None // Ensure text is not trimmed
        };

        if (DisplayElement?.Properties != null)
        {
            if (DisplayElement.Properties.TryGetValue("Content", out var content))
                textBlock.Text = content?.ToString() ?? "Text";

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

            // Apply font weight (Bold)
            if (DisplayElement.Properties.TryGetValue("FontWeight", out var fontWeight))
            {
                var weightStr = fontWeight?.ToString() ?? "Normal";
                textBlock.FontWeight = weightStr.Equals("Bold", StringComparison.OrdinalIgnoreCase)
                    ? FontWeights.Bold
                    : FontWeights.Normal;
            }

            // Apply font style (Italic)
            if (DisplayElement.Properties.TryGetValue("FontStyle", out var fontStyle))
            {
                var styleStr = fontStyle?.ToString() ?? "Normal";
                textBlock.FontStyle = styleStr.Equals("Italic", StringComparison.OrdinalIgnoreCase)
                    ? FontStyles.Italic
                    : FontStyles.Normal;
            }

            // Apply line height
            if (DisplayElement.Properties.TryGetValue("LineHeight", out var lineHeight))
            {
                var lineHeightValue = TryParseDouble(lineHeight, 1.2);
                if (lineHeightValue > 0)
                {
                    textBlock.LineHeight = textBlock.FontSize * lineHeightValue;
                }
            }

            // Apply letter spacing (WPF uses Typography.LetterSpacing in em units)
            if (DisplayElement.Properties.TryGetValue("LetterSpacing", out var letterSpacing))
            {
                var letterSpacingValue = TryParseDouble(letterSpacing, 0.0);
                // Convert pixels to em units (em = pixels / fontSize * 1000)
                if (textBlock.FontSize > 0)
                {
                    textBlock.FontStretch = System.Windows.FontStretches.Normal;
                    // Note: WPF doesn't have direct letter-spacing, but we can use a workaround with FormattedText
                    // For now, we'll document this limitation
                }
            }

            // Apply text decorations
            if (DisplayElement.Properties.TryGetValue("TextDecoration", out var textDecoration))
            {
                var decoration = textDecoration?.ToString()?.ToLower();
                if (decoration == "underline" || (DisplayElement["Underline"] as bool? == true))
                {
                    textBlock.TextDecorations = TextDecorations.Underline;
                }
                else if (decoration == "strikethrough" || (DisplayElement["Strikethrough"] as bool? == true))
                {
                    textBlock.TextDecorations = TextDecorations.Strikethrough;
                }
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
        var border = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent
        };

        // Try to load real image from properties
        if (DisplayElement?.Properties != null)
        {
            // Check for ImageSource, MediaId, or Source property
            string? imagePath = null;

            if (DisplayElement.Properties.TryGetValue("ImageSource", out var imgSource))
                imagePath = imgSource?.ToString();
            else if (DisplayElement.Properties.TryGetValue("MediaId", out var mediaId))
                imagePath = mediaId?.ToString();
            else if (DisplayElement.Properties.TryGetValue("Source", out var source))
                imagePath = source?.ToString();

            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    // Try to load image from file path or media directory
                    var image = new Image
                    {
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };

                    // Get Stretch mode from properties
                    if (DisplayElement.Properties.TryGetValue("Stretch", out var stretchMode))
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
        var rectangle = new System.Windows.Shapes.Rectangle
        {
            Fill = Brushes.LightBlue,
            Stroke = Brushes.DarkBlue,
            StrokeThickness = 2,
            // CRITICAL: Make rectangle stretch to fill the control
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Stretch = Stretch.Fill
        };

        if (DisplayElement?.Properties != null)
        {
            if (DisplayElement.Properties.TryGetValue("FillColor", out var fillColor))
            {
                try
                {
                    rectangle.Fill = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(fillColor?.ToString() ?? "#ADD8E6"));
                }
                catch
                {
                    rectangle.Fill = Brushes.LightBlue;
                }
            }

            if (DisplayElement.Properties.TryGetValue("BorderColor", out var borderColor))
            {
                try
                {
                    rectangle.Stroke = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(borderColor?.ToString() ?? "#00008B"));
                }
                catch
                {
                    rectangle.Stroke = Brushes.DarkBlue;
                }
            }

            if (DisplayElement.Properties.TryGetValue("BorderThickness", out var borderThickness))
            {
                rectangle.StrokeThickness = TryParseDouble(borderThickness, 2.0);
            }

            if (DisplayElement.Properties.TryGetValue("CornerRadius", out var cornerRadius))
            {
                var radius = TryParseDouble(cornerRadius, 0.0);
                rectangle.RadiusX = radius;
                rectangle.RadiusY = radius;
            }
        }

        return rectangle;
    }

    private UIElement CreateCircleElement()
    {
        var ellipse = new System.Windows.Shapes.Ellipse
        {
            Fill = Brushes.Gold,
            Stroke = Brushes.Orange,
            StrokeThickness = 2,
            // CRITICAL: Make ellipse stretch to fill the control
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Stretch = Stretch.Fill
        };

        if (DisplayElement?.Properties != null)
        {
            if (DisplayElement.Properties.TryGetValue("FillColor", out var fillColor))
            {
                try
                {
                    ellipse.Fill = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(fillColor?.ToString() ?? "#FFD700"));
                }
                catch
                {
                    ellipse.Fill = Brushes.Gold;
                }
            }

            if (DisplayElement.Properties.TryGetValue("BorderColor", out var borderColor))
            {
                try
                {
                    ellipse.Stroke = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(borderColor?.ToString() ?? "#FFA500"));
                }
                catch
                {
                    ellipse.Stroke = Brushes.Orange;
                }
            }

            if (DisplayElement.Properties.TryGetValue("BorderThickness", out var borderThickness))
            {
                ellipse.StrokeThickness = TryParseDouble(borderThickness, 2.0);
            }
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
