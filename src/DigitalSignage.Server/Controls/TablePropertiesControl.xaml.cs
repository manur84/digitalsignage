using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// Interaction logic for TablePropertiesControl.xaml
/// Provides live editing of table data with a DataGrid
/// </summary>
public partial class TablePropertiesControl : UserControl, INotifyPropertyChanged
{
    private DisplayElement? _element;
    private ObservableCollection<TableRow> _tableData = new();
    private List<string> _columns = new();
    private string _columnsText = string.Empty;
    private readonly IDialogService? _dialogService;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TablePropertiesControl()
    {
        InitializeComponent();
        DataContext = this;

        // Get IDialogService from App (for View code-behind, Service Locator is acceptable)
        if (Application.Current is App)
        {
            _dialogService = App.GetService<IDialogService>();
        }
    }

    public string ColumnsText
    {
        get => _columnsText;
        set
        {
            _columnsText = value;
            OnPropertyChanged(nameof(ColumnsText));
        }
    }

    public ObservableCollection<TableRow> TableData
    {
        get => _tableData;
        set
        {
            _tableData = value;
            OnPropertyChanged(nameof(TableData));
        }
    }

    /// <summary>
    /// Load table data from a DisplayElement
    /// </summary>
    public void LoadFromElement(DisplayElement? element)
    {
        _element = element;

        if (_element == null || _element.Type != "table")
        {
            _columns = new List<string>();
            _columnsText = string.Empty;
            TableData = new ObservableCollection<TableRow>();
            GenerateDataGridColumns();
            return;
        }

        // Parse columns
        if (_element.Properties.TryGetValue("Columns", out var colsValue) && colsValue != null)
        {
            var colsString = colsValue.ToString();
            if (!string.IsNullOrWhiteSpace(colsString))
            {
                _columns = colsString
                    .Split(',')
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
                _columnsText = colsString;
            }
        }

        if (_columns.Count == 0)
        {
            _columns = new List<string> { "Column 1", "Column 2", "Column 3" };
            _columnsText = string.Join(", ", _columns);
        }

        OnPropertyChanged(nameof(ColumnsText));

        // Parse rows
        var rows = new List<List<string>>();
        if (_element.Properties.TryGetValue("Rows", out var rowsValue) && rowsValue != null)
        {
            var rowsString = rowsValue.ToString();
            if (!string.IsNullOrWhiteSpace(rowsString) && rowsString != "[]")
            {
                try
                {
                    rows = JsonSerializer.Deserialize<List<List<string>>>(rowsString) ?? new();
                }
                catch (JsonException)
                {
                    // Invalid JSON, start with empty rows
                    rows = new List<List<string>>();
                }
            }
        }

        // Convert rows to TableRow objects
        TableData = new ObservableCollection<TableRow>(
            rows.Select(rowData => new TableRow(_columns.Count, rowData))
        );

        // Generate DataGrid columns
        GenerateDataGridColumns();

        // Bind DataGrid ItemsSource
        TableDataGrid.ItemsSource = TableData;
    }

