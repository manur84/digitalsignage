using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using DigitalSignage.Server.Controls;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

public partial class MainWindow : Window
{
    private Adorner? _currentAdorner;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Subscribe to element selection events
        AddHandler(DesignerItemControl.SelectedEvent, new RoutedEventHandler(OnElementSelected));
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

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

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void OnElementSelected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is DesignerItemControl control && DataContext is MainViewModel mainViewModel)
        {
            // Update the selected element in the ViewModel
            mainViewModel.Designer.SelectedElement = control.DisplayElement;

            // Remove existing adorner
            RemoveCurrentAdorner();

            // Add resize adorner to selected element
            if (control.DisplayElement != null)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(control);
                if (adornerLayer != null)
                {
                    _currentAdorner = new ResizeAdorner(control);
                    adornerLayer.Add(_currentAdorner);
                }
            }
        }
    }

    private void RemoveCurrentAdorner()
    {
        if (_currentAdorner != null)
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(_currentAdorner.AdornedElement);
            adornerLayer?.Remove(_currentAdorner);
            _currentAdorner = null;
        }
    }
}
