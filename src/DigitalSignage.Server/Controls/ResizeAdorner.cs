using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// Adorner that provides resize handles for designer items
/// </summary>
public class ResizeAdorner : Adorner
{
    private readonly VisualCollection _visualChildren;
    private readonly Thumb _topLeft, _topRight, _bottomLeft, _bottomRight;
    private readonly Thumb _top, _bottom, _left, _right;

    public ResizeAdorner(UIElement adornedElement) : base(adornedElement)
    {
        _visualChildren = new VisualCollection(this);

        // Create corner handles
        _topLeft = CreateResizeThumb(Cursors.SizeNWSE, HorizontalAlignment.Left, VerticalAlignment.Top);
        _topRight = CreateResizeThumb(Cursors.SizeNESW, HorizontalAlignment.Right, VerticalAlignment.Top);
        _bottomLeft = CreateResizeThumb(Cursors.SizeNESW, HorizontalAlignment.Left, VerticalAlignment.Bottom);
        _bottomRight = CreateResizeThumb(Cursors.SizeNWSE, HorizontalAlignment.Right, VerticalAlignment.Bottom);

        // Create edge handles
        _top = CreateResizeThumb(Cursors.SizeNS, HorizontalAlignment.Center, VerticalAlignment.Top);
        _bottom = CreateResizeThumb(Cursors.SizeNS, HorizontalAlignment.Center, VerticalAlignment.Bottom);
        _left = CreateResizeThumb(Cursors.SizeWE, HorizontalAlignment.Left, VerticalAlignment.Center);
        _right = CreateResizeThumb(Cursors.SizeWE, HorizontalAlignment.Right, VerticalAlignment.Center);

        // Add drag handlers
        _topLeft.DragDelta += (s, e) => ResizeTopLeft(e);
        _topRight.DragDelta += (s, e) => ResizeTopRight(e);
        _bottomLeft.DragDelta += (s, e) => ResizeBottomLeft(e);
        _bottomRight.DragDelta += (s, e) => ResizeBottomRight(e);
        _top.DragDelta += (s, e) => ResizeTop(e);
        _bottom.DragDelta += (s, e) => ResizeBottom(e);
        _left.DragDelta += (s, e) => ResizeLeft(e);
        _right.DragDelta += (s, e) => ResizeRight(e);
    }

    private Thumb CreateResizeThumb(Cursor cursor, HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var thumb = new Thumb
        {
            Cursor = cursor,
            Width = 8,
            Height = 8,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical
        };

        _visualChildren.Add(thumb);
        return thumb;
    }

    protected override int VisualChildrenCount => _visualChildren.Count;

    protected override Visual GetVisualChild(int index) => _visualChildren[index];

    protected override Size ArrangeOverride(Size finalSize)
    {
        var desiredWidth = AdornedElement.RenderSize.Width;
        var desiredHeight = AdornedElement.RenderSize.Height;

        var thumbSize = 8.0;
        var halfThumb = thumbSize / 2;

        // Arrange corner handles
        _topLeft.Arrange(new Rect(-halfThumb, -halfThumb, thumbSize, thumbSize));
        _topRight.Arrange(new Rect(desiredWidth - halfThumb, -halfThumb, thumbSize, thumbSize));
        _bottomLeft.Arrange(new Rect(-halfThumb, desiredHeight - halfThumb, thumbSize, thumbSize));
        _bottomRight.Arrange(new Rect(desiredWidth - halfThumb, desiredHeight - halfThumb, thumbSize, thumbSize));

        // Arrange edge handles
        _top.Arrange(new Rect(desiredWidth / 2 - halfThumb, -halfThumb, thumbSize, thumbSize));
        _bottom.Arrange(new Rect(desiredWidth / 2 - halfThumb, desiredHeight - halfThumb, thumbSize, thumbSize));
        _left.Arrange(new Rect(-halfThumb, desiredHeight / 2 - halfThumb, thumbSize, thumbSize));
        _right.Arrange(new Rect(desiredWidth - halfThumb, desiredHeight / 2 - halfThumb, thumbSize, thumbSize));

        return finalSize;
    }