    /// <summary>
    /// Generate DataGrid columns dynamically based on column names
    /// </summary>
    private void GenerateDataGridColumns()
    {
        TableDataGrid.Columns.Clear();

        for (int i = 0; i < _columns.Count; i++)
        {
            int columnIndex = i; // Capture for closure

            var column = new DataGridTextColumn
            {
                Header = _columns[i],
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                Binding = new Binding($"Cells[{columnIndex}]")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                }
            };

            TableDataGrid.Columns.Add(column);
        }
    }

    /// <summary>
    /// Save changes back to the DisplayElement
    /// </summary>
    private void SaveToElement()
    {
        if (_element == null)
            return;

        // Save columns
        _element["Columns"] = string.Join(", ", _columns);

        // Convert TableData to List<List<string>>
        var rows = TableData
            .Where(row => !row.IsEmpty()) // Skip empty rows
            .Select(row => row.Cells.Take(_columns.Count).ToList())
            .ToList();

        // Save rows as JSON
        _element["Rows"] = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = false });
    }

    private async void UpdateColumns_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Parse new columns from text
            var newColumns = ColumnsText
                .Split(',')
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();

            if (newColumns.Count == 0)
            {
                if (_dialogService != null)
                {
                    await _dialogService.ShowWarningAsync(
                        "Please enter at least one column name.",
                        "Invalid Columns");
                }
                else
                {
                    MessageBox.Show("Please enter at least one column name.", "Invalid Columns",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return;
            }

            // Update columns
            var oldColumnCount = _columns.Count;
            _columns = newColumns;

            // Adjust existing rows
            foreach (var row in TableData)
            {
                row.ResizeCells(_columns.Count);
            }

            // Regenerate DataGrid columns
            GenerateDataGridColumns();

            // Save changes
            SaveToElement();

            if (_dialogService != null)
            {
                await _dialogService.ShowInformationAsync(
                    $"Columns updated successfully!\n\nOld: {oldColumnCount} columns\nNew: {_columns.Count} columns",
                    "Columns Updated");
            }
            else
            {
                MessageBox.Show($"Columns updated successfully!\n\nOld: {oldColumnCount} columns\nNew: {_columns.Count} columns",
                    "Columns Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            if (_dialogService != null)
            {
                await _dialogService.ShowErrorAsync(
                    $"Error updating columns:\n\n{ex.Message}",
                    "Error");
            }
            else
            {
                MessageBox.Show($"Error updating columns:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        var newRow = new TableRow(_columns.Count);
        TableData.Add(newRow);
        SaveToElement();
    }

    private async void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (TableDataGrid.SelectedItems.Count == 0)
        {
            if (_dialogService != null)
            {
                await _dialogService.ShowInformationAsync(
                    "Please select one or more rows to delete.",
                    "No Selection");
            }
            else
            {
                MessageBox.Show("Please select one or more rows to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return;
        }

        var selectedRows = TableDataGrid.SelectedItems.Cast<TableRow>().ToList();

        bool confirmed;
        if (_dialogService != null)
        {
            confirmed = await _dialogService.ShowConfirmationAsync(
                $"Delete {selectedRows.Count} row(s)?",
                "Confirm Delete");
        }
        else
        {
            var result = MessageBox.Show($"Delete {selectedRows.Count} row(s)?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            confirmed = result == MessageBoxResult.Yes;
        }

        if (confirmed)
        {
            foreach (var row in selectedRows)
            {
                TableData.Remove(row);
            }
            SaveToElement();
        }
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a single row in the table
/// </summary>
public class TableRow : INotifyPropertyChanged
{
    private ObservableCollection<string> _cells;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TableRow(int columnCount, List<string>? initialData = null)
    {
        _cells = new ObservableCollection<string>();

        // Initialize cells
        for (int i = 0; i < columnCount; i++)
        {
            if (initialData != null && i < initialData.Count)
            {
                _cells.Add(initialData[i]);
            }
            else
            {
                _cells.Add(string.Empty);
            }
        }

        // Subscribe to cell changes
        _cells.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Cells));
    }

    public ObservableCollection<string> Cells
    {
        get => _cells;
        set
        {
            _cells = value;
            OnPropertyChanged(nameof(Cells));
        }
    }

    /// <summary>
    /// Resize cells to match new column count
    /// </summary>
    public void ResizeCells(int newColumnCount)
    {
        if (_cells.Count < newColumnCount)
        {
            // Add missing cells
            while (_cells.Count < newColumnCount)
            {
                _cells.Add(string.Empty);
            }
        }
        else if (_cells.Count > newColumnCount)
        {
            // Remove extra cells
            while (_cells.Count > newColumnCount)
            {
                _cells.RemoveAt(_cells.Count - 1);
            }
        }
    }

    /// <summary>
    /// Check if this row is empty
    /// </summary>
    public bool IsEmpty()
    {
        return _cells.All(cell => string.IsNullOrWhiteSpace(cell));
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
