using System.Windows.Controls;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Interaction logic for MobileAppManagementView.xaml
/// </summary>
public partial class MobileAppManagementView : UserControl
{
    public MobileAppManagementView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MobileAppManagementViewModel viewModel)
        {
            await viewModel.LoadRegistrationsAsync();
        }
    }
}
