using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Singleton manager for SQL data sources - manages active data sources and their cached data
/// </summary>
public class DataSourceManager : IDisposable
{
    private readonly ISqlDataSourceService _sqlDataSourceService;
    private readonly ILogger<DataSourceManager> _logger;
    private readonly ConcurrentDictionary<Guid, SqlDataSource> _activeDataSources;
    private readonly ConcurrentDictionary<Guid, string> _dataHashCache;
    private bool _disposed;

    /// <summary>
    /// Event raised when a data source has been updated with new data
    /// </summary>
    public event EventHandler<DataSourceUpdatedEventArgs>? DataSourceUpdated;

    public DataSourceManager(
        ISqlDataSourceService sqlDataSourceService,
        ILogger<DataSourceManager> logger)
    {
        _sqlDataSourceService = sqlDataSourceService ?? throw new ArgumentNullException(nameof(sqlDataSourceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeDataSources = new ConcurrentDictionary<Guid, SqlDataSource>();
        _dataHashCache = new ConcurrentDictionary<Guid, string>();

        _logger.LogInformation("DataSourceManager initialized");
    }

    /// <summary>
    /// Initializes the manager by loading all data sources and starting active ones
    /// </summary>
    public async Task<Result> InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing DataSourceManager - loading data sources");

            var dataSources = await _sqlDataSourceService.LoadDataSourcesAsync();

            foreach (var dataSource in dataSources)
            {
                if (dataSource.IsActive)
                {
                    var activateResult = await ActivateDataSourceAsync(dataSource);
                    if (activateResult.IsFailure)
                    {
                        _logger.LogWarning("Failed to activate data source {Name}: {ErrorMessage}",
                            dataSource.Name, activateResult.ErrorMessage);
                        // Continue with other data sources
                    }
                }
            }

            _logger.LogInformation("DataSourceManager initialized with {Count} active data sources",
                _activeDataSources.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize DataSourceManager: {Message}", ex.Message);
            return Result.Failure($"Failed to initialize: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Activates a data source (fetches initial data and starts periodic refresh)
    /// </summary>
    public async Task<Result> ActivateDataSourceAsync(SqlDataSource dataSource)
    {
        try
        {
            if (dataSource == null)
            {
                _logger.LogWarning("ActivateDataSourceAsync called with null dataSource");
                return Result.Failure("Data source cannot be null");
            }

            // Add to active sources
            _activeDataSources[dataSource.Id] = dataSource;

            // Fetch initial data
            var refreshResult = await RefreshDataSourceAsync(dataSource.Id);
            if (refreshResult.IsFailure)
            {
                _logger.LogWarning("Failed to fetch initial data for {Name}: {ErrorMessage}",
                    dataSource.Name, refreshResult.ErrorMessage);
                // Continue anyway - periodic refresh will retry
            }

            // Start periodic refresh
            _sqlDataSourceService.StartPeriodicRefresh(dataSource);

            _logger.LogInformation("Activated data source {Name} (ID: {Id})", dataSource.Name, dataSource.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate data source {Name}: {Message}", dataSource?.Name, ex.Message);
            return Result.Failure($"Failed to activate data source: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deactivates a data source (stops refresh and removes from active sources)
    /// </summary>
    public Result DeactivateDataSource(Guid dataSourceId)
    {
        try
        {
            if (dataSourceId == Guid.Empty)
            {
                _logger.LogWarning("DeactivateDataSource called with empty GUID");
                return Result.Failure("Data source ID cannot be empty");
            }

            _sqlDataSourceService.StopPeriodicRefresh(dataSourceId);

            if (_activeDataSources.TryRemove(dataSourceId, out var dataSource))
            {
                _dataHashCache.TryRemove(dataSourceId, out _);
                _logger.LogInformation("Deactivated data source {Name} (ID: {Id})", dataSource.Name, dataSource.Id);
                return Result.Success();
            }

            _logger.LogWarning("Data source {Id} not found for deactivation", dataSourceId);
            return Result.Failure($"Data source '{dataSourceId}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate data source {Id}: {Message}", dataSourceId, ex.Message);
            return Result.Failure($"Failed to deactivate data source: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Refreshes data for a specific data source
    /// </summary>
    public async Task<Result> RefreshDataSourceAsync(Guid dataSourceId)
    {
        try
        {
            if (dataSourceId == Guid.Empty)
            {
                _logger.LogWarning("RefreshDataSourceAsync called with empty GUID");
                return Result.Failure("Data source ID cannot be empty");
            }

            if (!_activeDataSources.TryGetValue(dataSourceId, out var dataSource))
            {
                _logger.LogWarning("Attempted to refresh inactive data source {Id}", dataSourceId);
                return Result.Failure($"Data source '{dataSourceId}' is not active");
            }

            // Fetch new data
            var newData = await _sqlDataSourceService.FetchDataAsync(dataSource);

            // Check if data changed
            var newDataHash = ComputeDataHash(newData);
            var dataChanged = true;

            if (_dataHashCache.TryGetValue(dataSourceId, out var oldHash))
            {
                dataChanged = oldHash != newDataHash;
            }

            // Update hash cache
            _dataHashCache[dataSourceId] = newDataHash;

            // Raise event if data changed
            if (dataChanged)
            {
                OnDataSourceUpdated(new DataSourceUpdatedEventArgs
                {
                    DataSourceId = dataSource.Id,
                    DataSourceName = dataSource.Name,
                    Data = newData,
                    DataChanged = true,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("Data source {Name} updated with {RowCount} rows (changed: {Changed})",
                    dataSource.Name, newData.Count, dataChanged);
            }
            else
            {
                _logger.LogDebug("Data source {Name} refreshed but data unchanged", dataSource.Name);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh data source {Id}: {Message}", dataSourceId, ex.Message);
            return Result.Failure($"Failed to refresh data source: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets cached data for a specific data source
    /// </summary>
    public Result<List<Dictionary<string, object>>> GetCachedData(Guid dataSourceId)
    {
        try
        {
            if (dataSourceId == Guid.Empty)
            {
                _logger.LogWarning("GetCachedData called with empty GUID");
                return Result<List<Dictionary<string, object>>>.Failure("Data source ID cannot be empty");
            }

            if (_activeDataSources.TryGetValue(dataSourceId, out var dataSource))
            {
                var data = dataSource.CachedData ?? new List<Dictionary<string, object>>();
                return Result<List<Dictionary<string, object>>>.Success(data);
            }

            _logger.LogDebug("Data source {Id} not found in active sources", dataSourceId);
            return Result<List<Dictionary<string, object>>>.Failure($"Data source '{dataSourceId}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cached data for {Id}", dataSourceId);
            return Result<List<Dictionary<string, object>>>.Failure($"Failed to get cached data: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets a data source by ID
    /// </summary>
    public Result<SqlDataSource> GetDataSource(Guid dataSourceId)
    {
        try
        {
            if (dataSourceId == Guid.Empty)
            {
                _logger.LogWarning("GetDataSource called with empty GUID");
                return Result<SqlDataSource>.Failure("Data source ID cannot be empty");
            }

            if (_activeDataSources.TryGetValue(dataSourceId, out var dataSource))
            {
                return Result<SqlDataSource>.Success(dataSource);
            }

            _logger.LogDebug("Data source {Id} not found in active sources", dataSourceId);
            return Result<SqlDataSource>.Failure($"Data source '{dataSourceId}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data source {Id}", dataSourceId);
            return Result<SqlDataSource>.Failure($"Failed to get data source: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets all active data sources
    /// </summary>
    public Result<List<SqlDataSource>> GetActiveDataSources()
    {
        try
        {
            var dataSources = _activeDataSources.Values.ToList();
            return Result<List<SqlDataSource>>.Success(dataSources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active data sources");
            return Result<List<SqlDataSource>>.Failure($"Failed to get active data sources: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets data sources linked to a specific layout
    /// </summary>
    public Result<List<SqlDataSource>> GetDataSourcesForLayout(Guid layoutId)
    {
        try
        {
            if (layoutId == Guid.Empty)
            {
                _logger.LogWarning("GetDataSourcesForLayout called with empty GUID");
                return Result<List<SqlDataSource>>.Failure("Layout ID cannot be empty");
            }

            // This would require layout-to-datasource mapping in the future
            // For now, return empty list
            _logger.LogDebug("Getting data sources for layout {LayoutId} (feature not yet implemented)", layoutId);
            return Result<List<SqlDataSource>>.Success(new List<SqlDataSource>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data sources for layout {LayoutId}", layoutId);
            return Result<List<SqlDataSource>>.Failure($"Failed to get data sources for layout: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Computes a hash of the data to detect changes
    /// </summary>
    private string ComputeDataHash(List<Dictionary<string, object>> data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
            return Convert.ToBase64String(hashBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute data hash");
            return Guid.NewGuid().ToString(); // Return unique value to force update
        }
    }

    /// <summary>
    /// Raises the DataSourceUpdated event
    /// </summary>
    protected virtual void OnDataSourceUpdated(DataSourceUpdatedEventArgs e)
    {
        DataSourceUpdated?.Invoke(this, e);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing DataSourceManager");

        // Stop all refresh timers
        _sqlDataSourceService.StopAllRefreshes();

        // Clear collections
        _activeDataSources.Clear();
        _dataHashCache.Clear();

        _disposed = true;
        _logger.LogInformation("DataSourceManager disposed");
    }
}
