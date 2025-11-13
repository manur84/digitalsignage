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
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(5) // Add some padding
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
