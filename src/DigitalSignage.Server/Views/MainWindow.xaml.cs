using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Behaviors;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Main window for the Digital Signage Manager application
/// Follows MVVM pattern with minimal code-behind
/// </summary>
public partial class MainWindow : Window
{
    private ElementSelectionBehavior? _selectionBehavior;
    private Point _dragStartPosition;
    private bool _isDragging;
    private DisplayElement? _draggingElement;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Initialize element selection behavior
        _selectionBehavior = new ElementSelectionBehavior(this, viewModel.Designer);
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
        if (sender is FrameworkElement element && element.Tag is DisplayElement displayElement)
        {
            // Select the element using the command
            ViewModel?.Designer?.SelectElementCommand?.Execute(displayElement);

            // Start drag operation
            _dragStartPosition = e.GetPosition(DesignerCanvas);
            _isDragging = true;
            _draggingElement = displayElement;
            element.CaptureMouse();

            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles mouse move for dragging elements
    /// </summary>
    private void Element_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed && _draggingElement != null)
        {
            var currentPosition = e.GetPosition(DesignerCanvas);
            var delta = currentPosition - _dragStartPosition;

            // Calculate new position
            var newX = _draggingElement.Position.X + delta.X;
            var newY = _draggingElement.Position.Y + delta.Y;

            // Snap to grid if Ctrl key is pressed
            if (Keyboard.Modifiers == ModifierKeys.Control && ViewModel?.Designer?.SnapToGrid == true)
            {
                var gridSize = ViewModel.Designer.GridSize;
                newX = Math.Round(newX / gridSize) * gridSize;
                newY = Math.Round(newY / gridSize) * gridSize;
            }

            // Ensure element stays within canvas bounds
            newX = Math.Max(0, Math.Min(newX, ViewModel?.Designer?.CurrentLayout?.Resolution?.Width ?? 1920 - _draggingElement.Size.Width));
            newY = Math.Max(0, Math.Min(newY, ViewModel?.Designer?.CurrentLayout?.Resolution?.Height ?? 1080 - _draggingElement.Size.Height));

            // Update position
            _draggingElement.Position.X = newX;
            _draggingElement.Position.Y = newY;

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
}
