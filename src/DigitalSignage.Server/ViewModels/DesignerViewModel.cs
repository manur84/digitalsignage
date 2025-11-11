using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

public partial class DesignerViewModel : ObservableObject
{
    private readonly ILayoutService _layoutService;
    private readonly ILogger<DesignerViewModel> _logger;

    [ObservableProperty]
    private DisplayLayout? _currentLayout;

    [ObservableProperty]
    private DisplayElement? _selectedElement;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private string _selectedTool = "select";

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _snapToGrid = true;

    [ObservableProperty]
    private int _gridSize = 10;

    [ObservableProperty]
    private DisplayElement? _selectedLayer;

    public ObservableCollection<DisplayElement> Elements { get; } = new();
    public ObservableCollection<DisplayElement> Layers { get; } = new();

    public DesignerViewModel(ILayoutService layoutService, ILogger<DesignerViewModel> logger)
    {
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create default layout
        CreateNewLayout();
    }

    [RelayCommand]
    private void CreateNewLayout()
    {
        CurrentLayout = new DisplayLayout
        {
            Id = Guid.NewGuid().ToString(),
            Name = "New Layout",
            Resolution = new Resolution { Width = 1920, Height = 1080, Orientation = "landscape" },
            BackgroundColor = "#FFFFFF",
            Elements = new List<DisplayElement>()
        };

        Elements.Clear();
        SelectedElement = null;
        _logger.LogInformation("Created new layout: {LayoutName}", CurrentLayout.Name);
    }

