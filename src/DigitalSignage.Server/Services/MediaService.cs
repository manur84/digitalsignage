using System.IO;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Drawing.Imaging;
using DigitalSignage.Core.Models;
using DigitalSignage.Core.Utilities;

namespace DigitalSignage.Server.Services;

public interface IMediaService
{
    Task<Result<string>> SaveMediaAsync(byte[] data, string fileName, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> GetMediaAsync(string fileName, CancellationToken cancellationToken = default);
    Task<Result> DeleteMediaAsync(string fileName, CancellationToken cancellationToken = default);
    Task<Result<List<string>>> GetAllMediaFilesAsync(CancellationToken cancellationToken = default);
    Task<Result<string>> GenerateThumbnailAsync(string fileName, int maxWidth = 200, int maxHeight = 200, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> GetThumbnailAsync(string fileName, CancellationToken cancellationToken = default);
}

public class MediaService : IMediaService
{
    private readonly string _mediaDirectory;
    private readonly ILogger<MediaService> _logger;

    public MediaService(ILogger<MediaService> logger)
    {
        _logger = logger;
        _mediaDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DigitalSignage",
            "Media");

        Directory.CreateDirectory(_mediaDirectory);
    }

    public async Task<Result<string>> SaveMediaAsync(byte[] data, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (data == null || data.Length == 0)
            {
                _logger.LogError("SaveMediaAsync called with null or empty data");
                return Result<string>.Failure("Data cannot be null or empty");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogError("SaveMediaAsync called with null or empty filename");
                return Result<string>.Failure("Filename cannot be empty");
            }

            // ✅ REFACTOR: Use shared PathHelper to eliminate code duplication
            if (!PathHelper.IsValidFileName(fileName))
            {
                _logger.LogWarning("Attempted path traversal attack with filename: {FileName}", fileName);
                return Result<string>.Failure("Invalid filename");
            }

            var filePath = Path.Combine(_mediaDirectory, fileName);
            await File.WriteAllBytesAsync(filePath, data, cancellationToken);
            _logger.LogDebug("Saved media file {FileName} ({Size} bytes)", fileName, data.Length);
            return Result<string>.Success(fileName);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Save media operation cancelled for {FileName}", fileName);
            return Result<string>.Failure("Operation was cancelled");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error while saving media file {FileName}", fileName);
            return Result<string>.Failure($"Failed to save media file: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while saving media file {FileName}", fileName);
            return Result<string>.Failure($"Access denied: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while saving media file {FileName}", fileName);
            return Result<string>.Failure($"Failed to save media file: {ex.Message}", ex);
        }
    }

