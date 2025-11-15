using DigitalSignage.Core.Models;

namespace DigitalSignage.Core.Interfaces;

/// <summary>
/// Interface for managing display layouts
/// </summary>
public interface ILayoutService
{
    Task<List<DisplayLayout>> GetAllLayoutsAsync(CancellationToken cancellationToken = default);
    Task<DisplayLayout?> GetLayoutByIdAsync(string layoutId, CancellationToken cancellationToken = default);
    Task<DisplayLayout> CreateLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken = default);
    Task<DisplayLayout> UpdateLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken = default);
    Task<Result> DeleteLayoutAsync(string layoutId, CancellationToken cancellationToken = default);
    Task<DisplayLayout> DuplicateLayoutAsync(string layoutId, string newName, CancellationToken cancellationToken = default);
    Task<string> ExportLayoutAsync(string layoutId, CancellationToken cancellationToken = default);
    Task<DisplayLayout> ImportLayoutAsync(string jsonData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all layouts that use a specific data source
    /// </summary>
    Task<List<DisplayLayout>> GetLayoutsWithDataSourceAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
}
