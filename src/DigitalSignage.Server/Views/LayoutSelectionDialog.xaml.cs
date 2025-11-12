using System.Windows;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Interaction logic for LayoutSelectionDialog.xaml
/// </summary>
public partial class LayoutSelectionDialog : Window
{
    public LayoutSelectionDialog(LayoutSelectionViewModel viewModel)
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
