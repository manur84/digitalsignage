using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Layout Selection Dialog with full CRUD operations
/// </summary>
public partial class LayoutSelectionViewModel : ObservableObject
{
    private readonly ILayoutService _layoutService;
    private readonly ILogger<LayoutSelectionViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<DisplayLayout> _layouts = new();

    [ObservableProperty]
    private ObservableCollection<DisplayLayout> _filteredLayouts = new();

    [ObservableProperty]
    private DisplayLayout? _selectedLayout;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Gets whether there are any layouts to display
    /// </summary>
    public bool HasLayouts => FilteredLayouts.Count > 0;

    /// <summary>
    /// Event raised when the dialog should close
    /// </summary>
    public event EventHandler<bool>? CloseRequested;

    public LayoutSelectionViewModel(
        ILayoutService layoutService,
        ILogger<LayoutSelectionViewModel> logger)
    {
        _layoutService = layoutService;
        _logger = logger;

        // Load layouts when constructed
        _ = LoadLayoutsAsync();
    }

    /// <summary>
    /// Load all available layouts from the database
    /// </summary>
    private async Task LoadLayoutsAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading layouts...";

        try
        {
            _logger.LogInformation("Loading layouts from database");

            var layouts = await _layoutService.GetAllLayoutsAsync();

            _logger.LogInformation("Loaded {Count} layouts", layouts.Count);

            Layouts.Clear();
            FilteredLayouts.Clear();
            foreach (var layout in layouts.OrderByDescending(l => l.Modified))
            {
                Layouts.Add(layout);
            }

            FilterLayouts();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load layouts");
            StatusMessage = $"Error loading layouts: {ex.Message}";
            MessageBox.Show(
                $"Failed to load layouts: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Filter layouts based on search text
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        FilterLayouts();
    }

    private void FilterLayouts()
    {
        FilteredLayouts.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var layout in Layouts)
            {
                FilteredLayouts.Add(layout);
            }
        }
        else
        {
            var searchLower = SearchText.ToLower();
            foreach (var layout in Layouts)
            {
                if (layout.Name.ToLower().Contains(searchLower) ||
                    (layout.Description?.ToLower().Contains(searchLower) ?? false))
                {
                    FilteredLayouts.Add(layout);
                }
            }
        }

