using System.Windows;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Interaction logic for TemplateManagerWindow.xaml
/// </summary>
public partial class TemplateManagerWindow : Window
{
    public TemplateManagerWindow(TemplateManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Subscribe to close event
        viewModel.CloseRequested += (s, e) => Close();
    }
}
