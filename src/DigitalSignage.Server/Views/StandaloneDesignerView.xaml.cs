using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

public partial class StandaloneDesignerView : UserControl
{
    private DisplayElement? _draggedElement;
    private Point _dragOffset;

    public StandaloneDesignerView()
    {
        InitializeComponent();
    }

    private StandaloneDesignerViewModel? ViewModel => DataContext as StandaloneDesignerViewModel;

    private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null)
            return;

        if (sender is Border border && border.Tag is DisplayElement element)
        {
            _draggedElement = element;
            ViewModel.SelectedElement = element;
            _dragOffset = e.GetPosition(border);
            border.CaptureMouse();
        }
    }

    private void Element_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedElement == null || sender is not Border border || !border.IsMouseCaptured || ViewModel == null)
            return;

        var host = FindCanvasHost(border);
        if (host == null)
            return;

        var position = e.GetPosition(host);
        var newX = position.X - _dragOffset.X;
        var newY = position.Y - _dragOffset.Y;
        ViewModel.UpdateElementPosition(_draggedElement, newX, newY);
    }

    private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border)
        {
            border.ReleaseMouseCapture();
        }
        _draggedElement = null;
    }

    private FrameworkElement? FindCanvasHost(DependencyObject current)
    {
        while (current != null)
        {
            if (current is Border border && border.Name == "CanvasHost")
                return border;

            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
