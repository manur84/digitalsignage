using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// Shape editor overlay inspired by https://github.com/PetrVobornik/WpfShapeEditor
/// and the vector-rotation approach described on
/// https://stackoverflow.com/questions/22257408/user-resizable-and-user-rotatable-shapes-on-canvas-with-wpf
/// </summary>
public class ShapeEditorOverlay : Canvas
{
    private const double HandleSize = 12;
    private const double MinSize = 20;
    private const double RotationHandleOffset = 30;

    private readonly Rectangle _selectionBorder;
    private readonly Dictionary<Thumb, ResizeDirection> _resizeHandles = new();
    private readonly Thumb _moveThumb;
    private readonly Thumb _rotationThumb;

    private DesignerItemControl? _attachedControl;
    private DisplayElement? _model;

    public Canvas? HostCanvas { get; set; }

    public ShapeEditorOverlay()
    {
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
        Background = Brushes.Transparent;

        _selectionBorder = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Children.Add(_selectionBorder);

        _moveThumb = new Thumb
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.SizeAll
        };
        _moveThumb.DragDelta += MoveThumbOnDragDelta;
        Children.Add(_moveThumb);

        _rotationThumb = new Thumb
        {
            Width = HandleSize,
            Height = HandleSize,
            Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand
        };
        _rotationThumb.DragStarted += RotationThumbOnDragStarted;
        _rotationThumb.DragDelta += RotationThumbOnDragDelta;
        _rotationThumb.DragCompleted += RotationThumbOnDragCompleted;
        Children.Add(_rotationThumb);

