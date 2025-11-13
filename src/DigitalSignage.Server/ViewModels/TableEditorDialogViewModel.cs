using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for TableEditorDialog
/// Allows editing of table columns and rows
/// </summary>
public partial class TableEditorDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _columnsText = string.Empty;

    [ObservableProperty]
    private string _rowsJson = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public List<string> Columns { get; private set; } = new();
    public List<List<string>> Rows { get; private set; } = new();

    public TableEditorDialogViewModel(List<string>? columns, List<List<string>>? rows)
    {
        // Initialize from provided data
        Columns = columns ?? new List<string> { "Column 1", "Column 2", "Column 3" };
        Rows = rows ?? new List<List<string>>
        {
            new() { "Row 1 Col 1", "Row 1 Col 2", "Row 1 Col 3" },
            new() { "Row 2 Col 1", "Row 2 Col 2", "Row 2 Col 3" }
        };

        // Convert to display format
        ColumnsText = string.Join(", ", Columns);
        RowsJson = JsonSerializer.Serialize(Rows, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Validates the input and parses it
    /// </summary>
    public bool Validate()
    {
        ErrorMessage = string.Empty;

        try
        {
            // Parse columns
            if (string.IsNullOrWhiteSpace(ColumnsText))
            {
                ErrorMessage = "Bitte geben Sie mindestens eine Spalte ein.";
                return false;
            }

            Columns = ColumnsText
                .Split(',')
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();

            if (Columns.Count == 0)
            {
                ErrorMessage = "Bitte geben Sie mindestens eine Spalte ein.";
                return false;
            }

            // Parse rows
            if (string.IsNullOrWhiteSpace(RowsJson))
            {
                Rows = new List<List<string>>();
                return true; // Empty rows is OK
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<List<string>>>(RowsJson);
                if (parsed == null)
                {
                    ErrorMessage = "Ungültiges JSON-Format für Zeilen.";
                    return false;
                }

                Rows = parsed;

                // Validate that each row has the correct number of columns
                foreach (var row in Rows)
                {
                    if (row.Count != Columns.Count)
                    {
                        ErrorMessage = $"Zeilenfehler: Jede Zeile muss {Columns.Count} Spalten haben.";
                        return false;
                    }
                }

                return true;
            }
            catch (JsonException ex)
            {
                ErrorMessage = $"JSON-Fehler: {ex.Message}";
                return false;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fehler: {ex.Message}";
            return false;
        }
    }
}
