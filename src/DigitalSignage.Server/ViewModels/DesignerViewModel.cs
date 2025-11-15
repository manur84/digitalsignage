using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Commands;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

public partial class DesignerViewModel : ObservableObject, IDisposable
{
    private readonly ILayoutService _layoutService;
    private readonly ILogger<DesignerViewModel> _logger;
    private readonly EnhancedMediaService _mediaService;
    private readonly ILogger<MediaBrowserViewModel> _mediaBrowserViewModelLogger;
    private readonly ILogger<Views.Dialogs.MediaBrowserDialog> _mediaBrowserDialogLogger;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DataSourceManager _dataSourceManager;
    private bool _disposed = false;

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

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public ObservableCollection<DisplayElement> Elements { get; } = new();
    public ObservableCollection<DisplayElement> Layers { get; } = new();
    public CommandHistory CommandHistory { get; } = new(maxHistorySize: 50);
    public SelectionService SelectionService { get; } = new();

    public DesignerViewModel(
        ILayoutService layoutService,
        ILogger<DesignerViewModel> logger,
        EnhancedMediaService mediaService,
        ILogger<MediaBrowserViewModel> mediaBrowserViewModelLogger,
        ILogger<Views.Dialogs.MediaBrowserDialog> mediaBrowserDialogLogger,
        IDialogService dialogService,
        IServiceProvider serviceProvider,
        DataSourceManager dataSourceManager)
    {
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediaService = mediaService ?? throw new ArgumentNullException(nameof(mediaService));
        _mediaBrowserViewModelLogger = mediaBrowserViewModelLogger ?? throw new ArgumentNullException(nameof(mediaBrowserViewModelLogger));
        _mediaBrowserDialogLogger = mediaBrowserDialogLogger ?? throw new ArgumentNullException(nameof(mediaBrowserDialogLogger));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _dataSourceManager = dataSourceManager ?? throw new ArgumentNullException(nameof(dataSourceManager));

        // Subscribe to command history changes
        CommandHistory.HistoryChanged += OnHistoryChanged;

        // Subscribe to selection changes
        SelectionService.SelectionChanged += OnSelectionChanged;

        // Subscribe to Elements collection changes
        Elements.CollectionChanged += OnElementsCollectionChanged;

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
    public Task CreateNewLayoutAsync(DisplayLayout layout)
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

        return Task.CompletedTask;
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
            _logger.LogInformation("Starting to load layout: {LayoutName}", layout.Name);

            // Ensure we're on the UI thread - check if already on UI thread first
            var dispatcher = System.Windows.Application.Current.Dispatcher;

            Action loadElements = () =>
            {
                CurrentLayout = layout;
                Elements.Clear();
                Layers.Clear();

                if (layout.Elements != null && layout.Elements.Count > 0)
                {
                    _logger.LogInformation("Loading {Count} elements into canvas", layout.Elements.Count);

                    foreach (var element in layout.Elements)
                    {
                        // Initialize default properties for loaded elements to prevent KeyNotFoundException
                        element.InitializeDefaultProperties();

                        // Ensure element is visible
                        element.Visible = true;

                        // Validate and fix Size - CRITICAL for rendering!
                        if (element.Size == null)
                        {
                            _logger.LogWarning("  ⚠ Element {Type} has NULL Size, creating default 100x100", element.Type);
                            element.Size = new Size { Width = 100, Height = 100 };
                        }
                        else if (element.Size.Width <= 0 || element.Size.Height <= 0)
                        {
                            _logger.LogWarning("  ⚠ Element {Type} has invalid Size ({W}x{H}), setting to 100x100",
                                element.Type, element.Size.Width, element.Size.Height);
                            element.Size.Width = Math.Max(element.Size.Width, 100);
                            element.Size.Height = Math.Max(element.Size.Height, 100);
                        }

                        // Validate Position
                        if (element.Position == null)
                        {
                            _logger.LogWarning("  ⚠ Element {Type} has NULL Position, creating default at (0,0)", element.Type);
                            element.Position = new Position { X = 0, Y = 0 };
                        }

                        // Subscribe to ZIndex changes for automatic layer updates
                        element.PropertyChanged += OnElementZIndexChanged;

                        Elements.Add(element);

                        // Use Information instead of Debug so we can see it in logs
                        _logger.LogInformation("  → Element #{Index}: Type={Type}, Pos=({X},{Y}), Size=({W}x{H}), Visible={Visible}, ZIndex={Z}",
                            Elements.Count, element.Type,
                            element.Position?.X ?? 0, element.Position?.Y ?? 0,
                            element.Size?.Width ?? 0, element.Size?.Height ?? 0,
                            element.Visible, element.ZIndex);
                    }

                    _logger.LogInformation("Successfully added {Count} elements to Elements collection", Elements.Count);
                }
                else
                {
                    _logger.LogWarning("Layout has no elements to load");
                }

                UpdateLayers();
                SelectedElement = null;
                HasUnsavedChanges = false; // Just loaded, no unsaved changes

                // CRITICAL: Force UI update - notify that Elements property changed
                // This ensures WPF re-evaluates the binding and updates ItemsControl
                OnPropertyChanged(nameof(Elements));

                _logger.LogInformation("Designer.Elements.Count = {Count}", Elements.Count);
            };

            if (dispatcher.CheckAccess())
            {
                loadElements();
            }
            else
            {
                await dispatcher.InvokeAsync(loadElements);
            }

            _logger.LogInformation("Loaded layout: {LayoutName} with {ElementCount} elements",
                layout.Name, Elements.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load layout: {LayoutName}", layout?.Name ?? "Unknown");
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
            await _dialogService.ShowInformationAsync(
                $"Layout '{CurrentLayout.Name}' saved successfully!",
                "Success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save layout: {LayoutName}", CurrentLayout?.Name);

            // Show error message
            await _dialogService.ShowErrorAsync(
                $"Failed to save layout: {ex.Message}",
                "Error");
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
    private async Task AddMediaLibraryElement()
    {
        _logger.LogInformation("=== Adding Media Library Element ===");

        try
        {
            // Use injected dependencies instead of service locator
            var viewModel = new MediaBrowserViewModel(_mediaService, _mediaBrowserViewModelLogger);

            // Create and show dialog
            var dialog = new Views.Dialogs.MediaBrowserDialog(viewModel, _mediaBrowserDialogLogger, _dialogService)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && dialog.SelectedMedia != null)
            {
                _logger.LogInformation("Media selected: {FilePath}", dialog.SelectedMedia.FilePath);

                // Create image element with selected media
                var imageElement = new DisplayElement
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "image",
                    Name = $"Media {Elements.Count + 1}",
                    Position = new Position { X = 100, Y = 100 },
                    Size = new Size { Width = 300, Height = 200 },
                    ZIndex = Elements.Count,
                    Properties = new Dictionary<string, object>
                    {
                        ["Source"] = dialog.SelectedMedia.FilePath,
                        ["Stretch"] = "Uniform"
                    }
                };

                // Initialize all default properties to prevent KeyNotFoundException
                imageElement.InitializeDefaultProperties();

                _logger.LogInformation("Position: ({X}, {Y})", imageElement.Position.X, imageElement.Position.Y);
                _logger.LogInformation("Size: {Width}x{Height}", imageElement.Size.Width, imageElement.Size.Height);
                _logger.LogInformation("Source: {Source}", imageElement["Source"]);

                var command = new AddElementCommand(Elements, imageElement);
                CommandHistory.ExecuteCommand(command);

                _logger.LogInformation("Element added. Total elements: {Count}", Elements.Count);

                SelectedElement = imageElement;
                UpdateLayers();
                _logger.LogInformation("=== Media Library Element Added Successfully ===");
            }
            else
            {
                _logger.LogInformation("Media selection cancelled, no element added");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding media library element");
            await _dialogService.ShowErrorAsync(
                $"Fehler beim Hinzufügen des Media-Elements:\n\n{ex.Message}",
                "Fehler");
        }
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
    private async Task AddQRCodeElementAsync()
    {
        try
        {
            _logger.LogInformation("=== Opening QR Code Properties Dialog ===");

            // Create ViewModel with required dependencies
            var viewModelLogger = _serviceProvider.GetRequiredService<ILogger<QRCodePropertiesViewModel>>();
            var viewModel = new QRCodePropertiesViewModel(viewModelLogger);

            // Create and show dialog
            var dialog = new Views.Dialogs.QRCodePropertiesDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                _logger.LogInformation("QR Code configured with content: {Content}", viewModel.Content);

                var qrCodeElement = new DisplayElement
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "qrcode",
                    Name = $"QR Code {Elements.Count + 1}",
                    Position = new Position { X = 100, Y = 100 },
                    Size = new Size { Width = 200, Height = 200 },
                    ZIndex = Elements.Count
                };

                // Apply properties from dialog
                viewModel.ApplyToElement(qrCodeElement);
                qrCodeElement.InitializeDefaultProperties();

                _logger.LogInformation("Position: ({X}, {Y})", qrCodeElement.Position.X, qrCodeElement.Position.Y);
                _logger.LogInformation("Size: {Width}x{Height}", qrCodeElement.Size.Width, qrCodeElement.Size.Height);

                // Add to layout
                var command = new AddElementCommand(Elements, qrCodeElement);
                CommandHistory.ExecuteCommand(command);

                _logger.LogInformation("QR Code element added. Total elements: {Count}", Elements.Count);

                SelectedElement = qrCodeElement;
                UpdateLayers();
                _logger.LogInformation("=== QR Code Element Added Successfully ===");
            }
            else
            {
                _logger.LogInformation("QR Code creation cancelled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding QR code element");
            await _dialogService.ShowErrorAsync(
                $"Failed to add QR code element:\n\n{ex.Message}",
                "Error");
        }
    }

    [RelayCommand]
    private async Task AddTableElementAsync()
    {
        try
        {
            _logger.LogInformation("=== Opening Table Properties Dialog ===");

            // Create ViewModel with default values
            var viewModel = new TablePropertiesViewModel();

            // Create and show dialog
            var dialog = new Views.Dialogs.TablePropertiesDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                _logger.LogInformation("Table configured: {Rows}x{Columns}", viewModel.Rows, viewModel.Columns);

                // Convert cell data to JSON-serializable format
                var cellData = viewModel.GetCellDataAsList();
                var cellDataJson = System.Text.Json.JsonSerializer.Serialize(cellData);

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
                        ["Rows"] = viewModel.Rows,
                        ["Columns"] = viewModel.Columns,
                        ["ShowHeaderRow"] = viewModel.ShowHeaderRow,
                        ["ShowHeaderColumn"] = viewModel.ShowHeaderColumn,
                        ["BorderColor"] = TablePropertiesViewModel.ColorToHex(viewModel.BorderColor),
                        ["BorderThickness"] = viewModel.BorderThickness,
                        ["BackgroundColor"] = TablePropertiesViewModel.ColorToHex(viewModel.BackgroundColor),
                        ["AlternateRowColor"] = TablePropertiesViewModel.ColorToHex(viewModel.AlternateRowColor),
                        ["HeaderBackgroundColor"] = TablePropertiesViewModel.ColorToHex(viewModel.HeaderBackgroundColor),
                        ["TextColor"] = TablePropertiesViewModel.ColorToHex(viewModel.TextColor),
                        ["FontFamily"] = viewModel.FontFamily,
                        ["FontSize"] = viewModel.FontSize,
                        ["CellPadding"] = viewModel.CellPadding,
                        ["CellData"] = cellDataJson
                    }
                };

                tableElement.InitializeDefaultProperties();

                _logger.LogInformation("Position: ({X}, {Y})", tableElement.Position.X, tableElement.Position.Y);
                _logger.LogInformation("Size: {Width}x{Height}", tableElement.Size.Width, tableElement.Size.Height);

                var command = new AddElementCommand(Elements, tableElement);
                CommandHistory.ExecuteCommand(command);

                _logger.LogInformation("Table element added. Total elements: {Count}", Elements.Count);

                SelectedElement = tableElement;
                UpdateLayers();
                _logger.LogInformation("=== Table Element Added Successfully ===");
            }
            else
            {
                _logger.LogInformation("Table creation cancelled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding table element");
            await _dialogService.ShowErrorAsync(
                $"Failed to add table element:\n\n{ex.Message}",
                "Error");
        }
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