    private void ResizeTopLeft(DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element && element is DesignerItemControl control && control.DisplayElement != null)
        {
            var newWidth = Math.Max(20, element.Width - e.HorizontalChange);
            var newHeight = Math.Max(20, element.Height - e.VerticalChange);

            if (newWidth != element.Width)
            {
                var deltaX = element.Width - newWidth;
                control.DisplayElement.Position.X += deltaX;
                control.DisplayElement.Size.Width = newWidth;
                element.Width = newWidth;
                Canvas.SetLeft(element, control.DisplayElement.Position.X);
            }

            if (newHeight != element.Height)
            {
                var deltaY = element.Height - newHeight;
                control.DisplayElement.Position.Y += deltaY;
                control.DisplayElement.Size.Height = newHeight;
                element.Height = newHeight;
                Canvas.SetTop(element, control.DisplayElement.Position.Y);
            }
        }
    }

    private void ResizeTopRight(DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element && element is DesignerItemControl control && control.DisplayElement != null)
        {
            var newWidth = Math.Max(20, element.Width + e.HorizontalChange);
            var newHeight = Math.Max(20, element.Height - e.VerticalChange);

            control.DisplayElement.Size.Width = newWidth;
            element.Width = newWidth;

            if (newHeight != element.Height)
            {
                var deltaY = element.Height - newHeight;
                control.DisplayElement.Position.Y += deltaY;
                control.DisplayElement.Size.Height = newHeight;
                element.Height = newHeight;
                Canvas.SetTop(element, control.DisplayElement.Position.Y);
            }
        }
    }

    private void ResizeBottomLeft(DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element && element is DesignerItemControl control && control.DisplayElement != null)
        {
            var newWidth = Math.Max(20, element.Width - e.HorizontalChange);
            var newHeight = Math.Max(20, element.Height + e.VerticalChange);

            if (newWidth != element.Width)
            {
                var deltaX = element.Width - newWidth;
                control.DisplayElement.Position.X += deltaX;
                control.DisplayElement.Size.Width = newWidth;
                element.Width = newWidth;
                Canvas.SetLeft(element, control.DisplayElement.Position.X);
            }

            control.DisplayElement.Size.Height = newHeight;
            element.Height = newHeight;
        }
    }

    private void ResizeBottomRight(DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element && element is DesignerItemControl control && control.DisplayElement != null)
        {
            var newWidth = Math.Max(20, element.Width + e.HorizontalChange);
            var newHeight = Math.Max(20, element.Height + e.VerticalChange);

            control.DisplayElement.Size.Width = newWidth;
            control.DisplayElement.Size.Height = newHeight;
            element.Width = newWidth;
            element.Height = newHeight;
        }
    }

    private void ResizeTop(DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element && element is DesignerItemControl control && control.DisplayElement != null)
        {
            var newHeight = Math.Max(20, element.Height - e.VerticalChange);

            if (newHeight != element.Height)
            {
                var deltaY = element.Height - newHeight;
                control.DisplayElement.Position.Y += deltaY;
                control.DisplayElement.Size.Height = newHeight;
                element.Height = newHeight;
                Canvas.SetTop(element, control.DisplayElement.Position.Y);
            }
        }
    }

    private void ResizeBottom(DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element && element is DesignerItemControl control && control.DisplayElement != null)
        {
            var newHeight = Math.Max(20, element.Height + e.VerticalChange);
            control.DisplayElement.Size.Height = newHeight;
            element.Height = newHeight;
        }
    }

    private void ResizeLeft(DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element && element is DesignerItemControl control && control.DisplayElement != null)
        {
            var newWidth = Math.Max(20, element.Width - e.HorizontalChange);

            if (newWidth != element.Width)
            {
                var deltaX = element.Width - newWidth;
                control.DisplayElement.Position.X += deltaX;
                control.DisplayElement.Size.Width = newWidth;
                element.Width = newWidth;
                Canvas.SetLeft(element, control.DisplayElement.Position.X);
            }
        }
    }

    private void ResizeRight(DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element && element is DesignerItemControl control && control.DisplayElement != null)
        {
            var newWidth = Math.Max(20, element.Width + e.HorizontalChange);
            control.DisplayElement.Size.Width = newWidth;
            element.Width = newWidth;
        }
    }
}
