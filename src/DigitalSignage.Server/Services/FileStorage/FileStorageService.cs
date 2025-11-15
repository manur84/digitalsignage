using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services.FileStorage;

/// <summary>
/// Base class for file-based storage services
/// </summary>
public abstract class FileStorageService<T> where T : class
{
    protected readonly ILogger _logger;
    protected readonly string _storageDirectory;
    protected readonly SemaphoreSlim _fileLock = new(1, 1);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    protected FileStorageService(ILogger logger)
    {
        _logger = logger;

        // Get base storage directory from AppData
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _storageDirectory = Path.Combine(appDataPath, "DigitalSignage");

        // Ensure base directory exists
        Directory.CreateDirectory(_storageDirectory);
    }

    /// <summary>
    /// Get the specific subdirectory for this storage type
    /// </summary>
    protected abstract string GetSubDirectory();

    /// <summary>
    /// Get the full path to the storage directory
    /// </summary>
    protected string GetStoragePath()
    {
        var path = Path.Combine(_storageDirectory, GetSubDirectory());
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Save an item to JSON file
    /// </summary>
    protected async Task SaveToFileAsync(string fileName, T item, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = Path.Combine(GetStoragePath(), fileName);
            var json = JsonSerializer.Serialize(item, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            _logger.LogDebug("Saved {Type} to {FilePath}", typeof(T).Name, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save {Type} to {FileName}", typeof(T).Name, fileName);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Load an item from JSON file
    /// </summary>
    protected async Task<T?> LoadFromFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = Path.Combine(GetStoragePath(), fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("File {FilePath} does not exist", filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var item = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            _logger.LogDebug("Loaded {Type} from {FilePath}", typeof(T).Name, filePath);
            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load {Type} from {FileName}", typeof(T).Name, fileName);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Save multiple items to a single JSON file
    /// </summary>
    protected async Task SaveListToFileAsync(string fileName, IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = Path.Combine(GetStoragePath(), fileName);
            var json = JsonSerializer.Serialize(items, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            _logger.LogDebug("Saved {Count} {Type} items to {FilePath}", items.Count(), typeof(T).Name, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save {Type} list to {FileName}", typeof(T).Name, fileName);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Load multiple items from a single JSON file
    /// </summary>
    protected async Task<List<T>> LoadListFromFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = Path.Combine(GetStoragePath(), fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("File {FilePath} does not exist, returning empty list", filePath);
                return new List<T>();
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var items = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions) ?? new List<T>();
            _logger.LogDebug("Loaded {Count} {Type} items from {FilePath}", items.Count, typeof(T).Name, filePath);
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load {Type} list from {FileName}", typeof(T).Name, fileName);
            return new List<T>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Delete a file
    /// </summary>
    protected async Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = Path.Combine(GetStoragePath(), fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogDebug("Deleted file {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file {FileName}", fileName);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// List all files in the storage directory
    /// </summary>
    protected async Task<List<string>> ListFilesAsync(string pattern = "*", CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var path = GetStoragePath();
            var files = Directory.GetFiles(path, pattern)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .ToList();

            _logger.LogDebug("Found {Count} files matching pattern {Pattern}", files.Count, pattern);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list files with pattern {Pattern}", pattern);
            return new List<string>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Check if a file exists
    /// </summary>
    protected bool FileExists(string fileName)
    {
        var filePath = Path.Combine(GetStoragePath(), fileName);
        return File.Exists(filePath);
    }

    /// <summary>
    /// Create a backup of a file
    /// </summary>
    protected async Task CreateBackupAsync(string fileName, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = Path.Combine(GetStoragePath(), fileName);
            if (File.Exists(filePath))
            {
                var backupPath = filePath + $".backup_{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(filePath, backupPath, true);
                _logger.LogDebug("Created backup of {FileName} at {BackupPath}", fileName, backupPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup of {FileName}", fileName);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Clean up old backup files
    /// </summary>
    protected async Task CleanupOldBackupsAsync(int daysToKeep = 7, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var path = GetStoragePath();
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);

            var backupFiles = Directory.GetFiles(path, "*.backup_*");
            foreach (var file in backupFiles)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    File.Delete(file);
                    _logger.LogDebug("Deleted old backup file {FileName}", fileInfo.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old backups");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void Dispose()
    {
        _fileLock?.Dispose();
    }
}