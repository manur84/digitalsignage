using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using DigitalSignage.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for Media Library management
/// </summary>
public partial class MediaLibraryViewModel : ObservableObject
{
    private readonly IMediaService _mediaService;
    private readonly DigitalSignageDbContext _dbContext;
    private readonly ILogger<MediaLibraryViewModel> _logger;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<MediaFile> _mediaFiles = new();

    [ObservableProperty]
    private MediaFile? _selectedMedia;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private MediaType? _filterType;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<MediaType?> FilterTypes { get; } = new()
    {
        null, // All
        MediaType.Image,
        MediaType.Video,
        MediaType.Audio,
        MediaType.Document,
        MediaType.Other
    };

    public MediaLibraryViewModel(
        IMediaService mediaService,
        DigitalSignageDbContext dbContext,
        IDialogService dialogService,
        ILogger<MediaLibraryViewModel> logger)
    {
        _mediaService = mediaService ?? throw new ArgumentNullException(nameof(mediaService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load media files on initialization
        _ = LoadMediaFilesAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = LoadMediaFilesAsync();
    }

    partial void OnFilterTypeChanged(MediaType? value)
    {
        _ = LoadMediaFilesAsync();
    }

    [RelayCommand]
    private async Task LoadMediaFilesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading media files...";

            var query = _dbContext.MediaFiles
                .Include(m => m.UploadedByUser)
                .AsQueryable();

            // Apply type filter
            if (FilterType.HasValue)
            {
                query = query.Where(m => m.Type == FilterType.Value);
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLower();
                query = query.Where(m =>
                    m.OriginalFileName.ToLower().Contains(searchLower) ||
                    (m.Description != null && m.Description.ToLower().Contains(searchLower)) ||
                    (m.Tags != null && m.Tags.ToLower().Contains(searchLower)));
            }

            var files = await query
                .OrderByDescending(m => m.UploadedAt)
                .ToListAsync();

            // Check if already on UI thread to avoid unnecessary context switch
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                MediaFiles.Clear();
                foreach (var file in files)
                {
                    MediaFiles.Add(file);
                }
            }
            else
            {
                dispatcher.Invoke(() =>
                {
                    MediaFiles.Clear();
                    foreach (var file in files)
                    {
                        MediaFiles.Add(file);
                    }
                });
            }

            StatusMessage = $"Loaded {files.Count} media file(s)";
            _logger.LogInformation("Loaded {Count} media files", files.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load media files");
            StatusMessage = $"Error loading media files: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UploadMediaAsync()
    {
        try
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Media File",
                Multiselect = true,
                Filter = "All Supported Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp;*.svg;*.mp4;*.avi;*.mov;*.wmv;*.flv;*.mkv;*.webm;*.mp3;*.wav;*.ogg;*.flac;*.aac;*.wma;*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt|" +
                         "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp;*.svg|" +
                         "Video Files|*.mp4;*.avi;*.mov;*.wmv;*.flv;*.mkv;*.webm|" +
                         "Audio Files|*.mp3;*.wav;*.ogg;*.flac;*.aac;*.wma|" +
                         "Document Files|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt|" +
                         "All Files|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                IsLoading = true;
                var uploadedCount = 0;
                var failedCount = 0;

                foreach (var filePath in openFileDialog.FileNames)
                {
                    try
                    {
                        StatusMessage = $"Uploading {Path.GetFileName(filePath)}...";

                        var fileData = await File.ReadAllBytesAsync(filePath);
                        var fileName = Path.GetFileName(filePath);

                        var saveResult = await _mediaService.SaveMediaAsync(fileData, fileName);

                        if (saveResult.IsFailure)
                        {
                            _logger.LogError("Failed to upload file {FilePath}: {ErrorMessage}", filePath, saveResult.ErrorMessage);
                            failedCount++;
                            continue;
                        }

                        uploadedCount++;
                        _logger.LogInformation("Uploaded media file: {FileName}", fileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to upload file: {FilePath}", filePath);
                        failedCount++;
                    }
                }

                StatusMessage = $"Upload complete: {uploadedCount} successful, {failedCount} failed";

                // Refresh the list
                await LoadMediaFilesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload media");
            StatusMessage = $"Error uploading media: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteMediaAsync()
    {
        if (SelectedMedia == null)
        {
            StatusMessage = "No media selected";
            return;
        }

        try
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"Are you sure you want to delete '{SelectedMedia.OriginalFileName}'?",
                "Confirm Delete");

            if (confirmed)
            {
                IsLoading = true;
                StatusMessage = $"Deleting {SelectedMedia.OriginalFileName}...";

                var deleteResult = await _mediaService.DeleteMediaAsync(SelectedMedia.FileName);

                if (deleteResult.IsFailure)
                {
                    _logger.LogError("Failed to delete media file {FileName}: {ErrorMessage}", SelectedMedia.OriginalFileName, deleteResult.ErrorMessage);
                    StatusMessage = $"Error deleting media: {deleteResult.ErrorMessage}";
                    return;
                }

                StatusMessage = $"Deleted {SelectedMedia.OriginalFileName}";
                _logger.LogInformation("Deleted media file: {FileName}", SelectedMedia.OriginalFileName);

                // Refresh the list
                await LoadMediaFilesAsync();
                SelectedMedia = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete media: {FileName}", SelectedMedia?.OriginalFileName);
            StatusMessage = $"Error deleting media: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadMediaFilesAsync();
    }

    [RelayCommand]
    private void ClearFilter()
    {
        SearchText = string.Empty;
        FilterType = null;
    }

    [RelayCommand]
    private async Task UpdateMediaDetailsAsync()
    {
        if (SelectedMedia == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Updating media details...";

            // Update in database
            _dbContext.MediaFiles.Update(SelectedMedia);
            await _dbContext.SaveChangesAsync();

            StatusMessage = "Media details updated successfully";
            _logger.LogInformation("Updated media details: {FileName}", SelectedMedia.OriginalFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update media details: {FileName}", SelectedMedia?.OriginalFileName);
            StatusMessage = $"Error updating media details: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Gets the file size formatted as a human-readable string
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Gets the icon for the media type
    /// </summary>
    public static string GetMediaTypeIcon(MediaType type)
    {
        return type switch
        {
            MediaType.Image => "🖼",
            MediaType.Video => "🎥",
            MediaType.Audio => "🔊",
            MediaType.Document => "📄",
            MediaType.Other => "📎",
            _ => "📎"
        };
    }
}
