using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;

namespace DigitalSignage.Server.Services;

public class LayoutService : ILayoutService, IDisposable
{
    private readonly ConcurrentDictionary<string, DisplayLayout> _layouts = new();
    private readonly string _dataDirectory;
    private readonly ILogger<LayoutService> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private bool _disposed = false;

    // Safe settings for JSON (no type names)
    private static readonly JsonSerializerSettings SafeJsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        Formatting = Formatting.Indented
    };

    // Allowed ID: letters, numbers, dash, underscore; 1..100 chars
    private static readonly Regex LayoutIdRegex = new("^[A-Za-z0-9_-]{1,100}$", RegexOptions.Compiled);
    private static readonly HashSet<string> ReservedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON","PRN","AUX","NUL",
        "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
        "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
    };

    public LayoutService(ILogger<LayoutService> logger)
    {
        _logger = logger;
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DigitalSignage",
            "Layouts");

        try
        {
            Directory.CreateDirectory(_dataDirectory);
            _logger.LogInformation("Layout directory created at {Directory}", _dataDirectory);
            LoadLayoutsFromDisk();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize LayoutService");
            throw;
        }
    }

    public Task<Result<List<DisplayLayout>>> GetAllLayoutsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();
            var layouts = _layouts.Values.ToList();
            return Task.FromResult(Result<List<DisplayLayout>>.Success(layouts));
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "LayoutService has been disposed");
            return Task.FromResult(Result<List<DisplayLayout>>.Failure("Service is no longer available", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all layouts");
            return Task.FromResult(Result<List<DisplayLayout>>.Failure("Failed to retrieve layouts", ex));
        }
    }

    public Task<Result<DisplayLayout>> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(layoutId))
            {
                _logger.LogWarning("GetLayoutByIdAsync called with null or empty layoutId");
                return Task.FromResult(Result<DisplayLayout>.Failure("Layout ID cannot be empty"));
            }

            if (!IsValidLayoutId(layoutId, out var reason))
            {
                _logger.LogWarning("Invalid layoutId provided: {LayoutId}. Reason: {Reason}", layoutId, reason);
                return Task.FromResult(Result<DisplayLayout>.Failure($"Invalid layout ID: {reason}"));
            }

            if (_layouts.TryGetValue(layoutId, out var layout))
            {
                return Task.FromResult(Result<DisplayLayout>.Success(layout));
            }

            _logger.LogWarning("Layout {LayoutId} not found", layoutId);
            return Task.FromResult(Result<DisplayLayout>.Failure($"Layout '{layoutId}' not found"));
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "LayoutService has been disposed");
            return Task.FromResult(Result<DisplayLayout>.Failure("Service is no longer available", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get layout {LayoutId}", layoutId);
            return Task.FromResult(Result<DisplayLayout>.Failure($"Failed to retrieve layout: {ex.Message}", ex));
        }
    }

    public async Task<Result<DisplayLayout>> CreateLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (layout == null)
            {
                return Result<DisplayLayout>.Failure("Layout cannot be null");
            }

            if (string.IsNullOrWhiteSpace(layout.Name))
            {
                return Result<DisplayLayout>.Failure("Layout name is required");
            }

            if (layout.Name.Length > 200)
            {
                return Result<DisplayLayout>.Failure("Layout name is too long (maximum 200 characters)");
            }

            EnsureLayoutDefaults(layout);
            layout.Id = Guid.NewGuid().ToString();
            layout.Created = DateTime.UtcNow;
            layout.Modified = DateTime.UtcNow;

            _layouts[layout.Id] = layout;
            await SaveLayoutToDiskAsync(layout, cancellationToken);

            _logger.LogInformation("Created layout {LayoutId} with name {LayoutName}", layout.Id, layout.Name);
            return Result<DisplayLayout>.Success(layout);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "LayoutService has been disposed");
            return Result<DisplayLayout>.Failure("Service is no longer available", ex);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Layout creation cancelled for {LayoutName}", layout?.Name);
            return Result<DisplayLayout>.Failure("Operation was cancelled");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to save layout {LayoutName} to file", layout?.Name);
            // Remove from memory if file save failed
            if (layout != null && !string.IsNullOrWhiteSpace(layout.Id))
            {
                _layouts.TryRemove(layout.Id, out _);
            }
            return Result<DisplayLayout>.Failure($"Failed to save layout: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create layout {LayoutName}", layout?.Name);
            return Result<DisplayLayout>.Failure($"Failed to create layout: {ex.Message}", ex);
        }
    }

    public async Task<Result<DisplayLayout>> UpdateLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (layout == null)
            {
                return Result<DisplayLayout>.Failure("Layout cannot be null");
            }

            if (string.IsNullOrWhiteSpace(layout.Id))
            {
                return Result<DisplayLayout>.Failure("Layout ID cannot be empty");
            }

            if (!IsValidLayoutId(layout.Id, out var reason))
            {
                return Result<DisplayLayout>.Failure($"Invalid layout ID: {reason}");
            }

            if (string.IsNullOrWhiteSpace(layout.Name))
            {
                return Result<DisplayLayout>.Failure("Layout name is required");
            }

            if (layout.Name.Length > 200)
            {
                return Result<DisplayLayout>.Failure("Layout name is too long (maximum 200 characters)");
            }

            if (!_layouts.ContainsKey(layout.Id))
            {
                _logger.LogWarning("Layout {LayoutId} not found for update", layout.Id);
                return Result<DisplayLayout>.Failure($"Layout '{layout.Id}' not found");
            }

            EnsureLayoutDefaults(layout);
            layout.Modified = DateTime.UtcNow;
            _layouts[layout.Id] = layout;
            await SaveLayoutToDiskAsync(layout, cancellationToken);

            _logger.LogInformation("Updated layout {LayoutId}", layout.Id);
            return Result<DisplayLayout>.Success(layout);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "LayoutService has been disposed");
            return Result<DisplayLayout>.Failure("Service is no longer available", ex);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Layout update cancelled for {LayoutId}", layout?.Id);
            return Result<DisplayLayout>.Failure("Operation was cancelled");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to save layout {LayoutId} to file", layout?.Id);
            return Result<DisplayLayout>.Failure($"Failed to save layout: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update layout {LayoutId}", layout?.Id);
            return Result<DisplayLayout>.Failure($"Failed to update layout: {ex.Message}", ex);
        }
    }

    public Task<Result> DeleteLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(layoutId))
            {
                _logger.LogWarning("DeleteLayoutAsync called with null or empty layoutId");
                return Task.FromResult(Result.Failure("Layout ID cannot be empty"));
            }

            if (!IsValidLayoutId(layoutId, out var reason))
            {
                _logger.LogWarning("Invalid layoutId provided for deletion: {LayoutId}. Reason: {Reason}", layoutId, reason);
                return Task.FromResult(Result.Failure($"Invalid layout ID: {reason}"));
            }

            if (_layouts.TryRemove(layoutId, out _))
            {
                var filePath = GetLayoutFilePath(layoutId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                _logger.LogInformation("Deleted layout {LayoutId}", layoutId);
                return Task.FromResult(Result.Success());
            }

            _logger.LogWarning("Layout {LayoutId} not found for deletion", layoutId);
            return Task.FromResult(Result.Failure($"Layout '{layoutId}' not found"));
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "LayoutService has been disposed");
            return Task.FromResult(Result.Failure("Service is no longer available", ex));
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to delete layout file {LayoutId}", layoutId);
            return Task.FromResult(Result.Failure($"Failed to delete layout file: {ex.Message}", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete layout {LayoutId}", layoutId);
            return Task.FromResult(Result.Failure($"Failed to delete layout: {ex.Message}", ex));
        }
    }

    public async Task<Result<DisplayLayout>> DuplicateLayoutAsync(
        string layoutId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(layoutId))
            {
                return Result<DisplayLayout>.Failure("Layout ID cannot be empty");
            }

            if (!IsValidLayoutId(layoutId, out var reason))
            {
                return Result<DisplayLayout>.Failure($"Invalid layout ID: {reason}");
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                return Result<DisplayLayout>.Failure("New layout name is required");
            }

            if (newName.Length > 200)
            {
                return Result<DisplayLayout>.Failure("Layout name is too long (maximum 200 characters)");
            }

            var originalResult = await GetLayoutByIdAsync(layoutId, cancellationToken);
            if (originalResult.IsFailure)
            {
                return Result<DisplayLayout>.Failure($"Failed to get original layout: {originalResult.ErrorMessage}", originalResult.Exception);
            }

            var duplicate = JsonConvert.DeserializeObject<DisplayLayout>(
                JsonConvert.SerializeObject(originalResult.Value, SafeJsonSettings), SafeJsonSettings)!;

            EnsureLayoutDefaults(duplicate);
            duplicate.Id = Guid.NewGuid().ToString();
            duplicate.Name = newName;
            duplicate.Created = DateTime.UtcNow;
            duplicate.Modified = DateTime.UtcNow;

            var createResult = await CreateLayoutAsync(duplicate, cancellationToken);
            if (createResult.IsFailure)
            {
                return Result<DisplayLayout>.Failure($"Failed to create duplicate: {createResult.ErrorMessage}", createResult.Exception);
            }

            _logger.LogInformation("Duplicated layout {OriginalId} to {NewId} with name {NewName}", layoutId, duplicate.Id, newName);
            return Result<DisplayLayout>.Success(createResult.Value!);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "LayoutService has been disposed");
            return Result<DisplayLayout>.Failure("Service is no longer available", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize/deserialize layout {LayoutId}", layoutId);
            return Result<DisplayLayout>.Failure("Failed to duplicate layout due to serialization error", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to duplicate layout {LayoutId}", layoutId);
            return Result<DisplayLayout>.Failure($"Failed to duplicate layout: {ex.Message}", ex);
        }
    }

    public async Task<Result<string>> ExportLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(layoutId))
            {
                return Result<string>.Failure("Layout ID cannot be empty");
            }

            if (!IsValidLayoutId(layoutId, out var reason))
            {
                return Result<string>.Failure($"Invalid layout ID: {reason}");
            }

            var layoutResult = await GetLayoutByIdAsync(layoutId, cancellationToken);
            if (layoutResult.IsFailure)
            {
                return Result<string>.Failure($"Failed to get layout: {layoutResult.ErrorMessage}", layoutResult.Exception);
            }

            var json = JsonConvert.SerializeObject(layoutResult.Value, SafeJsonSettings);
            _logger.LogInformation("Exported layout {LayoutId}", layoutId);
            return Result<string>.Success(json);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "LayoutService has been disposed");
            return Result<string>.Failure("Service is no longer available", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize layout {LayoutId}", layoutId);
            return Result<string>.Failure("Failed to export layout due to serialization error", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export layout {LayoutId}", layoutId);
            return Result<string>.Failure($"Failed to export layout: {ex.Message}", ex);
        }
    }

    public async Task<Result<DisplayLayout>> ImportLayoutAsync(string jsonData, CancellationToken cancellationToken = default)
    {
        try
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(jsonData))
            {
                return Result<DisplayLayout>.Failure("JSON data cannot be empty");
            }

            var layout = JsonConvert.DeserializeObject<DisplayLayout>(jsonData, SafeJsonSettings);
            if (layout == null)
            {
                return Result<DisplayLayout>.Failure("Invalid layout JSON: deserialization returned null");
            }

            _logger.LogInformation("Importing layout {LayoutName}", layout.Name);
            var createResult = await CreateLayoutAsync(layout, cancellationToken);
            if (createResult.IsFailure)
            {
                return Result<DisplayLayout>.Failure($"Failed to import layout: {createResult.ErrorMessage}", createResult.Exception);
            }

            _logger.LogInformation("Successfully imported layout {LayoutId} with name {LayoutName}", createResult.Value!.Id, createResult.Value.Name);
            return Result<DisplayLayout>.Success(createResult.Value);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "LayoutService has been disposed");
            return Result<DisplayLayout>.Failure("Service is no longer available", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse layout JSON");
            return Result<DisplayLayout>.Failure("Invalid layout JSON format", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import layout");
            return Result<DisplayLayout>.Failure($"Failed to import layout: {ex.Message}", ex);
        }
    }

    private async Task SaveLayoutToDiskAsync(DisplayLayout layout, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetLayoutFilePath(layout.Id);
            var json = JsonConvert.SerializeObject(layout, SafeJsonSettings);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            _logger.LogDebug("Saved layout {LayoutId} to {FilePath}", layout.Id, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save layout {LayoutId} to disk", layout.Id);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private void LoadLayoutsFromDisk()
    {
        if (!Directory.Exists(_dataDirectory))
        {
            _logger.LogWarning("Layout directory does not exist: {Directory}", _dataDirectory);
            return;
        }

        var files = Directory.GetFiles(_dataDirectory, "*.json");
        _logger.LogInformation("Loading {Count} layout files from {Directory}", files.Length, _dataDirectory);

        var loadedCount = 0;
        var failedCount = 0;

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var layout = JsonConvert.DeserializeObject<DisplayLayout>(json, SafeJsonSettings);
                if (layout != null && !string.IsNullOrWhiteSpace(layout.Id))
                {
                    EnsureLayoutDefaults(layout);
                    _layouts[layout.Id] = layout;
                    loadedCount++;
                }
                else
                {
                    _logger.LogWarning("Skipped invalid layout file: {File}", file);
                    failedCount++;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse layout file: {File}", file);
                failedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load layout file: {File}", file);
                failedCount++;
            }
        }

            _logger.LogInformation("Loaded {LoadedCount} layouts, {FailedCount} failed", loadedCount, failedCount);
    }

    private void EnsureLayoutDefaults(DisplayLayout layout)
    {
        if (layout == null)
        {
            return;
        }

        layout.LayoutType = string.IsNullOrWhiteSpace(layout.LayoutType) ? "standard" : layout.LayoutType;
        layout.Elements ??= new List<DisplayElement>();
        layout.DataSources ??= new List<DataSource>();
        layout.Metadata ??= new Dictionary<string, object>();
        layout.Tags ??= new List<string>();
    }

    private string GetLayoutFilePath(string layoutId)
    {
        // Sanitize layoutId to prevent path traversal
        var sanitizedId = Path.GetFileName(layoutId);
        return Path.Combine(_dataDirectory, $"{sanitizedId}.json");
    }

    private static bool IsValidLayoutId(string layoutId, out string reason)
    {
        reason = string.Empty;

        // Must not contain path separators or colon
        if (layoutId != Path.GetFileName(layoutId))
        {
            reason = "Path traversal detected";
            return false;
        }

        // Must match allowed pattern and length
        if (!LayoutIdRegex.IsMatch(layoutId))
        {
            reason = "ID must contain only letters, numbers, '-', '_' and be <= 100 chars";
            return false;
        }

        // Must not be reserved name
        if (ReservedFileNames.Contains(layoutId))
        {
            reason = "Reserved file name";
            return false;
        }

        // Must not contain any invalid file name characters
        if (layoutId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            reason = "Contains invalid file name characters";
            return false;
        }

        // No trailing dot or space
        if (layoutId.EndsWith(" ") || layoutId.EndsWith("."))
        {
            reason = "Must not end with space or dot";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Throws ObjectDisposedException if service has been disposed
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LayoutService));
        }
    }

    /// <summary>
    /// Disposes managed resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
            _fileLock?.Dispose();
            _logger.LogInformation("LayoutService disposed");
        }

        _disposed = true;
    }
}
