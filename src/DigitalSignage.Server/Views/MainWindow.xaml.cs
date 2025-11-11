using System.Windows;
using System.Windows.Documents;
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
