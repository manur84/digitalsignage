using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// Modern designer canvas with enhanced visual feedback, smooth animations, and optimized performance
/// </summary>
public class ModernDesignerCanvas : Canvas
{
    private Point? _selectionStartPoint;
    private Rectangle? _selectionRectangle;
    private readonly DoubleAnimation _fadeInAnimation;
    private readonly DoubleAnimation _fadeOutAnimation;

    // Grid visuals
    private DrawingVisual? _gridVisual;
    private bool _gridNeedsUpdate = true;

    // Performance optimization
    private readonly VisualCollection _visualChildren;
    private readonly Dictionary<DisplayElement, FrameworkElement> _elementVisualCache = new();

    #region Dependency Properties

    public static readonly DependencyProperty ShowGridProperty =
        DependencyProperty.Register(
            nameof(ShowGrid),
            typeof(bool),
            typeof(ModernDesignerCanvas),
            new PropertyMetadata(true, OnGridPropertyChanged));

    public static readonly DependencyProperty GridSizeProperty =
        DependencyProperty.Register(
            nameof(GridSize),
            typeof(int),
            typeof(ModernDesignerCanvas),
            new PropertyMetadata(10, OnGridPropertyChanged));

    public static readonly DependencyProperty SnapToGridProperty =
        DependencyProperty.Register(
            nameof(SnapToGrid),
            typeof(bool),
            typeof(ModernDesignerCanvas),
            new PropertyMetadata(true));

    public static readonly DependencyProperty GridColorProperty =
        DependencyProperty.Register(
            nameof(GridColor),
            typeof(Color),
            typeof(ModernDesignerCanvas),
            new PropertyMetadata(Color.FromArgb(40, 200, 200, 200), OnGridPropertyChanged));

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(
            nameof(AccentColor),
            typeof(Color),
            typeof(ModernDesignerCanvas),
            new PropertyMetadata(Color.FromRgb(0, 120, 215)));

    public static readonly DependencyProperty ShowRulersProperty =
        DependencyProperty.Register(
            nameof(ShowRulers),
            typeof(bool),
            typeof(ModernDesignerCanvas),
            new PropertyMetadata(false, OnShowRulersChanged));

    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(
            nameof(ZoomLevel),
            typeof(double),
            typeof(ModernDesignerCanvas),
            new PropertyMetadata(1.0, OnZoomLevelChanged));

    #endregion

    #region Properties

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public int GridSize
    {
        get => (int)GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    public Color GridColor
    {
        get => (Color)GetValue(GridColorProperty);
        set => SetValue(GridColorProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public bool ShowRulers
    {
        get => (bool)GetValue(ShowRulersProperty);
        set => SetValue(ShowRulersProperty, value);
    }

    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    protected override int VisualChildrenCount => _visualChildren.Count + base.VisualChildrenCount;

    protected override Visual GetVisualChild(int index)
    {
        if (index < _visualChildren.Count)
        {
            return _visualChildren[index];
        }

        return base.GetVisualChild(index - _visualChildren.Count);
    }

    #endregion

    public ModernDesignerCanvas()
    {
        _visualChildren = new VisualCollection(this);

        // Set default background
        Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
        ClipToBounds = true;
        Focusable = true;

        // Setup animations for smooth transitions
        _fadeInAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        _fadeOutAnimation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        // Enable drag and drop
        AllowDrop = true;
        SetupEventHandlers();

        // Performance: Use hardware acceleration
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    private void SetupEventHandlers()
    {
        // Mouse events for selection
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonDown += OnMouseRightButtonDown;

        // Keyboard events
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        // Drag and drop
        DragEnter += OnDragEnter;
        DragOver += OnDragOver;
        DragLeave += OnDragLeave;
        Drop += OnDrop;

        // Touch support
        IsManipulationEnabled = true;
        ManipulationStarting += OnManipulationStarting;
        ManipulationDelta += OnManipulationDelta;
        ManipulationCompleted += OnManipulationCompleted;

        // Size changes
        SizeChanged += OnSizeChanged;
    }

    #region Grid Rendering

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // Render grid using optimized visual
        if (ShowGrid && _gridNeedsUpdate)
        {
            UpdateGridVisual();
            _gridNeedsUpdate = false;
        }
    }

    private void UpdateGridVisual()
    {
        // Remove old grid visual
        if (_gridVisual != null)
        {
            _visualChildren.Remove(_gridVisual);
        }

        if (!ShowGrid) return;

        _gridVisual = new DrawingVisual();
        using (DrawingContext dc = _gridVisual.RenderOpen())
        {
            DrawGrid(dc);
        }

        _visualChildren.Insert(0, _gridVisual); // Add grid as first visual (behind everything)
    }

    private void DrawGrid(DrawingContext dc)
    {
        var pen = new Pen(new SolidColorBrush(GridColor), 0.5);
        pen.Freeze();

        var majorPen = new Pen(new SolidColorBrush(Color.FromArgb(80, GridColor.R, GridColor.G, GridColor.B)), 1);
        majorPen.Freeze();

        double width = ActualWidth * (1 / ZoomLevel);
        double height = ActualHeight * (1 / ZoomLevel);

        // Draw minor grid lines
        for (double x = 0; x <= width; x += GridSize)
        {
            // Every 5th line is major
            var currentPen = (x % (GridSize * 5) == 0) ? majorPen : pen;
            dc.DrawLine(currentPen, new Point(x, 0), new Point(x, height));
        }

        for (double y = 0; y <= height; y += GridSize)
        {
            var currentPen = (y % (GridSize * 5) == 0) ? majorPen : pen;
            dc.DrawLine(currentPen, new Point(0, y), new Point(width, y));
        }

        // Draw canvas border
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 100, 100, 100)), 2);
        borderPen.Freeze();
        dc.DrawRectangle(null, borderPen, new Rect(0, 0, width, height));
    }

