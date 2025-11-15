using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Controls;
using DigitalSignage.Tests.Wpf;
using FluentAssertions;
using Xunit;

namespace DigitalSignage.Tests.Views;

public class DesignerCanvasRenderingTests
{
    [Fact]
    public void DesignerItemsControl_should_size_and_position_elements_on_canvas()
    {
        StaTestHelper.Run(() =>
        {
            if (Application.Current == null)
            {
                new Application();
            }

            var element = new DisplayElement
            {
                Name = "Text 1",
                Position = new Position { X = 120, Y = 80 },
                Size = new DigitalSignage.Core.Models.Size { Width = 220, Height = 60 },
                ZIndex = 3
            };
            element.InitializeDefaultProperties();

            var elements = new ObservableCollection<DisplayElement> { element };

            var canvasFactory = new FrameworkElementFactory(typeof(Canvas));
            var itemsPanelTemplate = new ItemsPanelTemplate(canvasFactory);

            var itemsControl = new DesignerItemsControl
            {
                ItemsPanel = itemsPanelTemplate,
                Width = 800,
                Height = 600
            };
            itemsControl.ItemsSource = elements;

            var designerCanvas = new DesignerCanvas
            {
                Width = 800,
                Height = 600
            };
            designerCanvas.Children.Add(itemsControl);

            var window = new Window
            {
                Content = designerCanvas,
                Width = 800,
                Height = 600,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false
            };

            window.Show();
            designerCanvas.Measure(new System.Windows.Size(800, 600));
            designerCanvas.Arrange(new Rect(0, 0, 800, 600));
            designerCanvas.UpdateLayout();
            PumpDispatcher();

            var container = (ContentPresenter)itemsControl.ItemContainerGenerator.ContainerFromIndex(0);
            container.Should().NotBeNull("ItemContainerGenerator should create a ContentPresenter for the display element");

            container.ApplyTemplate();
            itemsControl.UpdateLayout();
            PumpDispatcher();

            container.ActualWidth.Should().Be(element.Size.Width);
            container.ActualHeight.Should().Be(element.Size.Height);
            Canvas.GetLeft(container).Should().Be(element.Position.X);
            Canvas.GetTop(container).Should().Be(element.Position.Y);
            Panel.GetZIndex(container).Should().Be(element.ZIndex);

            container.Content.Should().Be(element);
            container.Visibility.Should().Be(Visibility.Visible);

            window.Close();
        });
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new DispatcherOperationCallback(f =>
            {
                ((DispatcherFrame)f!).Continue = false;
                return null;
            }),
            frame);
        Dispatcher.PushFrame(frame);
    }

}
