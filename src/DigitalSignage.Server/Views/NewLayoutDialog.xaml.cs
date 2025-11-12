using System.Windows;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Interaction logic for NewLayoutDialog.xaml
/// </summary>
public partial class NewLayoutDialog : Window
{
    public NewLayoutDialog(NewLayoutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Subscribe to close request
        viewModel.CloseRequested += (sender, args) =>
        {
            DialogResult = args;
            Close();
        };
    }
}
