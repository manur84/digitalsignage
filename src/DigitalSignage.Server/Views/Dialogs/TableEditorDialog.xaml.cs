using System.Windows;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Interaction logic for TableEditorDialog.xaml
/// </summary>
public partial class TableEditorDialog : Window
{
    public TableEditorDialogViewModel ViewModel { get; }

    public TableEditorDialog(List<string>? columns, List<List<string>>? rows)
    {
        InitializeComponent();
        ViewModel = new TableEditorDialogViewModel(columns, rows);
        DataContext = ViewModel;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Validate())
        {
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
