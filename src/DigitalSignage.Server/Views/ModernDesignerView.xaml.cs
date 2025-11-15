using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DigitalSignage.Server.ViewModels;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Code-behind for the Modern Designer View
/// </summary>
public partial class ModernDesignerView : UserControl
{
    private DesignerViewModel? ViewModel => DataContext as DesignerViewModel;
    private Point _dragStartPoint;
    private bool _isDragging;
    private Dictionary<DisplayElement, Point> _originalPositions = new();

    public ModernDesignerView()
    {
        InitializeComponent();

        // Setup event handlers for advanced drag-drop and selection
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to designer canvas events
        if (DesignerCanvas != null)
        {
            DesignerCanvas.PreviewMouseLeftButtonDown += OnCanvasMouseLeftButtonDown;
            DesignerCanvas.PreviewMouseMove += OnCanvasMouseMove;
            DesignerCanvas.PreviewMouseLeftButtonUp += OnCanvasMouseLeftButtonUp;
            DesignerCanvas.KeyDown += OnCanvasKeyDown;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Unsubscribe from events to prevent memory leaks
        if (DesignerCanvas != null)
        {
            DesignerCanvas.PreviewMouseLeftButtonDown -= OnCanvasMouseLeftButtonDown;
            DesignerCanvas.PreviewMouseMove -= OnCanvasMouseMove;
            DesignerCanvas.PreviewMouseLeftButtonUp -= OnCanvasMouseLeftButtonUp;
            DesignerCanvas.KeyDown -= OnCanvasKeyDown;
        }
    }

    #region Mouse Event Handlers

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null) return;

        // Get the clicked element
        var hitTest = e.OriginalSource as FrameworkElement;
        var designerItem = FindParent<Controls.DesignerItemControl>(hitTest);

        if (designerItem?.DisplayElement != null)
        {
            _dragStartPoint = e.GetPosition(DesignerCanvas);

            // Handle selection with modifier keys
            bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (!designerItem.DisplayElement.IsSelected)
            {
                // Select the element
                var parameter = (designerItem.DisplayElement, isCtrlPressed, isShiftPressed);
                ViewModel.SelectElementCommand.Execute(parameter);
            }
            else if (isCtrlPressed)
            {
                // Toggle selection
                var parameter = (designerItem.DisplayElement, isCtrlPressed, isShiftPressed);
                ViewModel.SelectElementCommand.Execute(parameter);
            }

            // Prepare for potential drag operation
            if (ViewModel.SelectionService.SelectedElements.Count > 0)
            {
                // Store original positions for all selected elements
                _originalPositions.Clear();
                foreach (var element in ViewModel.SelectionService.SelectedElements)
                {
                    _originalPositions[element] = new Point(element.Position.X, element.Position.Y);
                }
            }

            e.Handled = true;
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (ViewModel == null) return;

        if (e.LeftButton == MouseButtonState.Pressed && _originalPositions.Count > 0)
        {
            var currentPoint = e.GetPosition(DesignerCanvas);
            var deltaX = currentPoint.X - _dragStartPoint.X;
            var deltaY = currentPoint.Y - _dragStartPoint.Y;

            // Start dragging if moved enough
            if (!_isDragging && (Math.Abs(deltaX) > 3 || Math.Abs(deltaY) > 3))
            {
                _isDragging = true;
                DesignerCanvas.CaptureMouse();
            }

            if (_isDragging)
            {
                // Move all selected elements
                foreach (var kvp in _originalPositions)
                {
                    var element = kvp.Key;
                    var originalPos = kvp.Value;

                    double newX = originalPos.X + deltaX;
                    double newY = originalPos.Y + deltaY;

                    // Apply snap to grid if enabled
                    if (ViewModel.SnapToGrid)
                    {
                        var snappedPoint = DesignerCanvas.SnapPointToGrid(new Point(newX, newY));
                        newX = snappedPoint.X;
                        newY = snappedPoint.Y;
                    }

                    // Update element position
                    element.Position = new Position { X = newX, Y = newY };
                }

                ViewModel.HasUnsavedChanges = true;
                e.Handled = true;
            }
        }
    }

    private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && ViewModel != null)
        {
            // Complete the drag operation
            _isDragging = false;
            DesignerCanvas.ReleaseMouseCapture();

            // Create undo command for the move operation
            if (_originalPositions.Count > 0)
            {
                // Record the move in command history
                foreach (var kvp in _originalPositions)
                {
                    var element = kvp.Key;
                    var originalPos = kvp.Value;
                    var newPos = element.Position;

                    if (originalPos.X != newPos.X || originalPos.Y != newPos.Y)
                    {
                        // Element was moved, record it for undo/redo
                        var moveCommand = new Commands.MoveElementCommand(
                            element,
                            new Position { X = originalPos.X, Y = originalPos.Y },
                            new Position { X = newPos.X, Y = newPos.Y });
                        ViewModel.CommandHistory.ExecuteCommand(moveCommand);
                    }
                }
            }

            _originalPositions.Clear();
            e.Handled = true;
        }
    }

    #endregion

    #region Keyboard Event Handlers

    private void OnCanvasKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;

        // Handle keyboard shortcuts
        bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        switch (e.Key)
        {
            case Key.A when isCtrlPressed:
                ViewModel.SelectAllCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Delete:
                ViewModel.DeleteSelectedElementCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Escape:
                ViewModel.ClearSelectionCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.C when isCtrlPressed:
                ViewModel.CopyCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.V when isCtrlPressed:
                ViewModel.PasteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.X when isCtrlPressed:
                ViewModel.CutCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Z when isCtrlPressed:
                ViewModel.UndoCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Y when isCtrlPressed:
                ViewModel.RedoCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.D when isCtrlPressed:
                ViewModel.DuplicateSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.G when isCtrlPressed && !isShiftPressed:
                ViewModel.GroupSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.G when isCtrlPressed && isShiftPressed:
                ViewModel.UngroupSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            // Arrow keys for nudging
            case Key.Left:
            case Key.Right:
            case Key.Up:
            case Key.Down:
                HandleArrowKeyMovement(e.Key, isShiftPressed);
                e.Handled = true;
                break;
        }
    }

    private void HandleArrowKeyMovement(Key key, bool largeMove)
    {
        if (ViewModel == null || ViewModel.SelectionService.SelectedElements.Count == 0)
            return;

        double moveAmount = largeMove ? 10 : 1;
        double deltaX = 0, deltaY = 0;

        switch (key)
        {
            case Key.Left:
                deltaX = -moveAmount;
                break;
            case Key.Right:
                deltaX = moveAmount;
                break;
            case Key.Up:
                deltaY = -moveAmount;
                break;
            case Key.Down:
                deltaY = moveAmount;
                break;
        }

        // Move all selected elements
        foreach (var element in ViewModel.SelectionService.SelectedElements)
        {
            var originalPos = new Position { X = element.Position.X, Y = element.Position.Y };
            element.Position = new Position
            {
                X = element.Position.X + deltaX,
                Y = element.Position.Y + deltaY
            };

            // Record the move for undo
            var moveCommand = new Commands.MoveElementCommand(element, originalPos, element.Position);
            ViewModel.CommandHistory.ExecuteCommand(moveCommand);
        }

        ViewModel.HasUnsavedChanges = true;
    }

    #endregion

    #region Helper Methods

    private T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null) return null;

        DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);

        if (parentObject == null) return null;

        if (parentObject is T parent)
            return parent;

        return FindParent<T>(parentObject);
    }

    #endregion
}