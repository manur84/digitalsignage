using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using Scriban;
using Scriban.Runtime;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for DataSourceText Properties Dialog - configures SQL Data Source text elements with Scriban templates
/// </summary>
public partial class DataSourceTextPropertiesViewModel : ObservableObject
{
    private readonly DataSourceManager _dataSourceManager;
    private readonly ILogger<DataSourceTextPropertiesViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<SqlDataSource> _availableDataSources = new();

    [ObservableProperty]
    private SqlDataSource? _selectedDataSource;

    [ObservableProperty]
    private string _template = "{{Name}}";

    [ObservableProperty]
    private int _rowIndex = 0;

    [ObservableProperty]
    private int _updateInterval = 5;

    [ObservableProperty]
    private string _fontFamily = "Arial";

    [ObservableProperty]
    private double _fontSize = 24;

    [ObservableProperty]
    private string _textColor = "#000000";

    [ObservableProperty]
    private string _textAlign = "left";

    [ObservableProperty]
    private string _previewResult = "";

    [ObservableProperty]
    private bool _hasPreviewError = false;

    [ObservableProperty]
    private string _previewError = "";

    /// <summary>
    /// Gets whether the dialog can be saved (data source is selected and template is not empty)
    /// </summary>
    public bool CanSave => SelectedDataSource != null && !string.IsNullOrWhiteSpace(Template);

    /// <summary>
    /// Gets the selected data source ID (for binding to DisplayElement)
    /// </summary>
    public Guid SelectedDataSourceId => SelectedDataSource?.Id ?? Guid.Empty;

    /// <summary>
    /// Example templates to help users
    /// </summary>
    public string TemplateHelp =>
        "Scriban Template Examples:\n" +
        "• Simple: {{Name}}\n" +
        "• Multiple values: {{Name}}: {{Value}}\n" +
        "• With formatting: Temperature: {{Temperature}}°C\n" +
        "• Conditional: {{ if Value > 100 }}High{{ else }}Normal{{ end }}\n" +
        "• Loop (all rows): {{ for row in Rows }}{{row.Name}} {{ end }}\n" +
        "\nAvailable variables depend on your SQL query columns.";

