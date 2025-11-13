using System.Windows;
using System.Windows.Input;
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
    /// Clean up resources when window is closing
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _selectionBehavior?.Detach();
        _selectionBehavior = null;
        base.OnClosed(e);
    }
}