    /// <summary>
    /// Adds an element at a specific position (used for drag and drop)
    /// </summary>
    [RelayCommand]
    private async Task AddElementAtPositionAsync(object? parameter)
    {
        if (parameter is ValueTuple<string, double, double> dragInfo)
        {
            var (elementType, x, y) = dragInfo;

            _logger.LogInformation("Adding element via drag and drop: Type={Type}, Position=({X}, {Y})",
                elementType, x, y);

            DisplayElement? newElement = null;

            switch (elementType)
            {
                case "text":
                    newElement = new DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "text",
                        Name = $"Text {Elements.Count + 1}",
                        Position = new Position { X = x, Y = y },
                        Size = new Size { Width = 200, Height = 50 },
                        ZIndex = Elements.Count,
                        Properties = new Dictionary<string, object>
                        {
                            ["Content"] = "New Text",
                            ["FontFamily"] = "Arial",
                            ["FontSize"] = 24,
                            ["Color"] = "#000000"
                        }
                    };
                    break;

                case "rectangle":
                    newElement = new DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "rectangle",
                        Name = $"Rectangle {Elements.Count + 1}",
                        Position = new Position { X = x, Y = y },
                        Size = new Size { Width = 200, Height = 100 },
                        ZIndex = Elements.Count,
                        Properties = new Dictionary<string, object>
                        {
                            ["FillColor"] = "#ADD8E6",
                            ["BorderColor"] = "#00008B",
                            ["BorderThickness"] = 2
                        }
                    };
                    break;

                case "circle":
                    newElement = new DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "circle",
                        Name = $"Circle {Elements.Count + 1}",
                        Position = new Position { X = x, Y = y },
                        Size = new Size { Width = 150, Height = 150 },
                        ZIndex = Elements.Count,
                        Properties = new Dictionary<string, object>
                        {
                            ["FillColor"] = "#FFD700",
                            ["BorderColor"] = "#FF8C00",
                            ["BorderThickness"] = 2
                        }
                    };
                    break;

