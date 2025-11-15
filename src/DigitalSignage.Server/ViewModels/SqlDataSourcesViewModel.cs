using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for managing SQL data sources
/// </summary>
public partial class SqlDataSourcesViewModel : ObservableObject, IDisposable
{
    private readonly ISqlDataSourceService _sqlDataSourceService;
    private readonly DataSourceManager _dataSourceManager;
    private readonly ILogger<SqlDataSourcesViewModel> _logger;
    private bool _disposed;

    #region Observable Properties

    [ObservableProperty]
    private ObservableCollection<SqlDataSource> _dataSources = new();

    [ObservableProperty]
    private SqlDataSource? _selectedDataSource;

    [ObservableProperty]
    private ObservableCollection<string> _tables = new();

    [ObservableProperty]
    private ObservableCollection<ColumnInfo> _columns = new();

    [ObservableProperty]
    private string _server = "localhost";

    [ObservableProperty]
    private int _port = 1433;

    [ObservableProperty]
    private string _database = string.Empty;

    [ObservableProperty]
    private SqlAuthenticationType _authType = SqlAuthenticationType.WindowsAuthentication;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string _connectionStatus = "Not connected";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _selectedTable;

    [ObservableProperty]
    private int _refreshInterval = 300;

    [ObservableProperty]
    private string _dataSourceName = string.Empty;

    [ObservableProperty]
    private string? _whereClause;

    [ObservableProperty]
    private string? _orderByClause;

    [ObservableProperty]
    private int _maxRows = 100;

    #endregion

    private string _currentConnectionString = string.Empty;

