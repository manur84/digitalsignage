using System.Text.Json.Serialization;

namespace DigitalSignage.Core.Models;

/// <summary>
/// Represents a SQL database connection and data source for dynamic content in layouts
/// </summary>
public class SqlDataSource
{
    /// <summary>
    /// Unique identifier for the data source
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User-friendly name for the data source
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted SQL Server connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Database table name to query
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// List of column names to retrieve from the table
    /// </summary>
    public List<string> SelectedColumns { get; set; } = new();

    /// <summary>
    /// Optional WHERE clause for filtering data (without the WHERE keyword)
    /// </summary>
    public string? WhereClause { get; set; }

    /// <summary>
    /// Optional ORDER BY clause for sorting data (without the ORDER BY keyword)
    /// </summary>
    public string? OrderByClause { get; set; }

    /// <summary>
    /// Maximum number of rows to retrieve (0 = unlimited)
    /// </summary>
    public int MaxRows { get; set; } = 100;

    /// <summary>
    /// Refresh interval in seconds (how often to fetch new data)
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 300; // 5 minutes default

    /// <summary>
    /// Whether the data source is active and should be refreshed automatically
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp of the last successful data update
    /// </summary>
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// Cached data from the last query execution (not persisted to database)
    /// </summary>
    [JsonIgnore]
    public List<Dictionary<string, object>>? CachedData { get; set; }

    /// <summary>
    /// Number of rows in the cached data
    /// </summary>
    [JsonIgnore]
    public int CachedRowCount => CachedData?.Count ?? 0;

    /// <summary>
    /// Whether there is cached data available
    /// </summary>
    [JsonIgnore]
    public bool HasCachedData => CachedData != null && CachedData.Count > 0;

    /// <summary>
    /// Last error message if data fetch failed (not persisted)
    /// </summary>
    [JsonIgnore]
    public string? LastError { get; set; }

    /// <summary>
    /// Whether the last data fetch was successful
    /// </summary>
    [JsonIgnore]
    public bool IsHealthy => string.IsNullOrEmpty(LastError);

    /// <summary>
    /// Timestamp when this data source was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this data source was last modified
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Generates the SQL query based on configuration
    /// </summary>
    /// <returns>Complete SQL SELECT query</returns>
    public string GenerateQuery()
    {
        if (string.IsNullOrWhiteSpace(TableName))
            throw new InvalidOperationException("Table name is required to generate query");

        var columns = SelectedColumns.Count > 0
            ? string.Join(", ", SelectedColumns.Select(c => $"[{c}]"))
            : "*";

        var query = $"SELECT ";

        if (MaxRows > 0)
            query += $"TOP {MaxRows} ";

        query += $"{columns} FROM [{TableName}]";

        if (!string.IsNullOrWhiteSpace(WhereClause))
            query += $" WHERE {WhereClause}";

        if (!string.IsNullOrWhiteSpace(OrderByClause))
            query += $" ORDER BY {OrderByClause}";

        return query;
    }

    /// <summary>
    /// Creates a copy of this data source (excluding cached data)
    /// </summary>
    public SqlDataSource Clone()
    {
        return new SqlDataSource
        {
            Id = Guid.NewGuid(), // New ID for cloned source
            Name = $"{Name} (Copy)",
            ConnectionString = ConnectionString,
            TableName = TableName,
            SelectedColumns = new List<string>(SelectedColumns),
            WhereClause = WhereClause,
            OrderByClause = OrderByClause,
            MaxRows = MaxRows,
            RefreshIntervalSeconds = RefreshIntervalSeconds,
            IsActive = false, // Clones start inactive
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Information about a database table column
/// </summary>
public class ColumnInfo
{
    /// <summary>
    /// Column name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SQL data type (e.g., "varchar", "int", "datetime")
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// Whether the column can contain NULL values
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Maximum length for character types (null if not applicable)
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Whether this column is selected for retrieval
    /// </summary>
    public bool IsSelected { get; set; }

    public override string ToString() => $"{Name} ({DataType})";
}

/// <summary>
/// SQL Server authentication type (enum for UI purposes)
/// </summary>
public enum SqlAuthenticationType
{
    /// <summary>
    /// Windows integrated authentication (current user credentials)
    /// </summary>
    WindowsAuthentication,

    /// <summary>
    /// SQL Server authentication (username/password)
    /// </summary>
    SqlServerAuthentication
}

/// <summary>
/// Event arguments for data source update events
/// </summary>
public class DataSourceUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// ID of the data source that was updated
    /// </summary>
    public Guid DataSourceId { get; set; }

    /// <summary>
    /// Name of the data source
    /// </summary>
    public string DataSourceName { get; set; } = string.Empty;

    /// <summary>
    /// The updated data
    /// </summary>
    public List<Dictionary<string, object>> Data { get; set; } = new();

    /// <summary>
    /// Whether the data changed compared to previous fetch
    /// </summary>
    public bool DataChanged { get; set; }

    /// <summary>
    /// Timestamp of the update
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
