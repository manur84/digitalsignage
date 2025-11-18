using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Serilog;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace DigitalSignage.Server.ViewModels;

public partial class DataSourceViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly DataSourceRepository _repository;
    private readonly ILogger _logger;

    [ObservableProperty]
    private DataSource? _selectedDataSource;

    [ObservableProperty]
    private string _testResult = string.Empty;

    [ObservableProperty]
    private bool _isTesting = false;

    [ObservableProperty]
    private bool _isStaticDataSource = false;

    [ObservableProperty]
    private string _staticDataJson = string.Empty;

    // Query Builder Properties
    [ObservableProperty]
    private string _queryTableName = string.Empty;

    [ObservableProperty]
    private string _queryColumns = "*";

    [ObservableProperty]
    private string _queryWhereClause = string.Empty;

    [ObservableProperty]
    private string _queryOrderBy = string.Empty;

    [ObservableProperty]
    private string _generatedQuery = string.Empty;

    // Schema discovery UI state
    [ObservableProperty]
    private ObservableCollection<string> _availableColumns = new();

    [ObservableProperty]
    private string _selectedAvailableColumn = string.Empty;

    [ObservableProperty]
    private bool _isLoadingColumns = false;

    public ObservableCollection<DataSource> DataSources { get; } = new();

    public DataSourceViewModel(IDataService dataService, DataSourceRepository repository)
    {
        _dataService = dataService;
        _repository = repository;
        _logger = Log.ForContext<DataSourceViewModel>();

        // Load data sources from database
        _ = LoadDataSourcesAsync();
    }

    partial void OnSelectedDataSourceChanged(DataSource? value)
    {
        if (value != null)
        {
            IsStaticDataSource = value.Type == DataSourceType.StaticData;
            StaticDataJson = value.StaticData ?? string.Empty;
        }
        else
        {
            IsStaticDataSource = false;
            StaticDataJson = string.Empty;
        }
    }

    partial void OnIsStaticDataSourceChanged(bool value)
    {
        if (SelectedDataSource != null)
        {
            SelectedDataSource.Type = value ? DataSourceType.StaticData : DataSourceType.SQL;
        }
    }

    partial void OnStaticDataJsonChanged(string value)
    {
        if (SelectedDataSource != null)
        {
            SelectedDataSource.StaticData = value;
        }
    }

    [RelayCommand]
    private void AddDataSource()
    {
        var newDataSource = new DataSource
        {
            Name = "New Data Source",
            Type = DataSourceType.SQL
        };

        DataSources.Add(newDataSource);
        SelectedDataSource = newDataSource;
    }

    [RelayCommand]
    private void AddStaticDataSource()
    {
        var newDataSource = new DataSource
        {
            Name = "New Static Data Source",
            Type = DataSourceType.StaticData,
            StaticData = @"{
  ""room_name"": ""Conference Room A"",
  ""status"": ""Available"",
  ""temperature"": ""22°C""
}"
        };

        DataSources.Add(newDataSource);
        SelectedDataSource = newDataSource;
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (SelectedDataSource == null)
        {
            TestResult = "No data source selected";
            return;
        }

        IsTesting = true;
        TestResult = "Testing connection...";

        try
        {
            var success = await _dataService.TestConnectionAsync(SelectedDataSource);
            TestResult = success ? "Connection successful!" : "Connection failed!";
        }
        catch (Exception ex)
        {
            TestResult = $"Error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task LoadDataSources()
    {
        await LoadDataSourcesAsync();
    }

    private async Task LoadDataSourcesAsync()
    {
        try
        {
            var dataSources = await _repository.GetAllAsync();

            DataSources.Clear();
            foreach (var ds in dataSources)
            {
                DataSources.Add(ds);
            }

            _logger.Information("Loaded {Count} data sources from database", dataSources.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load data sources from database");
            TestResult = $"Error loading data sources: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveDataSource()
    {
        if (SelectedDataSource == null) return;

        try
        {
            // Check if it's a new data source (empty/null Id) or existing
            if (string.IsNullOrEmpty(SelectedDataSource.Id) || await _repository.GetByIdAsync(SelectedDataSource.Id) == null)
            {
                // New data source
                await _repository.AddAsync(SelectedDataSource);
                TestResult = "Data source added successfully!";
            }
            else
            {
                // Update existing
                await _repository.UpdateAsync(SelectedDataSource);
                TestResult = "Data source updated successfully!";
            }

            // Reload from database to ensure consistency
            await LoadDataSourcesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save data source");
            TestResult = $"Error saving: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteDataSource()
    {
        if (SelectedDataSource == null) return;

        try
        {
            if (!string.IsNullOrEmpty(SelectedDataSource.Id))
            {
                await _repository.DeleteAsync(SelectedDataSource.Id);
            }

            DataSources.Remove(SelectedDataSource);
            SelectedDataSource = null;
            TestResult = "Data source deleted successfully!";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete data source");
            TestResult = $"Error deleting: {ex.Message}";
        }
    }

    /// <summary>
    /// Validates SQL input to prevent injection attacks
    /// </summary>
    private bool ValidateSqlInput(string input, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(input))
            return true;

        // Dangerous keywords that could lead to SQL injection or data manipulation
        var dangerousKeywords = new[]
        {
            "DROP", "DELETE", "INSERT", "UPDATE", "EXEC", "EXECUTE",
            "--", "/*", "*/", ";--", "xp_", "sp_"
        };

        var upperInput = input.ToUpperInvariant();

        foreach (var keyword in dangerousKeywords)
        {
            if (upperInput.Contains(keyword))
            {
                _logger.Warning("Dangerous SQL keyword '{Keyword}' detected in {Field}", keyword, fieldName);
                TestResult = $"❌ Query contains potentially dangerous SQL keyword: {keyword}";
                return false;
            }
        }

        return true;
    }

    // Identifier validation: letters, digits, underscore; allow dot for schema-qualified names
    private static readonly Regex IdentifierRegex = new("^[A-Za-z_][A-Za-z0-9_]*$");

    private bool IsValidIdentifier(string name)
    {
        return IdentifierRegex.IsMatch(name);
    }

    private string? BuildSafeTableReference(string table)
    {
        if (string.IsNullOrWhiteSpace(table)) return null;
        var parts = table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Length > 3) return null; // [db].[schema].[table] not fully supported, but allow up to 3 parts
        foreach (var p in parts)
        {
            if (!IsValidIdentifier(p)) return null;
        }
        // Quote each part with square brackets for SQL Server
        return string.Join('.', parts.Select(p => $"[{p}]"));
    }

    private string BuildSafeColumns(string rawColumns)
    {
        if (string.IsNullOrWhiteSpace(rawColumns) || rawColumns.Trim() == "*")
        {
            return "*";
        }

        var requested = rawColumns
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var safeList = new List<string>();

        // If we have a whitelist from the database, enforce it strictly
        var hasWhitelist = AvailableColumns != null && AvailableColumns.Count > 0;
        var whitelist = hasWhitelist
            ? new HashSet<string>(AvailableColumns, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var col in requested)
        {
            var c = col;
            // Handle simple aliasing like "Name as N" by splitting and validating identifier part only
            string ident = c;
            string? alias = null;

            var parts = c.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[1].Equals("AS", StringComparison.OrdinalIgnoreCase))
            {
                ident = parts[0];
                alias = parts[2];
            }

            if (!IsValidIdentifier(ident))
            {
                _logger.Warning("Rejected invalid column identifier: {Column}", ident);
                continue;
            }

            if (hasWhitelist && !whitelist.Contains(ident))
            {
                _logger.Warning("Rejected non-whitelisted column: {Column}", ident);
                continue;
            }

            var quoted = $"[{ident}]";
            if (!string.IsNullOrEmpty(alias))
            {
                if (IsValidIdentifier(alias))
                {
                    quoted += $" AS [{alias}]";
                }
            }
            safeList.Add(quoted);
        }

        if (safeList.Count == 0)
        {
            // Fallback: if nothing valid, use * but warn
            TestResult = "ℹ️ No valid columns selected; using *";
            return "*";
        }

        return string.Join(", ", safeList);
    }

    [RelayCommand]
    private void GenerateQuery()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(QueryTableName))
            {
                GeneratedQuery = "-- Please enter a table name";
                return;
            }

            // Validate all user inputs for SQL injection attempts (where/order by still basic validation)
            if (!ValidateSqlInput(QueryWhereClause, "Where Clause") ||
                !ValidateSqlInput(QueryOrderBy, "Order By"))
            {
                GeneratedQuery = "-- Query validation failed due to dangerous SQL patterns";
                return;
            }

            // Strictly validate and quote table name
            var safeTable = BuildSafeTableReference(QueryTableName.Trim());
            if (safeTable == null)
            {
                GeneratedQuery = "-- Invalid table name (only letters, digits, underscore; optional schema)";
                TestResult = "❌ Invalid table identifier";
                return;
            }

            // Strictly validate and quote columns (use whitelist if loaded)
            var safeColumns = BuildSafeColumns(QueryColumns ?? "*");

            // Build the SELECT clause
            var query = $"SELECT {safeColumns}";

            // Add FROM clause
            query += $"\nFROM {safeTable}";

            // Add WHERE clause if provided (kept as-is but pre-validated for dangerous tokens)
            if (!string.IsNullOrWhiteSpace(QueryWhereClause))
            {
                query += $"\nWHERE {QueryWhereClause.Trim()}";
            }

            // Add ORDER BY clause if provided (kept as-is but pre-validated for dangerous tokens)
            if (!string.IsNullOrWhiteSpace(QueryOrderBy))
            {
                query += $"\nORDER BY {QueryOrderBy.Trim()}";
            }

            GeneratedQuery = query;

            // Update the selected data source query
            if (SelectedDataSource != null)
            {
                SelectedDataSource.Query = query;
                TestResult = "✅ Query generated and applied to current data source";
            }
            else
            {
                TestResult = "✅ Query generated (no data source selected to apply)";
            }

            _logger.Information("Generated SQL query for table: {TableName}", QueryTableName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to generate query");
            GeneratedQuery = $"-- Error: {ex.Message}";
            TestResult = $"❌ Error generating query: {ex.Message}";
        }
    }

    [RelayCommand]
    private void LoadQueryFromDataSource()
    {
        if (SelectedDataSource == null || string.IsNullOrWhiteSpace(SelectedDataSource.Query))
        {
            TestResult = "No query to load from selected data source";
            return;
        }

        // Try to parse the query (basic parsing)
        var query = SelectedDataSource.Query.Trim();
        GeneratedQuery = query;

        // Basic parsing - this is a simplified version
        // In a production system, you'd use a proper SQL parser
        var upperQuery = query.ToUpperInvariant();

        try
        {
            // Extract table name (simplified)
            var fromIndex = upperQuery.IndexOf("FROM");
            if (fromIndex > 0)
            {
                var afterFrom = query.Substring(fromIndex + 4).Trim();
                var tableName = afterFrom.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0];
                QueryTableName = tableName;
            }

            TestResult = "Query loaded into builder (basic parsing)";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to parse query");
            TestResult = $"Query loaded but parsing failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadColumnsForTable()
    {
        AvailableColumns.Clear();

        if (SelectedDataSource == null)
        {
            TestResult = "Please select a data source first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(QueryTableName))
        {
            TestResult = "Please enter a table name to load columns.";
            return;
        }
        if (string.IsNullOrWhiteSpace(SelectedDataSource.ConnectionString))
        {
            TestResult = "Connection string is empty.";
            return;
        }

        IsLoadingColumns = true;
        try
        {
            List<string> cols;
            if (_dataService is ISqlDataService sql)
            {
                cols = await sql.GetColumnsAsync(SelectedDataSource.ConnectionString, QueryTableName);
            }
            else
            {
                // Fallback: build temporary query for provider that only supports DataSource-based discovery
                var temp = new DataSource
                {
                    ConnectionString = SelectedDataSource.ConnectionString,
                    Query = $"SELECT * FROM {QueryTableName}",
                    Type = DataSourceType.SQL
                };
                cols = await _dataService.GetColumnsAsync(temp);
            }

            foreach (var c in cols)
            {
                if (!string.IsNullOrWhiteSpace(c))
                {
                    AvailableColumns.Add(c);
                }
            }

            TestResult = cols.Count > 0
                ? $"✅ Loaded {cols.Count} columns from '{QueryTableName}'."
                : $"ℹ️ No columns found for '{QueryTableName}'.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load columns for table {Table}", QueryTableName);
            TestResult = $"❌ Error loading columns: {ex.Message}";
        }
        finally
        {
            IsLoadingColumns = false;
        }
    }

    [RelayCommand]
    private void AddSelectedColumn()
    {
        if (string.IsNullOrWhiteSpace(SelectedAvailableColumn))
        {
            return;
        }

        var current = (QueryColumns ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(current) || current == "*")
        {
            QueryColumns = SelectedAvailableColumn;
            return;
        }

        // Avoid duplicates
        var parts = current.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!parts.Contains(SelectedAvailableColumn, StringComparer.OrdinalIgnoreCase))
        {
            parts.Add(SelectedAvailableColumn);
            QueryColumns = string.Join(", ", parts);
        }
    }

    [RelayCommand]
    private void AddAllColumns()
    {
        if (AvailableColumns.Count == 0)
        {
            return;
        }
        QueryColumns = string.Join(", ", AvailableColumns);
    }
}
