using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Table Properties Dialog
/// </summary>
public partial class TablePropertiesViewModel : ObservableObject
{
    [ObservableProperty]
    private int _rows = 3;

    [ObservableProperty]
    private int _columns = 3;

    [ObservableProperty]
    private bool _showHeaderRow = true;

    [ObservableProperty]
    private bool _showHeaderColumn = false;

    [ObservableProperty]
    private Color _borderColor = Colors.Black;

    [ObservableProperty]
    private int _borderThickness = 1;

    [ObservableProperty]
    private Color _backgroundColor = Colors.White;

    [ObservableProperty]
    private Color _alternateRowColor = (Color)ColorConverter.ConvertFromString("#F5F5F5")!;

    [ObservableProperty]
    private Color _headerBackgroundColor = (Color)ColorConverter.ConvertFromString("#CCCCCC")!;

    [ObservableProperty]
    private Color _textColor = Colors.Black;

    [ObservableProperty]
    private string _fontFamily = "Arial";

    [ObservableProperty]
    private double _fontSize = 14;

    [ObservableProperty]
    private double _cellPadding = 5;

    /// <summary>
    /// Cell data storage: [row][column]
    /// </summary>
    public ObservableCollection<ObservableCollection<string>> CellData { get; } = new();

    /// <summary>
    /// Available font families
    /// </summary>
    public ObservableCollection<string> AvailableFonts { get; } = new()
    {
        "Arial",
        "Calibri",
        "Verdana",
        "Times New Roman",
        "Courier New",
        "Georgia",
        "Tahoma",
        "Trebuchet MS",
        "Segoe UI"
    };

    public TablePropertiesViewModel()
    {
        InitializeCellData();
    }

    /// <summary>
    /// Constructor with existing data for editing
    /// </summary>
    public TablePropertiesViewModel(int rows, int columns, bool showHeaderRow, bool showHeaderColumn,
        string borderColor, int borderThickness, string backgroundColor, string alternateRowColor,
        string headerBackgroundColor, string textColor, string fontFamily, double fontSize,
        double cellPadding, List<List<string>>? cellData = null)
    {
        _rows = rows;
        _columns = columns;
        _showHeaderRow = showHeaderRow;
        _showHeaderColumn = showHeaderColumn;
        _borderColor = (Color)ColorConverter.ConvertFromString(borderColor)!;
        _borderThickness = borderThickness;
        _backgroundColor = (Color)ColorConverter.ConvertFromString(backgroundColor)!;
        _alternateRowColor = (Color)ColorConverter.ConvertFromString(alternateRowColor)!;
        _headerBackgroundColor = (Color)ColorConverter.ConvertFromString(headerBackgroundColor)!;
        _textColor = (Color)ColorConverter.ConvertFromString(textColor)!;
        _fontFamily = fontFamily;
        _fontSize = fontSize;
        _cellPadding = cellPadding;

        InitializeCellData(cellData);
    }

    /// <summary>
    /// Initialize cell data with default headers or provided data
    /// </summary>
    private void InitializeCellData(List<List<string>>? existingData = null)
    {
        CellData.Clear();

        if (existingData != null && existingData.Count > 0)
        {
            // Load existing data
            foreach (var row in existingData)
            {
                var rowData = new ObservableCollection<string>(row);
                CellData.Add(rowData);
            }
        }
        else
        {
            // Create default data
            for (int i = 0; i < Rows; i++)
            {
                var row = new ObservableCollection<string>();
                for (int j = 0; j < Columns; j++)
                {
                    if (i == 0 && ShowHeaderRow)
                    {
                        row.Add($"Header {j + 1}");
                    }
                    else if (j == 0 && ShowHeaderColumn)
                    {
                        row.Add($"Row {i + 1}");
                    }
                    else
                    {
                        row.Add($"Cell {i + 1},{j + 1}");
                    }
                }
                CellData.Add(row);
            }
        }
    }

    /// <summary>
    /// Called when Rows property changes - adjust cell data
    /// </summary>
    partial void OnRowsChanged(int value)
    {
        if (value < 1)
        {
            Rows = 1;
            return;
        }

        if (value > 50)
        {
            Rows = 50;
            return;
        }

        AdjustRows();
    }

    /// <summary>
    /// Called when Columns property changes - adjust cell data
    /// </summary>
    partial void OnColumnsChanged(int value)
    {
        if (value < 1)
        {
            Columns = 1;
            return;
        }

        if (value > 20)
        {
            Columns = 20;
            return;
        }

        AdjustColumns();
    }

    /// <summary>
    /// Adjust rows in cell data
    /// </summary>
    private void AdjustRows()
    {
        while (CellData.Count < Rows)
        {
            // Add new row
            var row = new ObservableCollection<string>();
            for (int j = 0; j < Columns; j++)
            {
                if (j == 0 && ShowHeaderColumn)
                {
                    row.Add($"Row {CellData.Count + 1}");
                }
                else
                {
                    row.Add($"Cell {CellData.Count + 1},{j + 1}");
                }
            }
            CellData.Add(row);
        }

        while (CellData.Count > Rows)
        {
            // Remove last row
            CellData.RemoveAt(CellData.Count - 1);
        }
    }

    /// <summary>
    /// Adjust columns in cell data
    /// </summary>
    private void AdjustColumns()
    {
        for (int i = 0; i < CellData.Count; i++)
        {
            while (CellData[i].Count < Columns)
            {
                // Add new column
                if (i == 0 && ShowHeaderRow)
                {
                    CellData[i].Add($"Header {CellData[i].Count + 1}");
                }
                else
                {
                    CellData[i].Add($"Cell {i + 1},{CellData[i].Count + 1}");
                }
            }

            while (CellData[i].Count > Columns)
            {
                // Remove last column
                CellData[i].RemoveAt(CellData[i].Count - 1);
            }
        }
    }

    /// <summary>
    /// Called when ShowHeaderRow changes
    /// </summary>
    partial void OnShowHeaderRowChanged(bool value)
    {
        if (value && CellData.Count > 0)
        {
            // Update first row to headers
            for (int j = 0; j < CellData[0].Count; j++)
            {
                CellData[0][j] = $"Header {j + 1}";
            }
        }
    }

    /// <summary>
    /// Called when ShowHeaderColumn changes
    /// </summary>
    partial void OnShowHeaderColumnChanged(bool value)
    {
        if (value)
        {
            // Update first column to row labels
            for (int i = 0; i < CellData.Count; i++)
            {
                if (i == 0 && ShowHeaderRow)
                    continue; // Keep corner cell as header

                if (CellData[i].Count > 0)
                {
                    CellData[i][0] = $"Row {i + 1}";
                }
            }
        }
    }

    /// <summary>
    /// Get cell data as List for serialization
    /// </summary>
    public List<List<string>> GetCellDataAsList()
    {
        var result = new List<List<string>>();
        foreach (var row in CellData)
        {
            result.Add(new List<string>(row));
        }
        return result;
    }

    /// <summary>
    /// Converts Color to hex string
    /// </summary>
    public static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
