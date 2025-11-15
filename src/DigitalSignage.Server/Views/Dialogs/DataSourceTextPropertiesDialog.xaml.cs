using DigitalSignage.Server.ViewModels;
using System.Windows;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Interaction logic for DataSourceTextPropertiesDialog.xaml
/// SQL Data Source Text with Scriban template configuration dialog
/// </summary>
public partial class DataSourceTextPropertiesDialog : Window
{
    public DataSourceTextPropertiesViewModel ViewModel { get; }

    public DataSourceTextPropertiesDialog(DataSourceTextPropertiesViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        // Load data sources when dialog opens
        Loaded += async (s, e) => await ViewModel.LoadDataSourcesAsync();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanSave)
        {
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
