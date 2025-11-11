using Dapper;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DigitalSignage.Data.Services;

public class SqlDataService : ISqlDataService
{
    public async Task<Dictionary<string, object>> GetDataAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteQueryAsync(
            dataSource.ConnectionString,
            dataSource.Query,
            dataSource.Parameters,
            cancellationToken);
    }

    public async Task<Dictionary<string, object>> ExecuteQueryAsync(
        string connectionString,
        string query,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var dynamicParams = new DynamicParameters();
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                dynamicParams.Add(param.Key, param.Value);
            }
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
        }

        return resultDict;
    }

    public async Task<bool> TestConnectionAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        return await TestConnectionAsync(dataSource.ConnectionString, cancellationToken);
    }

    public async Task<bool> TestConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection.State == ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetColumnsAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(dataSource.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Extract table name from query (simplified - should be more robust)
        var query = dataSource.Query.ToLower();
        var fromIndex = query.IndexOf("from", StringComparison.Ordinal);
        if (fromIndex == -1) return new List<string>();

        var tableName = query.Substring(fromIndex + 4).Trim().Split(' ')[0];

        var columnQuery = @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @TableName
            ORDER BY ORDINAL_POSITION";

        var columns = await connection.QueryAsync<string>(
            columnQuery,
            new { TableName = tableName });

        return columns.ToList();
    }
}
