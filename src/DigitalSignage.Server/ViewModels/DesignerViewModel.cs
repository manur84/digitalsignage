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

    [ObservableProperty]
    private bool _hasUnsavedChanges = false;

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
            HasUnsavedChanges = true; // Mark as having unsaved changes on any command
        };

        // Subscribe to selection changes
        SelectionService.SelectionChanged += (s, e) =>
        {
            // Update SelectedElement to match primary selection
            SelectedElement = SelectionService.PrimarySelection;
            DeleteSelectedCommand.NotifyCanExecuteChanged();
            DuplicateSelectedCommand.NotifyCanExecuteChanged();
        };

        // Subscribe to Elements collection changes
        Elements.CollectionChanged += (s, e) =>
        {
            HasUnsavedChanges = true;
            UpdateLayers();
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
        HasUnsavedChanges = false; // New layout starts with no unsaved changes
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
            HasUnsavedChanges = false; // New layout starts with no unsaved changes
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
            HasUnsavedChanges = false; // Just loaded, no unsaved changes
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

            // Try to get existing layout
            var existingLayout = await _layoutService.GetLayoutByIdAsync(CurrentLayout.Id);

            if (existingLayout != null)
            {
                // Update existing layout
                await _layoutService.UpdateLayoutAsync(CurrentLayout);
                _logger.LogInformation("Updated layout: {LayoutName} ({Id})", CurrentLayout.Name, CurrentLayout.Id);
            }
            else
            {
                // Create new layout
                await _layoutService.CreateLayoutAsync(CurrentLayout);
                _logger.LogInformation("Created new layout: {LayoutName} ({Id})", CurrentLayout.Name, CurrentLayout.Id);
            }

            // Mark as saved
            HasUnsavedChanges = false;

            // Show success message (optional)
            System.Windows.MessageBox.Show(
                $"Layout '{CurrentLayout.Name}' saved successfully!",
                "Success",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save layout: {LayoutName}", CurrentLayout?.Name);

            // Show error message
            System.Windows.MessageBox.Show(
                $"Failed to save layout: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
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
                ["FontSize"] = 24.0, // MUST be Double for WPF binding
                ["Color"] = "#000000",
                ["FontWeight"] = "Normal"
            }
        };

        // Initialize all default properties to prevent KeyNotFoundException
        textElement.InitializeDefaultProperties();

        _logger.LogInformation("=== Adding Text Element ===");
        _logger.LogInformation("Element ID: {Id}", textElement.Id);
        _logger.LogInformation("Position: ({X}, {Y})", textElement.Position.X, textElement.Position.Y);
        _logger.LogInformation("Size: {Width}x{Height}", textElement.Size.Width, textElement.Size.Height);
        _logger.LogInformation("ZIndex: {ZIndex}", textElement.ZIndex);

        var command = new AddElementCommand(Elements, textElement);
        CommandHistory.ExecuteCommand(command);

        _logger.LogInformation("Element added to collection. Total elements: {Count}", Elements.Count);
        _logger.LogInformation("Elements in collection:");
        foreach (var elem in Elements)
        {
            _logger.LogInformation("  - {Name} at ({X},{Y}) size {W}x{H}",
                elem.Name, elem.Position.X, elem.Position.Y, elem.Size.Width, elem.Size.Height);
        }

        SelectedElement = textElement;
        UpdateLayers();
        _logger.LogInformation("=== Text Element Added Successfully ===");
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

        _logger.LogInformation("=== Adding Image Element ===");
        _logger.LogInformation("Element ID: {Id}", imageElement.Id);
        _logger.LogInformation("Position: ({X}, {Y})", imageElement.Position.X, imageElement.Position.Y);
        _logger.LogInformation("Size: {Width}x{Height}", imageElement.Size.Width, imageElement.Size.Height);

        var command = new AddElementCommand(Elements, imageElement);
        CommandHistory.ExecuteCommand(command);

        _logger.LogInformation("Element added. Total elements: {Count}", Elements.Count);

        SelectedElement = imageElement;
        UpdateLayers();
        _logger.LogInformation("=== Image Element Added Successfully ===");
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

        _logger.LogInformation("=== Adding Rectangle Element ===");
        _logger.LogInformation("Element ID: {Id}", rectangleElement.Id);
        _logger.LogInformation("Position: ({X}, {Y})", rectangleElement.Position.X, rectangleElement.Position.Y);
        _logger.LogInformation("Size: {Width}x{Height}", rectangleElement.Size.Width, rectangleElement.Size.Height);

        var command = new AddElementCommand(Elements, rectangleElement);
        CommandHistory.ExecuteCommand(command);

        _logger.LogInformation("Element added. Total elements: {Count}", Elements.Count);

        SelectedElement = rectangleElement;
        UpdateLayers();
        _logger.LogInformation("=== Rectangle Element Added Successfully ===");
    }

    [RelayCommand]
    private void AddCircleElement()
    {
        var circleElement = new DisplayElement
        {
            Id = Guid.NewGuid().ToString(),
            Type = "circle",
            Name = $"Circle {Elements.Count + 1}",
            Position = new Position { X = 100, Y = 100 },
            Size = new Size { Width = 150, Height = 150 },
            ZIndex = Elements.Count,
            Properties = new Dictionary<string, object>
            {
                ["FillColor"] = "#FFD700",
                ["BorderColor"] = "#FF8C00",
                ["BorderThickness"] = 2
            }
        };

        circleElement.InitializeDefaultProperties();
        var command = new AddElementCommand(Elements, circleElement);
        CommandHistory.ExecuteCommand(command);
        SelectedElement = circleElement;
        UpdateLayers();
        _logger.LogDebug("Added circle element: {ElementName}", circleElement.Name);
    }

    [RelayCommand]
    private void AddQRCodeElement()
    {
        var qrCodeElement = new DisplayElement
        {
            Id = Guid.NewGuid().ToString(),
            Type = "qrcode",
            Name = $"QR Code {Elements.Count + 1}",
            Position = new Position { X = 100, Y = 100 },
            Size = new Size { Width = 200, Height = 200 },
            ZIndex = Elements.Count,
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "https://example.com",
                ["ForegroundColor"] = "#000000",
                ["BackgroundColor"] = "#FFFFFF",
                ["ErrorCorrectionLevel"] = "M"
            }
        };

        qrCodeElement.InitializeDefaultProperties();
        var command = new AddElementCommand(Elements, qrCodeElement);
        CommandHistory.ExecuteCommand(command);
        SelectedElement = qrCodeElement;
        UpdateLayers();
        _logger.LogDebug("Added QR code element: {ElementName}", qrCodeElement.Name);
    }

    [RelayCommand]
    private void AddTableElement()
    {
        var tableElement = new DisplayElement
        {
            Id = Guid.NewGuid().ToString(),
            Type = "table",
            Name = $"Table {Elements.Count + 1}",
            Position = new Position { X = 100, Y = 100 },
            Size = new Size { Width = 600, Height = 400 },
            ZIndex = Elements.Count,
            Properties = new Dictionary<string, object>
            {
                ["DataSource"] = "",
                ["HeaderBackground"] = "#2196F3",
                ["HeaderForeground"] = "#FFFFFF",
                ["RowBackground"] = "#FFFFFF",
                ["AlternateRowBackground"] = "#F5F5F5",
                ["FontSize"] = 14,
                ["ShowBorder"] = true,
                ["BorderColor"] = "#CCCCCC"
            }
        };

        tableElement.InitializeDefaultProperties();
        var command = new AddElementCommand(Elements, tableElement);
        CommandHistory.ExecuteCommand(command);
        SelectedElement = tableElement;
        UpdateLayers();
        _logger.LogDebug("Added table element: {ElementName}", tableElement.Name);
    }

    [RelayCommand]
    private void AddDateTimeElement()
    {
        var dateTimeElement = new DisplayElement
        {
            Id = Guid.NewGuid().ToString(),
            Type = "datetime",
            Name = $"Date Time {Elements.Count + 1}",
            Position = new Position { X = 100, Y = 100 },
            Size = new Size { Width = 300, Height = 60 },
            ZIndex = Elements.Count,
            Properties = new Dictionary<string, object>
            {
                ["Format"] = "dddd, dd MMMM yyyy HH:mm:ss",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 24,
                ["Color"] = "#000000",
                ["UpdateInterval"] = 1000
            }
        };

        dateTimeElement.InitializeDefaultProperties();
        var command = new AddElementCommand(Elements, dateTimeElement);
        CommandHistory.ExecuteCommand(command);
        SelectedElement = dateTimeElement;
        UpdateLayers();
        _logger.LogDebug("Added datetime element: {ElementName}", dateTimeElement.Name);
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

    #region Alignment Commands

    private readonly AlignmentService _alignmentService = new();

    /// <summary>
    /// Aligns selected elements to the left
    /// </summary>
    [RelayCommand]
    private void AlignLeft()
    {
        if (!SelectionService.HasSelection) return;
        
        _alignmentService.AlignLeft(SelectionService.SelectedElements);
        _logger.LogInformation("Aligned {Count} elements to left", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Aligns selected elements to the right
    /// </summary>
    [RelayCommand]
    private void AlignRight()
    {
        if (!SelectionService.HasSelection) return;
        
        _alignmentService.AlignRight(SelectionService.SelectedElements);
        _logger.LogInformation("Aligned {Count} elements to right", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Aligns selected elements to the top
    /// </summary>
    [RelayCommand]
    private void AlignTop()
    {
        if (!SelectionService.HasSelection) return;
        
        _alignmentService.AlignTop(SelectionService.SelectedElements);
        _logger.LogInformation("Aligned {Count} elements to top", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Aligns selected elements to the bottom
    /// </summary>
    [RelayCommand]
    private void AlignBottom()
    {
        if (!SelectionService.HasSelection) return;
        
        _alignmentService.AlignBottom(SelectionService.SelectedElements);
        _logger.LogInformation("Aligned {Count} elements to bottom", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Centers selected elements horizontally
    /// </summary>
    [RelayCommand]
    private void CenterHorizontal()
    {
        if (!SelectionService.HasSelection) return;
        
        _alignmentService.CenterHorizontal(SelectionService.SelectedElements);
        _logger.LogInformation("Centered {Count} elements horizontally", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Centers selected elements vertically
    /// </summary>
    [RelayCommand]
    private void CenterVertical()
    {
        if (!SelectionService.HasSelection) return;
        
        _alignmentService.CenterVertical(SelectionService.SelectedElements);
        _logger.LogInformation("Centered {Count} elements vertically", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Centers selected element on canvas
    /// </summary>
    [RelayCommand]
    private void CenterOnCanvas()
    {
        if (SelectedElement == null || CurrentLayout == null) return;
        
        _alignmentService.CenterOnCanvas(SelectedElement, CurrentLayout.Resolution.Width, CurrentLayout.Resolution.Height);
        _logger.LogInformation("Centered element on canvas");
    }

    /// <summary>
    /// Distributes selected elements horizontally
    /// </summary>
    [RelayCommand]
    private void DistributeHorizontal()
    {
        if (SelectionService.SelectedElements.Count < 3) return;
        
        _alignmentService.DistributeHorizontal(SelectionService.SelectedElements);
        _logger.LogInformation("Distributed {Count} elements horizontally", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Distributes selected elements vertically
    /// </summary>
    [RelayCommand]
    private void DistributeVertical()
    {
        if (SelectionService.SelectedElements.Count < 3) return;
        
        _alignmentService.DistributeVertical(SelectionService.SelectedElements);
        _logger.LogInformation("Distributed {Count} elements vertically", SelectionService.SelectedElements.Count);
    }

    #endregion

    #region Copy/Paste Commands

    private DisplayElement? _clipboardElement;
    private List<DisplayElement>? _clipboardElements;

    /// <summary>
    /// Copies selected element(s) to clipboard
    /// </summary>
    [RelayCommand]
    private void Copy()
    {
        if (!SelectionService.HasSelection) return;

        _clipboardElements = SelectionService.SelectedElements.Select(e => new DisplayElement
        {
            Type = e.Type,
            Name = e.Name,
            Position = new Position { X = e.Position.X, Y = e.Position.Y },
            Size = new Size { Width = e.Size.Width, Height = e.Size.Height },
            ZIndex = e.ZIndex,
            Properties = new Dictionary<string, object>(e.Properties),
            Rotation = e.Rotation,
            Opacity = e.Opacity,
            Visible = e.Visible,
            DataBinding = e.DataBinding
        }).ToList();

        _logger.LogInformation("Copied {Count} element(s) to clipboard", _clipboardElements.Count);
    }

    /// <summary>
    /// Cuts selected element(s) to clipboard
    /// </summary>
    [RelayCommand]
    private void Cut()
    {
        if (!SelectionService.HasSelection) return;

        Copy();
        DeleteSelected();
        _logger.LogInformation("Cut {Count} element(s)", _clipboardElements?.Count ?? 0);
    }

    /// <summary>
    /// Pastes element(s) from clipboard
    /// </summary>
    [RelayCommand]
    private void Paste()
    {
        if (_clipboardElements == null || _clipboardElements.Count == 0) return;

        var newElements = new List<DisplayElement>();
        
        foreach (var clipboardElement in _clipboardElements)
        {
            var newElement = new DisplayElement
            {
                Id = Guid.NewGuid().ToString(),
                Type = clipboardElement.Type,
                Name = $"{clipboardElement.Name} (Copy)",
                Position = new Position
                {
                    X = clipboardElement.Position.X + 20,
                    Y = clipboardElement.Position.Y + 20
                },
                Size = new Size
                {
                    Width = clipboardElement.Size.Width,
                    Height = clipboardElement.Size.Height
                },
                ZIndex = Elements.Count + newElements.Count,
                Properties = new Dictionary<string, object>(clipboardElement.Properties),
                Rotation = clipboardElement.Rotation,
                Opacity = clipboardElement.Opacity,
                Visible = clipboardElement.Visible,
                DataBinding = clipboardElement.DataBinding
            };

            newElement.InitializeDefaultProperties();
            newElements.Add(newElement);

            var command = new AddElementCommand(Elements, newElement);
            CommandHistory.ExecuteCommand(command);
        }

        SelectionService.SelectMultiple(newElements);
        UpdateLayers();

        _logger.LogInformation("Pasted {Count} element(s)", newElements.Count);
    }

    private bool CanPaste() => _clipboardElements != null && _clipboardElements.Count > 0;

    #endregion

    #region Z-Order Commands

    /// <summary>
    /// Brings selected element(s) to front
    /// </summary>
    [RelayCommand]
    private void BringToFront()
    {
        if (!SelectionService.HasSelection) return;

        var maxZIndex = Elements.Max(e => e.ZIndex);
        foreach (var element in SelectionService.SelectedElements.OrderBy(e => e.ZIndex))
        {
            element.ZIndex = ++maxZIndex;
        }

        UpdateLayers();
        _logger.LogInformation("Brought {Count} element(s) to front", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Brings selected element(s) forward one level
    /// </summary>
    [RelayCommand]
    private void BringForward()
    {
        if (!SelectionService.HasSelection) return;

        foreach (var element in SelectionService.SelectedElements.OrderByDescending(e => e.ZIndex))
        {
            element.ZIndex++;
        }

        UpdateLayers();
        _logger.LogInformation("Brought {Count} element(s) forward", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Sends selected element(s) backward one level
    /// </summary>
    [RelayCommand]
    private void SendBackward()
    {
        if (!SelectionService.HasSelection) return;

        foreach (var element in SelectionService.SelectedElements.OrderBy(e => e.ZIndex))
        {
            if (element.ZIndex > 0)
            {
                element.ZIndex--;
            }
        }

        UpdateLayers();
        _logger.LogInformation("Sent {Count} element(s) backward", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Sends selected element(s) to back
    /// </summary>
    [RelayCommand]
    private void SendToBack()
    {
        if (!SelectionService.HasSelection) return;

        var minZIndex = 0;
        foreach (var element in SelectionService.SelectedElements.OrderByDescending(e => e.ZIndex))
        {
            element.ZIndex = minZIndex++;
        }

        // Reindex all other elements
        var otherElements = Elements.Except(SelectionService.SelectedElements).OrderBy(e => e.ZIndex).ToList();
        foreach (var element in otherElements)
        {
            element.ZIndex = minZIndex++;
        }

        UpdateLayers();
        _logger.LogInformation("Sent {Count} element(s) to back", SelectionService.SelectedElements.Count);
    }

    #endregion

    #region Arrow Key Movement Commands

    /// <summary>
    /// Moves selected element(s) left by 1px (or 10px with Shift)
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveElement))]
    private void MoveLeft(bool largeStep = false)
    {
        double delta = largeStep ? 10 : 1;
        foreach (var element in SelectionService.SelectedElements)
        {
            element.Position.X = Math.Max(0, element.Position.X - delta);
        }
        _logger.LogDebug("Moved {Count} element(s) left by {Delta}px", SelectionService.SelectedElements.Count, delta);
    }

    /// <summary>
    /// Moves selected element(s) right by 1px (or 10px with Shift)
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveElement))]
    private void MoveRight(bool largeStep = false)
    {
        double delta = largeStep ? 10 : 1;
        foreach (var element in SelectionService.SelectedElements)
        {
            element.Position.X += delta;
        }
        _logger.LogDebug("Moved {Count} element(s) right by {Delta}px", SelectionService.SelectedElements.Count, delta);
    }

    /// <summary>
    /// Moves selected element(s) up by 1px (or 10px with Shift)
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveElement))]
    private void MoveUp(bool largeStep = false)
    {
        double delta = largeStep ? 10 : 1;
        foreach (var element in SelectionService.SelectedElements)
        {
            element.Position.Y = Math.Max(0, element.Position.Y - delta);
        }
        _logger.LogDebug("Moved {Count} element(s) up by {Delta}px", SelectionService.SelectedElements.Count, delta);
    }

    /// <summary>
    /// Moves selected element(s) down by 1px (or 10px with Shift)
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMoveElement))]
    private void MoveDown(bool largeStep = false)
    {
        double delta = largeStep ? 10 : 1;
        foreach (var element in SelectionService.SelectedElements)
        {
            element.Position.Y += delta;
        }
        _logger.LogDebug("Moved {Count} element(s) down by {Delta}px", SelectionService.SelectedElements.Count, delta);
    }

    private bool CanMoveElement() => SelectionService.HasSelection;

    #endregion

    #region Transform Commands

    /// <summary>
    /// Flips selected element(s) horizontally
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTransformElement))]
    private void FlipHorizontal()
    {
        foreach (var element in SelectionService.SelectedElements)
        {
            // Toggle ScaleX property (add if doesn't exist)
            if (!element.Properties.ContainsKey("ScaleX"))
            {
                element.Properties["ScaleX"] = -1.0;
            }
            else
            {
                double scaleX = Convert.ToDouble(element.Properties["ScaleX"]);
                element.Properties["ScaleX"] = -scaleX;
            }
        }
        _logger.LogInformation("Flipped {Count} element(s) horizontally", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Flips selected element(s) vertically
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTransformElement))]
    private void FlipVertical()
    {
        foreach (var element in SelectionService.SelectedElements)
        {
            // Toggle ScaleY property (add if doesn't exist)
            if (!element.Properties.ContainsKey("ScaleY"))
            {
                element.Properties["ScaleY"] = -1.0;
            }
            else
            {
                double scaleY = Convert.ToDouble(element.Properties["ScaleY"]);
                element.Properties["ScaleY"] = -scaleY;
            }
        }
        _logger.LogInformation("Flipped {Count} element(s) vertically", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Rotates selected element(s) 90 degrees clockwise
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTransformElement))]
    private void Rotate90CW()
    {
        foreach (var element in SelectionService.SelectedElements)
        {
            element.Rotation = (element.Rotation + 90) % 360;
        }
        _logger.LogInformation("Rotated {Count} element(s) 90° clockwise", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Rotates selected element(s) 90 degrees counter-clockwise
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTransformElement))]
    private void Rotate90CCW()
    {
        foreach (var element in SelectionService.SelectedElements)
        {
            element.Rotation = (element.Rotation - 90 + 360) % 360;
        }
        _logger.LogInformation("Rotated {Count} element(s) 90° counter-clockwise", SelectionService.SelectedElements.Count);
    }

    private bool CanTransformElement() => SelectionService.HasSelection;

    #endregion

    #region Grouping Commands

    /// <summary>
    /// Groups selected elements
    /// </summary>
    [RelayCommand]
    private void GroupSelected()
    {
        if (!SelectionService.HasSelection || SelectionService.SelectedElements.Count < 2)
        {
            _logger.LogWarning("Cannot group: Need at least 2 selected elements");
            return;
        }

        // TODO: Implement grouping logic
        // For now, just log the action
        _logger.LogInformation("Grouping {Count} elements (not yet implemented)", SelectionService.SelectedElements.Count);
    }

    /// <summary>
    /// Ungroups selected group
    /// </summary>
    [RelayCommand]
    private void UngroupSelected()
    {
        if (!SelectionService.HasSelection)
        {
            _logger.LogWarning("Cannot ungroup: No selection");
            return;
        }

        // TODO: Implement ungrouping logic
        // For now, just log the action
        _logger.LogInformation("Ungrouping elements (not yet implemented)");
    }

    #endregion
}