    #endregion

    #region Snap to Grid

    public Point SnapPointToGrid(Point point)
    {
        if (!SnapToGrid) return point;

        return new Point(
            Math.Round(point.X / GridSize) * GridSize,
            Math.Round(point.Y / GridSize) * GridSize);
    }

    public System.Windows.Size SnapSizeToGrid(System.Windows.Size size)
    {
        if (!SnapToGrid) return size;

        return new System.Windows.Size(
            Math.Round(size.Width / GridSize) * GridSize,
            Math.Round(size.Height / GridSize) * GridSize);
    }

    #endregion

    #region Selection Rectangle

    private void StartSelectionRectangle(Point startPoint)
    {
        _selectionStartPoint = startPoint;

        // Create selection rectangle with modern styling
        _selectionRectangle = new Rectangle
        {
            Stroke = new SolidColorBrush(AccentColor),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Fill = new SolidColorBrush(Color.FromArgb(30, AccentColor.R, AccentColor.G, AccentColor.B)),
            IsHitTestVisible = false,
            Opacity = 0
        };

        SetLeft(_selectionRectangle, startPoint.X);
        SetTop(_selectionRectangle, startPoint.Y);
        Children.Add(_selectionRectangle);

        // Animate fade in
        _selectionRectangle.BeginAnimation(OpacityProperty, _fadeInAnimation);
    }

    private void UpdateSelectionRectangle(Point currentPoint)
    {
        if (_selectionRectangle == null || !_selectionStartPoint.HasValue)
            return;

        double x = Math.Min(currentPoint.X, _selectionStartPoint.Value.X);
        double y = Math.Min(currentPoint.Y, _selectionStartPoint.Value.Y);
        double width = Math.Abs(currentPoint.X - _selectionStartPoint.Value.X);
        double height = Math.Abs(currentPoint.Y - _selectionStartPoint.Value.Y);

        SetLeft(_selectionRectangle, x);
        SetTop(_selectionRectangle, y);
        _selectionRectangle.Width = width;
        _selectionRectangle.Height = height;
    }

    private void EndSelectionRectangle()
    {
        if (_selectionRectangle != null)
        {
            // Animate fade out
            var animation = _fadeOutAnimation.Clone();
            animation.Completed += (s, e) =>
            {
                Children.Remove(_selectionRectangle);
                _selectionRectangle = null;
            };
            _selectionRectangle.BeginAnimation(OpacityProperty, animation);
        }

        _selectionStartPoint = null;
    }

    #endregion

    #region Mouse Event Handlers

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();

        var clickedElement = FindVisualParent<DesignerItemControl>(e.OriginalSource as DependencyObject);
        var viewModel = DataContext as DesignerViewModel;

