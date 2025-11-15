using System.IO;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

public interface IMediaService
{
    Task<string> SaveMediaAsync(byte[] data, string fileName);
    Task<byte[]?> GetMediaAsync(string fileName);
    Task<bool> DeleteMediaAsync(string fileName);
    Task<List<string>> GetAllMediaFilesAsync();
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
}
