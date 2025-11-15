using System.Windows;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Interaction logic for DeviceDetailWindow.xaml
/// </summary>
public partial class DeviceDetailWindow : Window
{
    public DeviceDetailWindow(DeviceDetailViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Subscribe to close request
        viewModel.CloseRequested += (sender, args) =>
        {
            Close();
        };
    }
}
