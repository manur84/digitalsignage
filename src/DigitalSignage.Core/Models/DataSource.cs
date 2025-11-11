namespace DigitalSignage.Core.Models;

/// <summary>
/// Represents a data source for dynamic content
/// </summary>
public class DataSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public DataSourceType Type { get; set; } = DataSourceType.SQL;
    public string ConnectionString { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int RefreshInterval { get; set; } = 60; // seconds
    public DateTime? LastRefresh { get; set; }
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Static data in JSON format (used when Type = StaticData)
    /// </summary>
    public string StaticData { get; set; } = string.Empty;
}

public enum DataSourceType
{
    SQL,
    REST,
    StaticData,
    StoredProcedure
}

/// <summary>
/// SQL connection configuration
/// </summary>
public class SqlConnectionConfig
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IntegratedSecurity { get; set; } = false;
    public int ConnectionTimeout { get; set; } = 30;
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = false;

    public string ToConnectionString()
    {
        if (IntegratedSecurity)
        {
            return $"Server={Server};Database={Database};Integrated Security=true;Connection Timeout={ConnectionTimeout};Encrypt={Encrypt};TrustServerCertificate={TrustServerCertificate}";
        }
        else
        {
            return $"Server={Server};Database={Database};User Id={Username};Password={Password};Connection Timeout={ConnectionTimeout};Encrypt={Encrypt};TrustServerCertificate={TrustServerCertificate}";
        }
    }
}