        StatusMessage = $"Showing {FilteredLayouts.Count} of {Layouts.Count} layouts";
        OnPropertyChanged(nameof(HasLayouts));
    }

    /// <summary>
    /// Command to open selected layout and close the dialog
    /// </summary>
    [RelayCommand]
    private void OpenLayout()
    {
        if (SelectedLayout == null)
        {
            _logger.LogWarning("OpenLayout called with no layout selected");
            MessageBox.Show(
                "Please select a layout to open.",
                "No Layout Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _logger.LogInformation("Layout selected: {LayoutName} (ID: {LayoutId})",
            SelectedLayout.Name, SelectedLayout.Id);

        // Close dialog with success
        CloseRequested?.Invoke(this, true);
    }

    /// <summary>
    /// Command to rename selected layout
    /// </summary>
    [RelayCommand]
    private async Task RenameLayout()
    {
        if (SelectedLayout == null)
        {
            _logger.LogWarning("RenameLayout called with no layout selected");
            return;
        }

        _logger.LogInformation("Renaming layout: {LayoutName} (ID: {LayoutId})",
            SelectedLayout.Name, SelectedLayout.Id);

        // Create a simple input dialog
        var dialog = new Views.Dialogs.InputDialog(
            "Rename Layout",
            "Enter new name for the layout:",
            SelectedLayout.Name);

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var newName = dialog.InputText.Trim();

            if (newName == SelectedLayout.Name)
            {
                _logger.LogInformation("Layout name unchanged, skipping update");
                return;
            }

            // Check if name already exists
            if (Layouts.Any(l => l.Name.Equals(newName, StringComparison.OrdinalIgnoreCase) && l.Id != SelectedLayout.Id))
            {
                MessageBox.Show(
                    $"A layout with the name '{newName}' already exists. Please choose a different name.",
                    "Duplicate Name",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsLoading = true;
            StatusMessage = "Renaming layout...";

            try
            {
                // Update the layout name
                var layoutToUpdate = SelectedLayout;
                layoutToUpdate.Name = newName;
                layoutToUpdate.Modified = DateTime.Now;

                await _layoutService.UpdateLayoutAsync(layoutToUpdate);

                _logger.LogInformation("Layout renamed successfully to: {NewName}", newName);

                // Refresh the layouts list
                await LoadLayoutsAsync();

                // Reselect the renamed layout
                SelectedLayout = FilteredLayouts.FirstOrDefault(l => l.Id == layoutToUpdate.Id);

                MessageBox.Show(
                    $"Layout renamed successfully to '{newName}'.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rename layout");
                MessageBox.Show(
                    $"Failed to rename layout: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Command to duplicate selected layout
    /// </summary>
    [RelayCommand]
    private async Task DuplicateLayout()
    {
        if (SelectedLayout == null)
        {
            _logger.LogWarning("DuplicateLayout called with no layout selected");
            return;
        }

        _logger.LogInformation("Duplicating layout: {LayoutName} (ID: {LayoutId})",
            SelectedLayout.Name, SelectedLayout.Id);

        // Generate a default name for the duplicate
        var baseName = $"{SelectedLayout.Name} - Copy";
        var newName = baseName;
        var counter = 1;

        // Ensure unique name
        while (Layouts.Any(l => l.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
        {
            counter++;
            newName = $"{baseName} ({counter})";
        }

        // Ask user for name
        var dialog = new Views.Dialogs.InputDialog(
            "Duplicate Layout",
            "Enter name for the new layout:",
            newName);

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var duplicateName = dialog.InputText.Trim();

            // Check if name already exists
            if (Layouts.Any(l => l.Name.Equals(duplicateName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    $"A layout with the name '{duplicateName}' already exists. Please choose a different name.",
                    "Duplicate Name",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsLoading = true;
            StatusMessage = "Duplicating layout...";

            try
            {
                var duplicatedLayout = await _layoutService.DuplicateLayoutAsync(
                    SelectedLayout.Id,
                    duplicateName);

                _logger.LogInformation(
                    "Layout duplicated successfully: {NewName} (ID: {NewId})",
                    duplicatedLayout.Name,
                    duplicatedLayout.Id);

                // Refresh the layouts list
                await LoadLayoutsAsync();

                // Select the newly created layout
                SelectedLayout = FilteredLayouts.FirstOrDefault(l => l.Id == duplicatedLayout.Id);

                MessageBox.Show(
                    $"Layout '{duplicateName}' created successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to duplicate layout");
                MessageBox.Show(
                    $"Failed to duplicate layout: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Command to delete selected layout
    /// </summary>
    [RelayCommand]
    private async Task DeleteLayout()
    {
        if (SelectedLayout == null)
        {
            _logger.LogWarning("DeleteLayout called with no layout selected");
            return;
        }

        _logger.LogInformation("Deleting layout: {LayoutName} (ID: {LayoutId})",
            SelectedLayout.Name, SelectedLayout.Id);

        // Show confirmation dialog
        var result = MessageBox.Show(
            $"Are you sure you want to delete the layout '{SelectedLayout.Name}'?\n\n" +
            "This action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Yes)
        {
            IsLoading = true;
            StatusMessage = "Deleting layout...";

            try
            {
                var layoutId = SelectedLayout.Id;
                var layoutName = SelectedLayout.Name;

                var success = await _layoutService.DeleteLayoutAsync(layoutId);

                if (success)
                {
                    _logger.LogInformation("Layout deleted successfully: {LayoutName}", layoutName);

                    // Remove from collections
                    var layoutToRemove = Layouts.FirstOrDefault(l => l.Id == layoutId);
                    if (layoutToRemove != null)
                    {
                        Layouts.Remove(layoutToRemove);
                    }

                    FilterLayouts();

                    // Clear selection
                    SelectedLayout = null;

                    MessageBox.Show(
                        $"Layout '{layoutName}' deleted successfully.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    _logger.LogWarning("Failed to delete layout: {LayoutName}", layoutName);
                    MessageBox.Show(
                        $"Failed to delete layout '{layoutName}'.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete layout");
                MessageBox.Show(
                    $"Failed to delete layout: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Command to show layout properties
    /// </summary>
    [RelayCommand]
    private void ShowProperties()
    {
        if (SelectedLayout == null)
        {
            _logger.LogWarning("ShowProperties called with no layout selected");
            return;
        }

        _logger.LogInformation("Showing properties for layout: {LayoutName} (ID: {LayoutId})",
            SelectedLayout.Name, SelectedLayout.Id);

        var properties = $"Layout Properties\n" +
                        $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                        $"Name: {SelectedLayout.Name}\n" +
                        $"Description: {SelectedLayout.Description ?? "(none)"}\n\n" +
                        $"Resolution: {SelectedLayout.Resolution.Width} × {SelectedLayout.Resolution.Height}\n" +
                        $"Elements: {SelectedLayout.Elements?.Count ?? 0}\n\n" +
                        $"Created: {SelectedLayout.Created:dd.MM.yyyy HH:mm:ss}\n" +
                        $"Modified: {SelectedLayout.Modified:dd.MM.yyyy HH:mm:ss}\n\n" +
                        $"ID: {SelectedLayout.Id}";

        MessageBox.Show(
            properties,
            "Layout Properties",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>
    /// Command to cancel and close the dialog
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _logger.LogInformation("Layout selection cancelled");
        SelectedLayout = null;
        CloseRequested?.Invoke(this, false);
    }

    /// <summary>
    /// Command to refresh the layout list
    /// </summary>
    [RelayCommand]
    private async Task Refresh()
    {
        _logger.LogInformation("Refreshing layout list");
        await LoadLayoutsAsync();
    }

    /// <summary>
    /// Command to clear the search filter
    /// </summary>
    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }
}
