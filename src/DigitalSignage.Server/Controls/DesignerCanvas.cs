using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// Custom canvas for the visual designer with grid and snap-to-grid functionality
/// </summary>
public class DesignerCanvas : Canvas
{
    private bool _showGrid = true;
    private bool _snapToGrid = true;
    private int _gridSize = 10;
    private Point? _selectionStartPoint;
    private Rectangle? _selectionRectangle;

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

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
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
        // Start selection rectangle
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            _selectionStartPoint = e.GetPosition(this);
            CaptureMouse();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_selectionStartPoint.HasValue && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPoint = e.GetPosition(this);
            UpdateSelectionRectangle(_selectionStartPoint.Value, currentPoint);
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_selectionStartPoint.HasValue)
        {
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
}