    public async Task<Result<byte[]>> GetMediaAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogWarning("GetMediaAsync called with null or empty filename");
                return Result<byte[]>.Failure("Filename cannot be empty");
            }

            // ✅ REFACTOR: Use shared PathHelper to eliminate code duplication
            if (!PathHelper.IsValidFileName(fileName))
            {
                _logger.LogWarning("Attempted path traversal attack with filename: {FileName}", fileName);
                return Result<byte[]>.Failure("Invalid filename");
            }

            var filePath = Path.Combine(_mediaDirectory, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Media file not found: {FileName}", fileName);
                return Result<byte[]>.Failure($"Media file '{fileName}' not found");
            }

            var data = await File.ReadAllBytesAsync(filePath, cancellationToken);
            _logger.LogDebug("Retrieved media file {FileName} ({Size} bytes)", fileName, data.Length);
            return Result<byte[]>.Success(data);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Get media operation cancelled for {FileName}", fileName);
            return Result<byte[]>.Failure("Operation was cancelled");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error while reading media file {FileName}", fileName);
            return Result<byte[]>.Failure($"Failed to read media file: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while reading media file {FileName}", fileName);
            return Result<byte[]>.Failure($"Access denied: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while reading media file {FileName}", fileName);
            return Result<byte[]>.Failure($"Failed to read media file: {ex.Message}", ex);
        }
    }

    public async Task<Result> DeleteMediaAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogWarning("DeleteMediaAsync called with null or empty filename");
                return Result.Failure("Filename cannot be empty");
            }

            // ✅ REFACTOR: Use shared PathHelper to eliminate code duplication
            if (!PathHelper.IsValidFileName(fileName))
            {
                _logger.LogWarning("Attempted path traversal attack with filename: {FileName}", fileName);
                return Result.Failure("Invalid filename");
            }

            var filePath = Path.Combine(_mediaDirectory, fileName);
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath), cancellationToken);
                _logger.LogInformation("Deleted media file: {FileName}", fileName);
                return Result.Success();
            }

            _logger.LogDebug("Media file not found for deletion: {FileName}", fileName);
            return Result.Failure($"Media file '{fileName}' not found");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Delete media operation cancelled for {FileName}", fileName);
            return Result.Failure("Operation was cancelled");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error while deleting media file {FileName}", fileName);
            return Result.Failure($"Failed to delete media file: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while deleting media file {FileName}", fileName);
            return Result.Failure($"Access denied: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while deleting media file {FileName}", fileName);
            return Result.Failure($"Failed to delete media file: {ex.Message}", ex);
        }
    }

    public async Task<Result<List<string>>> GetAllMediaFilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_mediaDirectory))
            {
                _logger.LogWarning("Media directory does not exist: {Directory}", _mediaDirectory);
                return Result<List<string>>.Success(new List<string>());
            }

            var files = await Task.Run(() =>
            {
                return Directory.GetFiles(_mediaDirectory)
                    .Select(Path.GetFileName)
                    .Where(f => f != null)
                    .Cast<string>()
                    .ToList();
            }, cancellationToken);

            _logger.LogDebug("Found {Count} media files", files.Count);
            return Result<List<string>>.Success(files);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Get all media files operation was cancelled");
            return Result<List<string>>.Failure("Operation was cancelled");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error while listing media files");
            return Result<List<string>>.Failure($"Failed to list media files: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while listing media files");
            return Result<List<string>>.Failure($"Access denied: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while listing media files");
            return Result<List<string>>.Failure($"Failed to list media files: {ex.Message}", ex);
        }
    }

    public async Task<Result<string>> GenerateThumbnailAsync(string fileName, int maxWidth = 200, int maxHeight = 200, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogWarning("GenerateThumbnailAsync called with null or empty filename");
                return Result<string>.Failure("Filename cannot be empty");
            }

            // ✅ REFACTOR: Use shared PathHelper to eliminate code duplication
            if (!PathHelper.IsValidFileName(fileName))
            {
                _logger.LogWarning("Attempted path traversal attack with filename: {FileName}", fileName);
                return Result<string>.Failure("Invalid filename");
            }

            var filePath = Path.Combine(_mediaDirectory, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Source file not found for thumbnail generation: {FileName}", fileName);
                return Result<string>.Failure($"Source file '{fileName}' not found");
            }

            // Check if it's an image file
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            if (!imageExtensions.Contains(extension))
            {
                _logger.LogDebug("File {FileName} is not an image, skipping thumbnail generation", fileName);
                return Result<string>.Failure($"File '{fileName}' is not an image");
            }

            // Generate thumbnail filename
            var thumbnailFileName = $"thumb_{fileName}";
            var thumbnailPath = Path.Combine(_mediaDirectory, thumbnailFileName);

            // Check if thumbnail already exists
            if (File.Exists(thumbnailPath))
            {
                _logger.LogDebug("Thumbnail already exists for {FileName}", fileName);
                return Result<string>.Success(thumbnailFileName);
            }

            // Load the image
            using var originalImage = await Task.Run(() => Image.FromFile(filePath), cancellationToken);

            // Calculate thumbnail dimensions maintaining aspect ratio
            var ratioX = (double)maxWidth / originalImage.Width;
            var ratioY = (double)maxHeight / originalImage.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(originalImage.Width * ratio);
            var newHeight = (int)(originalImage.Height * ratio);

            // Create thumbnail
            using var thumbnail = new Bitmap(newWidth, newHeight);
            using (var graphics = Graphics.FromImage(thumbnail))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);
            }

            // Save thumbnail
            await Task.Run(() => thumbnail.Save(thumbnailPath, ImageFormat.Jpeg), cancellationToken);

            _logger.LogInformation("Generated thumbnail for {FileName}: {ThumbnailFileName} ({Width}x{Height})",
                fileName, thumbnailFileName, newWidth, newHeight);

            return Result<string>.Success(thumbnailFileName);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Thumbnail generation cancelled for {FileName}", fileName);
            return Result<string>.Failure("Operation was cancelled");
        }
        catch (OutOfMemoryException ex)
        {
            _logger.LogError(ex, "Out of memory while generating thumbnail for {FileName}", fileName);
            return Result<string>.Failure($"Out of memory: {ex.Message}", ex);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid image format for {FileName}", fileName);
            return Result<string>.Failure($"Invalid image format: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating thumbnail for {FileName}", fileName);
            return Result<string>.Failure($"Failed to generate thumbnail: {ex.Message}", ex);
        }
    }

    public async Task<Result<byte[]>> GetThumbnailAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogWarning("GetThumbnailAsync called with null or empty filename");
                return Result<byte[]>.Failure("Filename cannot be empty");
            }

            // Check if thumbnail exists
            var thumbnailFileName = fileName.StartsWith("thumb_") ? fileName : $"thumb_{fileName}";
            var thumbnailDataResult = await GetMediaAsync(thumbnailFileName, cancellationToken);

            if (thumbnailDataResult.IsSuccess)
            {
                return thumbnailDataResult;
            }

            // If thumbnail doesn't exist, try to generate it
            _logger.LogDebug("Thumbnail not found for {FileName}, attempting to generate", fileName);
            var generatedThumbFileNameResult = await GenerateThumbnailAsync(fileName, cancellationToken: cancellationToken);

            if (generatedThumbFileNameResult.IsFailure)
            {
                _logger.LogDebug("Could not generate thumbnail for {FileName}: {ErrorMessage}", fileName, generatedThumbFileNameResult.ErrorMessage);
                return Result<byte[]>.Failure($"Thumbnail not found and could not be generated: {generatedThumbFileNameResult.ErrorMessage}");
            }

            return await GetMediaAsync(generatedThumbFileNameResult.Value, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Get thumbnail operation cancelled for {FileName}", fileName);
            return Result<byte[]>.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting thumbnail for {FileName}", fileName);
            return Result<byte[]>.Failure($"Failed to get thumbnail: {ex.Message}", ex);
        }
    }
}
