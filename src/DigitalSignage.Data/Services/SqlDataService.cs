using Dapper;
using DigitalSignage.Core.Interfaces;
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

            string sql = @"SELECT COLUMN_NAME
                           FROM INFORMATION_SCHEMA.COLUMNS
                           WHERE TABLE_NAME = @TableName" + (schema != null ? " AND TABLE_SCHEMA = @Schema" : "") + @"
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
