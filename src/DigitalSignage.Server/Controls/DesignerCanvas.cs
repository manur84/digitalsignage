using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// Custom canvas for the visual designer with grid, snap-to-grid, and touch support
/// </summary>
public class DesignerCanvas : Canvas
{
    private Point? _selectionStartPoint;
    private Rectangle? _selectionRectangle;

    // Touch gesture support
    private Point? _touchStartPoint;
    private double _initialZoom = 1.0;

    public static readonly DependencyProperty ShowGridProperty =
        DependencyProperty.Register(
            nameof(ShowGrid),
            typeof(bool),
            typeof(DesignerCanvas),
            new PropertyMetadata(true, OnShowGridChanged));

    public static readonly DependencyProperty SnapToGridProperty =
        DependencyProperty.Register(
            nameof(SnapToGrid),
            typeof(bool),
            typeof(DesignerCanvas),
            new PropertyMetadata(true));

    public static readonly DependencyProperty GridSizeProperty =
        DependencyProperty.Register(
            nameof(GridSize),
            typeof(int),
            typeof(DesignerCanvas),
            new PropertyMetadata(10, OnGridSizeChanged));

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    public int GridSize
    {
        get => (int)GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    public DesignerCanvas()
    {
        Background = Brushes.White;
        ClipToBounds = true;
        Focusable = true;

        // Mouse events
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;

        // Keyboard events for multi-selection and commands
        KeyDown += OnKeyDown;

        // Enable touch support
        IsManipulationEnabled = true;
        ManipulationStarting += OnManipulationStarting;
        ManipulationDelta += OnManipulationDelta;
        ManipulationCompleted += OnManipulationCompleted;

        // Touch events
        TouchDown += OnTouchDown;
        TouchMove += OnTouchMove;
        TouchUp += OnTouchUp;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (ShowGrid)
        {
            DrawGrid(dc);
        }
    }

    private void DrawGrid(DrawingContext dc)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(230, 230, 230)), 1);
        pen.Freeze();

        // Draw vertical lines
        for (double x = 0; x < ActualWidth; x += GridSize)
        {
            dc.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
        }

        // Draw horizontal lines
        for (double y = 0; y < ActualHeight; y += GridSize)
        {
            dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
        }
    }

    public Point SnapPoint(Point point)
    {
        if (!SnapToGrid) return point;

        return new Point(
            Math.Round(point.X / GridSize) * GridSize,
            Math.Round(point.Y / GridSize) * GridSize);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Focus the canvas for keyboard events
        Focus();

        // Check if clicking on a DesignerItemControl for element selection
        var clickedElement = FindVisualParent<DesignerItemControl>(e.OriginalSource as DependencyObject);

        if (clickedElement != null && clickedElement.DisplayElement != null)
        {
            // Clicked on an element - handle selection with modifier keys
            var viewModel = DataContext as DesignerViewModel;
            if (viewModel != null)
            {
                bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                // Create parameter tuple for SelectElementCommand
                var parameter = (clickedElement.DisplayElement, isCtrlPressed, isShiftPressed);
                viewModel.SelectElementCommand.Execute(parameter);
            }

            // Don't start selection rectangle when clicking on an element
            return;
        }

        // Clicked on empty canvas - start selection rectangle
        if (e.Source == this || clickedElement == null)
        {
            var viewModel = DataContext as DesignerViewModel;
            if (viewModel != null)
            {
                // Clear selection if not holding Ctrl
                if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    viewModel.SelectionService.ClearSelection();
                }

                // Start selection rectangle
                _selectionStartPoint = e.GetPosition(this);
                var parameter = (_selectionStartPoint.Value.X, _selectionStartPoint.Value.Y);
                viewModel.StartSelectionRectangleCommand.Execute(parameter);
            }
            else
            {
                _selectionStartPoint = e.GetPosition(this);
            }

            CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_selectionStartPoint.HasValue && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPoint = e.GetPosition(this);

            // Update ViewModel's selection rectangle
            var viewModel = DataContext as DesignerViewModel;
            if (viewModel != null)
            {
                var parameter = (currentPoint.X, currentPoint.Y);
                viewModel.UpdateSelectionRectangleCommand.Execute(parameter);
            }

            // Update visual selection rectangle
            UpdateSelectionRectangle(_selectionStartPoint.Value, currentPoint);
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_selectionStartPoint.HasValue)
        {
            // End selection rectangle in ViewModel
            var viewModel = DataContext as DesignerViewModel;
            if (viewModel != null)
            {
                viewModel.EndSelectionRectangleCommand.Execute(null);
            }

            _selectionStartPoint = null;
            RemoveSelectionRectangle();
            ReleaseMouseCapture();
        }
    }

    private void UpdateSelectionRectangle(Point start, Point end)
    {
        if (_selectionRectangle == null)
        {
            _selectionRectangle = new Rectangle
            {
                Stroke = Brushes.Blue,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 255))
            };
            Children.Add(_selectionRectangle);
        }

        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);

        SetLeft(_selectionRectangle, left);
        SetTop(_selectionRectangle, top);
        _selectionRectangle.Width = width;
        _selectionRectangle.Height = height;
    }

    private void RemoveSelectionRectangle()
    {
        if (_selectionRectangle != null)
        {
            Children.Remove(_selectionRectangle);
            _selectionRectangle = null;
        }
    }

    private static void OnShowGridChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DesignerCanvas canvas)
        {
            canvas.InvalidateVisual();
        }
    }

    private static void OnGridSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DesignerCanvas canvas && canvas.ShowGrid)
        {
            canvas.InvalidateVisual();
        }
    }

    // Touch Support Event Handlers

    /// <summary>
    /// Called when manipulation (touch gesture) starts
    /// </summary>
    private void OnManipulationStarting(object? sender, ManipulationStartingEventArgs e)
    {
        e.ManipulationContainer = this;
        e.Handled = true;
    }

    /// <summary>
    /// Handles manipulation delta (pinch-to-zoom and pan gestures)
    /// </summary>
    private void OnManipulationDelta(object? sender, ManipulationDeltaEventArgs e)
    {
        var element = e.Source as FrameworkElement;
        if (element == null) return;

        // Get the parent that has the ScaleTransform and TranslateTransform
        var parent = VisualTreeHelper.GetParent(this) as FrameworkElement;
        if (parent == null) return;

        // Handle pinch-to-zoom
        if (e.DeltaManipulation.Scale.X != 0 || e.DeltaManipulation.Scale.Y != 0)
        {
            var scaleX = e.DeltaManipulation.Scale.X;
            var scaleY = e.DeltaManipulation.Scale.Y;

            // Use average of X and Y scale for uniform scaling
            var scale = (scaleX + scaleY) / 2;

            // Apply zoom via event or command (needs to be wired to ViewModel)
            var currentZoom = _initialZoom * scale;
            currentZoom = Math.Max(0.25, Math.Min(2.0, currentZoom)); // Clamp between 25% and 200%

            // Raise custom event for zoom change
            RaiseZoomChangedEvent(currentZoom);
        }

        // Handle two-finger pan
        if (e.DeltaManipulation.Translation.X != 0 || e.DeltaManipulation.Translation.Y != 0)
        {
            var deltaX = e.DeltaManipulation.Translation.X;
            var deltaY = e.DeltaManipulation.Translation.Y;

            // Raise custom event for pan change
            RaisePanChangedEvent(deltaX, deltaY);
        }

        e.Handled = true;
    }

    /// <summary>
    /// Called when manipulation completes
    /// </summary>
    private void OnManipulationCompleted(object? sender, ManipulationCompletedEventArgs e)
    {
        _initialZoom = 1.0; // Reset for next gesture
        e.Handled = true;
    }

    /// <summary>
    /// Handles single touch down (alternative to mouse)
    /// </summary>
    private void OnTouchDown(object? sender, TouchEventArgs e)
    {
        if (e.TouchDevice.Captured == null)
        {
            _touchStartPoint = e.GetTouchPoint(this).Position;
            e.TouchDevice.Capture(this);
            Focus();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles touch move (alternative to mouse)
    /// </summary>
    private void OnTouchMove(object? sender, TouchEventArgs e)
    {
        if (_touchStartPoint.HasValue && e.TouchDevice.Captured == this)
        {
            var currentPoint = e.GetTouchPoint(this).Position;
            // Touch move logic (similar to mouse move for selection)
            UpdateSelectionRectangle(_touchStartPoint.Value, currentPoint);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles touch up (alternative to mouse)
    /// </summary>
    private void OnTouchUp(object? sender, TouchEventArgs e)
    {
        if (_touchStartPoint.HasValue && e.TouchDevice.Captured == this)
        {
            _touchStartPoint = null;
            RemoveSelectionRectangle();
            e.TouchDevice.Capture(null);
            e.Handled = true;
        }
    }

    // Custom routed events for touch gestures

    public static readonly RoutedEvent ZoomChangedEvent = EventManager.RegisterRoutedEvent(
        "ZoomChanged",
        RoutingStrategy.Bubble,
        typeof(RoutedPropertyChangedEventHandler<double>),
        typeof(DesignerCanvas));

    public event RoutedPropertyChangedEventHandler<double> ZoomChanged
    {
        add => AddHandler(ZoomChangedEvent, value);
        remove => RemoveHandler(ZoomChangedEvent, value);
    }

    public static readonly RoutedEvent PanChangedEvent = EventManager.RegisterRoutedEvent(
        "PanChanged",
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(DesignerCanvas));

    public event RoutedEventHandler PanChanged
    {
        add => AddHandler(PanChangedEvent, value);
        remove => RemoveHandler(PanChangedEvent, value);
    }

    private void RaiseZoomChangedEvent(double newZoom)
    {
        var args = new RoutedPropertyChangedEventArgs<double>(_initialZoom, newZoom, ZoomChangedEvent);
        RaiseEvent(args);
    }

    private void RaisePanChangedEvent(double deltaX, double deltaY)
    {
        var args = new RoutedEventArgs(PanChangedEvent, this);
        RaiseEvent(args);
    }

    /// <summary>
    /// Handles keyboard shortcuts for multi-selection and commands
    /// </summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var viewModel = DataContext as DesignerViewModel;
        if (viewModel == null) return;

        bool ctrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

        // Ctrl+A: Select All
        if (ctrlPressed && e.Key == Key.A)
        {
            viewModel.SelectAllCommand.Execute(null);
            e.Handled = true;
        }
        // Delete: Delete selected elements
        else if (e.Key == Key.Delete)
        {
            if (viewModel.DeleteSelectedCommand.CanExecute(null))
            {
                viewModel.DeleteSelectedCommand.Execute(null);
            }
            e.Handled = true;
        }
        // Ctrl+D: Duplicate selected elements
        else if (ctrlPressed && e.Key == Key.D)
        {
            if (viewModel.DuplicateSelectedCommand.CanExecute(null))
            {
                viewModel.DuplicateSelectedCommand.Execute(null);
            }
            e.Handled = true;
        }
        // Escape: Clear selection
        else if (e.Key == Key.Escape)
        {
            viewModel.SelectionService.ClearSelection();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Finds a parent of a specific type in the visual tree
    /// </summary>
    private T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
                return parent;

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}