                case "media":
                    // Open media browser dialog
                    await AddMediaLibraryElement();
                    // The above method handles positioning, so return early
                    return;

                case "qrcode":
                    // Open QR code dialog, but position at drop location
                    await AddQRCodeElementWithPositionAsync(x, y);
                    return;

                case "table":
                    // Open table dialog, but position at drop location
                    await AddTableElementWithPositionAsync(x, y);
                    return;

                case "datetime":
                    newElement = new DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "datetime",
                        Name = $"Date Time {Elements.Count + 1}",
                        Position = new Position { X = x, Y = y },
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
                    break;

                default:
                    _logger.LogWarning("Unknown element type for drag and drop: {Type}", elementType);
                    return;
            }

            if (newElement != null)
            {
                newElement.InitializeDefaultProperties();
                var command = new AddElementCommand(Elements, newElement);
                CommandHistory.ExecuteCommand(command);
                SelectedElement = newElement;
                UpdateLayers();
                _logger.LogInformation("Element added via drag and drop: {ElementName} at ({X}, {Y})",
                    newElement.Name, x, y);
            }
        }
    }

    /// <summary>
    /// Adds a QR code element at a specific position
    /// </summary>
    private async Task AddQRCodeElementWithPositionAsync(double x, double y)
    {
        try
        {
            _logger.LogInformation("=== Opening QR Code Properties Dialog (with position) ===");

            var viewModelLogger = _serviceProvider.GetRequiredService<ILogger<QRCodePropertiesViewModel>>();
            var viewModel = new QRCodePropertiesViewModel(viewModelLogger);

            var dialog = new Views.Dialogs.QRCodePropertiesDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                var qrCodeElement = new DisplayElement
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "qrcode",
                    Name = $"QR Code {Elements.Count + 1}",
                    Position = new Position { X = x, Y = y },  // Use drop position
                    Size = new Size { Width = 200, Height = 200 },
                    ZIndex = Elements.Count
                };

                viewModel.ApplyToElement(qrCodeElement);
                qrCodeElement.InitializeDefaultProperties();

                var command = new AddElementCommand(Elements, qrCodeElement);
                CommandHistory.ExecuteCommand(command);

                SelectedElement = qrCodeElement;
                UpdateLayers();
                _logger.LogInformation("QR Code element added at ({X}, {Y})", x, y);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding QR code element");
            await _dialogService.ShowErrorAsync(
                $"Failed to add QR code element:\n\n{ex.Message}",
                "Error");
        }
    }

    /// <summary>
    /// Adds a table element at a specific position
    /// </summary>
    private async Task AddTableElementWithPositionAsync(double x, double y)
    {
        try
        {
            _logger.LogInformation("=== Opening Table Properties Dialog (with position) ===");

            var viewModel = new TablePropertiesViewModel();

            var dialog = new Views.Dialogs.TablePropertiesDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                var cellData = viewModel.GetCellDataAsList();
                var cellDataJson = System.Text.Json.JsonSerializer.Serialize(cellData);

                var tableElement = new DisplayElement
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "table",
                    Name = $"Table {Elements.Count + 1}",
                    Position = new Position { X = x, Y = y },  // Use drop position
                    Size = new Size { Width = 600, Height = 400 },
                    ZIndex = Elements.Count,
                    Properties = new Dictionary<string, object>
                    {
                        ["Rows"] = viewModel.Rows,
                        ["Columns"] = viewModel.Columns,
                        ["ShowHeaderRow"] = viewModel.ShowHeaderRow,
                        ["ShowHeaderColumn"] = viewModel.ShowHeaderColumn,
                        ["BorderColor"] = TablePropertiesViewModel.ColorToHex(viewModel.BorderColor),
                        ["BorderThickness"] = viewModel.BorderThickness,
                        ["BackgroundColor"] = TablePropertiesViewModel.ColorToHex(viewModel.BackgroundColor),
                        ["AlternateRowColor"] = TablePropertiesViewModel.ColorToHex(viewModel.AlternateRowColor),
                        ["HeaderBackgroundColor"] = TablePropertiesViewModel.ColorToHex(viewModel.HeaderBackgroundColor),
                        ["TextColor"] = TablePropertiesViewModel.ColorToHex(viewModel.TextColor),
                        ["FontFamily"] = viewModel.FontFamily,
                        ["FontSize"] = viewModel.FontSize,
                        ["CellPadding"] = viewModel.CellPadding,
                        ["CellData"] = cellDataJson
                    }
                };

                tableElement.InitializeDefaultProperties();

                var command = new AddElementCommand(Elements, tableElement);
                CommandHistory.ExecuteCommand(command);

                SelectedElement = tableElement;
                UpdateLayers();
                _logger.LogInformation("Table element added at ({X}, {Y})", x, y);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding table element");
            await _dialogService.ShowErrorAsync(
                $"Failed to add table element:\n\n{ex.Message}",
                "Error");
        }
    }

    [RelayCommand]
    private async Task AddDataGridElementAsync()
    {
        try
        {
            _logger.LogInformation("=== Opening DataGrid Properties Dialog ===");

            // Create ViewModel with required dependencies
            var viewModelLogger = _serviceProvider.GetRequiredService<ILogger<DataGridPropertiesViewModel>>();
            var viewModel = new DataGridPropertiesViewModel(_dataSourceManager, viewModelLogger);

            // Create and show dialog
            var dialog = new Views.Dialogs.DataGridPropertiesDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                if (viewModel.SelectedDataSource == null)
                {
                    _logger.LogWarning("No data source selected for datagrid");
                    return;
                }

                _logger.LogInformation("DataGrid configured with data source: {DataSourceName}",
                    viewModel.SelectedDataSource.Name);

                var dataGridElement = new DisplayElement
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "datagrid",
                    Name = $"DataGrid - {viewModel.SelectedDataSource.Name}",
                    Position = new Position { X = 100, Y = 100 },
                    Size = new Size { Width = 800, Height = 400 },
                    ZIndex = Elements.Count
                };

                // Apply properties from dialog
                viewModel.ApplyToElement(dataGridElement);
                dataGridElement.InitializeDefaultProperties();

                _logger.LogInformation("Position: ({X}, {Y})", dataGridElement.Position.X, dataGridElement.Position.Y);
                _logger.LogInformation("Size: {Width}x{Height}", dataGridElement.Size.Width, dataGridElement.Size.Height);

                // Add to layout
                var command = new AddElementCommand(Elements, dataGridElement);
                CommandHistory.ExecuteCommand(command);

                // Update linked data sources in layout
                if (CurrentLayout != null)
                {
                    var dataSourceId = viewModel.SelectedDataSource.Id;
                    if (!CurrentLayout.LinkedDataSourceIds.Contains(dataSourceId))
                    {
                        CurrentLayout.LinkedDataSourceIds.Add(dataSourceId);
                        _logger.LogInformation("Added data source {Id} to layout's linked data sources", dataSourceId);
                    }
                }

                _logger.LogInformation("DataGrid element added. Total elements: {Count}", Elements.Count);

                SelectedElement = dataGridElement;
                UpdateLayers();
                _logger.LogInformation("=== DataGrid Element Added Successfully ===");
            }
            else
            {
                _logger.LogInformation("DataGrid creation cancelled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding datagrid element");
            await _dialogService.ShowErrorAsync(
                $"Failed to add datagrid element:\n\n{ex.Message}",
                "Error");
        }
    }

    [RelayCommand]
    private async Task AddDataSourceTextElementAsync()
    {
        try
        {
            _logger.LogInformation("=== Opening DataSourceText Properties Dialog ===");

            // Create ViewModel with required dependencies
            var viewModelLogger = _serviceProvider.GetRequiredService<ILogger<DataSourceTextPropertiesViewModel>>();
            var viewModel = new DataSourceTextPropertiesViewModel(_dataSourceManager, viewModelLogger);

            // Create and show dialog
            var dialog = new Views.Dialogs.DataSourceTextPropertiesDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                if (viewModel.SelectedDataSource == null)
                {
                    _logger.LogWarning("No data source selected for datasourcetext");
                    return;
                }

                _logger.LogInformation("DataSourceText configured with data source: {DataSourceName}",
                    viewModel.SelectedDataSource.Name);

                var dataSourceTextElement = new DisplayElement
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "datasourcetext",
                    Name = $"DataSourceText - {viewModel.SelectedDataSource.Name}",
                    Position = new Position { X = 100, Y = 100 },
                    Size = new Size { Width = 400, Height = 100 },
                    ZIndex = Elements.Count
                };

                // Apply properties from dialog
                viewModel.ApplyToElement(dataSourceTextElement);
                dataSourceTextElement.InitializeDefaultProperties();

                _logger.LogInformation("Position: ({X}, {Y})", dataSourceTextElement.Position.X, dataSourceTextElement.Position.Y);
                _logger.LogInformation("Size: {Width}x{Height}", dataSourceTextElement.Size.Width, dataSourceTextElement.Size.Height);

                // Add to layout
                var command = new AddElementCommand(Elements, dataSourceTextElement);
                CommandHistory.ExecuteCommand(command);

                // Update linked data sources in layout
                if (CurrentLayout != null)
                {
                    var dataSourceId = viewModel.SelectedDataSource.Id;
                    if (!CurrentLayout.LinkedDataSourceIds.Contains(dataSourceId))
                    {
                        CurrentLayout.LinkedDataSourceIds.Add(dataSourceId);
                        _logger.LogInformation("Added data source {Id} to layout's linked data sources", dataSourceId);
                    }
                }

                _logger.LogInformation("DataSourceText element added. Total elements: {Count}", Elements.Count);

                SelectedElement = dataSourceTextElement;
                UpdateLayers();
                _logger.LogInformation("=== DataSourceText Element Added Successfully ===");
            }
            else
            {
                _logger.LogInformation("DataSourceText creation cancelled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding datasourcetext element");
            await _dialogService.ShowErrorAsync(
                $"Failed to add datasourcetext element:\n\n{ex.Message}",
                "Error");
        }
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
    private void AutoSizeText()
    {
        if (SelectedElement != null && SelectedElement.Type == "text")
        {
            // Get text properties
            var content = SelectedElement.Properties.TryGetValue("Content", out var contentObj)
                ? contentObj?.ToString() ?? "Text"
                : "Text";

            var fontSize = SelectedElement.Properties.TryGetValue("FontSize", out var fontSizeObj)
                ? Convert.ToDouble(fontSizeObj)
                : 16;

            var fontFamily = SelectedElement.Properties.TryGetValue("FontFamily", out var fontFamilyObj)
                ? fontFamilyObj?.ToString() ?? "Arial"
                : "Arial";

            var fontWeight = SelectedElement.Properties.TryGetValue("FontWeight", out var fontWeightObj)
                ? fontWeightObj?.ToString() ?? "Normal"
                : "Normal";

            // Calculate required size using FormattedText
            var formattedText = new System.Windows.Media.FormattedText(
                content,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(
                    new System.Windows.Media.FontFamily(fontFamily),
                    fontWeight.Equals("Bold", StringComparison.OrdinalIgnoreCase)
                        ? System.Windows.FontStyles.Normal
                        : System.Windows.FontStyles.Normal,
                    fontWeight.Equals("Bold", StringComparison.OrdinalIgnoreCase)
                        ? System.Windows.FontWeights.Bold
                        : System.Windows.FontWeights.Normal,
                    System.Windows.FontStretches.Normal
                ),
                fontSize,
                System.Windows.Media.Brushes.Black,
                new System.Windows.Media.NumberSubstitution(),
                System.Windows.Media.TextFormattingMode.Display,
                System.Windows.Media.VisualTreeHelper.GetDpi(System.Windows.Application.Current.MainWindow).PixelsPerDip
            );

            // Add padding (10px on each side)
            var newWidth = Math.Ceiling(formattedText.Width) + 20;
            var newHeight = Math.Ceiling(formattedText.Height) + 20;

            // Update element size
            SelectedElement.Size.Width = newWidth;
            SelectedElement.Size.Height = newHeight;

            _logger.LogDebug("Auto-sized text element: {ElementName}, New size: {Width}x{Height}",
                SelectedElement.Name, newWidth, newHeight);
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

    partial void OnHasUnsavedChangesChanged(bool value)
    {
        _logger.LogInformation("HasUnsavedChanges changed to: {Value}", value);
        StatusMessage = value ? "Modified - Unsaved changes" : "Ready";
    }

    [RelayCommand]
    private void ToggleLayerVisibility(DisplayElement element)
    {
        if (element == null) return;

        // Toggle visibility property using indexer for PropertyChanged notification
        bool currentVisibility = element["IsVisible"] as bool? ?? true;
        element["IsVisible"] = !currentVisibility;

        _logger.LogDebug("Toggled visibility for {ElementName}: {IsVisible}",
            element.Name, element["IsVisible"]);

        OnPropertyChanged(nameof(Layers));
    }

    [RelayCommand]
    private void ToggleLayerLock(DisplayElement element)
    {
        if (element == null) return;

        // Toggle lock property using indexer for PropertyChanged notification
        bool currentLock = element["IsLocked"] as bool? ?? false;
        element["IsLocked"] = !currentLock;

        _logger.LogDebug("Toggled lock for {ElementName}: {IsLocked}",
            element.Name, element["IsLocked"]);

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
        return element["IsVisible"] as bool? ?? true;
    }

    /// <summary>
    /// Checks if an element is locked
    /// </summary>
    public static bool IsElementLocked(DisplayElement element)
    {
        return element["IsLocked"] as bool? ?? false;
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
            // Toggle ScaleX property using indexer for PropertyChanged notification
            double currentScaleX = element["ScaleX"] as double? ?? 1.0;
            element["ScaleX"] = -currentScaleX;
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
            // Toggle ScaleY property using indexer for PropertyChanged notification
            double currentScaleY = element["ScaleY"] as double? ?? 1.0;
            element["ScaleY"] = -currentScaleY;
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

        try
        {
            var selectedElements = SelectionService.SelectedElements.ToList();

            // Calculate bounding box for the group
            double minX = selectedElements.Min(e => e.Position.X);
            double minY = selectedElements.Min(e => e.Position.Y);
            double maxX = selectedElements.Max(e => e.Position.X + e.Size.Width);
            double maxY = selectedElements.Max(e => e.Position.Y + e.Size.Height);

            // Create a group element
            var groupElement = new DisplayElement
            {
                Id = Guid.NewGuid().ToString(),
                Type = "group",
                Name = $"Group {DateTime.Now:HHmmss}",
                Position = new Position { X = minX, Y = minY },
                Size = new Size { Width = maxX - minX, Height = maxY - minY },
                ZIndex = selectedElements.Min(e => e.ZIndex),
                Children = new List<DisplayElement>()
            };

            // Add all selected elements as children
            foreach (var element in selectedElements)
            {
                // Store relative position within group
                element.ParentId = groupElement.Id;

                // Adjust child position to be relative to group
                double relativeX = element.Position.X - minX;
                double relativeY = element.Position.Y - minY;
                element.Position.X = relativeX;
                element.Position.Y = relativeY;

                groupElement.Children.Add(element);

                // Remove from layout elements
                if (CurrentLayout != null)
                {
                    CurrentLayout.Elements.Remove(element);
                }
            }

            // Initialize properties for the group
            groupElement.InitializeDefaultProperties();

            // Add group to layout
            if (CurrentLayout != null)
            {
                CurrentLayout.Elements.Add(groupElement);
            }

            // Select the group
            SelectionService.ClearSelection();
            SelectionService.SelectSingle(groupElement);

            _logger.LogInformation("Grouped {Count} elements into {GroupName}", selectedElements.Count, groupElement.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grouping elements");
        }
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

        try
        {
            var selectedElement = SelectionService.SelectedElements.FirstOrDefault();

            if (selectedElement == null || !selectedElement.IsGroup)
            {
                _logger.LogWarning("Cannot ungroup: Selected element is not a group");
                return;
            }

            // Get group position for absolute positioning
            double groupX = selectedElement.Position.X;
            double groupY = selectedElement.Position.Y;

            var children = selectedElement.Children.ToList();

            // Ungroup all children
            foreach (var child in children)
            {
                // Convert relative position back to absolute
                child.Position.X += groupX;
                child.Position.Y += groupY;
                child.ParentId = null;

                // Add back to layout
                if (CurrentLayout != null)
                {
                    CurrentLayout.Elements.Add(child);
                }
            }

            // Remove the group element
            if (CurrentLayout != null)
            {
                CurrentLayout.Elements.Remove(selectedElement);
            }

            // Select the ungrouped children
            SelectionService.ClearSelection();
            foreach (var child in children)
            {
                SelectionService.AddToSelection(child);
            }

            _logger.LogInformation("Ungrouped {Count} elements from {GroupName}", children.Count, selectedElement.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ungrouping elements");
        }
    }

    #endregion

    #region Grid Configuration Commands

    /// <summary>
    /// Opens the Grid Configuration Dialog
    /// </summary>
    [RelayCommand]
    private void OpenGridConfig()
    {
        try
        {
            var viewModel = new GridConfigViewModel(GridSize, "#E0E0E0", ShowGrid, SnapToGrid, true);
            var dialog = new Views.Dialogs.GridConfigDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                GridSize = viewModel.GridSize;
                ShowGrid = viewModel.ShowGrid;
                SnapToGrid = viewModel.SnapToGrid;

                _logger.LogInformation("Grid configuration updated: Size={Size}, ShowGrid={Show}, SnapToGrid={Snap}",
                    GridSize, ShowGrid, SnapToGrid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open grid configuration dialog");
        }
    }

    #endregion

    #region Data Mapping Commands

    /// <summary>
    /// Opens the Data Mapping Dialog for visual SQL → UI element mapping
    /// </summary>
    [RelayCommand]
    private async Task OpenDataMappingAsync()
    {
        try
        {
            if (CurrentLayout == null)
            {
                System.Windows.MessageBox.Show(
                    "Please create or open a layout first before configuring data mapping.",
                    "No Layout",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Check if there are any mappable elements
            var mappableElements = CurrentLayout.Elements
                .Where(e => e.Type == "text" || e.Type == "table" || e.Type == "datetime")
                .ToList();

            if (mappableElements.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "No mappable elements found in the layout.\n\nAdd Text, Table, or DateTime elements to enable data mapping.",
                    "No Mappable Elements",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            // Prompt for data source selection (simplified - in production you'd have a data source selector)
            // For now, we'll pass null and let the user know they need to configure a data source first
            DataSource? dataSource = null;

            // TODO: Add data source selection dialog
            // For MVP, user must configure data source elsewhere first

            var viewModel = _serviceProvider.GetRequiredService<DataMappingViewModel>();
            await viewModel.InitializeAsync(CurrentLayout, dataSource);

            var dialog = new Views.Dialogs.DataMappingDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                _logger.LogInformation("Data mapping configured for layout {LayoutId}", CurrentLayout.Id);

                // Trigger layout save
                if (CurrentLayout != null)
                {
                    await SaveLayoutAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open data mapping dialog");
            System.Windows.MessageBox.Show(
                $"Failed to open data mapping dialog: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    #endregion

    #region Help Commands

    /// <summary>
    /// Opens the Keyboard Shortcuts Help Dialog
    /// </summary>
    [RelayCommand]
    private void ShowKeyboardShortcuts()
    {
        try
        {
            var dialog = new Views.Dialogs.KeyboardShortcutsDialog
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            dialog.ShowDialog();

            _logger.LogInformation("Opened keyboard shortcuts help dialog");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open keyboard shortcuts dialog");
        }
    }

    #endregion

    #region Media Browser Commands

    /// <summary>
    /// Opens the Media Browser Dialog to select an image for an element
    /// </summary>
    [RelayCommand]
    private async Task BrowseImage(DisplayElement? element)
    {
        if (element == null)
        {
            _logger.LogWarning("BrowseImage called with null element");
            return;
        }

        try
        {
            _logger.LogInformation("Opening media browser for element: {ElementName}", element.Name);

            // Use injected dependencies instead of service locator
            var viewModel = new MediaBrowserViewModel(_mediaService, _mediaBrowserViewModelLogger);

            // Create and show dialog
            var dialog = new Views.Dialogs.MediaBrowserDialog(viewModel, _mediaBrowserDialogLogger, _dialogService)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && dialog.SelectedMedia != null)
            {
                // Set the image source to the file path
                element["Source"] = dialog.SelectedMedia.FilePath;
                _logger.LogInformation("Image source set to: {FilePath} for element {ElementName}",
                    dialog.SelectedMedia.FilePath, element.Name);

                // Mark as having unsaved changes
                HasUnsavedChanges = true;
            }
            else
            {
                _logger.LogInformation("Media browser cancelled or no file selected");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing images for element: {ElementName}", element?.Name);
            await _dialogService.ShowErrorAsync(
                $"Failed to open media browser:\n\n{ex.Message}",
                "Error");
        }
    }

    /// <summary>
    /// Opens the QR Code Properties Dialog to edit QR code configuration
    /// </summary>
    [RelayCommand]
    private async Task EditQRCodePropertiesAsync(DisplayElement? element)
    {
        if (element == null || element.Type != "qrcode")
        {
            _logger.LogWarning("EditQRCodeProperties called with null or non-qrcode element");
            return;
        }

        try
        {
            _logger.LogInformation("Opening QR code properties editor for element: {ElementName}", element.Name);

            // Create ViewModel with required dependencies
            var viewModelLogger = _serviceProvider.GetRequiredService<ILogger<QRCodePropertiesViewModel>>();
            var viewModel = new QRCodePropertiesViewModel(viewModelLogger);

            // Load current properties from element
            viewModel.LoadFromElement(element);

            // Create and show dialog
            var dialog = new Views.Dialogs.QRCodePropertiesDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                _logger.LogInformation("QR code properties updated");

                // Apply updated properties to element
                viewModel.ApplyToElement(element);

                // Mark as having unsaved changes
                HasUnsavedChanges = true;

                _logger.LogInformation("QR code properties updated successfully for element {ElementName}", element.Name);
            }
            else
            {
                _logger.LogInformation("QR code properties editor cancelled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing QR code properties for element: {ElementName}", element?.Name);
            await _dialogService.ShowErrorAsync(
                $"Failed to edit QR code properties:\n\n{ex.Message}",
                "Error");
        }
    }

    /// <summary>
    /// Opens the Table Properties Dialog to edit table configuration and data
    /// </summary>
    [RelayCommand]
    private async Task EditTablePropertiesAsync(DisplayElement? element)
    {
        if (element == null || element.Type != "table")
        {
            _logger.LogWarning("EditTableProperties called with null or non-table element");
            return;
        }

        try
        {
            _logger.LogInformation("Opening table properties editor for element: {ElementName}", element.Name);

            // Extract current table properties
            var rows = element.GetProperty("Rows", 3);
            var columns = element.GetProperty("Columns", 3);
            var showHeaderRow = element.GetProperty("ShowHeaderRow", true);
            var showHeaderColumn = element.GetProperty("ShowHeaderColumn", false);
            var borderColor = element.GetProperty("BorderColor", "#000000");
            var borderThickness = element.GetProperty("BorderThickness", 1);
            var backgroundColor = element.GetProperty("BackgroundColor", "#FFFFFF");
            var alternateRowColor = element.GetProperty("AlternateRowColor", "#F5F5F5");
            var headerBackgroundColor = element.GetProperty("HeaderBackgroundColor", "#CCCCCC");
            var textColor = element.GetProperty("TextColor", "#000000");
            var fontFamily = element.GetProperty("FontFamily", "Arial");
            var fontSize = element.GetProperty("FontSize", 14.0);
            var cellPadding = element.GetProperty("CellPadding", 5.0);

            // Parse cell data from JSON
            List<List<string>>? cellData = null;
            if (element.Properties.TryGetValue("CellData", out var cellDataValue) && cellDataValue != null)
            {
                var cellDataString = cellDataValue.ToString();
                if (!string.IsNullOrWhiteSpace(cellDataString) && cellDataString != "[]")
                {
                    try
                    {
                        cellData = System.Text.Json.JsonSerializer.Deserialize<List<List<string>>>(cellDataString);
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse existing cell data JSON");
                    }
                }
            }

            // Create ViewModel with current properties
            var viewModel = new TablePropertiesViewModel(
                rows, columns, showHeaderRow, showHeaderColumn,
                borderColor, borderThickness, backgroundColor, alternateRowColor,
                headerBackgroundColor, textColor, fontFamily, fontSize, cellPadding, cellData);

            // Create and show dialog
            var dialog = new Views.Dialogs.TablePropertiesDialog(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                _logger.LogInformation("Table properties updated: {Rows}x{Columns}", viewModel.Rows, viewModel.Columns);

                // Update table properties
                element["Rows"] = viewModel.Rows;
                element["Columns"] = viewModel.Columns;
                element["ShowHeaderRow"] = viewModel.ShowHeaderRow;
                element["ShowHeaderColumn"] = viewModel.ShowHeaderColumn;
                element["BorderColor"] = TablePropertiesViewModel.ColorToHex(viewModel.BorderColor);
                element["BorderThickness"] = viewModel.BorderThickness;
                element["BackgroundColor"] = TablePropertiesViewModel.ColorToHex(viewModel.BackgroundColor);
                element["AlternateRowColor"] = TablePropertiesViewModel.ColorToHex(viewModel.AlternateRowColor);
                element["HeaderBackgroundColor"] = TablePropertiesViewModel.ColorToHex(viewModel.HeaderBackgroundColor);
                element["TextColor"] = TablePropertiesViewModel.ColorToHex(viewModel.TextColor);
                element["FontFamily"] = viewModel.FontFamily;
                element["FontSize"] = viewModel.FontSize;
                element["CellPadding"] = viewModel.CellPadding;
                element["CellData"] = System.Text.Json.JsonSerializer.Serialize(viewModel.GetCellDataAsList());

                // Mark as having unsaved changes
                HasUnsavedChanges = true;

                _logger.LogInformation("Table properties updated successfully for element {ElementName}", element.Name);
            }
            else
            {
                _logger.LogInformation("Table properties editor cancelled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing table properties for element: {ElementName}", element?.Name);
            await _dialogService.ShowErrorAsync(
                $"Failed to edit table properties:\n\n{ex.Message}",
                "Error");
        }
    }

    #endregion

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unregister event handlers
            CommandHistory.HistoryChanged -= OnHistoryChanged;
            SelectionService.SelectionChanged -= OnSelectionChanged;
            Elements.CollectionChanged -= OnElementsCollectionChanged;
        }

        _disposed = true;
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        HasUnsavedChanges = true;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        SelectedElement = SelectionService.PrimarySelection;
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        DuplicateSelectedCommand.NotifyCanExecuteChanged();
    }

    private void OnElementsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Subscribe/Unsubscribe to ZIndex changes for new/removed elements
        if (e.NewItems != null)
        {
            foreach (DisplayElement element in e.NewItems)
            {
                element.PropertyChanged -= OnElementZIndexChanged; // Prevent double subscription
                element.PropertyChanged += OnElementZIndexChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (DisplayElement element in e.OldItems)
            {
                element.PropertyChanged -= OnElementZIndexChanged;
            }
        }

        HasUnsavedChanges = true;
        UpdateLayers();
    }

    /// <summary>
    /// Handles ZIndex property changes to automatically update the Layers panel
    /// </summary>
    private void OnElementZIndexChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DisplayElement.ZIndex))
        {
            // Update layers when any element's ZIndex changes
            UpdateLayers();
            _logger.LogDebug("ZIndex changed for element, updating layers panel");
        }
    }
}
