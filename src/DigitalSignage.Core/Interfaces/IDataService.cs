namespace DigitalSignage.Core.Interfaces;

/// <summary>
/// Interface for SQL data operations
/// </summary>
public interface ISqlDataService
{
    Task<Dictionary<string, object>> ExecuteQueryAsync(string connectionString, string query, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<List<string>> GetColumnsAsync(string connectionString, string tableName, CancellationToken cancellationToken = default);
}
