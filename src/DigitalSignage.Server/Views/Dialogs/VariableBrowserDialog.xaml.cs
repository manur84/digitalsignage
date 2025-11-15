using DigitalSignage.Server.ViewModels;
using System.Windows;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Interaction logic for VariableBrowserDialog.xaml
/// </summary>
public partial class VariableBrowserDialog : Window
{
    private readonly VariableBrowserViewModel _viewModel;

    public TemplateVariable? SelectedVariable => _viewModel.SelectedVariable;

    public VariableBrowserDialog(VariableBrowserViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        InitializeComponent();
    }

    private void InsertButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedVariable != null)
        {
            DialogResult = true;
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
