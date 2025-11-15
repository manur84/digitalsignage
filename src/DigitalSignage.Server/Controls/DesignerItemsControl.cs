using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// ItemsControl that automatically binds Canvas positioning metadata for designer elements.
/// This ensures ContentPresenter containers always receive width/height and Canvas.Left/Top/ZIndex values.
/// </summary>
public class DesignerItemsControl : ItemsControl
{
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        if (element is not FrameworkElement presenter)
        {
            return;
        }

        Bind(presenter, FrameworkElement.WidthProperty, "Size.Width");
        Bind(presenter, FrameworkElement.HeightProperty, "Size.Height");
        Bind(presenter, Canvas.LeftProperty, "Position.X");
        Bind(presenter, Canvas.TopProperty, "Position.Y");
        Bind(presenter, Panel.ZIndexProperty, "ZIndex");

        presenter.HorizontalAlignment = HorizontalAlignment.Left;
        presenter.VerticalAlignment = VerticalAlignment.Top;
    }

    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is FrameworkElement presenter)
        {
            ClearBinding(presenter, FrameworkElement.WidthProperty);
            ClearBinding(presenter, FrameworkElement.HeightProperty);
            ClearBinding(presenter, Canvas.LeftProperty);
            ClearBinding(presenter, Canvas.TopProperty);
            ClearBinding(presenter, Panel.ZIndexProperty);
        }

        base.ClearContainerForItemOverride(element, item);
    }

    private static void Bind(FrameworkElement element, DependencyProperty property, string path)
    {
        BindingOperations.SetBinding(
            element,
            property,
            new Binding(path)
            {
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
    }

    private static void ClearBinding(FrameworkElement element, DependencyProperty property)
    {
        BindingOperations.ClearBinding(element, property);
    }
}
