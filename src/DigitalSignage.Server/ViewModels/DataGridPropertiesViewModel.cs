using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for DataGrid Properties Dialog - configures SQL Data Source datagrid elements
/// </summary>
public partial class DataGridPropertiesViewModel : ObservableObject
{
    private readonly DataSourceManager _dataSourceManager;
    private readonly ILogger<DataGridPropertiesViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<SqlDataSource> _availableDataSources = new();

    [ObservableProperty]
    private SqlDataSource? _selectedDataSource;

    [ObservableProperty]
    private int _rowsPerPage = 10;

    [ObservableProperty]
    private bool _showHeader = true;

    [ObservableProperty]
    private bool _autoScroll = false;

    [ObservableProperty]
    private int _scrollInterval = 5;

    [ObservableProperty]
    private string _headerBackgroundColor = "#2196F3";

    [ObservableProperty]
    private string _headerTextColor = "#FFFFFF";

    [ObservableProperty]
    private string _rowBackgroundColor = "#FFFFFF";

    [ObservableProperty]
    private string _alternateRowColor = "#F5F5F5";

    [ObservableProperty]
    private string _borderColor = "#CCCCCC";

    [ObservableProperty]
    private double _borderThickness = 1.0;

    [ObservableProperty]
    private int _cellPadding = 5;

    [ObservableProperty]
    private double _fontSize = 14.0;

    [ObservableProperty]
    private ObservableCollection<Dictionary<string, object>> _previewData = new();

    /// <summary>
    /// Gets whether there is preview data available
    /// </summary>
    public bool HasPreviewData => PreviewData.Count > 0;

    /// <summary>
    /// Gets whether the dialog can be saved (data source is selected)
    /// </summary>
    public bool CanSave => SelectedDataSource != null;

    /// <summary>
    /// Gets the selected data source ID (for binding to DisplayElement)
    /// </summary>
    public Guid SelectedDataSourceId => SelectedDataSource?.Id ?? Guid.Empty;

    public DataGridPropertiesViewModel(
        DataSourceManager dataSourceManager,
        ILogger<DataGridPropertiesViewModel> logger)
    {
        _dataSourceManager = dataSourceManager ?? throw new ArgumentNullException(nameof(dataSourceManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads available data sources from the manager
    /// </summary>
    public async Task LoadDataSourcesAsync()
    {
        try
        {
            _logger.LogInformation("Loading available SQL data sources");

            AvailableDataSources.Clear();

            var dataSources = _dataSourceManager.GetActiveDataSources();

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
    /// Called when selected data source changes - loads preview data
    /// </summary>
    partial void OnSelectedDataSourceChanged(SqlDataSource? value)
    {
        LoadPreviewData();
        OnPropertyChanged(nameof(CanSave));
    }

    /// <summary>
    /// Loads preview data for the selected data source
    /// </summary>
    private void LoadPreviewData()
    {
        PreviewData.Clear();

        if (SelectedDataSource == null)
        {
            OnPropertyChanged(nameof(HasPreviewData));
            return;
        }

        try
        {
            var cachedData = _dataSourceManager.GetCachedData(SelectedDataSource.Id);

            if (cachedData != null && cachedData.Count > 0)
            {
                // Take first 3 rows for preview
                var previewRows = cachedData.Take(3);

                foreach (var row in previewRows)
                {
                    PreviewData.Add(row);
                }

                _logger.LogInformation("Loaded {Count} preview rows for data source {Name}",
                    PreviewData.Count, SelectedDataSource.Name);
            }
            else
            {
                _logger.LogWarning("No cached data available for data source {Name}", SelectedDataSource.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load preview data");
        }

        OnPropertyChanged(nameof(HasPreviewData));
    }

    /// <summary>
    /// Loads properties from an existing DisplayElement (for editing)
    /// </summary>
    public void LoadFromElement(DisplayElement element)
    {
        if (element == null || element.Type.ToLower() != "datagrid")
            return;

        try
        {
            // Load data source
            var dataSourceId = element.GetProperty<Guid>("DataSourceId", Guid.Empty);
            if (dataSourceId != Guid.Empty)
            {
                var dataSource = _dataSourceManager.GetDataSource(dataSourceId);
                if (dataSource != null)
                {
                    SelectedDataSource = dataSource;
                }
            }

            // Load display settings
            RowsPerPage = element.GetProperty<int>("RowsPerPage", 10);
            ShowHeader = element.GetProperty<bool>("ShowHeader", true);
            AutoScroll = element.GetProperty<bool>("AutoScroll", false);
            ScrollInterval = element.GetProperty<int>("ScrollInterval", 5);

            // Load styling
            HeaderBackgroundColor = element.GetProperty<string>("HeaderBackgroundColor", "#2196F3");
            HeaderTextColor = element.GetProperty<string>("HeaderTextColor", "#FFFFFF");
            RowBackgroundColor = element.GetProperty<string>("RowBackgroundColor", "#FFFFFF");
            AlternateRowColor = element.GetProperty<string>("AlternateRowColor", "#F5F5F5");
            BorderColor = element.GetProperty<string>("BorderColor", "#CCCCCC");
            BorderThickness = element.GetProperty<double>("BorderThickness", 1.0);
            CellPadding = element.GetProperty<int>("CellPadding", 5);
            FontSize = element.GetProperty<double>("FontSize", 14.0);

            _logger.LogInformation("Loaded properties from existing datagrid element");
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
            element.Type = "datagrid";

            // Set data source
            element.SetProperty("DataSourceId", SelectedDataSource.Id);

            // Set display settings
            element.SetProperty("RowsPerPage", RowsPerPage);
            element.SetProperty("ShowHeader", ShowHeader);
            element.SetProperty("AutoScroll", AutoScroll);
            element.SetProperty("ScrollInterval", ScrollInterval);

            // Set styling
            element.SetProperty("HeaderBackgroundColor", HeaderBackgroundColor);
            element.SetProperty("HeaderTextColor", HeaderTextColor);
            element.SetProperty("RowBackgroundColor", RowBackgroundColor);
            element.SetProperty("AlternateRowColor", AlternateRowColor);
            element.SetProperty("BorderColor", BorderColor);
            element.SetProperty("BorderThickness", BorderThickness);
            element.SetProperty("CellPadding", CellPadding);
            element.SetProperty("FontSize", FontSize);
            element.SetProperty("FontFamily", "Arial");
            element.SetProperty("TextColor", "#000000");

            // Set name
            element.Name = $"DataGrid - {SelectedDataSource.Name}";

            _logger.LogInformation("Applied properties to datagrid element");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply properties to element");
        }
    }
}
