using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Live Preview tab
/// </summary>
public partial class PreviewViewModel : ObservableObject
{
    private readonly IScribanService _scribanService;
    private readonly ILogger<PreviewViewModel> _logger;

    [ObservableProperty]
    private DisplayLayout? _currentLayout;

    [ObservableProperty]
    private string _previewStatus = "No layout loaded";

    [ObservableProperty]
    private bool _isRefreshing = false;

    [ObservableProperty]
    private string _testData = "{}";

    public ObservableCollection<DisplayElement> PreviewElements { get; } = new();

    public PreviewViewModel(
        IScribanService scribanService,
        ILogger<PreviewViewModel> logger)
    {
        _scribanService = scribanService;
        _logger = logger;
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

            // Use default test data
            var data = new Dictionary<string, object>
            {
                { "room_name", "Conference Room A" },
                { "status", "Available" },
                { "temperature", "22°C" },
                { "date", DateTime.Now.ToString("dd.MM.yyyy") },
                { "time", DateTime.Now.ToString("HH:mm") }
            };
            TestData = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

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

        // Initialize default properties to prevent KeyNotFoundException
        processedElement.InitializeDefaultProperties();

        // Process content with Scriban template engine for Text elements
        if (element.Type == "Text")
        {
            var content = element["Content"]?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    var processedContent = await _scribanService.ProcessTemplateAsync(content, data);
                    processedElement["Content"] = processedContent;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to render template for element {ElementName}", element.Name);
                    processedElement["Content"] = content; // Fallback to original content
                }
            }
        }

        return processedElement;
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
