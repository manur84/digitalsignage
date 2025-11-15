using System.Globalization;
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
/// Modern designer canvas with enhanced visual feedback, smooth animations, and optimized performance.
/// Uses DrawingGroup backing store for efficient grid rendering and frozen resources for better performance.
/// </summary>
public class ModernDesignerCanvas : Canvas
{
    #region Private Fields

    private Point? _selectionStartPoint;
    private Rectangle? _selectionRectangle;
    private readonly DoubleAnimation _fadeInAnimation;
    private readonly DoubleAnimation _fadeOutAnimation;

    // Grid rendering optimization with DrawingGroup backing store
    private DrawingGroup? _gridDrawingGroup;
    private bool _gridNeedsUpdate = true;

    // Cached frozen brushes and pens for performance
    private Pen? _gridPen;
    private Pen? _gridMajorPen;
    private Pen? _borderPen;
    private SolidColorBrush? _defaultBackground;
    private SolidColorBrush? _dragOverBackground;

    // Rulers and cursor guides
    private DrawingVisual? _horizontalRulerVisual;
    private DrawingVisual? _verticalRulerVisual;
    private DrawingVisual? _cursorGuideVisual;
    private bool _rulersNeedUpdate = true;
    private Point? _currentMousePosition;

    // Performance optimization
    private readonly VisualCollection _visualChildren;
    private readonly Dictionary<DisplayElement, FrameworkElement> _elementVisualCache = new();

    // Performance flags to minimize unnecessary updates
    private bool _isUpdatingGrid = false;
    private bool _isInBatchUpdate = false;

    #endregion

    #region Constants

    private const double RulerTickSize = 12.0;
    private const string ElementTypeDataFormat = "DesignerElementType";
    private const double MinGridSize = 5.0;
    private const double MaxGridSize = 100.0;

