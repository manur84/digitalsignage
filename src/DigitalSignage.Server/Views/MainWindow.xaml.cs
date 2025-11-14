using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Behaviors;
using DigitalSignage.Server.ViewModels;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Main window for the Digital Signage Manager application
/// Follows MVVM pattern with minimal code-behind
/// </summary>
public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow>? _logger;
    private ElementSelectionBehavior? _selectionBehavior;
    private Point _dragStartPosition;
    private bool _isDragging;
    private DisplayElement? _draggingElement;

    public MainWindow(MainViewModel viewModel, ILogger<MainWindow>? logger = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _logger = logger;

        // Initialize element selection behavior
        _selectionBehavior = new ElementSelectionBehavior(this, viewModel.Designer);

        _logger?.LogInformation("MainWindow initialized with element selection behavior");
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    /// <summary>
    /// Handles keyboard shortcuts for the Designer
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Ignore keyboard shortcuts when typing in text input controls
        if (e.OriginalSource is System.Windows.Controls.TextBox ||
            e.OriginalSource is System.Windows.Controls.ComboBox)
        {
            return;
        }

        // Single keys without modifiers
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            switch (e.Key)
            {
                case Key.T:
                    ViewModel?.Designer?.AddTextElementCommand?.Execute(null);
                    e.Handled = true;
                    break;

                case Key.I:
                    ViewModel?.Designer?.AddImageElementCommand?.Execute(null);
                    e.Handled = true;
                    break;

                case Key.R:
                    ViewModel?.Designer?.AddRectangleElementCommand?.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Delete:
                    ViewModel?.Designer?.DeleteSelectedElementCommand?.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Escape:
                    ViewModel?.Designer?.ClearSelectionCommand?.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
        // Keys with Ctrl modifier
        else if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.D:
                    ViewModel?.Designer?.DuplicateSelectedElementCommand?.Execute(null);
                    e.Handled = true;
                    break;

                case Key.A:
                    ViewModel?.Designer?.SelectAllCommand?.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Z:
                    ViewModel?.Designer?.UndoCommand?.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Y:
                    ViewModel?.Designer?.RedoCommand?.Execute(null);
                    e.Handled = true;
                    break;

                case Key.S:
                    ViewModel?.Designer?.SaveLayoutCommand?.Execute(null);
                    e.Handled = true;
                    break;

                case Key.OemPlus:
                case Key.Add:
                    ViewModel?.Designer?.ZoomInCommand?.Execute(null);
                    e.Handled = true;
                    break;

                case Key.OemMinus:
                case Key.Subtract:
                    ViewModel?.Designer?.ZoomOutCommand?.Execute(null);
                    e.Handled = true;
                    break;

                case Key.D0:
                case Key.NumPad0:
                    ViewModel?.Designer?.ZoomToFitCommand?.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }

    /// <summary>
    /// Handles the Exit menu item click
    /// </summary>
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Handles mouse left button down on designer elements
    /// </summary>
    private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Debug.WriteLine($"[DEBUG] Element_MouseLeftButtonDown fired! Sender: {sender?.GetType().Name}");
        _logger?.LogDebug("Element_MouseLeftButtonDown - Sender: {SenderType}, Source: {SourceType}",
            sender?.GetType().Name, e.Source?.GetType().Name);

        if (sender is FrameworkElement element && element.Tag is DisplayElement displayElement)
        {
            _logger?.LogInformation("Element clicked: Type={ElementType}, Position=({X}, {Y})",
                displayElement.Type, displayElement.Position.X, displayElement.Position.Y);

            // Check for modifier keys
            bool isCtrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            Debug.WriteLine($"[DEBUG] Element: {displayElement.Type}, Ctrl: {isCtrlPressed}, Shift: {isShiftPressed}");

            // MULTI-SELECTION SUPPORT: Pass modifier keys to SelectElementCommand
            // - Ctrl+Click: Toggle selection (add/remove from selection)
            // - Shift+Click: Range selection (select all elements between last and current)
            // - Click: Select single element (clear previous selection)
            if (isCtrlPressed || isShiftPressed)
            {
                // Multi-selection mode: use SelectionService directly
                if (isCtrlPressed)
                {
                    ViewModel?.Designer?.SelectionService?.ToggleSelection(displayElement);
                    _logger?.LogDebug("Toggle selection for element: {ElementType}", displayElement.Type);
                }
                else if (isShiftPressed && ViewModel?.Designer?.SelectionService?.PrimarySelection != null)
                {
                    // Shift+Click: Range selection (if implemented in SelectionService)
                    ViewModel?.Designer?.SelectionService?.SelectSingle(displayElement);
                    _logger?.LogDebug("Shift+Click selection for element: {ElementType}", displayElement.Type);
                }
            }
            else
            {
                // Single selection mode: clear previous selection
                ViewModel?.Designer?.SelectElementCommand?.Execute(displayElement);
                _logger?.LogDebug("Single selection for element: {ElementType}", displayElement.Type);
            }

            // Start drag operation
            _dragStartPosition = e.GetPosition(DesignerCanvas);
            _isDragging = true;
            _draggingElement = displayElement;
            element.CaptureMouse();

            Debug.WriteLine($"[DEBUG] Drag started at position: {_dragStartPosition}");

            e.Handled = true;
        }
        else
        {
            _logger?.LogWarning("Element_MouseLeftButtonDown called but element or tag is null");
        }
    }

    /// <summary>
    /// Handles mouse move for dragging elements
    /// SUPPORTS MULTI-ELEMENT DRAGGING: Moves all selected elements together
    /// </summary>
    private void Element_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed && _draggingElement != null)
        {
            var currentPosition = e.GetPosition(DesignerCanvas);
            var delta = currentPosition - _dragStartPosition;

            // MULTI-ELEMENT DRAG SUPPORT: Get all selected elements
            var selectedElements = ViewModel?.Designer?.SelectionService?.SelectedElements;
            var elementsToMove = selectedElements != null && selectedElements.Count > 0
                ? selectedElements.ToList()
                : new List<DisplayElement> { _draggingElement };

            Debug.WriteLine($"[DEBUG] Dragging {elementsToMove.Count} elements, Delta: ({delta.X:F2}, {delta.Y:F2})");

            // Calculate new position for primary element (for snapping)
            var newX = _draggingElement.Position.X + delta.X;
            var newY = _draggingElement.Position.Y + delta.Y;

            // Snap to grid if enabled (NOT when Ctrl is pressed, as Ctrl is for multi-select)
            if (ViewModel?.Designer?.SnapToGrid == true)
            {
                var gridSize = ViewModel.Designer.GridSize;
                var snappedX = Math.Round(newX / gridSize) * gridSize;
                var snappedY = Math.Round(newY / gridSize) * gridSize;

                // Adjust delta based on snapping
                delta = new Vector(snappedX - _draggingElement.Position.X, snappedY - _draggingElement.Position.Y);
            }

            // Move all selected elements by the same delta
            foreach (var element in elementsToMove)
            {
                var elementNewX = element.Position.X + delta.X;
                var elementNewY = element.Position.Y + delta.Y;

                // Ensure element stays within canvas bounds
                var canvasWidth = ViewModel?.Designer?.CurrentLayout?.Resolution?.Width ?? 1920;
                var canvasHeight = ViewModel?.Designer?.CurrentLayout?.Resolution?.Height ?? 1080;
                elementNewX = Math.Max(0, Math.Min(elementNewX, canvasWidth - element.Size.Width));
                elementNewY = Math.Max(0, Math.Min(elementNewY, canvasHeight - element.Size.Height));

                // Update position
                element.Position.X = elementNewX;
                element.Position.Y = elementNewY;
            }

            _dragStartPosition = currentPosition;
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles mouse left button up to end dragging
    /// </summary>
    private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            Debug.WriteLine("[DEBUG] Element_MouseLeftButtonUp - Ending drag operation");
            _logger?.LogDebug("Drag operation completed");

            _isDragging = false;
            _draggingElement = null;

            if (sender is FrameworkElement element)
            {
                element.ReleaseMouseCapture();
            }

            e.Handled = true;
        }
    }

    /// <summary>
    /// Clean up resources when window is closing
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _selectionBehavior?.Detach();
        _selectionBehavior = null;
        base.OnClosed(e);
    }

    /// <summary>
    /// Copies selected log entries to clipboard
    /// </summary>
    private void CopyLogsToClipboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu &&
                contextMenu.PlacementTarget is DataGrid dataGrid)
            {
                var selectedLogs = dataGrid.SelectedItems.Cast<LogEntry>().ToList();

                if (selectedLogs.Any())
                {
                    var logText = new System.Text.StringBuilder();
                    foreach (var log in selectedLogs.OrderBy(l => l.Timestamp))
                    {
                        logText.AppendLine($"[{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{log.Level,-8}] [{log.ClientName,-20}] {log.Message}");
                        if (!string.IsNullOrEmpty(log.Exception))
                        {
                            logText.AppendLine($"    Exception: {log.Exception}");
                        }
                    }

                    Clipboard.SetText(logText.ToString());
                    _logger?.LogInformation("Copied {Count} log entries to clipboard", selectedLogs.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to copy logs to clipboard");
            MessageBox.Show($"Failed to copy logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Shows detailed information for a log entry
    /// </summary>
    private void ShowLogDetails_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu &&
                contextMenu.PlacementTarget is DataGrid dataGrid)
            {
                var selectedLog = dataGrid.SelectedItem as LogEntry;

                if (selectedLog != null)
                {
                    var details = new System.Text.StringBuilder();
                    details.AppendLine($"Timestamp: {selectedLog.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                    details.AppendLine($"Level: {selectedLog.Level}");
                    details.AppendLine($"Client ID: {selectedLog.ClientId}");
                    details.AppendLine($"Client Name: {selectedLog.ClientName}");
                    details.AppendLine($"Source: {selectedLog.Source}");
                    details.AppendLine($"\nMessage:");
                    details.AppendLine(selectedLog.Message);

                    if (!string.IsNullOrEmpty(selectedLog.Exception))
                    {
                        details.AppendLine($"\nException:");
                        details.AppendLine(selectedLog.Exception);
                    }

                    MessageBox.Show(details.ToString(), "Log Entry Details", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show log details");
            MessageBox.Show($"Failed to show log details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
