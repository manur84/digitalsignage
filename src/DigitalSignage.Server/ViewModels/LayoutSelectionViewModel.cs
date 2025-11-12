using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Layout Selection Dialog
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
                FilteredLayouts.Add(layout);
            }

            StatusMessage = $"Loaded {layouts.Count} layouts";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load layouts");
            StatusMessage = $"Error loading layouts: {ex.Message}";
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
    }

    /// <summary>
    /// Command to select a layout and close the dialog
    /// </summary>
    [RelayCommand]
    private void SelectLayout(DisplayLayout? layout)
    {
        if (layout == null)
        {
            _logger.LogWarning("SelectLayout called with null layout");
            return;
        }

        _logger.LogInformation("Layout selected: {LayoutName} (ID: {LayoutId})",
            layout.Name, layout.Id);

        SelectedLayout = layout;

        // Close dialog with success
        CloseRequested?.Invoke(this, true);
    }

    /// <summary>
    /// Command to handle double-click on a layout
    /// </summary>
    [RelayCommand]
    private void LayoutDoubleClick(DisplayLayout? layout)
    {
        SelectLayout(layout);
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
