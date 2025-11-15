using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services.FileStorage;

/// <summary>
/// File-based storage service for data sources (SQL Server connections for layout data binding)
/// </summary>
public class DataSourceFileService : FileStorageService<DataSourceInfo>
{
    private readonly ConcurrentDictionary<Guid, DataSourceInfo> _dataSourcesCache = new();
    private const string DATASOURCES_FILE = "datasources.json";

    public DataSourceFileService(ILogger<DataSourceFileService> logger) : base(logger)
    {
        _ = Task.Run(async () => await LoadDataSourcesAsync());
    }

    protected override string GetSubDirectory() => "Settings";

    /// <summary>
    /// Load data sources into cache
    /// </summary>
    private async Task LoadDataSourcesAsync()
    {
        try
        {
            var dataSources = await LoadListFromFileAsync(DATASOURCES_FILE);
            _dataSourcesCache.Clear();

            foreach (var ds in dataSources)
            {
                _dataSourcesCache[ds.Id] = ds;
            }

            _logger.LogInformation("Loaded {Count} data sources into cache", dataSources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load data sources");
        }
    }

    /// <summary>
    /// Save data sources to file
    /// </summary>
    private async Task SaveDataSourcesAsync()
    {
        try
        {
            var dataSources = _dataSourcesCache.Values.ToList();
            await SaveListToFileAsync(DATASOURCES_FILE, dataSources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save data sources");
        }
    }

    /// <summary>
    /// Create a new data source
    /// </summary>
    public async Task<DataSourceInfo> CreateDataSourceAsync(DataSourceInfo dataSource)
    {
        try
        {
            if (dataSource.Id == Guid.Empty)
            {
                dataSource.Id = Guid.NewGuid();
            }

            dataSource.CreatedAt = DateTime.UtcNow;
            dataSource.ModifiedAt = DateTime.UtcNow;

            _dataSourcesCache[dataSource.Id] = dataSource;
            await SaveDataSourcesAsync();

            _logger.LogInformation("Created data source {Name} ({Id})", dataSource.Name, dataSource.Id);
            return dataSource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create data source {Name}", dataSource.Name);
            throw;
        }
    }

    /// <summary>
    /// Get all data sources
    /// </summary>
    public Task<List<DataSourceInfo>> GetAllDataSourcesAsync()
    {
        return Task.FromResult(_dataSourcesCache.Values.OrderBy(ds => ds.Name).ToList());
    }

    /// <summary>
    /// Get enabled data sources
    /// </summary>
    public Task<List<DataSourceInfo>> GetEnabledDataSourcesAsync()
    {
        var enabled = _dataSourcesCache.Values
            .Where(ds => ds.IsEnabled)
            .OrderBy(ds => ds.Name)
            .ToList();

        return Task.FromResult(enabled);
    }

    /// <summary>
    /// Get data source by ID
    /// </summary>
    public Task<DataSourceInfo?> GetDataSourceByIdAsync(Guid dataSourceId)
    {
        _dataSourcesCache.TryGetValue(dataSourceId, out var dataSource);
        return Task.FromResult(dataSource);
    }

    /// <summary>
    /// Update a data source
    /// </summary>
    public async Task<DataSourceInfo?> UpdateDataSourceAsync(Guid dataSourceId, DataSourceInfo updatedDataSource)
    {
        try
        {
            if (_dataSourcesCache.TryGetValue(dataSourceId, out var existing))
            {
                updatedDataSource.Id = dataSourceId;
                updatedDataSource.CreatedAt = existing.CreatedAt;
                updatedDataSource.ModifiedAt = DateTime.UtcNow;

                _dataSourcesCache[dataSourceId] = updatedDataSource;
                await SaveDataSourcesAsync();

                _logger.LogInformation("Updated data source {Name} ({Id})", updatedDataSource.Name, dataSourceId);
                return updatedDataSource;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update data source {Id}", dataSourceId);
            return null;
        }
    }

    /// <summary>
    /// Delete a data source
    /// </summary>
    public async Task<bool> DeleteDataSourceAsync(Guid dataSourceId)
    {
        try
        {
            if (_dataSourcesCache.TryRemove(dataSourceId, out var dataSource))
            {
                await SaveDataSourcesAsync();
                _logger.LogInformation("Deleted data source {Name} ({Id})", dataSource.Name, dataSourceId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete data source {Id}", dataSourceId);
            return false;
        }
    }

    /// <summary>
    /// Test a data source connection
    /// </summary>
    public async Task<(bool Success, string Message)> TestConnectionAsync(DataSourceInfo dataSource)
    {
        try
        {
            if (dataSource.Type != DataSourceType.SqlServer)
            {
                return (false, $"Data source type {dataSource.Type} is not currently supported");
            }

            using var connection = new SqlConnection(dataSource.ConnectionString);
            await connection.OpenAsync();

            // Test the query if provided
            if (!string.IsNullOrWhiteSpace(dataSource.TestQuery))
            {
                var result = await connection.QueryAsync(dataSource.TestQuery);
                var count = result.Count();
                return (true, $"Connection successful. Query returned {count} rows.");
            }

            return (true, "Connection successful.");
        }
        catch (SqlException sqlEx)
        {
            _logger.LogError(sqlEx, "SQL connection test failed for data source {Name}", dataSource.Name);
            return (false, $"SQL Error: {sqlEx.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for data source {Name}", dataSource.Name);
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Execute a query on a data source
    /// </summary>
    public async Task<DataTable?> ExecuteQueryAsync(Guid dataSourceId, string query, Dictionary<string, object>? parameters = null)
    {
        try
        {
            if (!_dataSourcesCache.TryGetValue(dataSourceId, out var dataSource))
            {
                _logger.LogWarning("Data source {Id} not found", dataSourceId);
                return null;
            }

            if (!dataSource.IsEnabled)
            {
                _logger.LogWarning("Data source {Name} is disabled", dataSource.Name);
                return null;
            }

            if (dataSource.Type != DataSourceType.SqlServer)
            {
                _logger.LogWarning("Data source type {Type} is not supported", dataSource.Type);
                return null;
            }

            using var connection = new SqlConnection(dataSource.ConnectionString);
            await connection.OpenAsync();

            // Use parameterized query for safety
            var dynamicParams = new DynamicParameters();
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    dynamicParams.Add(param.Key, param.Value);
                }
            }

            var result = await connection.QueryAsync(query, dynamicParams);

            // Convert to DataTable for compatibility
            var dataTable = new DataTable();
            var resultList = result.ToList();

            if (resultList.Any())
            {
                var firstRow = resultList.First() as IDictionary<string, object>;
                if (firstRow != null)
                {
                    // Add columns
                    foreach (var key in firstRow.Keys)
                    {
                        dataTable.Columns.Add(key);
                    }

                    // Add rows
                    foreach (var row in resultList)
                    {
                        var dict = row as IDictionary<string, object>;
                        if (dict != null)
                        {
                            var dataRow = dataTable.NewRow();
                            foreach (var kvp in dict)
                            {
                                dataRow[kvp.Key] = kvp.Value ?? DBNull.Value;
                            }
                            dataTable.Rows.Add(dataRow);
                        }
                    }
                }
            }

            dataSource.LastExecuted = DateTime.UtcNow;
            dataSource.LastRowCount = dataTable.Rows.Count;

            _logger.LogInformation("Executed query on data source {Name}, returned {RowCount} rows",
                dataSource.Name, dataTable.Rows.Count);

            return dataTable;
        }
        catch (SqlException sqlEx)
        {
            _logger.LogError(sqlEx, "SQL execution failed for data source {Id}", dataSourceId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query on data source {Id}", dataSourceId);
            return null;
        }
    }

    /// <summary>
    /// Get data source statistics
    /// </summary>
    public Task<Dictionary<string, object>> GetStatisticsAsync()
    {
        var stats = new Dictionary<string, object>
        {
            ["TotalDataSources"] = _dataSourcesCache.Count,
            ["EnabledDataSources"] = _dataSourcesCache.Values.Count(ds => ds.IsEnabled),
            ["SqlServerSources"] = _dataSourcesCache.Values.Count(ds => ds.Type == DataSourceType.SqlServer),
            ["LastExecuted"] = _dataSourcesCache.Values
                .Where(ds => ds.LastExecuted.HasValue)
                .OrderByDescending(ds => ds.LastExecuted)
                .FirstOrDefault()?.LastExecuted ?? DateTime.MinValue
        };

        return Task.FromResult(stats);
    }

    /// <summary>
    /// Refresh all enabled data sources (for background updates)
    /// </summary>
    public async Task RefreshAllDataSourcesAsync()
    {
        var enabledSources = _dataSourcesCache.Values.Where(ds => ds.IsEnabled && ds.AutoRefresh).ToList();

        foreach (var source in enabledSources)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(source.DefaultQuery))
                {
                    await ExecuteQueryAsync(source.Id, source.DefaultQuery);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh data source {Name}", source.Name);
            }
        }
    }
}

/// <summary>
/// Data source information
/// </summary>
public class DataSourceInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DataSourceType Type { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public string? DefaultQuery { get; set; }
    public string? TestQuery { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool AutoRefresh { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 300; // 5 minutes default
    public DateTime? LastExecuted { get; set; }
    public int? LastRowCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Data source types
/// </summary>
public enum DataSourceType
{
    SqlServer,
    // Future: MySQL, PostgreSQL, REST API, CSV, etc.
}