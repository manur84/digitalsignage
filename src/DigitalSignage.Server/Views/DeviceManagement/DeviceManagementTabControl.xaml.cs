using System.Windows.Controls;
using System.Windows.Input;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views.DeviceManagement;

/// <summary>
/// Interaction logic for DeviceManagementTabControl.xaml
/// Complete device management tab with device list and control panel
/// </summary>
public partial class DeviceManagementTabControl : UserControl
{
    public DeviceManagementTabControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handle double-click on device in the DataGrid to open device details window
    /// </summary>
    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Ensure we have a DataGrid
        if (sender is not DataGrid dataGrid)
            return;

        // Get the DataContext (ViewModel)
        if (DataContext is not DeviceManagementViewModel viewModel)
            return;

        // Ensure a device is selected
        if (viewModel.SelectedClient == null)
            return;

        // Execute the ShowDeviceDetails command
        if (viewModel.ShowDeviceDetailsCommand.CanExecute(null))
        {
            viewModel.ShowDeviceDetailsCommand.Execute(null);
        }
    }
}
