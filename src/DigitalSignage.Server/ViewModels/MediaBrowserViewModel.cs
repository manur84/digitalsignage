using CommunityToolkit.Mvvm.ComponentModel;
using DigitalSignage.Data.Entities;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Linq;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Media Browser Dialog
/// </summary>
public partial class MediaBrowserViewModel : ObservableObject
{
    private readonly EnhancedMediaService _mediaService;
    private readonly ILogger<MediaBrowserViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<MediaFile> _allMedia = new();

    [ObservableProperty]
    private ObservableCollection<MediaFile> _filteredMedia = new();

    [ObservableProperty]
    private MediaFile? _selectedMedia;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All Media";

    /// <summary>
    /// Initializes a new instance of the MediaBrowserViewModel
    /// </summary>
    public MediaBrowserViewModel(EnhancedMediaService mediaService, ILogger<MediaBrowserViewModel> logger)
    {
        _mediaService = mediaService ?? throw new ArgumentNullException(nameof(mediaService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogDebug("MediaBrowserViewModel created");
    }

    /// <summary>
    /// Loads all media files from the database
    /// </summary>
    public async Task LoadMediaAsync()
    {
        try
        {
            _logger.LogInformation("Loading media files...");
            var media = await _mediaService.GetAllMediaAsync();
            AllMedia = new ObservableCollection<MediaFile>(media);
            _logger.LogInformation("Loaded {Count} media files", media.Count);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load media files");
            throw;
        }
    }

    /// <summary>
    /// Called when SearchText property changes
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        _logger.LogDebug("Search text changed: {SearchText}", value);
        ApplyFilter();
    }

    /// <summary>
    /// Called when SelectedFilter property changes
    /// </summary>
    partial void OnSelectedFilterChanged(string value)
    {
        _logger.LogDebug("Filter changed: {Filter}", value);
        ApplyFilter();
    }

    /// <summary>
    /// Applies search and filter to the media collection
    /// </summary>
    private void ApplyFilter()
    {
        var filtered = AllMedia.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(m =>
                (m.FileName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.OriginalFileName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Apply type filter
        switch (SelectedFilter)
        {
            case "Images":
                filtered = filtered.Where(m => m.Type == MediaType.Image);
                break;
            case "Videos":
                filtered = filtered.Where(m => m.Type == MediaType.Video);
                break;
            case "Documents":
                filtered = filtered.Where(m => m.Type == MediaType.Document);
                break;
            case "All Media":
            default:
                // No additional filtering
                break;
        }

        FilteredMedia = new ObservableCollection<MediaFile>(filtered);
        _logger.LogDebug("Filtered to {Count} media files", FilteredMedia.Count);
    }
}
