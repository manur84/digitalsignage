using System.Windows;
using System.Windows.Controls;
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
    }

    private static void OnDisplayElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DesignerItemControl control && e.NewValue is DisplayElement element)
        {
            control.UpdateFromElement();
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
        if (DisplayElement == null) return;

        Canvas.SetLeft(this, DisplayElement.Position.X);
        Canvas.SetTop(this, DisplayElement.Position.Y);
        Width = DisplayElement.Size.Width;
        Height = DisplayElement.Size.Height;
        Panel.SetZIndex(this, DisplayElement.ZIndex);

        // Render content based on element type
        Content = CreateContentForElement();
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
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
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
        }

        return textBlock;
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
            StrokeThickness = 2
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
                // Snap to grid if parent is DesignerCanvas
                var newX = DisplayElement.Position.X + delta.X;
                var newY = DisplayElement.Position.Y + delta.Y;

                if (Parent is DesignerCanvas canvas)
                {
                    var snappedPoint = canvas.SnapPoint(new Point(newX, newY));
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

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
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
