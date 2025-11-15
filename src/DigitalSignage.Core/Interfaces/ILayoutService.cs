using DigitalSignage.Core.Models;

namespace DigitalSignage.Core.Interfaces;

/// <summary>
/// Interface for managing display layouts
/// </summary>
public interface ILayoutService
{
    /// <summary>
    /// Gets all layouts
    /// </summary>
    Task<Result<List<DisplayLayout>>> GetAllLayoutsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a layout by ID
    /// </summary>
    Task<Result<DisplayLayout>> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new layout
    /// </summary>
    Task<Result<DisplayLayout>> CreateLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing layout
    /// </summary>
    Task<Result<DisplayLayout>> UpdateLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a layout
    /// </summary>
    Task<Result> DeleteLayoutAsync(string layoutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Duplicates a layout with a new name
    /// </summary>
    Task<Result<DisplayLayout>> DuplicateLayoutAsync(string layoutId, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a layout to JSON
    /// </summary>
    Task<Result<string>> ExportLayoutAsync(string layoutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a layout from JSON
    /// </summary>
    Task<Result<DisplayLayout>> ImportLayoutAsync(string jsonData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all layouts that use a specific data source
    /// </summary>
    Task<Result<List<DisplayLayout>>> GetLayoutsWithDataSourceAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
}
