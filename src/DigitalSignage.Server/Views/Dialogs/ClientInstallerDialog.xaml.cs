using System.Windows;
using System.Windows.Controls;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views.Dialogs;

public partial class ClientInstallerDialog : Window
{
    public ClientInstallerDialog(ClientInstallerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClientInstallerViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.SshPassword = passwordBox.Password;
        }
    }
}
