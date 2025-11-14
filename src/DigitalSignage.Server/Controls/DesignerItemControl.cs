using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// Control for displaying and manipulating a design element on the canvas
/// </summary>
public class DesignerItemControl : ContentControl
{
    private Point _dragStartPosition;
    private bool _isDragging;
    private AlignmentGuidesAdorner? _alignmentAdorner;

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
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;

        // CRITICAL: Set alignment and minimum size to ensure control is visible
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        MinWidth = 10;
        MinHeight = 10;

        // Make control transparent by default so content is visible
        Background = Brushes.Transparent;

        System.Diagnostics.Debug.WriteLine("DesignerItemControl: Constructor called");
        Loaded += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"DesignerItemControl: Loaded event fired for element '{DisplayElement?.Name ?? "null"}'");
        };

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
        if (e.PropertyName == nameof(DisplayElement.Position) ||
            e.PropertyName == nameof(DisplayElement.Size) ||
            e.PropertyName == nameof(DisplayElement.ZIndex))
        {
            Dispatcher.Invoke(() => UpdateFromElement());
        }
    }

    private void OnPositionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DisplayElement != null)
        {
            Dispatcher.Invoke(() =>
            {
                Canvas.SetLeft(this, DisplayElement.Position.X);
                Canvas.SetTop(this, DisplayElement.Position.Y);
            });
        }
    }

    private void OnSizeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DisplayElement != null)
        {
            Dispatcher.Invoke(() =>
            {
                Width = DisplayElement.Size.Width;
                Height = DisplayElement.Size.Height;
            });
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

        // Set Canvas position
        Canvas.SetLeft(this, DisplayElement.Position.X);
        Canvas.SetTop(this, DisplayElement.Position.Y);
        Panel.SetZIndex(this, DisplayElement.ZIndex);

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
                textBlock.FontSize = Convert.ToDouble(fontSize);

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
                var lineHeightValue = Convert.ToDouble(lineHeight);
                if (lineHeightValue > 0)
                {
                    textBlock.LineHeight = textBlock.FontSize * lineHeightValue;
                }
            }

            // Apply letter spacing (WPF uses Typography.LetterSpacing in em units)
            if (DisplayElement.Properties.TryGetValue("LetterSpacing", out var letterSpacing))
            {
                var letterSpacingValue = Convert.ToDouble(letterSpacing);
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
                var thickness = Convert.ToDouble(borderThickness);
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
                border.CornerRadius = new CornerRadius(Convert.ToDouble(borderRadius));
            }
        }

        border.Child = textBlock;
        return border;
    }

    private UIElement CreateImageElement()
    {
        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
        };

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
            Text = "Image Element",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
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
                rectangle.StrokeThickness = Convert.ToDouble(borderThickness);
            }

            if (DisplayElement.Properties.TryGetValue("CornerRadius", out var cornerRadius))
            {
                rectangle.RadiusX = Convert.ToDouble(cornerRadius);
                rectangle.RadiusY = Convert.ToDouble(cornerRadius);
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
                ellipse.StrokeThickness = Convert.ToDouble(borderThickness);
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
                textBlock.FontSize = Convert.ToDouble(fontSize);

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

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            _dragStartPosition = e.GetPosition(Parent as UIElement);
            _isDragging = true;
            CaptureMouse();

            // Create alignment adorner on the canvas
            var designerCanvas = FindDesignerCanvas(this);
            if (designerCanvas != null)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(designerCanvas);
                if (adornerLayer != null)
                {
                    _alignmentAdorner = new AlignmentGuidesAdorner(designerCanvas);
                    adornerLayer.Add(_alignmentAdorner);
                }
            }

            // Raise selection event
            RaiseEvent(new RoutedEventArgs(SelectedEvent, this));

            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPosition = e.GetPosition(Parent as UIElement);
            var delta = currentPosition - _dragStartPosition;

            if (DisplayElement != null)
            {
                var designerCanvas = FindDesignerCanvas(this);
                var newX = DisplayElement.Position.X + delta.X;
                var newY = DisplayElement.Position.Y + delta.Y;

                // Calculate snapped position with alignment guides
                if (_alignmentAdorner != null && designerCanvas != null)
                {
                    // Get bounds of current element
                    var currentBounds = new Rect(newX, newY, DisplayElement.Size.Width, DisplayElement.Size.Height);

                    // Get bounds of canvas
                    var canvasBounds = new Rect(0, 0, designerCanvas.ActualWidth, designerCanvas.ActualHeight);

                    // Get bounds of all other elements on the canvas
                    var otherElementBounds = GetOtherElementBounds(designerCanvas);

                    // Calculate snapped position and update alignment guides
                    var snappedPoint = _alignmentAdorner.CalculateSnappedPosition(currentBounds, otherElementBounds, canvasBounds);
                    newX = snappedPoint.X;
                    newY = snappedPoint.Y;
                }
                else if (designerCanvas != null)
                {
                    // Fallback to simple grid snapping if adorner not available
                    var snappedPoint = designerCanvas.SnapPoint(new Point(newX, newY));
                    newX = snappedPoint.X;
                    newY = snappedPoint.Y;
                }

                DisplayElement.Position.X = newX;
                DisplayElement.Position.Y = newY;

                Canvas.SetLeft(this, newX);
                Canvas.SetTop(this, newY);
            }

            _dragStartPosition = currentPosition;
            e.Handled = true;
        }
    }

    /// <summary>
    /// Finds the DesignerCanvas in the visual tree
    /// </summary>
    private DesignerCanvas? FindDesignerCanvas(DependencyObject element)
    {
        var parent = VisualTreeHelper.GetParent(element);
        while (parent != null)
        {
            if (parent is DesignerCanvas canvas)
                return canvas;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    /// <summary>
    /// Gets the bounds of all other DesignerItemControls on the canvas (excluding this one)
    /// </summary>
    private IEnumerable<Rect> GetOtherElementBounds(DesignerCanvas canvas)
    {
        var bounds = new List<Rect>();

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(canvas); i++)
        {
            var child = VisualTreeHelper.GetChild(canvas, i);
            if (child is DesignerItemControl itemControl && itemControl != this && itemControl.DisplayElement != null)
            {
                var element = itemControl.DisplayElement;
                bounds.Add(new Rect(element.Position.X, element.Position.Y, element.Size.Width, element.Size.Height));
            }
        }

        return bounds;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();

            // Remove alignment adorner
            if (_alignmentAdorner != null)
            {
                var designerCanvas = FindDesignerCanvas(this);
                if (designerCanvas != null)
                {
                    var adornerLayer = AdornerLayer.GetAdornerLayer(designerCanvas);
                    if (adornerLayer != null)
                    {
                        adornerLayer.Remove(_alignmentAdorner);
                    }
                }
                _alignmentAdorner = null;
            }

            e.Handled = true;
        }
    }

    // Routed event for selection
    public static readonly RoutedEvent SelectedEvent =
        EventManager.RegisterRoutedEvent(
            "Selected",
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(DesignerItemControl));

    public event RoutedEventHandler Selected
    {
        add => AddHandler(SelectedEvent, value);
        remove => RemoveHandler(SelectedEvent, value);
    }
}
