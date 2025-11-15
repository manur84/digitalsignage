using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Timers;
using System.IO;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing SQL data sources and executing queries
/// </summary>
public class SqlDataSourceService : ISqlDataSourceService, IDisposable
{
    private readonly ILogger<SqlDataSourceService> _logger;
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly ConcurrentDictionary<Guid, System.Timers.Timer> _refreshTimers;
    private readonly byte[] _encryptionEntropy;
    private bool _disposed;

    public SqlDataSourceService(ILogger<SqlDataSourceService> logger)
    {
        _logger = logger;
        _configDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
        _configFilePath = Path.Combine(_configDirectory, "datasources.json");
        _refreshTimers = new ConcurrentDictionary<Guid, System.Timers.Timer>();

        // Generate consistent entropy for encryption
        _encryptionEntropy = Encoding.UTF8.GetBytes("DigitalSignage.SqlDataSource.v1");

        // Ensure config directory exists
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
            _logger.LogInformation("Created config directory at {Path}", _configDirectory);
        }
    }

    /// <summary>
    /// Tests a SQL Server connection
    /// </summary>
    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("TestConnectionAsync called with empty connection string");
            return false;
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            _logger.LogInformation("SQL connection test successful");
            return true;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL connection test failed: {Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during connection test: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Retrieves list of tables from the database
    /// </summary>
    public async Task<List<string>> GetTablesAsync(string connectionString)
    {
        var tables = new List<string>();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string query = @"
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_NAME";

            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            _logger.LogInformation("Retrieved {Count} tables from database", tables.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tables: {Message}", ex.Message);
            throw;
        }

        return tables;
    }

    /// <summary>
    /// Retrieves column information for a specific table
    /// </summary>
    public async Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string tableName)
    {
        var columns = new List<ColumnInfo>();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string query = @"
                SELECT
                    COLUMN_NAME,
                    DATA_TYPE,
                    IS_NULLABLE,
                    CHARACTER_MAXIMUM_LENGTH
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                ORDER BY ORDINAL_POSITION";

            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@TableName", tableName);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetString(2) == "YES",
                    MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    IsSelected = false
                });
            }

            _logger.LogInformation("Retrieved {Count} columns for table {TableName}", columns.Count, tableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve columns for table {TableName}: {Message}", tableName, ex.Message);
            throw;
        }

        return columns;
    }

    /// <summary>
    /// Fetches data from the database based on data source configuration
    /// </summary>
    public async Task<List<Dictionary<string, object>>> FetchDataAsync(SqlDataSource dataSource)
    {
        if (dataSource == null)
            throw new ArgumentNullException(nameof(dataSource));

        var results = new List<Dictionary<string, object>>();

        try
        {
            // Decrypt connection string
            var connectionString = DecryptConnectionString(dataSource.ConnectionString);

            // Generate query
            var query = dataSource.GenerateQuery();

            // Validate query for safety
            if (!ValidateQuery(query))
            {
                var error = "Query validation failed - potentially unsafe SQL detected";
                _logger.LogWarning("Query validation failed for data source {Name}: {Query}", dataSource.Name, query);
                dataSource.LastError = error;
                throw new InvalidOperationException(error);
            }

            _logger.LogDebug("Executing query for data source {Name}: {Query}", dataSource.Name, query);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(query, connection);
            command.CommandTimeout = 30; // 30 second timeout

            await using var reader = await command.ExecuteReaderAsync();

            // Read column names
            var columnNames = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            // Read data rows
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columnNames[i]] = value ?? DBNull.Value;
                }

                results.Add(row);
            }

            // Update data source status
            dataSource.LastUpdate = DateTime.UtcNow;
            dataSource.LastError = null;
            dataSource.CachedData = results;

            _logger.LogInformation("Fetched {RowCount} rows for data source {Name}", results.Count, dataSource.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch data for data source {Name}: {Message}", dataSource.Name, ex.Message);
            dataSource.LastError = ex.Message;
            throw;
        }

        return results;
    }

    /// <summary>
    /// Validates SQL query for safety
    /// </summary>
    public bool ValidateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        var upperQuery = query.ToUpperInvariant();

        // Dangerous keywords that should not appear in SELECT queries
        var dangerousKeywords = new[]
        {
            "DROP ",
            "DELETE ",
            "INSERT ",
            "UPDATE ",
            "TRUNCATE ",
            "EXEC ",
            "EXECUTE ",
            "XP_",
            "SP_EXECUTESQL",
            "--",
            "/*",
            "*/",
            ";--",
            "@@",
            "SHUTDOWN"
        };

        foreach (var keyword in dangerousKeywords)
        {
            if (upperQuery.Contains(keyword))
            {
                _logger.LogWarning("Dangerous SQL keyword detected: {Keyword}", keyword);
                return false;
            }
        }

        // Query must start with SELECT
        if (!upperQuery.TrimStart().StartsWith("SELECT"))
        {
            _logger.LogWarning("Query does not start with SELECT");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Saves data source to persistent storage
    /// </summary>
    public async Task SaveDataSourceAsync(SqlDataSource dataSource)
    {
        if (dataSource == null)
            throw new ArgumentNullException(nameof(dataSource));

        try
        {
            // Load existing data sources
            var dataSources = await LoadDataSourcesAsync();

            // Encrypt connection string before saving
            var encryptedConnectionString = EncryptConnectionString(dataSource.ConnectionString);

            // Create a clone for saving (without cached data)
            var dataSourceToSave = new SqlDataSource
            {
                Id = dataSource.Id,
                Name = dataSource.Name,
                ConnectionString = encryptedConnectionString,
                TableName = dataSource.TableName,
                SelectedColumns = new List<string>(dataSource.SelectedColumns),
                WhereClause = dataSource.WhereClause,
                OrderByClause = dataSource.OrderByClause,
                MaxRows = dataSource.MaxRows,
                RefreshIntervalSeconds = dataSource.RefreshIntervalSeconds,
                IsActive = dataSource.IsActive,
                LastUpdate = dataSource.LastUpdate,
                CreatedAt = dataSource.CreatedAt,
                ModifiedAt = DateTime.UtcNow
            };

            // Update or add
            var existingIndex = dataSources.FindIndex(ds => ds.Id == dataSource.Id);
            if (existingIndex >= 0)
            {
                dataSources[existingIndex] = dataSourceToSave;
                _logger.LogInformation("Updated data source {Name} (ID: {Id})", dataSource.Name, dataSource.Id);
            }
            else
            {
                dataSources.Add(dataSourceToSave);
                _logger.LogInformation("Added new data source {Name} (ID: {Id})", dataSource.Name, dataSource.Id);
            }

            // Save to file
            var json = JsonSerializer.Serialize(dataSources, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_configFilePath, json);
            _logger.LogInformation("Saved {Count} data sources to {Path}", dataSources.Count, _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save data source {Name}: {Message}", dataSource.Name, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Loads all data sources from persistent storage
    /// </summary>
    public async Task<List<SqlDataSource>> LoadDataSourcesAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                _logger.LogInformation("No data sources file found, returning empty list");
                return new List<SqlDataSource>();
            }

            var json = await File.ReadAllTextAsync(_configFilePath);
            var dataSources = JsonSerializer.Deserialize<List<SqlDataSource>>(json) ?? new List<SqlDataSource>();

            // Decrypt connection strings
            foreach (var dataSource in dataSources)
            {
                dataSource.ConnectionString = DecryptConnectionString(dataSource.ConnectionString);
            }

            _logger.LogInformation("Loaded {Count} data sources from {Path}", dataSources.Count, _configFilePath);
            return dataSources;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load data sources: {Message}", ex.Message);
            return new List<SqlDataSource>();
        }
    }

    /// <summary>
    /// Deletes a data source
    /// </summary>
    public async Task DeleteDataSourceAsync(Guid id)
    {
        try
        {
            // Stop refresh timer if running
            StopPeriodicRefresh(id);

            // Load existing data sources
            var dataSources = await LoadDataSourcesAsync();

            // Remove the data source
            var removed = dataSources.RemoveAll(ds => ds.Id == id);

            if (removed > 0)
            {
                // Save updated list
                var json = JsonSerializer.Serialize(dataSources, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_configFilePath, json);
                _logger.LogInformation("Deleted data source with ID {Id}", id);
            }
            else
            {
                _logger.LogWarning("Data source with ID {Id} not found for deletion", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete data source {Id}: {Message}", id, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific data source by ID
    /// </summary>
    public async Task<SqlDataSource?> GetDataSourceByIdAsync(Guid id)
    {
        var dataSources = await LoadDataSourcesAsync();
        return dataSources.FirstOrDefault(ds => ds.Id == id);
    }

    /// <summary>
    /// Starts periodic refresh for a data source
    /// </summary>
    public void StartPeriodicRefresh(SqlDataSource dataSource)
    {
        if (dataSource == null || !dataSource.IsActive)
            return;

        // Stop existing timer if any
        StopPeriodicRefresh(dataSource.Id);

        var timer = new System.Timers.Timer(dataSource.RefreshIntervalSeconds * 1000);
        timer.Elapsed += async (sender, e) => await OnRefreshTimerElapsed(dataSource);
        timer.AutoReset = true;
        timer.Start();

        if (_refreshTimers.TryAdd(dataSource.Id, timer))
        {
            _logger.LogInformation("Started periodic refresh for data source {Name} (interval: {Interval}s)",
                dataSource.Name, dataSource.RefreshIntervalSeconds);
        }
        else
        {
            timer.Dispose();
            _logger.LogWarning("Failed to add refresh timer for data source {Name}", dataSource.Name);
        }
    }

    /// <summary>
    /// Stops periodic refresh for a data source
    /// </summary>
    public void StopPeriodicRefresh(Guid dataSourceId)
    {
        if (_refreshTimers.TryRemove(dataSourceId, out var timer))
        {
            timer.Stop();
            timer.Dispose();
            _logger.LogInformation("Stopped periodic refresh for data source {Id}", dataSourceId);
        }
    }

    /// <summary>
    /// Stops all periodic refreshes
    /// </summary>
    public void StopAllRefreshes()
    {
        foreach (var kvp in _refreshTimers)
        {
            kvp.Value.Stop();
            kvp.Value.Dispose();
        }

        _refreshTimers.Clear();
        _logger.LogInformation("Stopped all periodic refreshes");
    }

    /// <summary>
    /// Timer elapsed handler for periodic refresh
    /// </summary>
    private async Task OnRefreshTimerElapsed(SqlDataSource dataSource)
    {
        try
        {
            _logger.LogDebug("Refreshing data for data source {Name}", dataSource.Name);
            await FetchDataAsync(dataSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing data source {Name}: {Message}", dataSource.Name, ex.Message);
        }
    }

    /// <summary>
    /// Encrypts a connection string using DPAPI
    /// </summary>
    private string EncryptConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return string.Empty;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(connectionString);
            var encryptedBytes = ProtectedData.Protect(plainBytes, _encryptionEntropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt connection string");
            return connectionString; // Fallback to plain text (not ideal but prevents data loss)
        }
    }

    /// <summary>
    /// Decrypts a connection string using DPAPI
    /// </summary>
    private string DecryptConnectionString(string encryptedConnectionString)
    {
        if (string.IsNullOrWhiteSpace(encryptedConnectionString))
            return string.Empty;

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedConnectionString);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, _encryptionEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            // Not encrypted (plain text) - return as is
            return encryptedConnectionString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt connection string");
            return encryptedConnectionString; // Fallback to returning encrypted value
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopAllRefreshes();
        _disposed = true;

        _logger.LogInformation("SqlDataSourceService disposed");
    }
}
