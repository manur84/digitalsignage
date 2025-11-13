using System.Windows;
using System.Windows.Documents;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Controls;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Behaviors;

/// <summary>
/// Behavior for handling element selection and adorner management in the Designer
/// Encapsulates the logic for adding/removing resize adorners when elements are selected
/// </summary>
public class ElementSelectionBehavior
{
    private Adorner? _currentAdorner;
    private readonly FrameworkElement _attachedElement;
    private readonly DesignerViewModel? _designerViewModel;

    /// <summary>
    /// Initializes a new instance of the ElementSelectionBehavior
    /// </summary>
    /// <param name="attachedElement">The element to attach this behavior to (typically the MainWindow or DesignerCanvas)</param>
    /// <param name="designerViewModel">The DesignerViewModel for accessing selected elements</param>
    public ElementSelectionBehavior(FrameworkElement attachedElement, DesignerViewModel? designerViewModel)
    {
        _attachedElement = attachedElement ?? throw new ArgumentNullException(nameof(attachedElement));
        _designerViewModel = designerViewModel;

        // Subscribe to element selection events
        _attachedElement.AddHandler(DesignerItemControl.SelectedEvent, new RoutedEventHandler(OnElementSelected));
    }

    /// <summary>
    /// Handles the element selected event and manages adorners
    /// </summary>
    private void OnElementSelected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is DesignerItemControl control && _designerViewModel != null)
        {
            // Update the selected element in the ViewModel
            _designerViewModel.SelectedElement = control.DisplayElement;

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

    /// <summary>
    /// Removes the current adorner if one exists
    /// </summary>
    private void RemoveCurrentAdorner()
    {
        if (_currentAdorner != null)
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(_currentAdorner.AdornedElement);
            adornerLayer?.Remove(_currentAdorner);
            _currentAdorner = null;
        }
    }

    /// <summary>
    /// Cleans up resources and unsubscribes from events
    /// </summary>
    public void Detach()
    {
        _attachedElement.RemoveHandler(DesignerItemControl.SelectedEvent, new RoutedEventHandler(OnElementSelected));
        RemoveCurrentAdorner();
    }
}