    [RelayCommand]
    private async Task LoadLayoutAsync(string layoutId)
    {
        try
        {
            var layout = await _layoutService.GetLayoutByIdAsync(layoutId);
            if (layout != null)
            {
                CurrentLayout = layout;
                Elements.Clear();
                foreach (var element in layout.Elements)
                {
                    Elements.Add(element);
                }
                SelectedElement = null;
                _logger.LogInformation("Loaded layout: {LayoutName}", layout.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load layout: {LayoutId}", layoutId);
        }
    }

    [RelayCommand]
    private async Task SaveLayoutAsync()
    {
        if (CurrentLayout == null) return;

        try
        {
            // Sync elements to layout
            CurrentLayout.Elements = Elements.ToList();

            await _layoutService.UpdateLayoutAsync(CurrentLayout);
            _logger.LogInformation("Saved layout: {LayoutName}", CurrentLayout.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save layout: {LayoutName}", CurrentLayout?.Name);
        }
    }

    [RelayCommand]
    private void SelectTool(string tool)
    {
        SelectedTool = tool;
        _logger.LogDebug("Selected tool: {Tool}", tool);
    }

    [RelayCommand]
    private void AddTextElement()
    {
        var textElement = new DisplayElement
        {
            Id = Guid.NewGuid().ToString(),
            Type = "text",
            Name = $"Text {Elements.Count + 1}",
            Position = new Position { X = 100, Y = 100 },
            Size = new Size { Width = 200, Height = 50 },
            ZIndex = Elements.Count,
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "Sample Text",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 24,
                ["Color"] = "#000000",
                ["FontWeight"] = "Normal"
            }
        };

        Elements.Add(textElement);
        SelectedElement = textElement;
        UpdateLayers();
        _logger.LogDebug("Added text element: {ElementName}", textElement.Name);
    }

    [RelayCommand]
    private void AddImageElement()
    {
        var imageElement = new DisplayElement
        {
            Id = Guid.NewGuid().ToString(),
            Type = "image",
            Name = $"Image {Elements.Count + 1}",
            Position = new Position { X = 100, Y = 100 },
            Size = new Size { Width = 300, Height = 200 },
            ZIndex = Elements.Count,
            Properties = new Dictionary<string, object>
            {
                ["Source"] = "",
                ["Stretch"] = "Uniform"
            }
        };

        Elements.Add(imageElement);
        SelectedElement = imageElement;
        UpdateLayers();
        _logger.LogDebug("Added image element: {ElementName}", imageElement.Name);
    }

    [RelayCommand]
    private void AddRectangleElement()
    {
        var rectangleElement = new DisplayElement
        {
            Id = Guid.NewGuid().ToString(),
            Type = "rectangle",
            Name = $"Rectangle {Elements.Count + 1}",
            Position = new Position { X = 100, Y = 100 },
            Size = new Size { Width = 200, Height = 150 },
            ZIndex = Elements.Count,
            Properties = new Dictionary<string, object>
            {
                ["FillColor"] = "#ADD8E6",
                ["BorderColor"] = "#00008B",
                ["BorderThickness"] = 2
            }
        };

        Elements.Add(rectangleElement);
        SelectedElement = rectangleElement;
        UpdateLayers();
        _logger.LogDebug("Added rectangle element: {ElementName}", rectangleElement.Name);
    }

    [RelayCommand]
    private void DeleteSelectedElement()
    {
        if (SelectedElement != null)
        {
            var elementName = SelectedElement.Name;
            Elements.Remove(SelectedElement);
            SelectedElement = null;
            UpdateLayers();
            _logger.LogDebug("Deleted element: {ElementName}", elementName);
        }
    }

    [RelayCommand]
    private void MoveElementUp()
    {
        if (SelectedElement != null)
        {
            SelectedElement.ZIndex++;
            UpdateLayers();
            _logger.LogDebug("Moved element up: {ElementName}, ZIndex: {ZIndex}",
                SelectedElement.Name, SelectedElement.ZIndex);
        }
    }

    [RelayCommand]
    private void MoveElementDown()
    {
        if (SelectedElement != null)
        {
            SelectedElement.ZIndex--;
            UpdateLayers();
            _logger.LogDebug("Moved element down: {ElementName}, ZIndex: {ZIndex}",
                SelectedElement.Name, SelectedElement.ZIndex);
        }
    }

    [RelayCommand]
    private void DuplicateSelectedElement()
    {
        if (SelectedElement != null)
        {
            var duplicate = new DisplayElement
            {
                Id = Guid.NewGuid().ToString(),
                Type = SelectedElement.Type,
                Name = $"{SelectedElement.Name} (Copy)",
                Position = new Position
                {
                    X = SelectedElement.Position.X + 20,
                    Y = SelectedElement.Position.Y + 20
                },
                Size = new Size
                {
                    Width = SelectedElement.Size.Width,
                    Height = SelectedElement.Size.Height
                },
                ZIndex = Elements.Count,
                Properties = new Dictionary<string, object>(SelectedElement.Properties)
            };

            Elements.Add(duplicate);
            SelectedElement = duplicate;
            UpdateLayers();
            _logger.LogDebug("Duplicated element: {ElementName}", duplicate.Name);
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(ZoomLevel + 0.1, 4.0);
        _logger.LogDebug("Zoom in: {ZoomLevel:P0}", ZoomLevel);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(ZoomLevel - 0.1, 0.25);
        _logger.LogDebug("Zoom out: {ZoomLevel:P0}", ZoomLevel);
    }

    [RelayCommand]
    private void ZoomToFit()
    {
        ZoomLevel = 1.0;
        _logger.LogDebug("Zoom to fit: 100%");
    }

    partial void OnSelectedElementChanged(DisplayElement? value)
    {
        _logger.LogDebug("Selection changed: {ElementName}", value?.Name ?? "None");
        SelectedLayer = value;
    }

    partial void OnSelectedLayerChanged(DisplayElement? value)
    {
        if (value != null && value != SelectedElement)
        {
            SelectedElement = value;
        }
    }

    [RelayCommand]
    private void ToggleLayerVisibility(DisplayElement element)
    {
        if (element == null) return;

        // Toggle visibility property
        if (element.Properties.ContainsKey("IsVisible"))
        {
            element.Properties["IsVisible"] = !(bool)element.Properties["IsVisible"];
        }
        else
        {
            element.Properties["IsVisible"] = false;
        }

        _logger.LogDebug("Toggled visibility for {ElementName}: {IsVisible}",
            element.Name, element.Properties["IsVisible"]);

        OnPropertyChanged(nameof(Layers));
    }

    [RelayCommand]
    private void ToggleLayerLock(DisplayElement element)
    {
        if (element == null) return;

        // Toggle lock property
        if (element.Properties.ContainsKey("IsLocked"))
        {
            element.Properties["IsLocked"] = !(bool)element.Properties["IsLocked"];
        }
        else
        {
            element.Properties["IsLocked"] = true;
        }

        _logger.LogDebug("Toggled lock for {ElementName}: {IsLocked}",
            element.Name, element.Properties["IsLocked"]);

        OnPropertyChanged(nameof(Layers));
    }

    [RelayCommand]
    private void MoveLayerUp(DisplayElement element)
    {
        if (element == null) return;

        var currentIndex = Elements.IndexOf(element);
        if (currentIndex < Elements.Count - 1)
        {
            // Swap Z-indices with the element above
            var upperElement = Elements[currentIndex + 1];
            var tempZ = element.ZIndex;
            element.ZIndex = upperElement.ZIndex;
            upperElement.ZIndex = tempZ;

            UpdateLayers();
            _logger.LogDebug("Moved layer up: {ElementName}", element.Name);
        }
    }

    [RelayCommand]
    private void MoveLayerDown(DisplayElement element)
    {
        if (element == null) return;

        var currentIndex = Elements.IndexOf(element);
        if (currentIndex > 0)
        {
            // Swap Z-indices with the element below
            var lowerElement = Elements[currentIndex - 1];
            var tempZ = element.ZIndex;
            element.ZIndex = lowerElement.ZIndex;
            lowerElement.ZIndex = tempZ;

            UpdateLayers();
            _logger.LogDebug("Moved layer down: {ElementName}", element.Name);
        }
    }

    [RelayCommand]
    private void SelectLayer(DisplayElement element)
    {
        if (element != null)
        {
            SelectedElement = element;
            SelectedLayer = element;
        }
    }

    private void UpdateLayers()
    {
        Layers.Clear();
        foreach (var element in Elements.OrderByDescending(e => e.ZIndex))
        {
            Layers.Add(element);
        }
    }

    /// <summary>
    /// Gets the icon for the element type
    /// </summary>
    public static string GetElementTypeIcon(string type)
    {
        return type.ToLower() switch
        {
            "text" => "T",
            "image" => "🖼",
            "rectangle" => "▭",
            "circle" => "⬤",
            "video" => "🎥",
            _ => "?"
        };
    }

    /// <summary>
    /// Checks if an element is visible
    /// </summary>
    public static bool IsElementVisible(DisplayElement element)
    {
        if (element.Properties.ContainsKey("IsVisible"))
        {
            return (bool)element.Properties["IsVisible"];
        }
        return true; // Default to visible
    }

    /// <summary>
    /// Checks if an element is locked
    /// </summary>
    public static bool IsElementLocked(DisplayElement element)
    {
        if (element.Properties.ContainsKey("IsLocked"))
        {
            return (bool)element.Properties["IsLocked"];
        }
        return false; // Default to unlocked
    }
}
