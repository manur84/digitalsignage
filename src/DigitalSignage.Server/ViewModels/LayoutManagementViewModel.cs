using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Data;
using DigitalSignage.Server.Views;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// Manages layout operations (New, Open, Save, Export, Import).
/// Extracted from MainViewModel to follow Single Responsibility Principle.
/// </summary>
public partial class LayoutManagementViewModel : ObservableObject, IDisposable
{
    private readonly ILayoutService _layoutService;
    private readonly DesignerViewModel _designer;
    private readonly DigitalSignageDbContext _dbContext;
    private readonly ILogger<LayoutManagementViewModel> _logger;
    private readonly IDialogService _dialogService;
    private bool _disposed = false;

    [ObservableProperty]
    private DisplayLayout? _currentLayout;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public LayoutManagementViewModel(
        ILayoutService layoutService,
        DesignerViewModel designerViewModel,
        DigitalSignageDbContext dbContext,
        IDialogService dialogService,
        ILogger<LayoutManagementViewModel> logger)
    {
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _designer = designerViewModel ?? throw new ArgumentNullException(nameof(designerViewModel));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to Designer.HasUnsavedChanges to update Save command
        _designer.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_designer.HasUnsavedChanges))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        };
    }

    [RelayCommand]
    private async Task NewLayout()
    {
        try
        {
            _logger.LogInformation("Opening new layout dialog");
            StatusText = "Create a new layout...";

            // Create the new layout view model
            var newLayoutViewModel = new NewLayoutViewModel(
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<NewLayoutViewModel>());

            // Create and show the dialog
            var dialog = new NewLayoutDialog(newLayoutViewModel);
            var result = dialog.ShowDialog();

            if (result == true && newLayoutViewModel.SelectedResolution != null)
            {
                _logger.LogInformation("Creating new layout: {LayoutName}", newLayoutViewModel.LayoutName);

                // Create new layout
                var newLayout = new DisplayLayout
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = newLayoutViewModel.LayoutName,
                    Description = newLayoutViewModel.Description,
                    Resolution = new Resolution
                    {
                        Width = newLayoutViewModel.SelectedResolution.Width,
                        Height = newLayoutViewModel.SelectedResolution.Height,
                        Orientation = newLayoutViewModel.SelectedResolution.Width > newLayoutViewModel.SelectedResolution.Height ? "landscape" : "portrait"
                    },
                    BackgroundColor = newLayoutViewModel.BackgroundColor,
                    Category = newLayoutViewModel.Category,
                    Tags = string.IsNullOrWhiteSpace(newLayoutViewModel.Tags)
                        ? new List<string>()
                        : newLayoutViewModel.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    Elements = new List<DisplayElement>(),
                    Created = DateTime.UtcNow,
                    Modified = DateTime.UtcNow
                };

                // Load the new layout into Designer
                await _designer.CreateNewLayoutAsync(newLayout);
                CurrentLayout = newLayout;

                StatusText = $"Created new layout: {newLayout.Name}";
                _logger.LogInformation("Successfully created new layout");
            }
            else
            {
                StatusText = "New layout cancelled";
                _logger.LogInformation("New layout creation cancelled by user");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new layout");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task NewFromTemplate()
    {
        try
        {
            _logger.LogInformation("Opening template selection dialog");
            StatusText = "Select a template...";

            // Create the template selection view model
            var templateViewModel = new TemplateSelectionViewModel(
                _dbContext,
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<TemplateSelectionViewModel>());

            // Create and show the dialog
            var dialog = new TemplateSelectionWindow(templateViewModel);
            var result = dialog.ShowDialog();

            if (result == true && templateViewModel.SelectedTemplate != null)
            {
                var template = templateViewModel.SelectedTemplate;
                _logger.LogInformation("Creating layout from template: {TemplateName}", template.Name);

                // Deserialize the elements from the template
                var elements = JsonSerializer.Deserialize<List<DisplayElement>>(template.ElementsJson)
                    ?? new List<DisplayElement>();

                // Create new layout from template with unique name
                var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8); // Short unique ID (8 chars)
                var newLayout = new DisplayLayout
                {
                    Name = $"{template.Name} - {uniqueId}",
                    Resolution = template.Resolution,
                    BackgroundColor = template.BackgroundColor,
                    BackgroundImage = template.BackgroundImage,
                    Elements = elements
                };

                // Update both the current layout and designer
                CurrentLayout = newLayout;
                _designer.CurrentLayout = newLayout;

                // Update the designer's elements collection
                _designer.Elements.Clear();
                foreach (var element in elements)
                {
                    _designer.Elements.Add(element);
                }

                StatusText = $"Layout created: {newLayout.Name}";
                _logger.LogInformation("Successfully created layout '{LayoutName}' from template '{TemplateName}'",
                    newLayout.Name, template.Name);
            }
            else
            {
                StatusText = "Template selection cancelled";
                _logger.LogInformation("Template selection cancelled by user");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create layout from template");
            StatusText = $"Error: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenLayout()
    {
        try
        {
            _logger.LogInformation("=== OPEN LAYOUT COMMAND STARTED ===");
            StatusText = "Select a layout to open...";

            // Create the layout selection view model
            var layoutSelectionViewModel = new LayoutSelectionViewModel(
                _layoutService,
                Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<LayoutSelectionViewModel>());

            _logger.LogInformation("Created LayoutSelectionViewModel, showing dialog...");

            // Create and show the dialog
            var dialog = new LayoutSelectionDialog(layoutSelectionViewModel);
            var result = dialog.ShowDialog();

            _logger.LogInformation("Dialog closed with result: {Result}", result);
            _logger.LogInformation("Selected layout: {LayoutName}", layoutSelectionViewModel.SelectedLayout?.Name ?? "NULL");

            if (result == true && layoutSelectionViewModel.SelectedLayout != null)
            {
                var selectedLayout = layoutSelectionViewModel.SelectedLayout;
                _logger.LogInformation("✅ Layout selected: {LayoutName} (ID: {LayoutId})", selectedLayout.Name, selectedLayout.Id);
                _logger.LogInformation("   Elements count: {Count}", selectedLayout.Elements?.Count ?? 0);
                _logger.LogInformation("   Resolution: {W}x{H}", selectedLayout.Resolution?.Width ?? 0, selectedLayout.Resolution?.Height ?? 0);

                // CRITICAL: Load the selected layout into Designer
                _logger.LogInformation("🔄 Calling Designer.LoadLayoutAsync()...");
                await _designer.LoadLayoutAsync(selectedLayout);

                _logger.LogInformation("✅ Designer.LoadLayoutAsync() completed");
                _logger.LogInformation("   Designer.Elements.Count = {Count}", _designer.Elements.Count);
                _logger.LogInformation("   Designer.CurrentLayout = {LayoutName}", _designer.CurrentLayout?.Name ?? "NULL");

                // Update CurrentLayout
                CurrentLayout = selectedLayout;
                _logger.LogInformation("✅ CurrentLayout updated to: {LayoutName}", CurrentLayout.Name);

                StatusText = $"Loaded layout: {selectedLayout.Name}";
                _logger.LogInformation("=== OPEN LAYOUT COMMAND COMPLETED SUCCESSFULLY ===");
            }
            else
            {
                StatusText = "Layout selection cancelled";
                _logger.LogInformation("Layout selection cancelled by user (result={Result}, selectedLayout={IsNull})",
                    result, layoutSelectionViewModel.SelectedLayout == null ? "NULL" : "NOT NULL");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ FAILED TO OPEN LAYOUT");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        if (CurrentLayout == null)
        {
            StatusText = "No layout to save";
            return;
        }

        try
        {
            _logger.LogInformation("Saving layout: {LayoutName}", CurrentLayout.Name);

            // Update elements from Designer
            CurrentLayout.Elements = _designer.Elements.ToList();
            CurrentLayout.Modified = DateTime.UtcNow;

            if (string.IsNullOrEmpty(CurrentLayout.Id))
            {
                CurrentLayout.Id = Guid.NewGuid().ToString();
                CurrentLayout.Created = DateTime.UtcNow;
                await _layoutService.CreateLayoutAsync(CurrentLayout);
                StatusText = $"Layout created successfully: {CurrentLayout.Name}";
                _logger.LogInformation("Created new layout: {LayoutId}", CurrentLayout.Id);
            }
            else
            {
                await _layoutService.UpdateLayoutAsync(CurrentLayout);
                StatusText = $"Layout saved successfully: {CurrentLayout.Name}";
                _logger.LogInformation("Updated layout: {LayoutId}", CurrentLayout.Id);
            }

            // Reset unsaved changes flag
            _designer.HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save layout");
            StatusText = $"Failed to save layout: {ex.Message}";
            await _dialogService.ShowErrorAsync(
                $"Failed to save layout: {ex.Message}",
                "Error");
        }
    }

    private bool CanSave() => CurrentLayout != null && _designer.HasUnsavedChanges;

    partial void OnCurrentLayoutChanged(DisplayLayout? value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SaveAs()
    {
        if (CurrentLayout == null)
        {
            StatusText = "No layout to save";
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Layout (*.json)|*.json",
                DefaultExt = ".json",
                FileName = $"{CurrentLayout.Name}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                CurrentLayout.Elements = _designer.Elements.ToList();
                CurrentLayout.Modified = DateTime.UtcNow;

                var json = await _layoutService.ExportLayoutAsync(CurrentLayout.Id);
                await System.IO.File.WriteAllTextAsync(dialog.FileName, json);

                StatusText = $"Layout saved to: {dialog.FileName}";
                _logger.LogInformation("Layout saved as: {FileName}", dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save layout as file");
            StatusText = $"Failed to save: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Export()
    {
        if (CurrentLayout == null)
        {
            StatusText = "No layout to export";
            return;
        }

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Layout (*.json)|*.json",
                DefaultExt = ".json",
                FileName = $"{CurrentLayout.Name}_export.json"
            };

            if (dialog.ShowDialog() == true)
            {
                CurrentLayout.Elements = _designer.Elements.ToList();
                var json = await _layoutService.ExportLayoutAsync(CurrentLayout.Id);
                await System.IO.File.WriteAllTextAsync(dialog.FileName, json);

                StatusText = $"Layout exported to: {dialog.FileName}";
                _logger.LogInformation("Layout exported: {FileName}", dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export layout");
            StatusText = $"Failed to export: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Import()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Layout (*.json)|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                StatusText = "Importing layout...";
                var json = await System.IO.File.ReadAllTextAsync(dialog.FileName);
                var layout = await _layoutService.ImportLayoutAsync(json);

                // Load into designer
                await _designer.LoadLayoutAsync(layout);
                CurrentLayout = layout;

                StatusText = $"Layout imported: {layout.Name}";
                _logger.LogInformation("Layout imported from: {FileName}", dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import layout");
            StatusText = $"Failed to import: {ex.Message}";
            await _dialogService.ShowErrorAsync(
                $"Failed to import layout:\n{ex.Message}",
                "Import Error");
        }
    }

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
            // Cleanup if needed
        }

        _disposed = true;
    }
}
