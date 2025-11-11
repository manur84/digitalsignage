using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Enhanced media service with database integration, validation, and metadata management
/// </summary>
public class EnhancedMediaService : IMediaService
{
    private readonly string _mediaDirectory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EnhancedMediaService> _logger;
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
        ILogger<EnhancedMediaService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _mediaDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DigitalSignage",
            "Media");

        Directory.CreateDirectory(_mediaDirectory);
        _logger.LogInformation("Media directory initialized: {Directory}", _mediaDirectory);
    }

    public async Task<string> SaveMediaAsync(byte[] data, string fileName)
    {
        try
        {
            // Validate file size
            if (data.Length > MaxFileSizeBytes)
            {
                throw new InvalidOperationException($"File size exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024} MB");
            }

            // Validate file extension
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var mediaType = GetMediaType(extension);

            if (mediaType == MediaType.Other && !IsAllowedExtension(extension))
            {
                throw new InvalidOperationException($"File type '{extension}' is not allowed");
            }

            // Generate unique filename to avoid conflicts
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(_mediaDirectory, uniqueFileName);

            // Calculate file hash for duplicate detection
            var hash = CalculateSHA256Hash(data);

            // Save file to disk
            await File.WriteAllBytesAsync(filePath, data);
            _logger.LogInformation("Media file saved: {FileName} ({Size} bytes)", uniqueFileName, data.Length);

            // Save metadata to database
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            // Check for duplicates
            var existingFile = await dbContext.MediaFiles
                .FirstOrDefaultAsync(m => m.Hash == hash);

            if (existingFile != null)
            {
                _logger.LogWarning("Duplicate file detected: {Hash}, existing file: {ExistingFile}",
                    hash, existingFile.FileName);
                // Delete the newly saved file since it's a duplicate
                File.Delete(filePath);
                return existingFile.FileName;
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
                UploadedByUserId = 1, // TODO: Get from current user context
                UploadedAt = DateTime.UtcNow
            };

            dbContext.MediaFiles.Add(mediaFile);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Media metadata saved to database: {FileName}", uniqueFileName);

            return uniqueFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save media file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<byte[]?> GetMediaAsync(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_mediaDirectory, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Media file not found: {FileName}", fileName);
                return null;
            }

            // Update access statistics in database
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

                    var mediaFile = await dbContext.MediaFiles
                        .FirstOrDefaultAsync(m => m.FileName == fileName);

                    if (mediaFile != null)
                    {
                        mediaFile.LastAccessedAt = DateTime.UtcNow;
                        mediaFile.AccessCount++;
                        await dbContext.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update media access statistics for {FileName}", fileName);
                }
            });

            var data = await File.ReadAllBytesAsync(filePath);
            _logger.LogDebug("Media file retrieved: {FileName} ({Size} bytes)", fileName, data.Length);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve media file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<bool> DeleteMediaAsync(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_mediaDirectory, fileName);

            // Delete from database
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            var mediaFile = await dbContext.MediaFiles
                .FirstOrDefaultAsync(m => m.FileName == fileName);

            if (mediaFile != null)
            {
                dbContext.MediaFiles.Remove(mediaFile);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Media metadata deleted from database: {FileName}", fileName);
            }

            // Delete file from disk
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Media file deleted from disk: {FileName}", fileName);
                return true;
            }

            _logger.LogWarning("Media file not found on disk: {FileName}", fileName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete media file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<List<string>> GetAllMediaFilesAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            var files = await dbContext.MediaFiles
                .OrderByDescending(m => m.UploadedAt)
                .Select(m => m.FileName)
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} media files", files.Count);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve media file list");
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
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
