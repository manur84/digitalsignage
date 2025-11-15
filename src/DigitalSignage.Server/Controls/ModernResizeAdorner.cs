using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// Modern resize adorner with improved visual feedback and smooth animations
/// </summary>
public class ModernResizeAdorner : Adorner
{
    private readonly VisualCollection _visualChildren;
    private readonly Dictionary<Thumb, ResizeDirection> _resizeHandles = new();
    private readonly Rectangle _selectionBorder;
    private Path _rotationHandle = null!;
    private double _rotationStartAngle;
    private Point _rotationStartPoint;
    private bool _isRotating;

    // Animation
    private readonly DoubleAnimation _fadeInAnimation;
    private readonly DoubleAnimation _fadeOutAnimation;

    // Colors
    private readonly Color _primaryColor = Color.FromRgb(0, 120, 215); // Windows blue
    private readonly Color _hoverColor = Color.FromRgb(0, 150, 255);
    private readonly Color _activeColor = Color.FromRgb(0, 100, 200);

    public enum ResizeDirection
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    protected override int VisualChildrenCount => _visualChildren.Count;

    public ModernResizeAdorner(UIElement adornedElement) : base(adornedElement)
    {
        _visualChildren = new VisualCollection(this);

        // Create animations
        _fadeInAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        _fadeOutAnimation = new DoubleAnimation(1, 0.7, TimeSpan.FromMilliseconds(100))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        // Create selection border
        _selectionBorder = new Rectangle
        {
            Stroke = new SolidColorBrush(_primaryColor),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        _visualChildren.Add(_selectionBorder);

        // Create resize handles
        CreateResizeHandles();

        // Create rotation handle
        CreateRotationHandle();

        // Animate appearance
        Opacity = 0;
        BeginAnimation(OpacityProperty, _fadeInAnimation);
    }

    private void CreateResizeHandles()
    {
        var positions = new[]
        {
            (ResizeDirection.TopLeft, new Point(0, 0)),
            (ResizeDirection.TopCenter, new Point(0.5, 0)),
            (ResizeDirection.TopRight, new Point(1, 0)),
            (ResizeDirection.MiddleLeft, new Point(0, 0.5)),
            (ResizeDirection.MiddleRight, new Point(1, 0.5)),
            (ResizeDirection.BottomLeft, new Point(0, 1)),
            (ResizeDirection.BottomCenter, new Point(0.5, 1)),
            (ResizeDirection.BottomRight, new Point(1, 1))
        };

        foreach (var (direction, relativePosition) in positions)
        {
            var handle = CreateResizeHandle(direction);
            _resizeHandles[handle] = direction;
            _visualChildren.Add(handle);
        }
    }

    private Thumb CreateResizeHandle(ResizeDirection direction)
    {
        var handle = new Thumb
        {
            Width = 10,
            Height = 10,
            Background = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(_primaryColor),
            BorderThickness = new Thickness(2),
            Cursor = GetCursorForDirection(direction),
            Template = CreateHandleTemplate()
        };

        // Add hover effect
        handle.MouseEnter += (s, e) =>
        {
            handle.BorderBrush = new SolidColorBrush(_hoverColor);
            handle.Width = handle.Height = 12;
        };

        handle.MouseLeave += (s, e) =>
        {
            handle.BorderBrush = new SolidColorBrush(_primaryColor);
            handle.Width = handle.Height = 10;
        };

        handle.DragStarted += OnResizeStarted;
        handle.DragDelta += OnResizeDelta;
        handle.DragCompleted += OnResizeCompleted;

        return handle;
    }

    private ControlTemplate CreateHandleTemplate()
    {
        var template = new ControlTemplate(typeof(Thumb));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Thumb.BackgroundProperty));
        factory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Thumb.BorderBrushProperty));
        factory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Thumb.BorderThicknessProperty));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));

        // Add drop shadow
        var shadow = new DropShadowEffect
        {
            Color = Colors.Black,
            Direction = 270,
            ShadowDepth = 2,
            Opacity = 0.3,
            BlurRadius = 3
        };
        factory.SetValue(UIElement.EffectProperty, shadow);

        template.VisualTree = factory;
        return template;
    }

    private void CreateRotationHandle()
    {
        // Create rotation handle path (curved arrow)
        var geometry = Geometry.Parse("M 0,0 A 5,5 0 1,1 10,0");
        _rotationHandle = new Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(_primaryColor),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Colors.White),
            Width = 24,
            Height = 24,
            Cursor = Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        // Add hover effect
        _rotationHandle.MouseEnter += (s, e) =>
        {
            _rotationHandle.Stroke = new SolidColorBrush(_hoverColor);
            var transform = new ScaleTransform(1.2, 1.2);
            _rotationHandle.RenderTransform = transform;
        };

        _rotationHandle.MouseLeave += (s, e) =>
        {
            _rotationHandle.Stroke = new SolidColorBrush(_primaryColor);
            _rotationHandle.RenderTransform = Transform.Identity;
        };

        _rotationHandle.MouseLeftButtonDown += OnRotationStarted;
        _rotationHandle.MouseMove += OnRotationDelta;
        _rotationHandle.MouseLeftButtonUp += OnRotationCompleted;

        _visualChildren.Add(_rotationHandle);
    }

    private Cursor GetCursorForDirection(ResizeDirection direction)
    {
        return direction switch
        {
            ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
            ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
            ResizeDirection.TopCenter or ResizeDirection.BottomCenter => Cursors.SizeNS,
            ResizeDirection.MiddleLeft or ResizeDirection.MiddleRight => Cursors.SizeWE,
            _ => Cursors.Arrow
        };
    }

    protected override Visual GetVisualChild(int index)
    {
        return _visualChildren[index];
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var adornedElementRect = new Rect(AdornedElement.RenderSize);

        // Position selection border
        _selectionBorder.Arrange(adornedElementRect);

        // Position resize handles
        foreach (var kvp in _resizeHandles)
        {
            var handle = kvp.Key;
            var direction = kvp.Value;

            var position = GetHandlePosition(direction, adornedElementRect);
            var handleRect = new Rect(
                position.X - handle.Width / 2,
                position.Y - handle.Height / 2,
                handle.Width,
                handle.Height);

            handle.Arrange(handleRect);
        }

        // Position rotation handle (above top center)
        var rotationPosition = new Point(adornedElementRect.Width / 2, -30);
        var rotationRect = new Rect(
            rotationPosition.X - _rotationHandle.Width / 2,
            rotationPosition.Y - _rotationHandle.Height / 2,
            _rotationHandle.Width,
            _rotationHandle.Height);
        _rotationHandle.Arrange(rotationRect);

        return finalSize;
    }

    private Point GetHandlePosition(ResizeDirection direction, Rect elementRect)
    {
        return direction switch
        {
            ResizeDirection.TopLeft => new Point(0, 0),
            ResizeDirection.TopCenter => new Point(elementRect.Width / 2, 0),
            ResizeDirection.TopRight => new Point(elementRect.Width, 0),
            ResizeDirection.MiddleLeft => new Point(0, elementRect.Height / 2),
            ResizeDirection.MiddleRight => new Point(elementRect.Width, elementRect.Height / 2),
            ResizeDirection.BottomLeft => new Point(0, elementRect.Height),
            ResizeDirection.BottomCenter => new Point(elementRect.Width / 2, elementRect.Height),
            ResizeDirection.BottomRight => new Point(elementRect.Width, elementRect.Height),
            _ => new Point()
        };
    }

    #region Resize Handlers

    private void OnResizeStarted(object sender, DragStartedEventArgs e)
    {
        // Visual feedback
        _selectionBorder.Stroke = new SolidColorBrush(_activeColor);
        _selectionBorder.StrokeThickness = 3;
    }

    private void OnResizeDelta(object sender, DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element && sender is Thumb handle)
        {
            if (_resizeHandles.TryGetValue(handle, out var direction))
            {
                double newWidth = element.Width;
                double newHeight = element.Height;
                double newLeft = Canvas.GetLeft(element);
                double newTop = Canvas.GetTop(element);

                switch (direction)
                {
                    case ResizeDirection.TopLeft:
                        newWidth = Math.Max(10, element.Width - e.HorizontalChange);
                        newHeight = Math.Max(10, element.Height - e.VerticalChange);
                        newLeft += e.HorizontalChange;
                        newTop += e.VerticalChange;
                        break;

                    case ResizeDirection.TopCenter:
                        newHeight = Math.Max(10, element.Height - e.VerticalChange);
                        newTop += e.VerticalChange;
                        break;

                    case ResizeDirection.TopRight:
                        newWidth = Math.Max(10, element.Width + e.HorizontalChange);
                        newHeight = Math.Max(10, element.Height - e.VerticalChange);
                        newTop += e.VerticalChange;
                        break;

                    case ResizeDirection.MiddleLeft:
                        newWidth = Math.Max(10, element.Width - e.HorizontalChange);
                        newLeft += e.HorizontalChange;
                        break;

                    case ResizeDirection.MiddleRight:
                        newWidth = Math.Max(10, element.Width + e.HorizontalChange);
                        break;

                    case ResizeDirection.BottomLeft:
                        newWidth = Math.Max(10, element.Width - e.HorizontalChange);
                        newHeight = Math.Max(10, element.Height + e.VerticalChange);
                        newLeft += e.HorizontalChange;
                        break;

                    case ResizeDirection.BottomCenter:
                        newHeight = Math.Max(10, element.Height + e.VerticalChange);
                        break;

                    case ResizeDirection.BottomRight:
                        newWidth = Math.Max(10, element.Width + e.HorizontalChange);
                        newHeight = Math.Max(10, element.Height + e.VerticalChange);
                        break;
                }

                // Apply changes
                element.Width = newWidth;
                element.Height = newHeight;
                Canvas.SetLeft(element, newLeft);
                Canvas.SetTop(element, newTop);
            }
        }
    }

    private void OnResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        // Reset visual feedback
        _selectionBorder.Stroke = new SolidColorBrush(_primaryColor);
        _selectionBorder.StrokeThickness = 2;
    }

    #endregion

    #region Rotation Handlers

    private void OnRotationStarted(object sender, MouseButtonEventArgs e)
    {
        if (AdornedElement is FrameworkElement element)
        {
            _isRotating = true;
            _rotationStartPoint = e.GetPosition(null);

            // Get current rotation
            if (element.RenderTransform is RotateTransform rotateTransform)
            {
                _rotationStartAngle = rotateTransform.Angle;
            }
            else
            {
                _rotationStartAngle = 0;
            }

            _rotationHandle.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnRotationDelta(object sender, MouseEventArgs e)
    {
        if (_isRotating && AdornedElement is FrameworkElement element)
        {
            var currentPoint = e.GetPosition(null);
            var centerPoint = new Point(
                Canvas.GetLeft(element) + element.Width / 2,
                Canvas.GetTop(element) + element.Height / 2);

            // Calculate angle
            var startVector = _rotationStartPoint - centerPoint;
            var currentVector = currentPoint - centerPoint;

            var angle = Vector.AngleBetween(startVector, currentVector);
            var newAngle = _rotationStartAngle + angle;

            // Snap to 15-degree increments if Shift is pressed
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                newAngle = Math.Round(newAngle / 15) * 15;
            }

            // Apply rotation
            element.RenderTransform = new RotateTransform(newAngle, element.Width / 2, element.Height / 2);
        }
    }

    private void OnRotationCompleted(object sender, MouseButtonEventArgs e)
    {
        if (_isRotating)
        {
            _isRotating = false;
            _rotationHandle.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    #endregion

    protected override void OnRender(DrawingContext drawingContext)
    {
        // Additional visual effects can be rendered here if needed
        base.OnRender(drawingContext);
    }
}