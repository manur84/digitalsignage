using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Models;
using DigitalSignage.Data.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Visual Data Mapping Dialog
/// Maps SQL query result fields to layout elements
/// </summary>
public partial class DataMappingViewModel : ObservableObject
{
    private readonly ILogger<DataMappingViewModel> _logger;
    private readonly SqlDataService _sqlDataService;

    [ObservableProperty]
    private ObservableCollection<DataFieldInfo> _availableDataFields = new();

    [ObservableProperty]
    private ObservableCollection<LayoutElementInfo> _availableElements = new();

    [ObservableProperty]
    private ObservableCollection<DataMapping> _currentMappings = new();

    [ObservableProperty]
    private DataFieldInfo? _selectedDataField;

    [ObservableProperty]
    private LayoutElementInfo? _selectedElement;

    [ObservableProperty]
    private DataMapping? _selectedMapping;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasChanges;

    private DisplayLayout? _currentLayout;
    private DataSource? _currentDataSource;

    public DataMappingViewModel(
        ILogger<DataMappingViewModel> logger,
        SqlDataService sqlDataService)
    {
        _logger = logger;
        _sqlDataService = sqlDataService;
    }

    /// <summary>
    /// Initialize the dialog with a layout and data source
    /// </summary>
    public async Task InitializeAsync(DisplayLayout layout, DataSource? dataSource)
    {
        try
        {
            _currentLayout = layout;
            _currentDataSource = dataSource;

            // Load available elements from layout
            LoadAvailableElements();

            // Load available data fields from data source
            if (dataSource != null)
            {
                await LoadAvailableDataFieldsAsync(dataSource);
            }

            // Load existing mappings
            LoadExistingMappings();

            StatusMessage = "Ready to create data mappings";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize data mapping dialog");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Load available elements from the current layout
    /// </summary>
    private void LoadAvailableElements()
    {
        AvailableElements.Clear();

        if (_currentLayout == null) return;

        foreach (var element in _currentLayout.Elements)
        {
            // Only show mappable element types (text, table, datetime)
            if (element.Type == "text" || element.Type == "table" || element.Type == "datetime")
            {
                AvailableElements.Add(new LayoutElementInfo
                {
                    ElementId = element.Id,
                    ElementName = element.Name,
                    ElementType = element.Type,
                    CurrentBinding = element.DataBinding
                });
            }
        }

        _logger.LogInformation("Loaded {Count} mappable elements from layout", AvailableElements.Count);
    }

    /// <summary>
    /// Load available data fields by executing the data source query
    /// </summary>
    private async Task LoadAvailableDataFieldsAsync(DataSource dataSource)
    {
        AvailableDataFields.Clear();

        try
        {
            if (dataSource.Type == DataSourceType.SQL)
            {
                // Execute query to get schema
                var result = await _sqlDataService.ExecuteQueryAsync(
                    dataSource.ConnectionString,
                    dataSource.Query,
                    dataSource.Parameters);

                // Check if query returned rows
                if (result.ContainsKey("_rows") && result["_rows"] is IEnumerable<object> rows)
                {
                    var rowList = rows.ToList();
                    if (rowList.Count > 0)
                    {
                        // Get column names from first row
                        var firstRow = rowList[0] as IDictionary<string, object>;
                        if (firstRow != null)
                        {
                            foreach (var column in firstRow.Keys)
                            {
                                var value = firstRow[column];
                                var dataType = value?.GetType().Name ?? "String";

                                AvailableDataFields.Add(new DataFieldInfo
                                {
                                    FieldName = column,
                                    DataType = dataType,
                                    SampleValue = value?.ToString() ?? "(null)"
                                });
                            }

                            _logger.LogInformation("Loaded {Count} data fields from query result", AvailableDataFields.Count);
                            StatusMessage = $"Found {AvailableDataFields.Count} data fields";
                        }
                    }
                    else
                    {
                        StatusMessage = "Query returned no results. Cannot determine fields.";
                        _logger.LogWarning("Query returned no results for field discovery");
                    }
                }
                else
                {
                    StatusMessage = "Query returned no results. Cannot determine fields.";
                    _logger.LogWarning("Query result has no _rows key");
                }
            }
            else if (dataSource.Type == DataSourceType.StaticData)
            {
                // Parse static JSON data
                StatusMessage = "Static data sources not yet supported for mapping";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load data fields from data source");
            StatusMessage = $"Error loading data fields: {ex.Message}";
        }
    }

    /// <summary>
    /// Load existing mappings from elements
    /// </summary>
    private void LoadExistingMappings()
    {
        CurrentMappings.Clear();

        if (_currentLayout == null) return;

        foreach (var element in _currentLayout.Elements)
        {
            if (!string.IsNullOrWhiteSpace(element.DataBinding))
            {
                CurrentMappings.Add(new DataMapping
                {
                    ElementId = element.Id,
                    ElementName = element.Name,
                    DataField = element.DataBinding,
                    MappingExpression = element.DataBinding
                });
            }
        }

        _logger.LogInformation("Loaded {Count} existing mappings", CurrentMappings.Count);
    }

    /// <summary>
    /// Add a new mapping
    /// </summary>
    [RelayCommand]
    private void AddMapping()
    {
        if (SelectedDataField == null || SelectedElement == null)
        {
            StatusMessage = "Please select both a data field and an element";
            return;
        }

        // Check if mapping already exists for this element
        var existingMapping = CurrentMappings.FirstOrDefault(m => m.ElementId == SelectedElement.ElementId);
        if (existingMapping != null)
        {
            StatusMessage = $"Element '{SelectedElement.ElementName}' is already mapped to '{existingMapping.DataField}'. Remove it first.";
            return;
        }

        // Create new mapping
        var mapping = new DataMapping
        {
            ElementId = SelectedElement.ElementId,
            ElementName = SelectedElement.ElementName,
            DataField = SelectedDataField.FieldName,
            MappingExpression = $"{{{{{SelectedDataField.FieldName}}}}}" // {{FieldName}} format for Scriban
        };

        CurrentMappings.Add(mapping);
        HasChanges = true;
        StatusMessage = $"Mapped '{SelectedDataField.FieldName}' → '{SelectedElement.ElementName}'";

        _logger.LogInformation(
            "Created mapping: {DataField} → {Element}",
            SelectedDataField.FieldName,
            SelectedElement.ElementName);

        // Clear selection
        SelectedDataField = null;
        SelectedElement = null;
    }

    /// <summary>
    /// Remove selected mapping
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRemoveMapping))]
    private void RemoveMapping()
    {
        if (SelectedMapping == null) return;

        CurrentMappings.Remove(SelectedMapping);
        HasChanges = true;
        StatusMessage = $"Removed mapping for '{SelectedMapping.ElementName}'";

        _logger.LogInformation("Removed mapping for element {Element}", SelectedMapping.ElementName);

        SelectedMapping = null;
    }