    public DataSourceTextPropertiesViewModel(
        DataSourceManager dataSourceManager,
        ILogger<DataSourceTextPropertiesViewModel> logger)
    {
        _dataSourceManager = dataSourceManager ?? throw new ArgumentNullException(nameof(dataSourceManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads available data sources from the manager
    /// </summary>
    public Task LoadDataSourcesAsync()
    {
        try
        {
            _logger.LogInformation("Loading available SQL data sources");

            AvailableDataSources.Clear();

            var dataSourcesResult = _dataSourceManager.GetActiveDataSources();
            if (dataSourcesResult.IsFailure)
            {
                _logger.LogError("Failed to load data sources: {ErrorMessage}", dataSourcesResult.ErrorMessage);
                return Task.CompletedTask;
            }

            var dataSources = dataSourcesResult.Value;

            foreach (var ds in dataSources)
            {
                AvailableDataSources.Add(ds);
            }

            _logger.LogInformation("Loaded {Count} active data sources", dataSources.Count);

            // Auto-select first data source if available
            if (AvailableDataSources.Count > 0 && SelectedDataSource == null)
            {
                SelectedDataSource = AvailableDataSources[0];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load data sources");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Refreshes the data sources list
    /// </summary>
    [RelayCommand]
    private async Task RefreshDataSourcesAsync()
    {
        await LoadDataSourcesAsync();
    }

    /// <summary>
    /// Called when selected data source changes - updates preview
    /// </summary>
    partial void OnSelectedDataSourceChanged(SqlDataSource? value)
    {
        UpdatePreview();
        OnPropertyChanged(nameof(CanSave));
    }

    /// <summary>
    /// Called when template changes - updates preview
    /// </summary>
    partial void OnTemplateChanged(string value)
    {
        UpdatePreview();
        OnPropertyChanged(nameof(CanSave));
    }

    /// <summary>
    /// Called when row index changes - updates preview
    /// </summary>
    partial void OnRowIndexChanged(int value)
    {
        UpdatePreview();
    }

    /// <summary>
    /// Updates the preview by rendering the template with actual data
    /// </summary>
    [RelayCommand]
    private void UpdatePreview()
    {
        HasPreviewError = false;
        PreviewError = "";
        PreviewResult = "";

        if (SelectedDataSource == null || string.IsNullOrWhiteSpace(Template))
        {
            PreviewResult = "(No data source or template)";
            return;
        }

        try
        {
            var cachedDataResult = _dataSourceManager.GetCachedData(SelectedDataSource.Id);

            if (cachedDataResult.IsFailure)
            {
                PreviewResult = $"(Error: {cachedDataResult.ErrorMessage})";
                _logger.LogError("Failed to load cached data for {Name}: {ErrorMessage}",
                    SelectedDataSource.Name, cachedDataResult.ErrorMessage);
                return;
            }

            var cachedData = cachedDataResult.Value;

            if (cachedData == null || cachedData.Count == 0)
            {
                PreviewResult = "(No data available)";
                _logger.LogWarning("No cached data available for data source {Name}", SelectedDataSource.Name);
                return;
            }

            // Get the row at RowIndex (or first row if index out of bounds)
            var rowData = RowIndex >= 0 && RowIndex < cachedData.Count
                ? cachedData[RowIndex]
                : cachedData[0];

            // Render template using Scriban
            var template = Scriban.Template.Parse(Template);

            // Create script object with row data + all rows
            var scriptObject = new ScriptObject();

            // Add all columns from the row
            foreach (var kvp in rowData)
            {
                scriptObject.Add(kvp.Key, kvp.Value);
            }

            // Add Rows collection for loops
            scriptObject.Add("Rows", cachedData);
            scriptObject.Add("RowIndex", RowIndex);
            scriptObject.Add("TotalRows", cachedData.Count);

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            var result = template.Render(context);

            PreviewResult = result;
            _logger.LogInformation("Template preview rendered successfully: {Result}", result);
        }
        catch (Exception ex)
        {
            HasPreviewError = true;
            PreviewError = ex.Message;
            PreviewResult = $"(Error: {ex.Message})";
            _logger.LogError(ex, "Failed to render template preview");
        }
    }

    /// <summary>
    /// Loads properties from an existing DisplayElement (for editing)
    /// </summary>
    public void LoadFromElement(DisplayElement element)
    {
        if (element == null || element.Type.ToLower() != "datasourcetext")
            return;

        try
        {
            // Load data source
            var dataSourceId = element.GetProperty<Guid>("DataSourceId", Guid.Empty);
            if (dataSourceId != Guid.Empty)
            {
                var dataSourceResult = _dataSourceManager.GetDataSource(dataSourceId);
                if (dataSourceResult.IsSuccess)
                {
                    SelectedDataSource = dataSourceResult.Value;
                }
                else
                {
                    _logger.LogWarning("Failed to load data source {DataSourceId}: {ErrorMessage}",
                        dataSourceId, dataSourceResult.ErrorMessage);
                }
            }

            // Load template settings
            Template = element.GetProperty<string>("Template", "{{Name}}");
            RowIndex = element.GetProperty<int>("RowIndex", 0);
            UpdateInterval = element.GetProperty<int>("UpdateInterval", 5);

            // Load text formatting
            FontFamily = element.GetProperty<string>("FontFamily", "Arial");
            FontSize = element.GetProperty<double>("FontSize", 24);
            TextColor = element.GetProperty<string>("TextColor", "#000000");
            TextAlign = element.GetProperty<string>("TextAlign", "left");

            _logger.LogInformation("Loaded properties from existing datasourcetext element");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load properties from element");
        }
    }

    /// <summary>
    /// Applies properties to a DisplayElement
    /// </summary>
    public void ApplyToElement(DisplayElement element)
    {
        if (element == null || SelectedDataSource == null)
            return;

        try
        {
            element.Type = "datasourcetext";

            // Set data source
            element.SetProperty("DataSourceId", SelectedDataSource.Id);

            // Set template settings
            element.SetProperty("Template", Template);
            element.SetProperty("RowIndex", RowIndex);
            element.SetProperty("UpdateInterval", UpdateInterval);

            // Set text formatting
            element.SetProperty("FontFamily", FontFamily);
            element.SetProperty("FontSize", FontSize);
            element.SetProperty("TextColor", TextColor);
            element.SetProperty("TextAlign", TextAlign);

            // Set name
            element.Name = $"DataSourceText - {SelectedDataSource.Name}";

            _logger.LogInformation("Applied properties to datasourcetext element");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply properties to element");
        }
    }
}
