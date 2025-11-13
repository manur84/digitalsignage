using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Commands;
using DigitalSignage.Server.Services;
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

    [ObservableProperty]
    private bool _isSelectionRectangleActive;

    [ObservableProperty]
    private double _selectionRectangleX;

    [ObservableProperty]
    private double _selectionRectangleY;

    [ObservableProperty]
    private double _selectionRectangleWidth;

    [ObservableProperty]
    private double _selectionRectangleHeight;

    public ObservableCollection<DisplayElement> Elements { get; } = new();
    public ObservableCollection<DisplayElement> Layers { get; } = new();
    public CommandHistory CommandHistory { get; } = new(maxHistorySize: 50);
    public SelectionService SelectionService { get; } = new();

    public DesignerViewModel(ILayoutService layoutService, ILogger<DesignerViewModel> logger)
    {
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to command history changes
        CommandHistory.HistoryChanged += (s, e) =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };

        // Subscribe to selection changes
        SelectionService.SelectionChanged += (s, e) =>
        {
            // Update SelectedElement to match primary selection
            SelectedElement = SelectionService.PrimarySelection;
            DeleteSelectedCommand.NotifyCanExecuteChanged();
            DuplicateSelectedCommand.NotifyCanExecuteChanged();
        };

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
        Layers.Clear();
        SelectedElement = null;
        _logger.LogInformation("Created new layout: {LayoutName}", CurrentLayout.Name);
    }

    /// <summary>
    /// Creates a new layout with specified properties
    /// </summary>
    public async Task CreateNewLayoutAsync(DisplayLayout layout)
    {
        try
        {
            CurrentLayout = layout;
            Elements.Clear();
            Layers.Clear();
            SelectedElement = null;
            _logger.LogInformation("Created new layout: {LayoutName} ({Width}x{Height})",
                layout.Name, layout.Resolution.Width, layout.Resolution.Height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new layout");
        }
    }

    [RelayCommand]
    private async Task LoadLayoutAsync(string layoutId)
    {
        try
        {
            var layout = await _layoutService.GetLayoutByIdAsync(layoutId);
            if (layout != null)
            {
                await LoadLayoutAsync(layout);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load layout: {LayoutId}", layoutId);
        }
    }

    /// <summary>
    /// Loads an existing layout into the designer
    /// </summary>
    public async Task LoadLayoutAsync(DisplayLayout layout)
    {
        try
        {
            CurrentLayout = layout;
            Elements.Clear();
            Layers.Clear();

            if (layout.Elements != null)
            {
                foreach (var element in layout.Elements)
                {
                    // Initialize default properties for loaded elements to prevent KeyNotFoundException
                    element.InitializeDefaultProperties();
                    Elements.Add(element);
                }
            }

            UpdateLayers();
            SelectedElement = null;
            _logger.LogInformation("Loaded layout: {LayoutName} with {ElementCount} elements",
                layout.Name, layout.Elements?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load layout: {LayoutName}", layout.Name);
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

        // Initialize all default properties to prevent KeyNotFoundException
        textElement.InitializeDefaultProperties();

        var command = new AddElementCommand(Elements, textElement);
        CommandHistory.ExecuteCommand(command);
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

        // Initialize all default properties to prevent KeyNotFoundException
        imageElement.InitializeDefaultProperties();

        var command = new AddElementCommand(Elements, imageElement);
        CommandHistory.ExecuteCommand(command);
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

        // Initialize all default properties to prevent KeyNotFoundException
        rectangleElement.InitializeDefaultProperties();

        var command = new AddElementCommand(Elements, rectangleElement);
        CommandHistory.ExecuteCommand(command);
        SelectedElement = rectangleElement;
        UpdateLayers();
        _logger.LogDebug("Added rectangle element: {ElementName}", rectangleElement.Name);
    }

    [RelayCommand]
    private void DeleteSelectedElement()
    {
        if (SelectedElement != null)
        {
            var element = SelectedElement;
            var command = new DeleteElementCommand(Elements, element);
            CommandHistory.ExecuteCommand(command);
            SelectedElement = null;
            UpdateLayers();
            _logger.LogDebug("Deleted element: {ElementName}", element.Name);
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

            // Ensure all default properties are initialized
            duplicate.InitializeDefaultProperties();

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

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        CommandHistory.Undo();
        UpdateLayers();
        _logger.LogDebug("Undo: {Description}", CommandHistory.RedoDescription);
    }

    private bool CanUndo() => CommandHistory.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        CommandHistory.Redo();
        UpdateLayers();
        _logger.LogDebug("Redo: {Description}", CommandHistory.UndoDescription);
    }

    private bool CanRedo() => CommandHistory.CanRedo;

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

    // Multi-selection commands

    /// <summary>
    /// Selects an element with modifier keys support (Ctrl for multi-select, Shift for range)
    /// </summary>
    [RelayCommand]
    private void SelectElement(object? parameter)
    {
        if (parameter is not (DisplayElement element, bool isCtrlPressed, bool isShiftPressed))
        {
            if (parameter is DisplayElement elem)
            {
                SelectionService.SelectSingle(elem);
            }
            return;
        }

        if (isCtrlPressed)
        {
            // Ctrl+Click: Toggle selection
            SelectionService.ToggleSelection(element);
        }
        else if (isShiftPressed)
        {
            // Shift+Click: Range selection
            if (SelectionService.PrimarySelection != null)
            {
                SelectionService.SelectRange(SelectionService.PrimarySelection, element, Elements);
            }
            else
            {
                SelectionService.SelectSingle(element);
            }
        }
        else
        {
            // Normal click: Single selection
            SelectionService.SelectSingle(element);
        }

        _logger.LogDebug("Selected element: {ElementName}, Total selected: {Count}",
            element.Name, SelectionService.SelectionCount);
    }

    /// <summary>
    /// Starts selection rectangle
    /// </summary>
    [RelayCommand]
    private void StartSelectionRectangle(object? parameter)
    {
        if (parameter is (double x, double y))
        {
            IsSelectionRectangleActive = true;
            SelectionRectangleX = x;
            SelectionRectangleY = y;
            SelectionRectangleWidth = 0;
            SelectionRectangleHeight = 0;
            _logger.LogDebug("Started selection rectangle at ({X}, {Y})", x, y);
        }
    }

    /// <summary>
    /// Updates selection rectangle
    /// </summary>
    [RelayCommand]
    private void UpdateSelectionRectangle(object? parameter)
    {
        if (!IsSelectionRectangleActive) return;

        if (parameter is (double x, double y))
        {
            var width = x - SelectionRectangleX;
            var height = y - SelectionRectangleY;

            // Normalize rectangle (handle dragging in any direction)
            if (width < 0)
            {
                SelectionRectangleX += width;
                width = -width;
            }

            if (height < 0)
            {
                SelectionRectangleY += height;
                height = -height;
            }

            SelectionRectangleWidth = width;
            SelectionRectangleHeight = height;
        }
    }

    /// <summary>
    /// Ends selection rectangle and selects elements within it
    /// </summary>
    [RelayCommand]
    private void EndSelectionRectangle()
    {
        if (!IsSelectionRectangleActive) return;

        if (SelectionRectangleWidth > 5 && SelectionRectangleHeight > 5)
        {
            SelectionService.SelectInRectangle(
                SelectionRectangleX,
                SelectionRectangleY,
                SelectionRectangleWidth,
                SelectionRectangleHeight,
                Elements);

            _logger.LogInformation("Selected {Count} elements with rectangle",
                SelectionService.SelectionCount);
        }

        IsSelectionRectangleActive = false;
        SelectionRectangleWidth = 0;
        SelectionRectangleHeight = 0;
    }

    /// <summary>
    /// Deletes all selected elements
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private void DeleteSelected()
    {
        var elementsToDelete = SelectionService.SelectedElements.ToList();

        foreach (var element in elementsToDelete)
        {
            var command = new DeleteElementCommand(Elements, element);
            CommandHistory.ExecuteCommand(command);
        }

        SelectionService.ClearSelection();
        UpdateLayers();

        _logger.LogInformation("Deleted {Count} selected elements", elementsToDelete.Count);
    }

    private bool CanDeleteSelected() => SelectionService.HasSelection;

    /// <summary>
    /// Duplicates all selected elements
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDuplicateSelected))]
    private void DuplicateSelected()
    {
        var elementsToDuplicate = SelectionService.SelectedElements.ToList();
        var newElements = new List<DisplayElement>();

        foreach (var element in elementsToDuplicate)
        {
            var duplicate = new DisplayElement
            {
                Id = Guid.NewGuid().ToString(),
                Type = element.Type,
                Name = $"{element.Name} (Copy)",
                Position = new Position
                {
                    X = element.Position.X + 20,
                    Y = element.Position.Y + 20
                },
                Size = new Size
                {
                    Width = element.Size.Width,
                    Height = element.Size.Height
                },
                ZIndex = Elements.Count + newElements.Count,
                Properties = new Dictionary<string, object>(element.Properties)
            };

            // Ensure all default properties are initialized
            duplicate.InitializeDefaultProperties();

            newElements.Add(duplicate);
            Elements.Add(duplicate);
        }

        SelectionService.SelectMultiple(newElements);
        UpdateLayers();

        _logger.LogInformation("Duplicated {Count} selected elements", newElements.Count);
    }

    private bool CanDuplicateSelected() => SelectionService.HasSelection;

    /// <summary>
    /// Selects all elements
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        SelectionService.SelectMultiple(Elements);
        _logger.LogInformation("Selected all {Count} elements", Elements.Count);
    }

    /// <summary>
    /// Clears selection
    /// </summary>
    [RelayCommand]
    private void ClearSelection()
    {
        SelectionService.ClearSelection();
        _logger.LogDebug("Cleared selection");
    }
}
