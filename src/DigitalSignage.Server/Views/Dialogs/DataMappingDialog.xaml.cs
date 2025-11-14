using DigitalSignage.Server.ViewModels;
using System.Windows;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Interaction logic for DataMappingDialog.xaml
/// </summary>
public partial class DataMappingDialog : Window
{
    private readonly DataMappingViewModel _viewModel;

    public DataMappingDialog(DataMappingViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    /// <summary>
    /// Handle Save button click
    /// </summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
