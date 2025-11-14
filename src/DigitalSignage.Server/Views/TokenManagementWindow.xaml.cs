using DigitalSignage.Server.ViewModels;
using System.Windows;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Interaction logic for TokenManagementWindow.xaml
/// </summary>
public partial class TokenManagementWindow : Window
{
    public TokenManagementWindow(TokenManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