    #endregion

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
            new PropertyMetadata(10, OnGridPropertyChanged, CoerceGridSize));

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
            new PropertyMetadata(Color.FromRgb(0, 120, 215), OnAccentColorChanged));

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
            new PropertyMetadata(1.0, OnZoomLevelChanged, CoerceZoomLevel));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets whether to display the grid overlay.
    /// </summary>
    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>
    /// Gets or sets the grid cell size in pixels.
    /// </summary>
    public int GridSize
    {
        get => (int)GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether elements snap to grid when moved or resized.
    /// </summary>
    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    /// <summary>
    /// Gets or sets the grid line color.
    /// </summary>
    public Color GridColor
    {
        get => (Color)GetValue(GridColorProperty);
        set => SetValue(GridColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the accent color for selection and highlights.
    /// </summary>
    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to display rulers around the canvas.
    /// </summary>
    public bool ShowRulers
    {
        get => (bool)GetValue(ShowRulersProperty);
        set => SetValue(ShowRulersProperty, value);
    }

    /// <summary>
    /// Gets or sets the zoom level (1.0 = 100%).
    /// </summary>
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

    #region Constructor

    public ModernDesignerCanvas()
    {
        _visualChildren = new VisualCollection(this);

        // Initialize frozen brushes for performance
        InitializeFrozenResources();

        // Set default background
        Background = _defaultBackground;
        ClipToBounds = true;
        Focusable = true;

        // Setup animations for smooth transitions
        _fadeInAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        _fadeInAnimation.Freeze();

        _fadeOutAnimation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        _fadeOutAnimation.Freeze();

        // Enable drag and drop
        AllowDrop = true;
        SetupEventHandlers();

        // Performance: Use hardware acceleration
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

        // Initialize grid drawing group for efficient updates
        _gridDrawingGroup = new DrawingGroup();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes frozen resources for optimal rendering performance.
    /// </summary>
    private void InitializeFrozenResources()
    {
        // Create and freeze default background
        _defaultBackground = new SolidColorBrush(Color.FromRgb(245, 245, 245));
        _defaultBackground.Freeze();

        _dragOverBackground = new SolidColorBrush(Color.FromArgb(20, 0, 120, 215));
        _dragOverBackground.Freeze();

        // Grid pens will be created when grid properties change
        UpdateGridPens();
    }

    /// <summary>
    /// Updates frozen grid pens when grid properties change.
    /// </summary>
    private void UpdateGridPens()
    {
        // Create new frozen pens based on current grid color
        _gridPen = new Pen(new SolidColorBrush(GridColor), 0.5);
        _gridPen.Freeze();

        var majorColor = Color.FromArgb(80, GridColor.R, GridColor.G, GridColor.B);
        _gridMajorPen = new Pen(new SolidColorBrush(majorColor), 1);
        _gridMajorPen.Freeze();

        var borderColor = Color.FromArgb(100, 100, 100, 100);
        _borderPen = new Pen(new SolidColorBrush(borderColor), 2);
        _borderPen.Freeze();
    }

    private void SetupEventHandlers()
    {
        // Mouse events for selection
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonDown += OnMouseRightButtonDown;
        MouseLeave += OnMouseLeave;

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

    #endregion

    #region Grid Rendering

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // Use DrawingGroup backing store for efficient grid rendering
        if (ShowGrid)
        {
            // Only update the drawing group if grid needs update
            if (_gridNeedsUpdate && !_isUpdatingGrid)
            {
                _isUpdatingGrid = true;
                UpdateGridDrawingGroup();
                _gridNeedsUpdate = false;
                _isUpdatingGrid = false;
            }

            // Draw the cached grid drawing group
            if (_gridDrawingGroup != null)
            {
                dc.DrawDrawing(_gridDrawingGroup);
            }
        }

        // Update ruler visuals if needed
        if (_rulersNeedUpdate && ShowRulers)
        {
            UpdateRulerVisuals();
        }
    }

    /// <summary>
    /// Updates the grid drawing group backing store.
    /// This is only called when grid properties change, not on every render.
    /// </summary>
    private void UpdateGridDrawingGroup()
    {
        if (_gridDrawingGroup == null)
        {
            _gridDrawingGroup = new DrawingGroup();
        }

        using (DrawingContext dc = _gridDrawingGroup.Open())
        {
            DrawGrid(dc);
        }
    }

    /// <summary>
    /// Draws the grid lines to the provided drawing context.
    /// </summary>
    private void DrawGrid(DrawingContext dc)
    {
        if (_gridPen == null || _gridMajorPen == null || _borderPen == null)
        {
            UpdateGridPens();
        }

        double width = ActualWidth * (1 / ZoomLevel);
        double height = ActualHeight * (1 / ZoomLevel);

        if (width <= 0 || height <= 0)
            return;

        // Use guidelines for pixel-perfect rendering
        var guidelines = new GuidelineSet();

        // Draw vertical lines
        for (double x = 0; x <= width; x += GridSize)
        {
            guidelines.GuidelinesX.Add(x);
            var currentPen = (x % (GridSize * 5) == 0) ? _gridMajorPen : _gridPen;
            dc.DrawLine(currentPen, new Point(x, 0), new Point(x, height));
        }

        // Draw horizontal lines
        for (double y = 0; y <= height; y += GridSize)
        {
            guidelines.GuidelinesY.Add(y);
            var currentPen = (y % (GridSize * 5) == 0) ? _gridMajorPen : _gridPen;
            dc.DrawLine(currentPen, new Point(0, y), new Point(width, y));
        }

        dc.PushGuidelineSet(guidelines);

        // Draw canvas border
        dc.DrawRectangle(null, _borderPen, new Rect(0, 0, width, height));

        dc.Pop();
    }

    /// <summary>
    /// Begins a batch update to minimize rendering calls.
    /// </summary>
    public void BeginBatchUpdate()
    {
        _isInBatchUpdate = true;
    }

    /// <summary>
    /// Ends a batch update and triggers a single render update.
    /// </summary>
    public void EndBatchUpdate()
    {
        _isInBatchUpdate = false;
        if (_gridNeedsUpdate)
        {
            InvalidateVisual();
        }
    }

    #endregion

    #region Snap to Grid

    /// <summary>
    /// Snaps a point to the nearest grid position if snap to grid is enabled.
    /// </summary>
    public Point SnapPointToGrid(Point point)
    {
        if (!SnapToGrid) return point;

        return new Point(
            Math.Round(point.X / GridSize) * GridSize,
            Math.Round(point.Y / GridSize) * GridSize);
    }

    /// <summary>
    /// Snaps a size to the grid if snap to grid is enabled.
    /// </summary>
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
        var strokeBrush = new SolidColorBrush(AccentColor);
        strokeBrush.Freeze();

        var fillBrush = new SolidColorBrush(Color.FromArgb(30, AccentColor.R, AccentColor.G, AccentColor.B));
        fillBrush.Freeze();

        _selectionRectangle = new Rectangle
        {
            Stroke = strokeBrush,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Fill = fillBrush,
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

        if (ShowRulers)
        {
            _currentMousePosition = e.GetPosition(this);
            UpdateCursorGuides();
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

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_currentMousePosition.HasValue)
        {
            _currentMousePosition = null;
            UpdateCursorGuides();
        }
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
        var elementType = GetElementTypeFromData(e.Data);
        if (!string.IsNullOrEmpty(elementType))
        {
            e.Effects = DragDropEffects.Copy;
            Background = _dragOverBackground;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var elementType = GetElementTypeFromData(e.Data);
        e.Effects = string.IsNullOrEmpty(elementType) ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        // Reset visual feedback
        Background = _defaultBackground;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var elementType = GetElementTypeFromData(e.Data);
        if (!string.IsNullOrEmpty(elementType))
        {
            var position = e.GetPosition(this);

            if (SnapToGrid)
            {
                position = SnapPointToGrid(position);
            }

            if (DataContext is DesignerViewModel viewModel)
            {
                viewModel.AddElementAtPositionCommand.Execute((elementType, position.X, position.Y));
            }
        }

        Background = _defaultBackground;
        e.Handled = true;
    }

    private static string? GetElementTypeFromData(IDataObject data)
    {
        if (data.GetDataPresent(ElementTypeDataFormat))
            return data.GetData(ElementTypeDataFormat) as string;

        if (data.GetDataPresent("ElementType"))
            return data.GetData("ElementType") as string;

        if (data.GetDataPresent(DataFormats.StringFormat))
            return data.GetData(DataFormats.StringFormat)?.ToString();

        if (data.GetDataPresent(typeof(string)))
            return data.GetData(typeof(string)) as string;

        return null;
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
            // Update grid pens if color changed
            if (e.Property == GridColorProperty)
            {
                canvas.UpdateGridPens();
            }

            // Mark grid as needing update
            canvas._gridNeedsUpdate = true;

            // Only invalidate visual if not in batch update mode
            if (!canvas._isInBatchUpdate)
            {
                canvas.InvalidateVisual();
            }
        }
    }

    private static void OnAccentColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModernDesignerCanvas canvas)
        {
            // Update drag over background with new accent color
            var newAccent = (Color)e.NewValue;
            canvas._dragOverBackground = new SolidColorBrush(Color.FromArgb(20, newAccent.R, newAccent.G, newAccent.B));
            canvas._dragOverBackground.Freeze();
        }
    }

    private static void OnShowRulersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModernDesignerCanvas canvas)
        {
            canvas._rulersNeedUpdate = true;
            if ((bool)e.NewValue)
            {
                canvas.UpdateRulerVisuals();
                canvas.UpdateCursorGuides();
            }
            else
            {
                canvas.RemoveVisual(ref canvas._horizontalRulerVisual);
                canvas.RemoveVisual(ref canvas._verticalRulerVisual);
                canvas.RemoveVisual(ref canvas._cursorGuideVisual);
            }
        }
    }

    private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModernDesignerCanvas canvas)
        {
            var transform = new ScaleTransform(canvas.ZoomLevel, canvas.ZoomLevel);
            canvas.LayoutTransform = transform;

            // Mark grid as needing update
            canvas._gridNeedsUpdate = true;
            canvas._rulersNeedUpdate = true;

            // Only invalidate if not in batch update
            if (!canvas._isInBatchUpdate)
            {
                canvas.InvalidateVisual();
            }
        }
    }

    #endregion

    #region Coercion Callbacks

    private static object CoerceGridSize(DependencyObject d, object value)
    {
        var size = (int)value;
        return Math.Max(MinGridSize, Math.Min(MaxGridSize, size));
    }

    private static object CoerceZoomLevel(DependencyObject d, object value)
    {
        var zoom = (double)value;
        return Math.Max(0.1, Math.Min(5.0, zoom));
    }

    #endregion

    #region Helpers

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Mark grid and rulers as needing update
        _gridNeedsUpdate = true;
        _rulersNeedUpdate = true;

        // Only invalidate if not in batch update
        if (!_isInBatchUpdate)
        {
            InvalidateVisual();
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null) return null;

        var parentObject = VisualTreeHelper.GetParent(child);

        if (parentObject is T parent)
            return parent;

        return FindVisualParent<T>(parentObject);
    }

    private void RemoveVisual(ref DrawingVisual? visual)
    {
        if (visual != null)
        {
            _visualChildren.Remove(visual);
            visual = null;
        }
    }

    private void UpdateRulerVisuals()
    {
        RemoveVisual(ref _horizontalRulerVisual);
        RemoveVisual(ref _verticalRulerVisual);

        if (!ShowRulers || ActualWidth <= 0 || ActualHeight <= 0)
        {
            _rulersNeedUpdate = false;
            return;
        }

        _horizontalRulerVisual = new DrawingVisual();
        using (var dc = _horizontalRulerVisual.RenderOpen())
        {
            DrawHorizontalRuler(dc);
        }
        _visualChildren.Add(_horizontalRulerVisual);

        _verticalRulerVisual = new DrawingVisual();
        using (var dc = _verticalRulerVisual.RenderOpen())
        {
            DrawVerticalRuler(dc);
        }
        _visualChildren.Add(_verticalRulerVisual);

        _rulersNeedUpdate = false;
    }

    private void DrawHorizontalRuler(DrawingContext dc)
    {
        // Create and freeze pens for ruler
        var linePen = new Pen(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), 1);
        linePen.Freeze();

        var tickPen = new Pen(new SolidColorBrush(Color.FromArgb(140, AccentColor.R, AccentColor.G, AccentColor.B)), 1);
        tickPen.Freeze();

        dc.DrawLine(linePen, new Point(0, 0), new Point(ActualWidth, 0));

        double step = Math.Max(GridSize * ZoomLevel, 8);
        int index = 0;
        for (double x = 0; x <= ActualWidth; x += step, index++)
        {
            bool isMajor = index % 5 == 0;
            double tickHeight = isMajor ? RulerTickSize : RulerTickSize / 2;
            dc.DrawLine(tickPen, new Point(x, 0), new Point(x, tickHeight));

            if (isMajor)
            {
                DrawMeasurementLabel(dc, new Point(x + 2, tickHeight + 2), x / ZoomLevel);
            }
        }
    }

    private void DrawVerticalRuler(DrawingContext dc)
    {
        // Create and freeze pens for ruler
        var linePen = new Pen(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), 1);
        linePen.Freeze();

        var tickPen = new Pen(new SolidColorBrush(Color.FromArgb(140, AccentColor.R, AccentColor.G, AccentColor.B)), 1);
        tickPen.Freeze();

        dc.DrawLine(linePen, new Point(0, 0), new Point(0, ActualHeight));

        double step = Math.Max(GridSize * ZoomLevel, 8);
        int index = 0;
        for (double y = 0; y <= ActualHeight; y += step, index++)
        {
            bool isMajor = index % 5 == 0;
            double tickWidth = isMajor ? RulerTickSize : RulerTickSize / 2;
            dc.DrawLine(tickPen, new Point(0, y), new Point(tickWidth, y));

            if (isMajor)
            {
                DrawMeasurementLabel(dc, new Point(tickWidth + 2, y + 2), y / ZoomLevel);
            }
        }
    }

    private void DrawMeasurementLabel(DrawingContext dc, Point location, double logicalValue)
    {
        var formatted = new FormattedText(
            Math.Round(logicalValue).ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            9,
            Brushes.Gray,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(formatted, location);
    }

    private void UpdateCursorGuides()
    {
        RemoveVisual(ref _cursorGuideVisual);

        if (!ShowRulers || !_currentMousePosition.HasValue || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var position = _currentMousePosition.Value;
        if (position.X < 0 || position.X > ActualWidth || position.Y < 0 || position.Y > ActualHeight)
            return;

        _cursorGuideVisual = new DrawingVisual();
        using (var dc = _cursorGuideVisual.RenderOpen())
        {
            var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(90, AccentColor.R, AccentColor.G, AccentColor.B)), 1)
            {
                DashStyle = DashStyles.Dot
            };
            guidePen.Freeze();

            dc.DrawLine(guidePen, new Point(position.X, 0), new Point(position.X, ActualHeight));
            dc.DrawLine(guidePen, new Point(0, position.Y), new Point(ActualWidth, position.Y));

            var logicalX = Math.Round(position.X / ZoomLevel);
            var logicalY = Math.Round(position.Y / ZoomLevel);
            var label = $"X:{logicalX}  Y:{logicalY}";
            var formatted = new FormattedText(
                label,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
            background.Freeze();

            var rect = new Rect(position.X + 10, position.Y + 10, formatted.Width + 10, formatted.Height + 6);
            dc.DrawRectangle(background, null, rect);
            dc.DrawText(formatted, new Point(rect.X + 5, rect.Y + 3));
        }

        _visualChildren.Add(_cursorGuideVisual);
    }

    #endregion

    #region Public Methods for Performance Optimization

    /// <summary>
    /// Forces an immediate grid update if needed.
    /// Use sparingly as this bypasses the batch update optimization.
    /// </summary>
    public void ForceGridUpdate()
    {
        if (_gridNeedsUpdate)
        {
            UpdateGridDrawingGroup();
            _gridNeedsUpdate = false;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Clears the element visual cache.
    /// Call this when elements are removed from the layout.
    /// </summary>
    public void ClearElementCache()
    {
        _elementVisualCache.Clear();
    }

    #endregion
}