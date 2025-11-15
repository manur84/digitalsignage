using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services.FileStorage;

/// <summary>
/// File-based storage service for layouts
/// </summary>
public class LayoutFileService : FileStorageService<DisplayLayout>
{
    public LayoutFileService(ILogger<LayoutFileService> logger) : base(logger)
    {
    }

    protected override string GetSubDirectory() => "Layouts";

    /// <summary>
    /// Get all layouts
    /// </summary>
    public async Task<List<DisplayLayout>> GetAllLayoutsAsync()
    {
        try
        {
            var files = await ListFilesAsync("*.json");
            var layouts = new List<DisplayLayout>();

            foreach (var file in files)
            {
                var layout = await LoadFromFileAsync(file);
                if (layout != null)
                {
                    layouts.Add(layout);
                }
            }

            return layouts.OrderByDescending(l => l.LastModified).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all layouts");
            return new List<DisplayLayout>();
        }
    }

    /// <summary>
    /// Get layout by ID
    /// </summary>
    public async Task<DisplayLayout?> GetLayoutByIdAsync(Guid layoutId)
    {
        try
        {
            var fileName = GetLayoutFileName(layoutId);
            return await LoadFromFileAsync(fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get layout {LayoutId}", layoutId);
            return null;
        }
    }

    /// <summary>
    /// Save or update a layout
    /// </summary>
    public async Task<DisplayLayout> SaveLayoutAsync(DisplayLayout layout)
    {
        try
        {
            // Generate ID if new
            if (layout.Id == Guid.Empty)
            {
                layout.Id = Guid.NewGuid();
                layout.Created = DateTime.UtcNow;
            }

            layout.LastModified = DateTime.UtcNow;

            // Create backup if updating existing
            var fileName = GetLayoutFileName(layout.Id);
            if (FileExists(fileName))
            {
                await CreateBackupAsync(fileName);
            }

            await SaveToFileAsync(fileName, layout);

            _logger.LogInformation("Saved layout {LayoutId} - {LayoutName}", layout.Id, layout.Name);
            return layout;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save layout {LayoutName}", layout.Name);
            throw;
        }
    }

    /// <summary>
    /// Delete a layout
    /// </summary>
    public async Task<bool> DeleteLayoutAsync(Guid layoutId)
    {
        try
        {
            var fileName = GetLayoutFileName(layoutId);

            // Create backup before deletion
            if (FileExists(fileName))
            {
                await CreateBackupAsync(fileName);
                await DeleteFileAsync(fileName);
                _logger.LogInformation("Deleted layout {LayoutId}", layoutId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete layout {LayoutId}", layoutId);
            return false;
        }
    }

    /// <summary>
    /// Duplicate a layout
    /// </summary>
    public async Task<DisplayLayout?> DuplicateLayoutAsync(Guid layoutId, string newName)
    {
        try
        {
            var original = await GetLayoutByIdAsync(layoutId);
            if (original == null) return null;

            var duplicate = new DisplayLayout
            {
                Id = Guid.NewGuid(),
                Name = newName,
                Description = original.Description,
                Resolution = original.Resolution,
                BackgroundColor = original.BackgroundColor,
                BackgroundImage = original.BackgroundImage,
                Elements = original.Elements?.Select(e => new DisplayElement
                {
                    Id = Guid.NewGuid(),
                    Type = e.Type,
                    X = e.X,
                    Y = e.Y,
                    Width = e.Width,
                    Height = e.Height,
                    ZIndex = e.ZIndex,
                    Content = e.Content,
                    Style = e.Style,
                    DataBinding = e.DataBinding,
                    Animation = e.Animation,
                    Interaction = e.Interaction,
                    Visibility = e.Visibility
                }).ToList() ?? new List<DisplayElement>(),
                DataSources = original.DataSources,
                LinkedDataSourceIds = original.LinkedDataSourceIds,
                Category = original.Category,
                Tags = original.Tags,
                Metadata = original.Metadata,
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            return await SaveLayoutAsync(duplicate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to duplicate layout {LayoutId}", layoutId);
            return null;
        }
    }

    /// <summary>
    /// Search layouts by name or description
    /// </summary>
    public async Task<List<DisplayLayout>> SearchLayoutsAsync(string searchTerm)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllLayoutsAsync();

            var allLayouts = await GetAllLayoutsAsync();
            var lowerSearchTerm = searchTerm.ToLowerInvariant();

            return allLayouts.Where(l =>
                (l.Name?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false) ||
                (l.Description?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false) ||
                (l.Category?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false) ||
                (l.Tags?.Any(t => t.ToLowerInvariant().Contains(lowerSearchTerm)) ?? false)
            ).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search layouts with term {SearchTerm}", searchTerm);
            return new List<DisplayLayout>();
        }
    }

    /// <summary>
    /// Get layouts by category
    /// </summary>
    public async Task<List<DisplayLayout>> GetLayoutsByCategoryAsync(string category)
    {
        try
        {
            var allLayouts = await GetAllLayoutsAsync();
            return allLayouts.Where(l =>
                string.Equals(l.Category, category, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get layouts by category {Category}", category);
            return new List<DisplayLayout>();
        }
    }

    /// <summary>
    /// Export layout to a specific path
    /// </summary>
    public async Task<bool> ExportLayoutAsync(Guid layoutId, string exportPath)
    {
        try
        {
            var layout = await GetLayoutByIdAsync(layoutId);
            if (layout == null) return false;

            var json = System.Text.Json.JsonSerializer.Serialize(layout, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            });

            await File.WriteAllTextAsync(exportPath, json);
            _logger.LogInformation("Exported layout {LayoutId} to {ExportPath}", layoutId, exportPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export layout {LayoutId}", layoutId);
            return false;
        }
    }

    /// <summary>
    /// Import layout from a specific path
    /// </summary>
    public async Task<DisplayLayout?> ImportLayoutAsync(string importPath)
    {
        try
        {
            if (!File.Exists(importPath))
            {
                _logger.LogWarning("Import file does not exist: {ImportPath}", importPath);
                return null;
            }

            var json = await File.ReadAllTextAsync(importPath);
            var layout = System.Text.Json.JsonSerializer.Deserialize<DisplayLayout>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (layout == null) return null;

            // Generate new ID for imported layout
            layout.Id = Guid.NewGuid();
            layout.Created = DateTime.UtcNow;
            layout.LastModified = DateTime.UtcNow;

            return await SaveLayoutAsync(layout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import layout from {ImportPath}", importPath);
            return null;
        }
    }

    /// <summary>
    /// Get layout statistics
    /// </summary>
    public async Task<Dictionary<string, object>> GetLayoutStatisticsAsync()
    {
        try
        {
            var layouts = await GetAllLayoutsAsync();

            return new Dictionary<string, object>
            {
                ["TotalLayouts"] = layouts.Count,
                ["Categories"] = layouts.Where(l => !string.IsNullOrEmpty(l.Category))
                                       .Select(l => l.Category)
                                       .Distinct()
                                       .Count(),
                ["TotalElements"] = layouts.Sum(l => l.Elements?.Count ?? 0),
                ["LastModified"] = layouts.Any() ? layouts.Max(l => l.LastModified) : DateTime.MinValue,
                ["MostUsedCategory"] = layouts.Where(l => !string.IsNullOrEmpty(l.Category))
                                              .GroupBy(l => l.Category)
                                              .OrderByDescending(g => g.Count())
                                              .FirstOrDefault()?.Key ?? "None"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get layout statistics");
            return new Dictionary<string, object>();
        }
    }

    private string GetLayoutFileName(Guid layoutId)
    {
        return $"layout_{layoutId}.json";
    }
}