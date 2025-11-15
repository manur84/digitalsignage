using DigitalSignage.Server.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Interaction logic for TablePropertiesDialog.xaml
/// </summary>
public partial class TablePropertiesDialog : Window
{
    public TablePropertiesViewModel ViewModel { get; }

    public TablePropertiesDialog(TablePropertiesViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;

        // Dynamically generate columns based on number of columns
        GenerateDataGridColumns();

        // Listen for column count changes to regenerate columns
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TablePropertiesViewModel.Columns))
            {
                GenerateDataGridColumns();
            }
        };
    }

    /// <summary>
    /// Generate DataGrid columns dynamically based on column count
    /// </summary>
    private void GenerateDataGridColumns()
    {
        TableDataGrid.Columns.Clear();

        for (int i = 0; i < ViewModel.Columns; i++)
        {
            int columnIndex = i; // Capture for binding

            var column = new DataGridTextColumn
            {
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                Binding = new Binding($"[{columnIndex}]")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                }
            };

            TableDataGrid.Columns.Add(column);
        }
    }

    /// <summary>
    /// Preview text input to allow only numeric input
    /// </summary>
    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow only digits and decimal point
        var regex = new Regex("[^0-9.]+");
        e.Handled = regex.IsMatch(e.Text);
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
