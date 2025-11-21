using DigitalSignage.Server.Utilities;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using DigitalSignage.Core.Models;
using DigitalSignage.Core.Utilities;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Enhanced media service with database integration, validation, and metadata management
/// </summary>
public class EnhancedMediaService : IMediaService
{
    private readonly string _mediaDirectory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EnhancedMediaService> _logger;
    private readonly ThumbnailService _thumbnailService;
    private readonly HashSet<string> _allowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg"
    };
    private readonly HashSet<string> _allowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm"
    };
    private readonly HashSet<string> _allowedAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".ogg", ".flac", ".aac", ".wma"
    };
    private readonly HashSet<string> _allowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt"
    };
    private const long MaxFileSizeBytes = 104857600; // 100 MB

    public EnhancedMediaService(
        IServiceProvider serviceProvider,
        ThumbnailService thumbnailService,
        ILogger<EnhancedMediaService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _mediaDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DigitalSignage",
            "Media");

        Directory.CreateDirectory(_mediaDirectory);
        _logger.LogInformation("Media directory initialized: {Directory}", _mediaDirectory);
    }

    public async Task<Result<string>> SaveMediaAsync(byte[] data, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate inputs
            if (data == null || data.Length == 0)
            {
                return Result<string>.Failure("Data cannot be null or empty");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return Result<string>.Failure("Filename cannot be empty");
            }

            // ✅ REFACTOR: Use shared PathHelper to eliminate code duplication
            if (!PathHelper.IsValidFileName(fileName))
            {
                return Result<string>.Failure("Invalid filename");
            }

            // Validate file size
            if (data.Length > MaxFileSizeBytes)
            {
                return Result<string>.Failure($"File size exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024} MB");
            }

            // Validate file extension
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var mediaType = GetMediaType(extension);

            if (mediaType == MediaType.Other && !IsAllowedExtension(extension))
            {
                return Result<string>.Failure($"File type '{extension}' is not allowed");
            }

            // Generate unique filename to avoid conflicts
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(_mediaDirectory, uniqueFileName);

            // Calculate file hash for duplicate detection
            var hash = CalculateSHA256Hash(data);

            // Save file to disk
            await File.WriteAllBytesAsync(filePath, data, cancellationToken);
            _logger.LogInformation("Media file saved: {FileName} ({Size} bytes)", uniqueFileName, data.Length);

            // Save metadata to database
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            // Check for duplicates
            var existingFile = await dbContext.MediaFiles
                .FirstOrDefaultAsync(m => m.Hash == hash, cancellationToken);

            if (existingFile != null)
            {
                _logger.LogWarning("Duplicate file detected: {Hash}, existing file: {ExistingFile}",
                    hash, existingFile.FileName);
                // Delete the newly saved file since it's a duplicate
                File.Delete(filePath);
                return Result<string>.Success(existingFile.FileName);
            }

            var mediaFile = new MediaFile
            {
                FileName = uniqueFileName,
                OriginalFileName = fileName,
                FilePath = filePath,
                Type = mediaType,
                MimeType = GetMimeType(extension),
                FileSizeBytes = data.Length,
                Hash = hash,
                // Single-user mode: All media uploads use User ID 1 (Administrator)
                // Multi-user authentication is not currently implemented
                UploadedByUserId = 1,
                UploadedAt = DateTime.UtcNow
            };

            // Generate thumbnail based on media type
            string? thumbnailPath = null;
            try
            {
                thumbnailPath = mediaType switch
                {
                    MediaType.Image => _thumbnailService.GenerateImageThumbnail(filePath, fileName),
                    MediaType.Video => _thumbnailService.GenerateVideoThumbnail(filePath, fileName),
                    MediaType.Document => _thumbnailService.GenerateDocumentThumbnail(filePath, fileName),
                    _ => null
                };

                if (thumbnailPath != null)
                {
                    mediaFile.ThumbnailPath = thumbnailPath;
                    _logger.LogInformation("Thumbnail generated for {FileName}: {ThumbnailPath}",
                        uniqueFileName, thumbnailPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate thumbnail for {FileName}, continuing without thumbnail",
                    fileName);
                // Continue without thumbnail - not a critical error
            }

            dbContext.MediaFiles.Add(mediaFile);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Media metadata saved to database: {FileName}", uniqueFileName);

            return Result<string>.Success(uniqueFileName);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Save media operation was cancelled for {FileName}", fileName);
            return Result<string>.Failure("Operation was cancelled");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error saving media file: {FileName}", fileName);
            return Result<string>.Failure($"Failed to save file: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied saving media file: {FileName}", fileName);
            return Result<string>.Failure($"Access denied: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save media file: {FileName}", fileName);
            return Result<string>.Failure($"Failed to save media file: {ex.Message}", ex);
        }
    }

    public async Task<Result<byte[]>> GetMediaAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return Result<byte[]>.Failure("Filename cannot be empty");
            }

            // ✅ REFACTOR: Use shared PathHelper to eliminate code duplication
            if (!PathHelper.IsValidFileName(fileName))
            {
                return Result<byte[]>.Failure("Invalid filename");
            }

            var filePath = Path.Combine(_mediaDirectory, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Media file not found: {FileName}", fileName);
                return Result<byte[]>.Failure($"Media file '{fileName}' not found");
            }

            // Update access statistics in database - await properly
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

                var mediaFile = await dbContext.MediaFiles
                    .FirstOrDefaultAsync(m => m.FileName == fileName, cancellationToken);

                if (mediaFile != null)
                {
                    mediaFile.LastAccessedAt = DateTime.UtcNow;
                    mediaFile.AccessCount++;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update media access statistics for {FileName}", fileName);
                // Continue with file retrieval even if statistics update fails
            }

            var data = await File.ReadAllBytesAsync(filePath, cancellationToken);
            _logger.LogDebug("Media file retrieved: {FileName} ({Size} bytes)", fileName, data.Length);
            return Result<byte[]>.Success(data);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Get media operation was cancelled for {FileName}", fileName);
            return Result<byte[]>.Failure("Operation was cancelled");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error retrieving media file: {FileName}", fileName);
            return Result<byte[]>.Failure($"Failed to read file: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied retrieving media file: {FileName}", fileName);
            return Result<byte[]>.Failure($"Access denied: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve media file: {FileName}", fileName);
            return Result<byte[]>.Failure($"Failed to retrieve media file: {ex.Message}", ex);
        }
    }

    public async Task<Result> DeleteMediaAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return Result.Failure("Filename cannot be empty");
            }

            // ✅ REFACTOR: Use shared PathHelper to eliminate code duplication
            if (!PathHelper.IsValidFileName(fileName))
            {
                return Result.Failure("Invalid filename");
            }

            var filePath = Path.Combine(_mediaDirectory, fileName);

            // Delete from database
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            var mediaFile = await dbContext.MediaFiles
                .FirstOrDefaultAsync(m => m.FileName == fileName, cancellationToken);

            if (mediaFile != null)
            {
                // Delete thumbnail if it exists
                if (!string.IsNullOrEmpty(mediaFile.ThumbnailPath))
                {
                    _thumbnailService.DeleteThumbnail(mediaFile.ThumbnailPath);
                }

                dbContext.MediaFiles.Remove(mediaFile);
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Media metadata deleted from database: {FileName}", fileName);
            }

            // Delete file from disk
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath), cancellationToken);
                _logger.LogInformation("Media file deleted from disk: {FileName}", fileName);
                return Result.Success();
            }

            _logger.LogWarning("Media file not found on disk: {FileName}", fileName);
            return Result.Failure($"Media file '{fileName}' not found");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Delete media operation was cancelled for {FileName}", fileName);
            return Result.Failure("Operation was cancelled");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error deleting media file: {FileName}", fileName);
            return Result.Failure($"Failed to delete file: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied deleting media file: {FileName}", fileName);
            return Result.Failure($"Access denied: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete media file: {FileName}", fileName);
            return Result.Failure($"Failed to delete media file: {ex.Message}", ex);
        }
    }

    public async Task<Result<List<string>>> GetAllMediaFilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            var files = await dbContext.MediaFiles
                .OrderByDescending(m => m.UploadedAt)
                .Select(m => m.FileName)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {Count} media files", files.Count);
            return Result<List<string>>.Success(files);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Get all media files operation was cancelled");
            return Result<List<string>>.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve media file list");
            return Result<List<string>>.Failure($"Failed to retrieve media files: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets all media files with full metadata
    /// </summary>
    public async Task<List<MediaFile>> GetAllMediaAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            var files = await dbContext.MediaFiles
                .OrderByDescending(m => m.UploadedAt)
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} media files with metadata", files.Count);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve media file list with metadata");
            throw;
        }
    }

    private MediaType GetMediaType(string extension)
    {
        if (_allowedImageExtensions.Contains(extension))
            return MediaType.Image;
        if (_allowedVideoExtensions.Contains(extension))
            return MediaType.Video;
        if (_allowedAudioExtensions.Contains(extension))
            return MediaType.Audio;
        if (_allowedDocumentExtensions.Contains(extension))
            return MediaType.Document;
        return MediaType.Other;
    }

    private bool IsAllowedExtension(string extension)
    {
        return _allowedImageExtensions.Contains(extension) ||
               _allowedVideoExtensions.Contains(extension) ||
               _allowedAudioExtensions.Contains(extension) ||
               _allowedDocumentExtensions.Contains(extension);
    }

    private string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            ".flv" => "video/x-flv",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".flac" => "audio/flac",
            ".aac" => "audio/aac",
            ".wma" => "audio/x-ms-wma",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private string CalculateSHA256Hash(byte[] data)
    {
        // ✅ REFACTOR: Use shared HashingHelper to eliminate code duplication
        return HashingHelper.ComputeSha256HashFromBytes(data);
    }

    public async Task<Result<string>> GenerateThumbnailAsync(string fileName, int maxWidth = 200, int maxHeight = 200, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return Result<string>.Failure("Filename cannot be empty");
            }

            // ✅ REFACTOR: Use shared PathHelper to eliminate code duplication
            if (!PathHelper.IsValidFileName(fileName))
            {
                return Result<string>.Failure("Invalid filename");
            }

            var filePath = Path.Combine(_mediaDirectory, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found for thumbnail generation: {FileName}", fileName);
                return Result<string>.Failure($"File '{fileName}' not found");
            }

            // Use ThumbnailService to generate thumbnail
            var thumbnailPath = await Task.Run(() => _thumbnailService.GenerateImageThumbnail(filePath, fileName), cancellationToken);

            if (thumbnailPath != null)
            {
                _logger.LogInformation("Generated thumbnail for {FileName}: {ThumbnailPath}", fileName, thumbnailPath);
                return Result<string>.Success(Path.GetFileName(thumbnailPath));
            }

            return Result<string>.Failure($"Failed to generate thumbnail for '{fileName}'");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Generate thumbnail operation was cancelled for {FileName}", fileName);
            return Result<string>.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail for {FileName}", fileName);
            return Result<string>.Failure($"Failed to generate thumbnail: {ex.Message}", ex);
        }
    }

    public async Task<Result<byte[]>> GetThumbnailAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return Result<byte[]>.Failure("Filename cannot be empty");
            }

            // ✅ REFACTOR: Use shared PathHelper to eliminate code duplication
            if (!PathHelper.IsValidFileName(fileName))
            {
                return Result<byte[]>.Failure("Invalid filename");
            }

            // Check if thumbnail already exists for this file
            var thumbnailPattern = $"thumb_{Path.GetFileNameWithoutExtension(fileName)}_*.jpg";
            var thumbnailDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DigitalSignage",
                "Thumbnails");

            if (Directory.Exists(thumbnailDir))
            {
                var thumbnailFiles = Directory.GetFiles(thumbnailDir, thumbnailPattern);
                if (thumbnailFiles.Length > 0)
                {
                    // Return the most recent thumbnail
                    var latestThumbnail = thumbnailFiles
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .First();

                    var data = await File.ReadAllBytesAsync(latestThumbnail.FullName, cancellationToken);
                    return Result<byte[]>.Success(data);
                }
            }

            // If no thumbnail exists, try to generate one
            var thumbnailResult = await GenerateThumbnailAsync(fileName, cancellationToken: cancellationToken);
            if (thumbnailResult.IsSuccess && !string.IsNullOrEmpty(thumbnailResult.Value))
            {
                var thumbnailPath = Path.Combine(thumbnailDir, thumbnailResult.Value);
                if (File.Exists(thumbnailPath))
                {
                    var data = await File.ReadAllBytesAsync(thumbnailPath, cancellationToken);
                    return Result<byte[]>.Success(data);
                }
            }

            return Result<byte[]>.Failure($"Thumbnail not found for '{fileName}'");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Get thumbnail operation was cancelled for {FileName}", fileName);
            return Result<byte[]>.Failure("Operation was cancelled");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error getting thumbnail for {FileName}", fileName);
            return Result<byte[]>.Failure($"Failed to read thumbnail: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get thumbnail for {FileName}", fileName);
            return Result<byte[]>.Failure($"Failed to get thumbnail: {ex.Message}", ex);
        }
    }
}
