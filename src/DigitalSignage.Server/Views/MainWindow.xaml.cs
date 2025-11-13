using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Controls;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

public partial class MainWindow : Window
{
    private Adorner? _currentAdorner;
    private bool _isDragging = false;
    private Point _dragStartPoint;
    private DisplayElement? _draggedElement;

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

    // ============================================
    // ELEMENT INTERACTION EVENT HANDLERS
    // ============================================

    private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is DisplayElement element)
        {
            // Deselect all other elements
            foreach (var el in ViewModel.Designer.Elements)
            {
                el.IsSelected = false;
            }

            // Select this element
            element.IsSelected = true;
            ViewModel.Designer.SelectedElement = element;

            // Start dragging
            _isDragging = true;
            _dragStartPoint = e.GetPosition((UIElement)border.Parent);
            _draggedElement = element;

            border.CaptureMouse();

            System.Diagnostics.Debug.WriteLine($"Element selected and drag started: {element.Name}");

            e.Handled = true;
        }
    }

    private void Element_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && _draggedElement != null && sender is Border border)
        {
            Point currentPosition = e.GetPosition((UIElement)border.Parent);

            double deltaX = currentPosition.X - _dragStartPoint.X;
            double deltaY = currentPosition.Y - _dragStartPoint.Y;

            // Update element position
            _draggedElement.Position.X += deltaX;
            _draggedElement.Position.Y += deltaY;

            // Snap to grid (optional - 10px grid)
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                _draggedElement.Position.X = Math.Round(_draggedElement.Position.X / 10) * 10;
                _draggedElement.Position.Y = Math.Round(_draggedElement.Position.Y / 10) * 10;
            }

            _dragStartPoint = currentPosition;

            System.Diagnostics.Debug.WriteLine($"Element moved to: ({_draggedElement.Position.X}, {_draggedElement.Position.Y})");
        }
    }

    private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && sender is Border border)
        {
            _isDragging = false;
            _draggedElement = null;
            border.ReleaseMouseCapture();

            System.Diagnostics.Debug.WriteLine("Drag ended");

            e.Handled = true;
        }
    }

    private void Element_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            // Visual feedback on hover
            border.Opacity = 0.9;
            Mouse.OverrideCursor = Cursors.Hand;
        }
    }

    private void Element_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            // Restore opacity
            border.Opacity = 1.0;
            Mouse.OverrideCursor = null;
        }
    }
}
