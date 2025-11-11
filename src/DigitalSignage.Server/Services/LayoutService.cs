using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO;

namespace DigitalSignage.Server.Services;

public class LayoutService : ILayoutService
{
    private readonly ConcurrentDictionary<string, DisplayLayout> _layouts = new();
    private readonly string _dataDirectory;

    public LayoutService()
    {
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DigitalSignage",
            "Layouts");

        Directory.CreateDirectory(_dataDirectory);
        LoadLayoutsFromDisk();
    }

    public Task<List<DisplayLayout>> GetAllLayoutsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_layouts.Values.ToList());
    }

    public Task<DisplayLayout?> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        _layouts.TryGetValue(layoutId, out var layout);
        return Task.FromResult(layout);
    }

    public Task<DisplayLayout> CreateLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken = default)
    {
        layout.Id = Guid.NewGuid().ToString();
        layout.Created = DateTime.UtcNow;
        layout.Modified = DateTime.UtcNow;

        _layouts[layout.Id] = layout;
        SaveLayoutToDisk(layout);

        return Task.FromResult(layout);
    }

    public Task<DisplayLayout> UpdateLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken = default)
    {
        layout.Modified = DateTime.UtcNow;
        _layouts[layout.Id] = layout;
        SaveLayoutToDisk(layout);

        return Task.FromResult(layout);
    }

    public Task<bool> DeleteLayoutAsync(string layoutId, CancellationToken cancellationToken = default)
    {
        if (_layouts.TryRemove(layoutId, out _))
        {
            var filePath = GetLayoutFilePath(layoutId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
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
        var layout = JsonConvert.DeserializeObject<DisplayLayout>(jsonData);
        if (layout == null)
        {
            throw new InvalidOperationException("Invalid layout JSON");
        }

        return CreateLayoutAsync(layout, cancellationToken);
    }

    private void SaveLayoutToDisk(DisplayLayout layout)
    {
        var filePath = GetLayoutFilePath(layout.Id);
        var json = JsonConvert.SerializeObject(layout, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    private void LoadLayoutsFromDisk()
    {
        if (!Directory.Exists(_dataDirectory)) return;

        foreach (var file in Directory.GetFiles(_dataDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var layout = JsonConvert.DeserializeObject<DisplayLayout>(json);
                if (layout != null)
                {
                    _layouts[layout.Id] = layout;
                }
            }
            catch
            {
                // Log error but continue loading other layouts
            }
        }
    }

    private string GetLayoutFilePath(string layoutId)
    {
        return Path.Combine(_dataDirectory, $"{layoutId}.json");
    }
}
