using System.Windows.Controls;
using System.Windows.Input;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views.LayoutManager;

public partial class LayoutManagerTabControl : UserControl
{
    public LayoutManagerTabControl()
    {
        InitializeComponent();
    }

    private void LayoutsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is LayoutManagerViewModel vm)
        {
            var layout = (LayoutsDataGrid.SelectedItem as DisplayLayout);
            vm.OpenLayoutPreview(layout);
        }
    }
}
