using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Live Preview tab
/// </summary>
public partial class PreviewViewModel : ObservableObject
{
    private readonly TemplateService _templateService;
    private readonly DataSourceRepository _dataSourceRepository;
    private readonly ILogger<PreviewViewModel> _logger;

    [ObservableProperty]
    private DisplayLayout? _currentLayout;

    [ObservableProperty]
    private DataSource? _selectedTestDataSource;

    [ObservableProperty]
    private string _previewStatus = "No layout loaded";

    [ObservableProperty]
    private bool _isRefreshing = false;

    [ObservableProperty]
    private string _testData = "{}";

    public ObservableCollection<DisplayElement> PreviewElements { get; } = new();
    public ObservableCollection<DataSource> AvailableDataSources { get; } = new();

    public PreviewViewModel(
        TemplateService templateService,
        DataSourceRepository dataSourceRepository,
        ILogger<PreviewViewModel> logger)
    {
        _templateService = templateService;
        _dataSourceRepository = dataSourceRepository;
        _logger = logger;

        // Load available data sources
        _ = LoadDataSourcesAsync();
    }

    /// <summary>
    /// Load layout for preview
    /// </summary>
    public void LoadLayout(DisplayLayout layout)
    {
        try
        {
            _logger.LogInformation("Loading layout for preview: {LayoutName}", layout.Name);

            CurrentLayout = layout;
            PreviewStatus = $"Preview: {layout.Name}";

            // Refresh preview
            _ = RefreshPreview();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load layout for preview");
            PreviewStatus = $"Error loading layout: {ex.Message}";
        }
    }

    /// <summary>
    /// Load available data sources
    /// </summary>
    private async Task LoadDataSourcesAsync()
    {
        try
        {
            var dataSources = await _dataSourceRepository.GetAllAsync();

            AvailableDataSources.Clear();
            foreach (var ds in dataSources.Where(d => d.Type == DataSourceType.StaticData))
            {
                AvailableDataSources.Add(ds);
            }

            _logger.LogInformation("Loaded {Count} static data sources for preview", AvailableDataSources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load data sources");
        }
    }

    /// <summary>
    /// Refresh the preview with test data
    /// </summary>
    [RelayCommand]
    private async Task RefreshPreview()
    {
        if (CurrentLayout == null)
        {
            PreviewStatus = "No layout loaded";
            return;
        }

        IsRefreshing = true;
        PreviewStatus = "Refreshing preview...";

        try
        {
            _logger.LogInformation("Refreshing preview for layout: {LayoutName}", CurrentLayout.Name);

            // Get test data
            Dictionary<string, object> data;
            if (SelectedTestDataSource != null && !string.IsNullOrWhiteSpace(SelectedTestDataSource.StaticData))
            {
                // Use selected test data source
                data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                    SelectedTestDataSource.StaticData) ?? new Dictionary<string, object>();
                TestData = SelectedTestDataSource.StaticData;
            }
            else
            {
                // Use default test data
                data = new Dictionary<string, object>
                {
                    { "room_name", "Conference Room A" },
                    { "status", "Available" },
                    { "temperature", "22°C" },
                    { "date", DateTime.Now.ToString("dd.MM.yyyy") },
                    { "time", DateTime.Now.ToString("HH:mm") }
                };
                TestData = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }

            // Process elements with template engine
            PreviewElements.Clear();
            foreach (var element in CurrentLayout.Elements)
            {
                var processedElement = await ProcessElementWithDataAsync(element, data);
                PreviewElements.Add(processedElement);
            }

            PreviewStatus = $"Preview refreshed at {DateTime.Now:HH:mm:ss}";
            _logger.LogInformation("Preview refreshed successfully with {Count} elements", PreviewElements.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh preview");
            PreviewStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Process an element with template data
    /// </summary>
    private async Task<DisplayElement> ProcessElementWithDataAsync(DisplayElement element, Dictionary<string, object> data)
    {
        var processedElement = new DisplayElement
        {
            Id = element.Id,
            Name = element.Name,
            Type = element.Type,
            Position = element.Position,
            Size = element.Size,
            ZIndex = element.ZIndex,
            Properties = new Dictionary<string, object>(element.Properties)
        };

        // Process content with Scriban template engine for Text elements
        if (element.Type == "Text" && element.Properties.TryGetValue("Content", out var contentObj))
        {
            var content = contentObj?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    var processedContent = await _templateService.ProcessTemplateAsync(content, data);
                    processedElement.Properties["Content"] = processedContent;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to render template for element {ElementName}", element.Name);
                    processedElement.Properties["Content"] = content; // Fallback to original content
                }
            }
        }

        return processedElement;
    }

    /// <summary>
    /// Select a different test data source
    /// </summary>
    partial void OnSelectedTestDataSourceChanged(DataSource? value)
    {
        if (value != null && CurrentLayout != null)
        {
            _logger.LogInformation("Test data source changed to: {DataSourceName}", value.Name);
            _ = RefreshPreview();
        }
    }

    /// <summary>
    /// Clear the preview
    /// </summary>
    [RelayCommand]
    private void ClearPreview()
    {
        PreviewElements.Clear();
        CurrentLayout = null;
        PreviewStatus = "Preview cleared";
        _logger.LogInformation("Preview cleared");
    }
}
