using System.Windows;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Interaction logic for SystemDiagnosticsWindow.xaml
/// </summary>
public partial class SystemDiagnosticsWindow : Window
{
    public SystemDiagnosticsWindow(SystemDiagnosticsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
