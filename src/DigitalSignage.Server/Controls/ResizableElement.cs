using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// A resizable and movable element for the designer canvas
/// </summary>
public class ResizableElement : ContentControl
{
    private const double ThumbSize = 8;
    private bool _isDragging;
    private Point _dragStartPoint;
    private Thumb[] _resizeThumbs = Array.Empty<Thumb>();

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(ResizableElement),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public event EventHandler<Point>? PositionChanged;
    public event EventHandler<Size>? SizeChanged;

    static ResizableElement()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ResizableElement),
            new FrameworkPropertyMetadata(typeof(ResizableElement)));
    }

    public ResizableElement()
    {
        Focusable = true;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        CreateResizeThumbs();
    }

    private void CreateResizeThumbs()
    {
        if (Template == null) return;

        var adornerLayer = AdornerLayer.GetAdornerLayer(this);
        if (adornerLayer == null) return;

        // Create resize thumbs at corners and edges
        _resizeThumbs = new Thumb[]
        {
            CreateThumb(Cursors.SizeNWSE, 0, 0), // Top-left
            CreateThumb(Cursors.SizeNS, 0.5, 0), // Top
            CreateThumb(Cursors.SizeNESW, 1, 0), // Top-right
            CreateThumb(Cursors.SizeWE, 1, 0.5), // Right
            CreateThumb(Cursors.SizeNWSE, 1, 1), // Bottom-right
            CreateThumb(Cursors.SizeNS, 0.5, 1), // Bottom
            CreateThumb(Cursors.SizeNESW, 0, 1), // Bottom-left
            CreateThumb(Cursors.SizeWE, 0, 0.5)  // Left
        };

        UpdateThumbsVisibility();
    }

    private Thumb CreateThumb(Cursor cursor, double horizontalAlignment, double verticalAlignment)
    {
        var thumb = new Thumb
        {
            Width = ThumbSize,
            Height = ThumbSize,
            Cursor = cursor,
            Background = Brushes.White,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1)
        };

        thumb.DragDelta += (s, e) => OnThumbDragDelta(s, e, horizontalAlignment, verticalAlignment);
        return thumb;
    }

    private void OnThumbDragDelta(object sender, DragDeltaEventArgs e, double hAlign, double vAlign)
    {
        var newWidth = Width;
        var newHeight = Height;
        var newLeft = Canvas.GetLeft(this);
        var newTop = Canvas.GetTop(this);

        // Horizontal resize
        if (hAlign == 0) // Left
        {
            newWidth = Math.Max(20, Width - e.HorizontalChange);
            newLeft = Canvas.GetLeft(this) + (Width - newWidth);
        }
        else if (hAlign == 1) // Right
        {
            newWidth = Math.Max(20, Width + e.HorizontalChange);
        }

        // Vertical resize
        if (vAlign == 0) // Top
        {
            newHeight = Math.Max(20, Height - e.VerticalChange);
            newTop = Canvas.GetTop(this) + (Height - newHeight);
        }
        else if (vAlign == 1) // Bottom
        {
            newHeight = Math.Max(20, Height + e.VerticalChange);
        }

        Width = newWidth;
        Height = newHeight;
        Canvas.SetLeft(this, newLeft);
        Canvas.SetTop(this, newTop);

        SizeChanged?.Invoke(this, new Size(newWidth, newHeight));
        PositionChanged?.Invoke(this, new Point(newLeft, newTop));
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Thumb) return;

        IsSelected = true;
        _isDragging = true;
        _dragStartPoint = e.GetPosition(Parent as UIElement);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetPosition(Parent as UIElement);
        var offset = currentPoint - _dragStartPoint;

        var left = Canvas.GetLeft(this) + offset.X;
        var top = Canvas.GetTop(this) + offset.Y;

        Canvas.SetLeft(this, left);
        Canvas.SetTop(this, top);

        _dragStartPoint = currentPoint;
        PositionChanged?.Invoke(this, new Point(left, top));
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
        }
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResizableElement element)
        {
            element.UpdateThumbsVisibility();
        }
    }

    private void UpdateThumbsVisibility()
    {
        foreach (var thumb in _resizeThumbs)
        {
            thumb.Visibility = IsSelected ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
