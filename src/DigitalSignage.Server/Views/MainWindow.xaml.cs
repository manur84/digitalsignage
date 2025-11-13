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

        // Register keyboard shortcuts
        RegisterKeyboardShortcuts();
    }

    private void RegisterKeyboardShortcuts()
    {
        // Text Element - T
        var addTextGesture = new KeyGesture(Key.T);
        var addTextBinding = new KeyBinding(((MainViewModel)DataContext).Designer.AddTextElementCommand, addTextGesture);
        InputBindings.Add(addTextBinding);

        // Image Element - I
        var addImageGesture = new KeyGesture(Key.I);
        var addImageBinding = new KeyBinding(((MainViewModel)DataContext).Designer.AddImageElementCommand, addImageGesture);
        InputBindings.Add(addImageBinding);

        // Rectangle Element - R
        var addRectGesture = new KeyGesture(Key.R);
        var addRectBinding = new KeyBinding(((MainViewModel)DataContext).Designer.AddRectangleElementCommand, addRectGesture);
        InputBindings.Add(addRectBinding);

        // Delete - Delete Key
        var deleteGesture = new KeyGesture(Key.Delete);
        var deleteBinding = new KeyBinding(((MainViewModel)DataContext).Designer.DeleteSelectedElementCommand, deleteGesture);
        InputBindings.Add(deleteBinding);

        // Duplicate - Ctrl+D
        var duplicateGesture = new KeyGesture(Key.D, ModifierKeys.Control);
        var duplicateBinding = new KeyBinding(((MainViewModel)DataContext).Designer.DuplicateSelectedElementCommand, duplicateGesture);
        InputBindings.Add(duplicateBinding);

        // Select All - Ctrl+A
        var selectAllGesture = new KeyGesture(Key.A, ModifierKeys.Control);
        var selectAllBinding = new KeyBinding(((MainViewModel)DataContext).Designer.SelectAllCommand, selectAllGesture);
        InputBindings.Add(selectAllBinding);

        // Clear Selection - Escape
        var clearSelGesture = new KeyGesture(Key.Escape);
        var clearSelBinding = new KeyBinding(((MainViewModel)DataContext).Designer.ClearSelectionCommand, clearSelGesture);
        InputBindings.Add(clearSelBinding);

        // Undo - Ctrl+Z
        var undoGesture = new KeyGesture(Key.Z, ModifierKeys.Control);
        var undoBinding = new KeyBinding(((MainViewModel)DataContext).Designer.UndoCommand, undoGesture);
        InputBindings.Add(undoBinding);

        // Redo - Ctrl+Y
        var redoGesture = new KeyGesture(Key.Y, ModifierKeys.Control);
        var redoBinding = new KeyBinding(((MainViewModel)DataContext).Designer.RedoCommand, redoGesture);
        InputBindings.Add(redoBinding);

        // Save - Ctrl+S
        var saveGesture = new KeyGesture(Key.S, ModifierKeys.Control);
        var saveBinding = new KeyBinding(((MainViewModel)DataContext).Designer.SaveLayoutCommand, saveGesture);
        InputBindings.Add(saveBinding);

        // Zoom In - Ctrl+Plus
        var zoomInGesture = new KeyGesture(Key.OemPlus, ModifierKeys.Control);
        var zoomInBinding = new KeyBinding(((MainViewModel)DataContext).Designer.ZoomInCommand, zoomInGesture);
        InputBindings.Add(zoomInBinding);

        // Zoom Out - Ctrl+Minus
        var zoomOutGesture = new KeyGesture(Key.OemMinus, ModifierKeys.Control);
        var zoomOutBinding = new KeyBinding(((MainViewModel)DataContext).Designer.ZoomOutCommand, zoomOutGesture);
        InputBindings.Add(zoomOutBinding);

        // Zoom to Fit - Ctrl+0
        var zoomFitGesture = new KeyGesture(Key.D0, ModifierKeys.Control);
        var zoomFitBinding = new KeyBinding(((MainViewModel)DataContext).Designer.ZoomToFitCommand, zoomFitGesture);
        InputBindings.Add(zoomFitBinding);
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
