namespace DigitalSignage.Core.Models;

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
