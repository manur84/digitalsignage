using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO;

namespace DigitalSignage.Server.Services;

public class LayoutService : ILayoutService
{
    private readonly ConcurrentDictionary<string, DisplayLayout> _layouts = new();
    private readonly string _dataDirectory;
    private readonly ILogger<LayoutService> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

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

    public Task<List<DisplayLayout>> GetAllLayoutsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_layouts.Values.ToList());
    }

    public Task<DisplayLayout?> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(layoutId))
        {
            _logger.LogWarning("GetLayoutByIdAsync called with null or empty layoutId");
            return Task.FromResult<DisplayLayout?>(null);
        }

        _layouts.TryGetValue(layoutId, out var layout);
        return Task.FromResult(layout);
    }

    public async Task<DisplayLayout> CreateLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken = default)
    {
        if (layout == null)
        {
            throw new ArgumentNullException(nameof(layout));
        }

        if (string.IsNullOrWhiteSpace(layout.Name))
        {
            throw new ArgumentException("Layout name cannot be empty", nameof(layout));
        }

        try
        {
            layout.Id = Guid.NewGuid().ToString();
            layout.Created = DateTime.UtcNow;
            layout.Modified = DateTime.UtcNow;

            _layouts[layout.Id] = layout;
            await SaveLayoutToDiskAsync(layout, cancellationToken);

            _logger.LogInformation("Created layout {LayoutId} with name {LayoutName}", layout.Id, layout.Name);
            return layout;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create layout {LayoutName}", layout.Name);
            throw;
        }
    }

    public async Task<DisplayLayout> UpdateLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken = default)
    {
        if (layout == null)
        {
            throw new ArgumentNullException(nameof(layout));
        }

        if (string.IsNullOrWhiteSpace(layout.Id))
        {
            throw new ArgumentException("Layout ID cannot be empty", nameof(layout));
        }

        if (!_layouts.ContainsKey(layout.Id))
        {
            throw new InvalidOperationException($"Layout {layout.Id} does not exist");
        }

        try
        {
            layout.Modified = DateTime.UtcNow;
            _layouts[layout.Id] = layout;
            await SaveLayoutToDiskAsync(layout, cancellationToken);

            _logger.LogInformation("Updated layout {LayoutId}", layout.Id);
            return layout;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update layout {LayoutId}", layout.Id);
            throw;
        }
    }

    public async Task<Result> DeleteLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(layoutId))
        {
            _logger.LogWarning("DeleteLayoutAsync called with null or empty layoutId");
            return Result.Failure("Layout ID cannot be empty.");
        }

        try
        {
            if (!_layouts.TryRemove(layoutId, out var removedLayout))
            {
                _logger.LogWarning("Layout {LayoutId} not found for deletion", layoutId);
                return Result.Failure($"Layout '{layoutId}' was not found.");
            }

            var filePath = GetLayoutFilePath(layoutId);
            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted layout file {FilePath}", filePath);
                }
            }
            finally
            {
                _fileLock.Release();
            }

            _logger.LogInformation("Deleted layout {LayoutId}", layoutId);
            var name = string.IsNullOrWhiteSpace(removedLayout?.Name) ? layoutId : removedLayout!.Name;
            return Result.Success($"Layout '{name}' deleted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete layout {LayoutId}", layoutId);
            return Result.Failure($"Failed to delete layout: {ex.Message}");
        }
    }

    public async Task<DisplayLayout> DuplicateLayoutAsync(
        string layoutId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var original = await GetLayoutByIdAsync(layoutId, cancellationToken);
        if (original == null)
        {
            throw new InvalidOperationException($"Layout {layoutId} not found");
        }

        var duplicate = JsonConvert.DeserializeObject<DisplayLayout>(
            JsonConvert.SerializeObject(original))!;

        duplicate.Id = Guid.NewGuid().ToString();
        duplicate.Name = newName;
        duplicate.Created = DateTime.UtcNow;
        duplicate.Modified = DateTime.UtcNow;

        return await CreateLayoutAsync(duplicate, cancellationToken);
    }

    public async Task<string> ExportLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        var layout = await GetLayoutByIdAsync(layoutId, cancellationToken);
        if (layout == null)
        {
            throw new InvalidOperationException($"Layout {layoutId} not found");
        }

        return JsonConvert.SerializeObject(layout, Formatting.Indented);
    }

    public Task<DisplayLayout> ImportLayoutAsync(string jsonData, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            throw new ArgumentException("JSON data cannot be empty", nameof(jsonData));
        }

        try
        {
            var layout = JsonConvert.DeserializeObject<DisplayLayout>(jsonData);
            if (layout == null)
            {
                throw new InvalidOperationException("Invalid layout JSON: deserialization returned null");
            }

            _logger.LogInformation("Importing layout {LayoutName}", layout.Name);
            return CreateLayoutAsync(layout, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse layout JSON");
            throw new InvalidOperationException("Invalid layout JSON format", ex);
        }
    }

    private async Task SaveLayoutToDiskAsync(DisplayLayout layout, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetLayoutFilePath(layout.Id);
            var json = JsonConvert.SerializeObject(layout, Formatting.Indented);
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
                var layout = JsonConvert.DeserializeObject<DisplayLayout>(json);
                if (layout != null && !string.IsNullOrWhiteSpace(layout.Id))
                {
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

    public Task<List<DisplayLayout>> GetLayoutsWithDataSourceAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var layoutsWithDataSource = _layouts.Values
                .Where(layout =>
                    layout.LinkedDataSourceIds != null &&
                    layout.LinkedDataSourceIds.Contains(dataSourceId))
                .ToList();

            _logger.LogInformation("Found {Count} layouts using data source {DataSourceId}",
                layoutsWithDataSource.Count, dataSourceId);

            return Task.FromResult(layoutsWithDataSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting layouts with data source {DataSourceId}", dataSourceId);
            return Task.FromResult(new List<DisplayLayout>());
        }
    }

    private string GetLayoutFilePath(string layoutId)
    {
        // Sanitize layoutId to prevent path traversal
        var sanitizedId = Path.GetFileName(layoutId);
        return Path.Combine(_dataDirectory, $"{sanitizedId}.json");
    }
}