        if (clickedElement != null && clickedElement.DisplayElement != null)
        {
            // Handle element selection
            if (viewModel != null)
            {
                bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                var parameter = (clickedElement.DisplayElement, isCtrlPressed, isShiftPressed);
                viewModel.SelectElementCommand.Execute(parameter);
            }
        }
        else
        {
            // Start selection rectangle on empty canvas
            if (viewModel != null)
            {
                if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    viewModel.SelectionService.ClearSelection();
                }
            }

            StartSelectionRectangle(e.GetPosition(this));
            CaptureMouse();
        }

        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_selectionStartPoint.HasValue && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateSelectionRectangle(e.GetPosition(this));

            // Update selection in ViewModel
            var viewModel = DataContext as DesignerViewModel;
            if (viewModel != null)
            {
                var currentPoint = e.GetPosition(this);
                viewModel.UpdateSelectionRectangleCommand.Execute((currentPoint.X, currentPoint.Y));
            }
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_selectionStartPoint.HasValue)
        {
            var viewModel = DataContext as DesignerViewModel;
            if (viewModel != null)
            {
                viewModel.EndSelectionRectangleCommand.Execute(null);
            }

            EndSelectionRectangle();
            ReleaseMouseCapture();
        }
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Context menu will be handled by XAML
        Focus();
    }

    #endregion

    #region Keyboard Event Handlers

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var viewModel = DataContext as DesignerViewModel;
        if (viewModel == null) return;

        // Handle keyboard shortcuts
        switch (e.Key)
        {
            case Key.Delete:
                viewModel.DeleteSelectedElementCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                viewModel.ClearSelectionCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        // Reserved for future use
    }

    #endregion

    #region Drag and Drop

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("ElementType"))
        {
            e.Effects = DragDropEffects.Copy;

            // Visual feedback
            Background = new SolidColorBrush(Color.FromArgb(20, AccentColor.R, AccentColor.G, AccentColor.B));
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("ElementType"))
        {
            e.Effects = DragDropEffects.Copy;

            // Show drop preview (optional)
            var position = e.GetPosition(this);
            // Could show a ghost element here
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        // Reset visual feedback
        Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("ElementType"))
        {
            var elementType = e.Data.GetData("ElementType") as string;
            var position = e.GetPosition(this);

            if (SnapToGrid)
            {
                position = SnapPointToGrid(position);
            }

            var viewModel = DataContext as DesignerViewModel;
            if (viewModel != null && !string.IsNullOrEmpty(elementType))
            {
                viewModel.AddElementAtPositionCommand.Execute((elementType, position.X, position.Y));
            }
        }

        // Reset visual feedback
        Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
        e.Handled = true;
    }

    #endregion

    #region Touch Support

    private void OnManipulationStarting(object sender, ManipulationStartingEventArgs e)
    {
        e.ManipulationContainer = this;
        e.Mode = ManipulationModes.All;
        e.Handled = true;
    }

    private void OnManipulationDelta(object sender, ManipulationDeltaEventArgs e)
    {
        // Handle pinch to zoom
        if (e.DeltaManipulation.Scale.X != 1.0 || e.DeltaManipulation.Scale.Y != 1.0)
        {
            double newZoom = ZoomLevel * e.DeltaManipulation.Scale.X;
            newZoom = Math.Max(0.1, Math.Min(5.0, newZoom)); // Clamp between 10% and 500%
            ZoomLevel = newZoom;
        }

        e.Handled = true;
    }

    private void OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
    {
        e.Handled = true;
    }

    #endregion

    #region Property Changed Handlers

    private static void OnGridPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModernDesignerCanvas canvas)
        {
            canvas._gridNeedsUpdate = true;
            canvas.InvalidateVisual();
        }
    }

    private static void OnShowRulersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // TODO: Implement rulers
    }

    private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModernDesignerCanvas canvas)
        {
            var transform = new ScaleTransform(canvas.ZoomLevel, canvas.ZoomLevel);
            canvas.LayoutTransform = transform;
            canvas._gridNeedsUpdate = true;
            canvas.InvalidateVisual();
        }
    }

    #endregion

    #region Helpers

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _gridNeedsUpdate = true;
        InvalidateVisual();
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null) return null;

        var parentObject = VisualTreeHelper.GetParent(child);

        if (parentObject is T parent)
            return parent;

        return FindVisualParent<T>(parentObject);
    }

    #endregion
}
