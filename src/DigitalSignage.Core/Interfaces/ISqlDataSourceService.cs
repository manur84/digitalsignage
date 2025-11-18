using DigitalSignage.Core.Models;

namespace DigitalSignage.Core.Interfaces;

/// <summary>
/// Service for managing SQL data sources and executing queries
/// </summary>
public interface ISqlDataSourceService
{
    /// <summary>
    /// Tests a SQL Server connection with the provided connection string
    /// </summary>
    /// <param name="connectionString">SQL Server connection string</param>
    /// <returns>True if connection successful, false otherwise</returns>
    Task<bool> TestConnectionAsync(string connectionString);

    /// <summary>
    /// Retrieves a list of all tables in the database
    /// </summary>
    /// <param name="connectionString">SQL Server connection string</param>
    /// <returns>List of table names</returns>
    Task<List<string>> GetTablesAsync(string connectionString);

    /// <summary>
    /// Retrieves column information for a specific table
    /// </summary>
    /// <param name="connectionString">SQL Server connection string</param>
    /// <param name="tableName">Name of the table</param>
    /// <returns>Dictionary of column information (column name → column info)</returns>
    Task<List<ColumnInfo>> GetColumnsAsync(string connectionString, string tableName);

    /// <summary>
    /// Fetches data from the database based on the data source configuration
    /// </summary>
    /// <param name="dataSource">SQL data source configuration</param>
    /// <returns>List of rows, where each row is a dictionary of column values</returns>
    Task<List<Dictionary<string, object>>> FetchDataAsync(SqlDataSource dataSource);

    /// <summary>
    /// Saves a data source to persistent storage
    /// </summary>
    /// <param name="dataSource">Data source to save</param>
    Task SaveDataSourceAsync(SqlDataSource dataSource);

    /// <summary>
    /// Loads all data sources from persistent storage
    /// </summary>
    /// <returns>List of all configured data sources</returns>
    Task<List<SqlDataSource>> LoadDataSourcesAsync();

    /// <summary>
    /// Deletes a data source from persistent storage
    /// </summary>
    /// <param name="id">ID of the data source to delete</param>
    Task DeleteDataSourceAsync(Guid id);

    /// <summary>
    /// Gets a specific data source by ID
    /// </summary>
    /// <param name="id">Data source ID</param>
    /// <returns>Data source if found, null otherwise</returns>
    Task<SqlDataSource?> GetDataSourceByIdAsync(Guid id);

    /// <summary>
    /// Starts periodic refresh for a data source
    /// </summary>
    /// <param name="dataSource">Data source to start refreshing</param>
    void StartPeriodicRefresh(SqlDataSource dataSource);

    /// <summary>
    /// Stops periodic refresh for a data source
    /// </summary>
    /// <param name="dataSourceId">ID of the data source</param>
    void StopPeriodicRefresh(Guid dataSourceId);

    /// <summary>
    /// Stops all periodic refreshes
    /// </summary>
    void StopAllRefreshes();

    /// <summary>
    /// Validates a SQL query for safety (prevents injection)
    /// </summary>
    /// <param name="query">SQL query to validate</param>
    /// <returns>True if query is safe, false otherwise</returns>
    bool ValidateQuery(string query);
}