    private bool CanRemoveMapping() => SelectedMapping != null;

    /// <summary>
    /// Clear all mappings
    /// </summary>
    [RelayCommand]
    private void ClearAllMappings()
    {
        if (CurrentMappings.Count == 0)
        {
            StatusMessage = "No mappings to clear";
            return;
        }

        var count = CurrentMappings.Count;
        CurrentMappings.Clear();
        HasChanges = true;
        StatusMessage = $"Cleared {count} mapping(s)";

        _logger.LogInformation("Cleared all mappings");
    }

    /// <summary>
    /// Save mappings to layout elements
    /// </summary>
    [RelayCommand]
    private void SaveMappings()
    {
        if (_currentLayout == null) return;

        try
        {
            // Clear all existing bindings
            foreach (var element in _currentLayout.Elements)
            {
                element.DataBinding = null;
            }

            // Apply new mappings
            foreach (var mapping in CurrentMappings)
            {
                var element = _currentLayout.Elements.FirstOrDefault(e => e.Id == mapping.ElementId);
                if (element != null)
                {
                    element.DataBinding = mapping.MappingExpression;
                }
            }

            HasChanges = false;
            StatusMessage = $"Saved {CurrentMappings.Count} mapping(s)";

            _logger.LogInformation("Saved {Count} mappings to layout", CurrentMappings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save mappings");
            StatusMessage = $"Error saving mappings: {ex.Message}";
        }
    }
}

/// <summary>
/// Information about a data field from a query result
/// </summary>
public class DataFieldInfo
{
    public string FieldName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string SampleValue { get; set; } = string.Empty;
}

/// <summary>
/// Information about a layout element available for mapping
/// </summary>
public class LayoutElementInfo
{
    public string ElementId { get; set; } = string.Empty;
    public string ElementName { get; set; } = string.Empty;
    public string ElementType { get; set; } = string.Empty;
    public string? CurrentBinding { get; set; }
}

/// <summary>
/// Represents a data field to element mapping
/// </summary>
public class DataMapping
{
    public string ElementId { get; set; } = string.Empty;
    public string ElementName { get; set; } = string.Empty;
    public string DataField { get; set; } = string.Empty;
    public string MappingExpression { get; set; } = string.Empty;
}
