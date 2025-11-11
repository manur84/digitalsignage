using Dapper;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

namespace DigitalSignage.Data.Services;

public class SqlDataService : ISqlDataService
{
    private readonly ILogger<SqlDataService> _logger;

    public SqlDataService(ILogger<SqlDataService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            var resultDict = new Dictionary<string, object>();
            var resultList = result.ToList();

            if (resultList.Any())
            {
                var firstRow = resultList.First() as IDictionary<string, object>;
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

    public async Task<bool> TestConnectionAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        if (dataSource == null)
        {
            _logger.LogWarning("TestConnectionAsync called with null dataSource");
            return false;
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
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var isOpen = connection.State == ConnectionState.Open;
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
}
