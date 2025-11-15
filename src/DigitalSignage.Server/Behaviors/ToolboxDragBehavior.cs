using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DigitalSignage.Server.Behaviors;

/// <summary>
/// Attached behavior to enable drag functionality from toolbox buttons
/// </summary>
public static class ToolboxDragBehavior
{
    #region ElementType Attached Property

    public static readonly DependencyProperty ElementTypeProperty =
        DependencyProperty.RegisterAttached(
            "ElementType",
            typeof(string),
            typeof(ToolboxDragBehavior),
            new PropertyMetadata(null));

    public static void SetElementType(DependencyObject element, string value)
    {
        element.SetValue(ElementTypeProperty, value);
    }

    public static string GetElementType(DependencyObject element)
    {
        return (string)element.GetValue(ElementTypeProperty);
    }

    #endregion

    #region IsDragEnabled Attached Property

    public static readonly DependencyProperty IsDragEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsDragEnabled",
            typeof(bool),
            typeof(ToolboxDragBehavior),
            new PropertyMetadata(false, OnIsDragEnabledChanged));

    public static void SetIsDragEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsDragEnabledProperty, value);
    }

    public static bool GetIsDragEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsDragEnabledProperty);
    }

    private static void OnIsDragEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
            if ((bool)e.NewValue)
            {
                element.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                element.PreviewMouseMove += OnPreviewMouseMove;
                element.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
                element.QueryContinueDrag += OnQueryContinueDrag;
            }
            else
            {
                element.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                element.PreviewMouseMove -= OnPreviewMouseMove;
                element.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
                element.QueryContinueDrag -= OnQueryContinueDrag;
            }
        }
    }

    #endregion

    #region Private Fields

    private static Point? _dragStartPoint;
    private static bool _isDragging;

    #endregion

    #region Event Handlers

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _dragStartPoint.HasValue && !_isDragging)
        {
            var currentPosition = e.GetPosition(null);
            var diff = _dragStartPoint.Value - currentPosition;

            // Start drag if mouse has moved minimum distance
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragging = true;
                StartDrag(sender as FrameworkElement);
            }
        }
    }

    private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = null;
        _isDragging = false;
    }

    private static void OnQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        // Cancel drag if escape is pressed
        if (e.EscapePressed)
        {
            e.Action = DragAction.Cancel;
            _isDragging = false;
            _dragStartPoint = null;
            e.Handled = true;
        }
    }

    private static void StartDrag(FrameworkElement? element)
    {
        if (element == null) return;

        var elementType = GetElementType(element);
        if (string.IsNullOrEmpty(elementType)) return;

        // Create data object with element type
        var dragData = new DataObject("DesignerElementType", elementType);

        // Create a visual representation for the drag cursor
        var dragAdorner = CreateDragAdorner(element, elementType);
        if (dragAdorner != null)
        {
            dragData.SetData("DragAdorner", dragAdorner);
        }

        // Start the drag operation
        DragDrop.DoDragDrop(element, dragData, DragDropEffects.Copy);

        _isDragging = false;
        _dragStartPoint = null;
    }

    private static Visual? CreateDragAdorner(FrameworkElement element, string elementType)
    {
        // Create a simple visual representation based on element type
        var container = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(128, 100, 149, 237)), // Semi-transparent cornflower blue
            BorderBrush = Brushes.DarkBlue,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8)
        };

        var textBlock = new TextBlock
        {
            Text = GetElementDisplayName(elementType),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 14
        };

        container.Child = textBlock;
        container.Measure(new Size(200, 100));
        container.Arrange(new Rect(0, 0, container.DesiredSize.Width, container.DesiredSize.Height));

        return container;
    }

    private static string GetElementDisplayName(string elementType)
    {
        return elementType switch
        {
            "text" => "Text Element",
            "image" => "Image",
            "media" => "Media Library",
            "rectangle" => "Rectangle",
            "circle" => "Circle",
            "qrcode" => "QR Code",
            "table" => "Table",
            "datetime" => "Date/Time",
            _ => elementType
        };
    }

    #endregion
}