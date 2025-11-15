using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services.FileStorage;

/// <summary>
/// File-based storage service for media management
/// </summary>
public class MediaFileService : FileStorageService<MediaFileInfo>
{
    private readonly ConcurrentDictionary<string, MediaFileInfo> _mediaCache = new();
    private readonly ConcurrentDictionary<string, string> _hashToFileMap = new();
    private const string MEDIA_INDEX_FILE = "media_index.json";
    private string _mediaRootPath;

    public MediaFileService(ILogger<MediaFileService> logger) : base(logger)
    {
        _mediaRootPath = Path.Combine(_storageDirectory, "Media");
        InitializeMediaFolders();
        _ = Task.Run(async () => await LoadMediaIndexAsync());
    }

    protected override string GetSubDirectory() => "Media";

    private void InitializeMediaFolders()
    {
        // Create media subfolders
        Directory.CreateDirectory(Path.Combine(_mediaRootPath, "images"));
        Directory.CreateDirectory(Path.Combine(_mediaRootPath, "videos"));
        Directory.CreateDirectory(Path.Combine(_mediaRootPath, "files"));
        Directory.CreateDirectory(Path.Combine(_mediaRootPath, "thumbnails"));
    }

    /// <summary>
    /// Load media index into cache
    /// </summary>
    private async Task LoadMediaIndexAsync()
    {
        try
        {
            var mediaFiles = await LoadListFromFileAsync(MEDIA_INDEX_FILE);
            _mediaCache.Clear();
            _hashToFileMap.Clear();

            foreach (var media in mediaFiles)
            {
                _mediaCache[media.Id.ToString()] = media;
                if (!string.IsNullOrEmpty(media.Hash))
                {
                    _hashToFileMap[media.Hash] = media.Id.ToString();
                }
            }

            _logger.LogInformation("Loaded {Count} media files into cache", mediaFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load media index");
        }
    }

    /// <summary>
    /// Save media index to file
    /// </summary>
    private async Task SaveMediaIndexAsync()
    {
        try
        {
            var mediaFiles = _mediaCache.Values.ToList();
            await SaveListToFileAsync(MEDIA_INDEX_FILE, mediaFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save media index");
        }
    }

    /// <summary>
    /// Save media file with deduplication
    /// </summary>
    public async Task<MediaFileInfo?> SaveMediaAsync(byte[] data, string originalFileName, MediaFileType type, string? description = null)
    {
        try
        {
            // Calculate hash for deduplication
            var hash = CalculateHash(data);

            // Check if file already exists
            if (_hashToFileMap.TryGetValue(hash, out var existingId) &&
                _mediaCache.TryGetValue(existingId, out var existingMedia))
            {
                _logger.LogInformation("Media file already exists with hash {Hash}, returning existing", hash);
                return existingMedia;
            }

            // Determine subfolder based on type
            var subfolder = type switch
            {
                MediaFileType.Image => "images",
                MediaFileType.Video => "videos",
                _ => "files"
            };

            // Generate unique filename
            var fileId = Guid.NewGuid();
            var extension = Path.GetExtension(originalFileName);
            var fileName = $"{fileId}{extension}";
            var filePath = Path.Combine(_mediaRootPath, subfolder, fileName);

            // Save file to disk
            await File.WriteAllBytesAsync(filePath, data);

            // Generate thumbnail for images
            string? thumbnailPath = null;
            if (type == MediaFileType.Image)
            {
                thumbnailPath = await GenerateThumbnailAsync(data, fileId.ToString());
            }

            // Create media info
            var mediaInfo = new MediaFileInfo
            {
                Id = fileId,
                FileName = fileName,
                OriginalFileName = originalFileName,
                FilePath = Path.Combine(subfolder, fileName),
                Type = type,
                FileSize = data.Length,
                Hash = hash,
                ThumbnailPath = thumbnailPath,
                Description = description,
                UploadedAt = DateTime.UtcNow,
                MimeType = GetMimeType(extension)
            };

            // Add to cache
            _mediaCache[fileId.ToString()] = mediaInfo;
            _hashToFileMap[hash] = fileId.ToString();

            // Save index
            await SaveMediaIndexAsync();

            _logger.LogInformation("Saved media file {FileName} ({FileSize} bytes) with hash {Hash}",
                originalFileName, data.Length, hash);

            return mediaInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save media file {FileName}", originalFileName);
            return null;
        }
    }

    /// <summary>
    /// Get all media files
    /// </summary>
    public Task<List<MediaFileInfo>> GetAllMediaAsync()
    {
        return Task.FromResult(_mediaCache.Values.OrderByDescending(m => m.UploadedAt).ToList());
    }

    /// <summary>
    /// Get media by type
    /// </summary>
    public Task<List<MediaFileInfo>> GetMediaByTypeAsync(MediaFileType type)
    {
        var media = _mediaCache.Values
            .Where(m => m.Type == type)
            .OrderByDescending(m => m.UploadedAt)
            .ToList();
        return Task.FromResult(media);
    }

    /// <summary>
    /// Get media file by ID
    /// </summary>
    public Task<MediaFileInfo?> GetMediaByIdAsync(Guid mediaId)
    {
        _mediaCache.TryGetValue(mediaId.ToString(), out var media);
        return Task.FromResult(media);
    }

    /// <summary>
    /// Get media file data
    /// </summary>
    public async Task<byte[]?> GetMediaDataAsync(Guid mediaId)
    {
        try
        {
            if (_mediaCache.TryGetValue(mediaId.ToString(), out var media))
            {
                var fullPath = Path.Combine(_mediaRootPath, media.FilePath);
                if (File.Exists(fullPath))
                {
                    return await File.ReadAllBytesAsync(fullPath);
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get media data for {MediaId}", mediaId);
            return null;
        }
    }

    /// <summary>
    /// Delete media file
    /// </summary>
    public async Task<bool> DeleteMediaAsync(Guid mediaId)
    {
        try
        {
            if (_mediaCache.TryRemove(mediaId.ToString(), out var media))
            {
                // Remove from hash map
                if (!string.IsNullOrEmpty(media.Hash))
                {
                    _hashToFileMap.TryRemove(media.Hash, out _);
                }

                // Delete physical file
                var fullPath = Path.Combine(_mediaRootPath, media.FilePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                // Delete thumbnail if exists
                if (!string.IsNullOrEmpty(media.ThumbnailPath))
                {
                    var thumbnailFullPath = Path.Combine(_mediaRootPath, media.ThumbnailPath);
                    if (File.Exists(thumbnailFullPath))
                    {
                        File.Delete(thumbnailFullPath);
                    }
                }

                // Save index
                await SaveMediaIndexAsync();

                _logger.LogInformation("Deleted media file {FileName} ({MediaId})", media.OriginalFileName, mediaId);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete media {MediaId}", mediaId);
            return false;
        }
    }

    /// <summary>
    /// Search media files
    /// </summary>
    public Task<List<MediaFileInfo>> SearchMediaAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return GetAllMediaAsync();

        var lowerSearchTerm = searchTerm.ToLowerInvariant();
        var results = _mediaCache.Values
            .Where(m =>
                (m.OriginalFileName?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false) ||
                (m.Description?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false) ||
                (m.Tags?.Any(t => t.ToLowerInvariant().Contains(lowerSearchTerm)) ?? false))
            .OrderByDescending(m => m.UploadedAt)
            .ToList();

        return Task.FromResult(results);
    }

    /// <summary>
    /// Get media statistics
    /// </summary>
    public Task<Dictionary<string, object>> GetMediaStatisticsAsync()
    {
        var stats = new Dictionary<string, object>
        {
            ["TotalFiles"] = _mediaCache.Count,
            ["TotalSize"] = _mediaCache.Values.Sum(m => m.FileSize),
            ["ImageCount"] = _mediaCache.Values.Count(m => m.Type == MediaFileType.Image),
            ["VideoCount"] = _mediaCache.Values.Count(m => m.Type == MediaFileType.Video),
            ["OtherCount"] = _mediaCache.Values.Count(m => m.Type == MediaFileType.Other),
            ["DuplicatesAvoided"] = _hashToFileMap.Count > 0 ? _mediaCache.Count - _hashToFileMap.Count : 0
        };

        return Task.FromResult(stats);
    }

    /// <summary>
    /// Calculate SHA256 hash of data
    /// </summary>
    private string CalculateHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Generate thumbnail for image
    /// </summary>
    private async Task<string?> GenerateThumbnailAsync(byte[] imageData, string fileId)
    {
        try
        {
            using var ms = new MemoryStream(imageData);
            using var image = Image.FromStream(ms);

            // Calculate thumbnail size (max 200x200)
            var maxSize = 200;
            var ratioX = (double)maxSize / image.Width;
            var ratioY = (double)maxSize / image.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);

            using var thumbnail = new Bitmap(newWidth, newHeight);
            using var graphics = Graphics.FromImage(thumbnail);

            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            graphics.DrawImage(image, 0, 0, newWidth, newHeight);

            var thumbnailFileName = $"{fileId}_thumb.jpg";
            var thumbnailPath = Path.Combine(_mediaRootPath, "thumbnails", thumbnailFileName);

            thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);

            return Path.Combine("thumbnails", thumbnailFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail for {FileId}", fileId);
            return null;
        }
    }

    /// <summary>
    /// Get MIME type from file extension
    /// </summary>
    private string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// Media file information
/// </summary>
public class MediaFileInfo
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public MediaFileType Type { get; set; }
    public long FileSize { get; set; }
    public string? Hash { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? MimeType { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}