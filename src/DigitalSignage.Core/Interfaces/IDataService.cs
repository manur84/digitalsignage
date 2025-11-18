using DigitalSignage.Core.Models;

namespace DigitalSignage.Core.Interfaces;

/// <summary>
/// Interface for data retrieval from various sources
/// </summary>
public interface IDataService
{
    Task<Dictionary<string, object>> GetDataAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task<List<string>> GetColumnsAsync(DataSource dataSource, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for SQL data operations
/// </summary>
public interface ISqlDataService : IDataService
{
    Task<Dictionary<string, object>> ExecuteQueryAsync(string connectionString, string query, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<List<string>> GetColumnsAsync(string connectionString, string tableName, CancellationToken cancellationToken = default);
}