    public SqlDataSourcesViewModel(
        ISqlDataSourceService sqlDataSourceService,
        DataSourceManager dataSourceManager,
        ILogger<SqlDataSourcesViewModel> logger)
    {
        _sqlDataSourceService = sqlDataSourceService ?? throw new ArgumentNullException(nameof(sqlDataSourceService));
        _dataSourceManager = dataSourceManager ?? throw new ArgumentNullException(nameof(dataSourceManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load existing data sources
        _ = LoadDataSourcesAsync();
    }

    /// <summary>
    /// Loads all saved data sources
    /// </summary>
    [RelayCommand]
    private async Task LoadDataSourcesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading data sources...";

            var dataSources = await _sqlDataSourceService.LoadDataSourcesAsync();

            DataSources.Clear();
            foreach (var ds in dataSources)
            {
                DataSources.Add(ds);
            }

            StatusMessage = $"Loaded {dataSources.Count} data sources";
            _logger.LogInformation("Loaded {Count} data sources", dataSources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load data sources");
            StatusMessage = $"Error loading data sources: {ex.Message}";
            MessageBox.Show($"Failed to load data sources: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Connects to the SQL Server
    /// </summary>
    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            IsConnecting = true;
            ConnectionStatus = "Connecting...";
            StatusMessage = "Testing SQL Server connection...";

            // Validate inputs
            if (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Database))
            {
                MessageBox.Show("Please enter server and database name", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (AuthType == SqlAuthenticationType.SqlServerAuthentication &&
                string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Please enter username for SQL Server authentication", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build connection config using existing SqlConnectionConfig
            var serverWithPort = Port == 1433 ? Server : $"{Server},{Port}";
            var config = new SqlConnectionConfig
            {
                Server = serverWithPort,
                Database = Database,
                IntegratedSecurity = (AuthType == SqlAuthenticationType.WindowsAuthentication),
                Username = Username,
                Password = Password,
                ConnectionTimeout = 15,
                Encrypt = true,
                TrustServerCertificate = true
            };

            // Build connection string
            _currentConnectionString = config.ToConnectionString();

            // Test connection
            var success = await _sqlDataSourceService.TestConnectionAsync(_currentConnectionString);

            if (success)
            {
                IsConnected = true;
                ConnectionStatus = $"Connected to {Server}\\{Database}";
                StatusMessage = "Connection successful!";

                // Load tables
                await LoadTablesAsync();

                _logger.LogInformation("Successfully connected to SQL Server: {Server}\\{Database}", Server, Database);
            }
            else
            {
                IsConnected = false;
                ConnectionStatus = "Connection failed";
                StatusMessage = "Connection failed - check credentials and network";
                MessageBox.Show("Failed to connect to SQL Server. Please check your connection settings.",
                    "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to SQL Server");
            IsConnected = false;
            ConnectionStatus = "Connection error";
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Connection error: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    /// <summary>
    /// Disconnects from the SQL Server
    /// </summary>
    [RelayCommand]
    private void Disconnect()
    {
        IsConnected = false;
        ConnectionStatus = "Not connected";
        StatusMessage = "Disconnected";
        Tables.Clear();
        Columns.Clear();
        SelectedTable = null;
        _currentConnectionString = string.Empty;

        _logger.LogInformation("Disconnected from SQL Server");
    }

    /// <summary>
    /// Loads tables from the connected database
    /// </summary>
    private async Task LoadTablesAsync()
    {
        try
        {
            StatusMessage = "Loading tables...";

            var tables = await _sqlDataSourceService.GetTablesAsync(_currentConnectionString);

            Tables.Clear();
            foreach (var table in tables)
            {
                Tables.Add(table);
            }

            StatusMessage = $"Loaded {tables.Count} tables";
            _logger.LogInformation("Loaded {Count} tables from database", tables.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tables");
            StatusMessage = $"Error loading tables: {ex.Message}";
        }
    }

    /// <summary>
    /// Loads columns for the selected table
    /// </summary>
    partial void OnSelectedTableChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value) && IsConnected)
        {
            _ = LoadColumnsAsync(value);
        }
        else
        {
            Columns.Clear();
        }
    }

    private async Task LoadColumnsAsync(string tableName)
    {
        try
        {
            StatusMessage = $"Loading columns for {tableName}...";

            var columns = await _sqlDataSourceService.GetColumnsAsync(_currentConnectionString, tableName);

            Columns.Clear();
            foreach (var column in columns)
            {
                Columns.Add(column);
            }

            StatusMessage = $"Loaded {columns.Count} columns from {tableName}";
            _logger.LogInformation("Loaded {Count} columns from table {TableName}", columns.Count, tableName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load columns for table {TableName}", tableName);
            StatusMessage = $"Error loading columns: {ex.Message}";
        }
    }

    /// <summary>
    /// Creates a new data source
    /// </summary>
    [RelayCommand]
    private void NewDataSource()
    {
        // Clear connection state
        Disconnect();

        // Reset form
        DataSourceName = "New Data Source";
        SelectedTable = null;
        RefreshInterval = 300;
        WhereClause = null;
        OrderByClause = null;
        MaxRows = 100;
        SelectedDataSource = null;

        StatusMessage = "Ready to create new data source";
    }

    /// <summary>
    /// Saves the current data source configuration
    /// </summary>
    [RelayCommand]
    private async Task SaveDataSourceAsync()
    {
        try
        {
            // Validation
            if (string.IsNullOrWhiteSpace(DataSourceName))
            {
                MessageBox.Show("Please enter a name for the data source", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsConnected)
            {
                MessageBox.Show("Please connect to a database first", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(SelectedTable))
            {
                MessageBox.Show("Please select a table", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedColumns = Columns.Where(c => c.IsSelected).Select(c => c.Name).ToList();
            if (selectedColumns.Count == 0)
            {
                MessageBox.Show("Please select at least one column", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsLoading = true;
            StatusMessage = "Saving data source...";

            // Create or update data source
            var dataSource = SelectedDataSource ?? new SqlDataSource();
            dataSource.Name = DataSourceName;
            dataSource.ConnectionString = _currentConnectionString;
            dataSource.TableName = SelectedTable;
            dataSource.SelectedColumns = selectedColumns;
            dataSource.WhereClause = WhereClause;
            dataSource.OrderByClause = OrderByClause;
            dataSource.MaxRows = MaxRows;
            dataSource.RefreshIntervalSeconds = RefreshInterval;
            dataSource.IsActive = false; // Start inactive
            dataSource.ModifiedAt = DateTime.UtcNow;

            // Save to persistent storage
            await _sqlDataSourceService.SaveDataSourceAsync(dataSource);

            // Reload data sources list
            await LoadDataSourcesAsync();

            StatusMessage = $"Data source '{DataSourceName}' saved successfully";
            MessageBox.Show($"Data source '{DataSourceName}' saved successfully!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);

            _logger.LogInformation("Saved data source {Name} (ID: {Id})", dataSource.Name, dataSource.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save data source");
            StatusMessage = $"Error saving data source: {ex.Message}";
            MessageBox.Show($"Failed to save data source: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Edits an existing data source
    /// </summary>
    [RelayCommand]
    private async Task EditDataSourceAsync()
    {
        if (SelectedDataSource == null)
        {
            MessageBox.Show("Please select a data source to edit", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Load configuration into UI
            DataSourceName = SelectedDataSource.Name;
            RefreshInterval = SelectedDataSource.RefreshIntervalSeconds;
            WhereClause = SelectedDataSource.WhereClause;
            OrderByClause = SelectedDataSource.OrderByClause;
            MaxRows = SelectedDataSource.MaxRows;

            // Parse connection string (basic parsing)
            try
            {
                var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(SelectedDataSource.ConnectionString);
                var dataSource = builder.DataSource;

                // Parse server and port
                if (dataSource != null && dataSource.Contains(','))
                {
                    var serverParts = dataSource.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (serverParts.Length > 0)
                        Server = serverParts[0];
                    if (serverParts.Length > 1 && int.TryParse(serverParts[1], out int parsedPort))
                        Port = parsedPort;
                    else
                        Port = 1433;
                }
                else
                {
                    Server = dataSource ?? "localhost";
                    Port = 1433;
                }

                Database = builder.InitialCatalog;
                AuthType = builder.IntegratedSecurity ? SqlAuthenticationType.WindowsAuthentication : SqlAuthenticationType.SqlServerAuthentication;
                Username = builder.UserID;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse connection string");
                // Use defaults
                Server = "localhost";
                Port = 1433;
                Database = string.Empty;
                AuthType = SqlAuthenticationType.WindowsAuthentication;
                Username = string.Empty;
            }

            // Connect
            await ConnectAsync();

            if (IsConnected)
            {
                // Select table
                SelectedTable = SelectedDataSource.TableName;

                // Wait for columns to load
                await Task.Delay(500);

                // Select columns
                foreach (var column in Columns)
                {
                    column.IsSelected = SelectedDataSource.SelectedColumns.Contains(column.Name);
                }

                StatusMessage = $"Loaded data source '{SelectedDataSource.Name}' for editing";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load data source for editing");
            MessageBox.Show($"Failed to load data source: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Deletes a data source
    /// </summary>
    [RelayCommand]
    private async Task DeleteDataSourceAsync()
    {
        if (SelectedDataSource == null)
        {
            MessageBox.Show("Please select a data source to delete", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete the data source '{SelectedDataSource.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Deleting data source '{SelectedDataSource.Name}'...";

            await _sqlDataSourceService.DeleteDataSourceAsync(SelectedDataSource.Id);

            StatusMessage = $"Data source '{SelectedDataSource.Name}' deleted";
            _logger.LogInformation("Deleted data source {Name} (ID: {Id})",
                SelectedDataSource.Name, SelectedDataSource.Id);

            // Reload list
            await LoadDataSourcesAsync();

            MessageBox.Show("Data source deleted successfully", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete data source");
            StatusMessage = $"Error deleting data source: {ex.Message}";
            MessageBox.Show($"Failed to delete data source: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Toggles the active state of a data source
    /// </summary>
    [RelayCommand]
    private async Task ToggleActiveAsync()
    {
        if (SelectedDataSource == null)
            return;

        try
        {
            IsLoading = true;
            SelectedDataSource.IsActive = !SelectedDataSource.IsActive;

            // Save updated state
            await _sqlDataSourceService.SaveDataSourceAsync(SelectedDataSource);

            if (SelectedDataSource.IsActive)
            {
                // Activate in manager
                await _dataSourceManager.ActivateDataSourceAsync(SelectedDataSource);
                StatusMessage = $"Activated data source '{SelectedDataSource.Name}'";
            }
            else
            {
                // Deactivate in manager
                _dataSourceManager.DeactivateDataSource(SelectedDataSource.Id);
                StatusMessage = $"Deactivated data source '{SelectedDataSource.Name}'";
            }

            _logger.LogInformation("Toggled active state for data source {Name} to {Active}",
                SelectedDataSource.Name, SelectedDataSource.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle active state");
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to toggle active state: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.LogInformation("SqlDataSourcesViewModel disposed");
    }
}
