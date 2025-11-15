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
    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing DataSourceManager - loading data sources");

            var dataSources = await _sqlDataSourceService.LoadDataSourcesAsync();

            foreach (var dataSource in dataSources)
            {
                if (dataSource.IsActive)
                {
                    await ActivateDataSourceAsync(dataSource);
                }
            }

            _logger.LogInformation("DataSourceManager initialized with {Count} active data sources",
                _activeDataSources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize DataSourceManager: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Activates a data source (fetches initial data and starts periodic refresh)
    /// </summary>
    public async Task ActivateDataSourceAsync(SqlDataSource dataSource)
    {
        if (dataSource == null)
            throw new ArgumentNullException(nameof(dataSource));

        try
        {
            // Add to active sources
            _activeDataSources[dataSource.Id] = dataSource;

            // Fetch initial data
            await RefreshDataSourceAsync(dataSource.Id);

            // Start periodic refresh
            _sqlDataSourceService.StartPeriodicRefresh(dataSource);

            _logger.LogInformation("Activated data source {Name} (ID: {Id})", dataSource.Name, dataSource.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate data source {Name}: {Message}", dataSource.Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Deactivates a data source (stops refresh and removes from active sources)
    /// </summary>
    public void DeactivateDataSource(Guid dataSourceId)
    {
        try
        {
            _sqlDataSourceService.StopPeriodicRefresh(dataSourceId);

            if (_activeDataSources.TryRemove(dataSourceId, out var dataSource))
            {
                _dataHashCache.TryRemove(dataSourceId, out _);
                _logger.LogInformation("Deactivated data source {Name} (ID: {Id})", dataSource.Name, dataSource.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate data source {Id}: {Message}", dataSourceId, ex.Message);
        }
    }

    /// <summary>
    /// Refreshes data for a specific data source
    /// </summary>
    public async Task RefreshDataSourceAsync(Guid dataSourceId)
    {
        if (!_activeDataSources.TryGetValue(dataSourceId, out var dataSource))
        {
            _logger.LogWarning("Attempted to refresh inactive data source {Id}", dataSourceId);
            return;
        }

        try
        {
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh data source {Id}: {Message}", dataSourceId, ex.Message);
        }
    }

    /// <summary>
    /// Gets cached data for a specific data source
    /// </summary>
    public List<Dictionary<string, object>>? GetCachedData(Guid dataSourceId)
    {
        if (_activeDataSources.TryGetValue(dataSourceId, out var dataSource))
        {
            return dataSource.CachedData;
        }

        return null;
    }

    /// <summary>
    /// Gets a data source by ID
    /// </summary>
    public SqlDataSource? GetDataSource(Guid dataSourceId)
    {
        _activeDataSources.TryGetValue(dataSourceId, out var dataSource);
        return dataSource;
    }

    /// <summary>
    /// Gets all active data sources
    /// </summary>
    public List<SqlDataSource> GetActiveDataSources()
    {
        return _activeDataSources.Values.ToList();
    }

    /// <summary>
    /// Gets data sources linked to a specific layout
    /// </summary>
    public List<SqlDataSource> GetDataSourcesForLayout(Guid layoutId)
    {
        // This would require layout-to-datasource mapping in the future
        // For now, return empty list
        return new List<SqlDataSource>();
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
