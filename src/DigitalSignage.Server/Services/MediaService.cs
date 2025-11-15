using System.IO;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Drawing.Imaging;

namespace DigitalSignage.Server.Services;

public interface IMediaService
{
    Task<string> SaveMediaAsync(byte[] data, string fileName);
    Task<byte[]?> GetMediaAsync(string fileName);
    Task<bool> DeleteMediaAsync(string fileName);
    Task<List<string>> GetAllMediaFilesAsync();
    Task<string?> GenerateThumbnailAsync(string fileName, int maxWidth = 200, int maxHeight = 200);
    Task<byte[]?> GetThumbnailAsync(string fileName);
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

    public async Task<string> SaveMediaAsync(byte[] data, string fileName)
    {
        if (data == null || data.Length == 0)
        {
            _logger.LogError("SaveMediaAsync called with null or empty data");
            throw new ArgumentException("Data cannot be null or empty", nameof(data));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogError("SaveMediaAsync called with null or empty filename");
            throw new ArgumentException("Filename cannot be empty", nameof(fileName));
        }

        // Validate path traversal
        if (fileName.Contains("..") || Path.GetFileName(fileName) != fileName)
        {
            _logger.LogWarning("Attempted path traversal attack with filename: {FileName}", fileName);
            throw new ArgumentException("Invalid filename", nameof(fileName));
        }

        try
        {
            var filePath = Path.Combine(_mediaDirectory, fileName);
            await File.WriteAllBytesAsync(filePath, data);
            _logger.LogDebug("Saved media file {FileName} ({Size} bytes)", fileName, data.Length);
            return fileName;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error while saving media file {FileName}", fileName);
            throw new InvalidOperationException($"Failed to save media file {fileName}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while saving media file {FileName}", fileName);
            throw new InvalidOperationException($"Access denied for media file {fileName}", ex);
        }
    }

    public async Task<byte[]?> GetMediaAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogWarning("GetMediaAsync called with null or empty filename");
            return null;
        }

        // Validate path traversal
        if (fileName.Contains("..") || Path.GetFileName(fileName) != fileName)
        {
            _logger.LogWarning("Attempted path traversal attack with filename: {FileName}", fileName);
            return null;
        }

        try
        {
            var filePath = Path.Combine(_mediaDirectory, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Media file not found: {FileName}", fileName);
                return null;
            }

            var data = await File.ReadAllBytesAsync(filePath);
            _logger.LogDebug("Retrieved media file {FileName} ({Size} bytes)", fileName, data.Length);
            return data;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error while reading media file {FileName}", fileName);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while reading media file {FileName}", fileName);
            return null;
        }
    }

    public Task<bool> DeleteMediaAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogWarning("DeleteMediaAsync called with null or empty filename");
            return Task.FromResult(false);
        }

        // Validate path traversal
        if (fileName.Contains("..") || Path.GetFileName(fileName) != fileName)
        {
            _logger.LogWarning("Attempted path traversal attack with filename: {FileName}", fileName);
            return Task.FromResult(false);
        }

        try
        {
            var filePath = Path.Combine(_mediaDirectory, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted media file: {FileName}", fileName);
                return Task.FromResult(true);
            }

            _logger.LogDebug("Media file not found for deletion: {FileName}", fileName);
            return Task.FromResult(false);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error while deleting media file {FileName}", fileName);
            return Task.FromResult(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while deleting media file {FileName}", fileName);
            return Task.FromResult(false);
        }
    }

    public Task<List<string>> GetAllMediaFilesAsync()
    {
        try
        {
            if (!Directory.Exists(_mediaDirectory))
            {
                _logger.LogWarning("Media directory does not exist: {Directory}", _mediaDirectory);
                return Task.FromResult(new List<string>());
            }

            var files = Directory.GetFiles(_mediaDirectory)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .ToList();

            _logger.LogDebug("Found {Count} media files", files.Count);
            return Task.FromResult(files);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error while listing media files");
            return Task.FromResult(new List<string>());
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while listing media files");
            return Task.FromResult(new List<string>());
        }
    }

    public async Task<string?> GenerateThumbnailAsync(string fileName, int maxWidth = 200, int maxHeight = 200)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogWarning("GenerateThumbnailAsync called with null or empty filename");
            return null;
        }

        // Validate path traversal
        if (fileName.Contains("..") || Path.GetFileName(fileName) != fileName)
        {
            _logger.LogWarning("Attempted path traversal attack with filename: {FileName}", fileName);
            return null;
        }

        try
        {
            var filePath = Path.Combine(_mediaDirectory, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Source file not found for thumbnail generation: {FileName}", fileName);
                return null;
            }

            // Check if it's an image file
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            if (!imageExtensions.Contains(extension))
            {
                _logger.LogDebug("File {FileName} is not an image, skipping thumbnail generation", fileName);
                return null;
            }

            // Generate thumbnail filename
            var thumbnailFileName = $"thumb_{fileName}";
            var thumbnailPath = Path.Combine(_mediaDirectory, thumbnailFileName);

            // Check if thumbnail already exists
            if (File.Exists(thumbnailPath))
            {
                _logger.LogDebug("Thumbnail already exists for {FileName}", fileName);
                return thumbnailFileName;
            }

            // Load the image
            using var originalImage = await Task.Run(() => Image.FromFile(filePath));

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
            await Task.Run(() => thumbnail.Save(thumbnailPath, ImageFormat.Jpeg));

            _logger.LogInformation("Generated thumbnail for {FileName}: {ThumbnailFileName} ({Width}x{Height})",
                fileName, thumbnailFileName, newWidth, newHeight);

            return thumbnailFileName;
        }
        catch (OutOfMemoryException ex)
        {
            _logger.LogError(ex, "Out of memory while generating thumbnail for {FileName}", fileName);
            return null;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid image format for {FileName}", fileName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating thumbnail for {FileName}", fileName);
            return null;
        }
    }

    public async Task<byte[]?> GetThumbnailAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogWarning("GetThumbnailAsync called with null or empty filename");
            return null;
        }

        // Check if thumbnail exists
        var thumbnailFileName = fileName.StartsWith("thumb_") ? fileName : $"thumb_{fileName}";
        var thumbnailData = await GetMediaAsync(thumbnailFileName);

        if (thumbnailData != null)
        {
            return thumbnailData;
        }

        // If thumbnail doesn't exist, try to generate it
        _logger.LogDebug("Thumbnail not found for {FileName}, attempting to generate", fileName);
        var generatedThumbFileName = await GenerateThumbnailAsync(fileName);

        if (generatedThumbFileName != null)
        {
            return await GetMediaAsync(generatedThumbFileName);
        }

        _logger.LogDebug("Could not generate thumbnail for {FileName}", fileName);
        return null;
    }
}
