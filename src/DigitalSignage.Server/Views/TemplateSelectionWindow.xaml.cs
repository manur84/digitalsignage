using System.Windows;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Interaction logic for TemplateSelectionWindow.xaml
/// </summary>
public partial class TemplateSelectionWindow : Window
{
    public TemplateSelectionWindow(TemplateSelectionViewModel viewModel)
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
