using Dapper;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Core.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text;
using System.Text.Json;

namespace DigitalSignage.Data.Services;

public class SqlDataService : ISqlDataService
{
    private readonly ILogger<SqlDataService> _logger;
    private readonly object? _queryCacheService;

    public SqlDataService(ILogger<SqlDataService> logger, object? queryCacheService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queryCacheService = queryCacheService; // Optional dependency for cache service
    }
    public async Task<Dictionary<string, object>> GetDataAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        if (dataSource == null)
        {
            _logger.LogError("GetDataAsync called with null dataSource");
            throw new ArgumentNullException(nameof(dataSource));
        }

        // Handle static data sources
        if (dataSource.Type == DataSourceType.StaticData)
        {
            return await GetStaticDataAsync(dataSource, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(dataSource.ConnectionString))
        {
            _logger.LogError("DataSource has empty ConnectionString");
            throw new ArgumentException("ConnectionString cannot be empty", nameof(dataSource));
        }

        if (string.IsNullOrWhiteSpace(dataSource.Query))
        {
            _logger.LogError("DataSource has empty Query");
            throw new ArgumentException("Query cannot be empty", nameof(dataSource));
        }

        try
        {
            _logger.LogDebug("Executing query from DataSource {DataSourceId}", dataSource.Id);
            return await ExecuteQueryAsync(
                dataSource.ConnectionString,
                dataSource.Query,
                dataSource.Parameters,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get data from DataSource {DataSourceId}", dataSource.Id);
            throw;
        }
    }

    public async Task<Dictionary<string, object>> ExecuteQueryAsync(
        string connectionString,
        string query,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("ExecuteQueryAsync called with empty connectionString");
            throw new ArgumentException("ConnectionString cannot be empty", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogError("ExecuteQueryAsync called with empty query");
            throw new ArgumentException("Query cannot be empty", nameof(query));
        }

        try
        {
            // Sanitize connection string - whitelist allowed keys
            // ✅ REFACTOR: Use shared ConnectionStringHelper to eliminate code duplication
            connectionString = ConnectionStringHelper.SanitizeConnectionString(connectionString);
            // Apply connection pooling settings to connection string
            connectionString = ApplyConnectionPoolSettings(connectionString);

            _logger.LogDebug("Executing SQL query: {Query}", query);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            _logger.LogDebug("SQL connection opened successfully");

            var dynamicParams = new DynamicParameters();
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    if (param.Key == null)
                    {
                        _logger.LogWarning("Skipping parameter with null key");
                        continue;
                    }
                    dynamicParams.Add(param.Key, param.Value);
                }
                _logger.LogDebug("Added {ParameterCount} parameters to query", parameters.Count);
            }

            var result = await connection.QueryAsync<dynamic>(
                query,
                dynamicParams,
                commandTimeout: 30);

            // ✅ FIX: Add null-check for result to prevent NullReferenceException
            var resultDict = new Dictionary<string, object>();
            var resultList = result?.ToList() ?? new List<dynamic>();

            // Use index access instead of First() for better performance
            if (resultList.Count > 0)
            {
                var firstRow = resultList[0] as IDictionary<string, object>;
                if (firstRow != null)
                {
                    foreach (var kvp in firstRow)
                    {
                        resultDict[kvp.Key] = kvp.Value ?? string.Empty;
                    }
                }

                // Also include all rows
                resultDict["_rows"] = resultList;
                resultDict["_count"] = resultList.Count;

                _logger.LogInformation("Query executed successfully, returned {RowCount} rows", resultList.Count);
            }
            else
            {
                resultDict["_rows"] = new List<object>();
                resultDict["_count"] = 0;
                _logger.LogInformation("Query executed successfully, returned 0 rows");
            }

            return resultDict;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL error executing query: {ErrorMessage}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing query");
            throw;
        }
    }

    // ✅ REFACTOR: SanitizeConnectionString moved to shared ConnectionStringHelper utility
    // This eliminates code duplication with SqlDataSourceService

    /// <summary>
    /// Applies connection pooling settings to connection string if not already present
    /// </summary>
    private string ApplyConnectionPoolSettings(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);

        // Apply default pooling settings if not explicitly set
        if (!connectionString.Contains("Min Pool Size", StringComparison.OrdinalIgnoreCase))
        {
            builder.MinPoolSize = 5;
        }

        if (!connectionString.Contains("Max Pool Size", StringComparison.OrdinalIgnoreCase))
        {
            builder.MaxPoolSize = 100;
        }

        if (!connectionString.Contains("Pooling", StringComparison.OrdinalIgnoreCase))
        {
            builder.Pooling = true;
        }

        if (!connectionString.Contains("Connection Timeout", StringComparison.OrdinalIgnoreCase))
        {
            builder.ConnectTimeout = 30;
        }

        return builder.ConnectionString;
    }

    public async Task<bool> TestConnectionAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        if (dataSource == null)
        {
            _logger.LogWarning("TestConnectionAsync called with null dataSource");
            return false;
        }

        // Handle static data sources
        if (dataSource.Type == DataSourceType.StaticData)
        {
            return await TestStaticDataAsync(dataSource, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(dataSource.ConnectionString))
        {
            _logger.LogWarning("TestConnectionAsync called with empty ConnectionString");
            return false;
        }

        return await TestConnectionAsync(dataSource.ConnectionString, cancellationToken);
    }

    public async Task<bool> TestConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("TestConnectionAsync called with empty connectionString");
            return false;
        }

        try
        {
            _logger.LogDebug("Testing SQL connection");
            // Sanitize + apply pooling before opening
            // ✅ REFACTOR: Use shared ConnectionStringHelper
            connectionString = ApplyConnectionPoolSettings(ConnectionStringHelper.SanitizeConnectionString(connectionString));

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var isOpen = connection.State == System.Data.ConnectionState.Open;
            if (isOpen)
            {
                _logger.LogInformation("SQL connection test successful");
            }
            else
            {
                _logger.LogWarning("SQL connection test failed: connection not in Open state");
            }

            return isOpen;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL connection test failed: {ErrorMessage}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error testing SQL connection");
            return false;
        }
    }

    public async Task<List<string>> GetColumnsAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        if (dataSource == null)
        {
            _logger.LogError("GetColumnsAsync called with null dataSource");
            throw new ArgumentNullException(nameof(dataSource));
        }

        if (string.IsNullOrWhiteSpace(dataSource.ConnectionString))
        {
            _logger.LogError("DataSource has empty ConnectionString");
            throw new ArgumentException("ConnectionString cannot be empty", nameof(dataSource));
        }

        if (string.IsNullOrWhiteSpace(dataSource.Query))
        {
            _logger.LogError("DataSource has empty Query");
            throw new ArgumentException("Query cannot be empty", nameof(dataSource));
        }

        try
        {
            await using var connection = new SqlConnection(dataSource.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // Extract table name from query (simplified - should be more robust)
            var query = dataSource.Query.ToLower();
            var fromIndex = query.IndexOf("from", StringComparison.Ordinal);
            if (fromIndex == -1)
            {
                _logger.LogWarning("Could not find 'FROM' clause in query");
                return new List<string>();
            }

            var afterFrom = query.Substring(fromIndex + 4).Trim();
            if (string.IsNullOrWhiteSpace(afterFrom))
            {
                _logger.LogWarning("Query has empty content after 'FROM' clause");
                return new List<string>();
            }

            var parts = afterFrom.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                _logger.LogWarning("Could not extract table name from query");
                return new List<string>();
            }

            var tableName = parts[0];

            // Sanitize table name to prevent SQL injection
            if (tableName.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '.'))
            {
                _logger.LogWarning("Table name contains invalid characters: {TableName}", tableName);
                return new List<string>();
            }

            _logger.LogDebug("Retrieving columns for table: {TableName}", tableName);

            var columnQuery = @"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                ORDER BY ORDINAL_POSITION";

            var columns = await connection.QueryAsync<string>(
                columnQuery,
                new { TableName = tableName });

            var columnList = columns.ToList();
            _logger.LogInformation("Retrieved {ColumnCount} columns for table {TableName}", columnList.Count, tableName);

            return columnList;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL error retrieving columns: {ErrorMessage}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving columns");
            throw;
        }
    }

    // New overload for schema-based validation without requiring a full query text
    public async Task<List<string>> GetColumnsAsync(
        string connectionString,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("connectionString is required", nameof(connectionString));
        }
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("tableName is required", nameof(tableName));
        }

        try
        {
            // ✅ REFACTOR: Use shared ConnectionStringHelper
            await using var connection = new SqlConnection(ApplyConnectionPoolSettings(ConnectionStringHelper.SanitizeConnectionString(connectionString)));
            await connection.OpenAsync(cancellationToken);

            // Parse optional schema
            string? schema = null;
            var tn = tableName.Trim();
            if (tn.Contains('.'))
            {
                var parts = tn.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    schema = parts[0];
                    tn = parts[1];
                }
            }

            // Sanitize identifiers
            bool IsValidIdent(string s) => s.All(c => char.IsLetterOrDigit(c) || c == '_');
            if (!IsValidIdent(tn) || (schema != null && !IsValidIdent(schema)))
            {
                _logger.LogWarning("Invalid identifier in table reference: {Table}", tableName);
                return new List<string>();
            }

            // Build SQL with optional schema filter
            var sql = schema != null
                ? @"SELECT COLUMN_NAME
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = @Schema
                    ORDER BY ORDINAL_POSITION"
                : @"SELECT COLUMN_NAME
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = @TableName
                    ORDER BY ORDINAL_POSITION";

            var columns = await connection.QueryAsync<string>(sql, new { TableName = tn, Schema = schema });
            return columns.ToList();
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL error retrieving columns for {Table}", tableName);
            throw;
        }
    }

    /// <summary>
    /// Retrieves static data from a DataSource configured with StaticData type
    /// </summary>
    private async Task<Dictionary<string, object>> GetStaticDataAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dataSource.StaticData))
        {
            _logger.LogWarning("Static data source {DataSourceId} has empty StaticData", dataSource.Id);
            return new Dictionary<string, object>();
        }

        try
        {
            _logger.LogDebug("Parsing static data from DataSource {DataSourceId}", dataSource.Id);

            // Parse JSON data
            using var jsonDocument = JsonDocument.Parse(dataSource.StaticData);
            var resultDict = new Dictionary<string, object>();

            // Convert JSON to dictionary
            if (jsonDocument.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in jsonDocument.RootElement.EnumerateObject())
                {
                    resultDict[property.Name] = ConvertJsonValue(property.Value);
                }
            }
            else if (jsonDocument.RootElement.ValueKind == JsonValueKind.Array)
            {
                // If it's an array, treat it as rows
                var rows = new List<object>();
                foreach (var element in jsonDocument.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        var row = new Dictionary<string, object>();
                        foreach (var property in element.EnumerateObject())
                        {
                            row[property.Name] = ConvertJsonValue(property.Value);
                        }
                        rows.Add(row);
                    }
                }
                resultDict["_rows"] = rows;
                resultDict["_count"] = rows.Count;

                // Also add first row properties to top level for template compatibility
                if (rows.Count > 0 && rows[0] is Dictionary<string, object> firstRow)
                {
                    foreach (var kvp in firstRow)
                    {
                        resultDict[kvp.Key] = kvp.Value;
                    }
                }
            }

            _logger.LogInformation("Successfully parsed static data from DataSource {DataSourceId}", dataSource.Id);
            return await Task.FromResult(resultDict);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in static data source {DataSourceId}: {ErrorMessage}", dataSource.Id, ex.Message);
            throw new InvalidOperationException($"Invalid JSON in static data: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing static data from DataSource {DataSourceId}", dataSource.Id);
            throw;
        }
    }

    /// <summary>
    /// Tests if static data is valid JSON
    /// </summary>
    private async Task<bool> TestStaticDataAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dataSource.StaticData))
        {
            _logger.LogWarning("TestStaticDataAsync called with empty StaticData");
            return false;
        }

        try
        {
            _logger.LogDebug("Testing static data JSON validity");
            using var jsonDocument = JsonDocument.Parse(dataSource.StaticData);
            _logger.LogInformation("Static data JSON is valid");
            return await Task.FromResult(true);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Static data JSON is invalid: {ErrorMessage}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error testing static data");
            return false;
        }
    }

    /// <summary>
    /// Converts JSON element to appropriate .NET type
    /// </summary]
    private object ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToList(),
            JsonValueKind.Object => ParseJsonObject(element),
            _ => element.ToString()
        };
    }

    /// <summary>
    /// Parses JSON object into dictionary
    /// </summary>
    private Dictionary<string, object> ParseJsonObject(JsonElement element)
    {
        var dict = new Dictionary<string, object>();
        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = ConvertJsonValue(property.Value);
        }
        return dict;
    }
}