        CreateResizeHandles();
    }

    public void Attach(DesignerItemControl control)
    {
        if (_attachedControl == control)
            return;

        Detach();

        _attachedControl = control;
        _model = control.DisplayElement;

        if (_model == null)
            return;

        SubscribeToModel();
        UpdateOverlay();

        Visibility = Visibility.Visible;
        IsHitTestVisible = true;
    }

    public void Detach()
    {
        if (_model != null)
        {
            _model.PropertyChanged -= OnModelPropertyChanged;
            _model.Position.PropertyChanged -= OnModelPositionChanged;
            _model.Size.PropertyChanged -= OnModelSizeChanged;
        }

        _attachedControl = null;
        _model = null;
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
    }

    private void SubscribeToModel()
    {
        if (_model == null)
            return;

        _model.PropertyChanged += OnModelPropertyChanged;
        _model.Position.PropertyChanged += OnModelPositionChanged;
        _model.Size.PropertyChanged += OnModelSizeChanged;
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DisplayElement.Rotation) ||
            e.PropertyName == nameof(DisplayElement.ZIndex))
        {
            UpdateOverlay();
        }
    }

    private void OnModelPositionChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateOverlayPosition();
    }

    private void OnModelSizeChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateOverlaySize();
    }

    private void UpdateOverlay()
    {
        UpdateOverlaySize();
        UpdateOverlayPosition();
    }

    private void UpdateOverlaySize()
    {
        if (_model == null)
            return;

        Width = _model.Size.Width;
        Height = _model.Size.Height;
        _selectionBorder.Width = Width;
        _selectionBorder.Height = Height;

        ArrangeHandles();
        UpdateRotationVisual();
    }

    private void UpdateOverlayPosition()
    {
        if (_model == null)
            return;

        Canvas.SetLeft(this, _model.Position.X);
        Canvas.SetTop(this, _model.Position.Y);
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = new RotateTransform(_model.Rotation);
    }

    private void UpdateRotationVisual()
    {
        if (_model == null)
            return;

        var rotation = _model.Rotation;
        var transform = new RotateTransform(rotation, Width / 2, Height / 2);

        _selectionBorder.RenderTransformOrigin = new Point(0.5, 0.5);
        _selectionBorder.RenderTransform = transform;

        _moveThumb.RenderTransformOrigin = new Point(0.5, 0.5);
        _moveThumb.RenderTransform = transform;

        _rotationThumb.RenderTransformOrigin = new Point(0.5, 0.5);
        _rotationThumb.RenderTransform = transform;

        foreach (var handle in _resizeHandles.Keys)
        {
            handle.RenderTransformOrigin = new Point(0.5, 0.5);
            handle.RenderTransform = transform;
        }
    }

    private void ArrangeHandles()
    {
        var w = Width;
        var h = Height;

        _moveThumb.Width = w;
        _moveThumb.Height = h;
        Canvas.SetLeft(_moveThumb, 0);
        Canvas.SetTop(_moveThumb, 0);

        foreach (var pair in _resizeHandles)
        {
            var handle = pair.Key;
            var dir = pair.Value;
            double left = 0, top = 0;

            switch (dir)
            {
                case ResizeDirection.TopLeft:
                    left = -HandleSize / 2;
                    top = -HandleSize / 2;
                    break;
                case ResizeDirection.TopCenter:
                    left = w / 2 - HandleSize / 2;
                    top = -HandleSize / 2;
                    break;
                case ResizeDirection.TopRight:
                    left = w - HandleSize / 2;
                    top = -HandleSize / 2;
                    break;
                case ResizeDirection.MiddleLeft:
                    left = -HandleSize / 2;
                    top = h / 2 - HandleSize / 2;
                    break;
                case ResizeDirection.MiddleRight:
                    left = w - HandleSize / 2;
                    top = h / 2 - HandleSize / 2;
                    break;
                case ResizeDirection.BottomLeft:
                    left = -HandleSize / 2;
                    top = h - HandleSize / 2;
                    break;
                case ResizeDirection.BottomCenter:
                    left = w / 2 - HandleSize / 2;
                    top = h - HandleSize / 2;
                    break;
                case ResizeDirection.BottomRight:
                    left = w - HandleSize / 2;
                    top = h - HandleSize / 2;
                    break;
            }

            Canvas.SetLeft(handle, left);
            Canvas.SetTop(handle, top);
        }

        Canvas.SetLeft(_rotationThumb, w / 2 - HandleSize / 2);
        Canvas.SetTop(_rotationThumb, -RotationHandleOffset);
    }

    private void CreateResizeHandles()
    {
        foreach (ResizeDirection direction in Enum.GetValues(typeof(ResizeDirection)))
        {
            if (direction == ResizeDirection.None)
                continue;

            var thumb = new Thumb
            {
                Width = HandleSize,
                Height = HandleSize,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                BorderThickness = new Thickness(1),
                Cursor = GetCursor(direction),
                Tag = direction
            };

            thumb.DragDelta += ResizeThumbOnDragDelta;
            Children.Add(thumb);
            _resizeHandles[thumb] = direction;
        }
    }

    private static Cursor GetCursor(ResizeDirection direction) => direction switch
    {
        ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
        ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
        ResizeDirection.TopCenter or ResizeDirection.BottomCenter => Cursors.SizeNS,
        ResizeDirection.MiddleLeft or ResizeDirection.MiddleRight => Cursors.SizeWE,
        _ => Cursors.SizeAll
    };

    private void ResizeThumbOnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_model == null)
            return;

        var thumb = (Thumb)sender;
        var direction = (ResizeDirection)thumb.Tag;

        var localDelta = RotateVector(e.HorizontalChange, e.VerticalChange, -_model.Rotation);

        var width = _model.Size.Width;
        var height = _model.Size.Height;
        var posX = _model.Position.X;
        var posY = _model.Position.Y;

        switch (direction)
        {
            case ResizeDirection.TopLeft:
                width = Math.Max(MinSize, width - localDelta.X);
                height = Math.Max(MinSize, height - localDelta.Y);
                var shiftTL = RotateVector(_model.Size.Width - width, _model.Size.Height - height, _model.Rotation);
                posX += shiftTL.X;
                posY += shiftTL.Y;
                break;
            case ResizeDirection.TopCenter:
                height = Math.Max(MinSize, height - localDelta.Y);
                var shiftTC = RotateVector(0, _model.Size.Height - height, _model.Rotation);
                posX += shiftTC.X;
                posY += shiftTC.Y;
                break;
            case ResizeDirection.TopRight:
                width = Math.Max(MinSize, width + localDelta.X);
                height = Math.Max(MinSize, height - localDelta.Y);
                var shiftTR = RotateVector(0, _model.Size.Height - height, _model.Rotation);
                posX += shiftTR.X;
                posY += shiftTR.Y;
                break;
            case ResizeDirection.MiddleLeft:
                width = Math.Max(MinSize, width - localDelta.X);
                var shiftML = RotateVector(_model.Size.Width - width, 0, _model.Rotation);
                posX += shiftML.X;
                posY += shiftML.Y;
                break;
            case ResizeDirection.MiddleRight:
                width = Math.Max(MinSize, width + localDelta.X);
                break;
            case ResizeDirection.BottomLeft:
                width = Math.Max(MinSize, width - localDelta.X);
                var shiftBL = RotateVector(_model.Size.Width - width, 0, _model.Rotation);
                posX += shiftBL.X;
                posY += shiftBL.Y;
                height = Math.Max(MinSize, height + localDelta.Y);
                break;
            case ResizeDirection.BottomCenter:
                height = Math.Max(MinSize, height + localDelta.Y);
                break;
            case ResizeDirection.BottomRight:
                width = Math.Max(MinSize, width + localDelta.X);
                height = Math.Max(MinSize, height + localDelta.Y);
                break;
        }

        _model.Position.X = posX;
        _model.Position.Y = posY;
        _model.Size.Width = width;
        _model.Size.Height = height;
    }

    private void MoveThumbOnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_model == null)
            return;

        _model.Position.X += e.HorizontalChange;
        _model.Position.Y += e.VerticalChange;
    }

    private Point _rotationStartPoint;
    private double _rotationStartAngle;

    private void RotationThumbOnDragStarted(object sender, DragStartedEventArgs e)
    {
        if (_model == null)
            return;

        _rotationStartAngle = _model.Rotation;
        _rotationStartPoint = Mouse.GetPosition(HostCanvas ?? this);
    }

    private void RotationThumbOnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_model == null)
            return;

        var canvas = HostCanvas ?? this;
        var currentPoint = Mouse.GetPosition(canvas);

        var center = new Point(
            _model.Position.X + _model.Size.Width / 2,
            _model.Position.Y + _model.Size.Height / 2);

        var startVector = _rotationStartPoint - center;
        var currentVector = currentPoint - center;

        var angle = Vector.AngleBetween(startVector, currentVector);
        var newAngle = _rotationStartAngle + angle;

        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            newAngle = Math.Round(newAngle / 15) * 15;
        }

        _model.Rotation = newAngle;
    }

    private void RotationThumbOnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        // nothing for now
    }

    private static Vector RotateVector(double x, double y, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        return new Vector(x * cos - y * sin, x * sin + y * cos);
    }

    private enum ResizeDirection
    {
        None,
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
}
