using System.Windows;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Interaction logic for GridConfigDialog.xaml
/// </summary>
public partial class GridConfigDialog : Window
{
    public GridConfigDialog(GridConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
