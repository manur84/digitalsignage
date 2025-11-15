using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Variable Browser dialog showing available template variables
/// </summary>
public partial class VariableBrowserViewModel : ObservableObject
{
    private readonly ILogger<VariableBrowserViewModel> _logger;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private TemplateVariable? _selectedVariable;

    [ObservableProperty]
    private string _variableSyntax = string.Empty;

    public ObservableCollection<TemplateVariable> Variables { get; } = new();
    public ObservableCollection<TemplateVariable> FilteredVariables { get; } = new();

    public VariableBrowserViewModel(ILogger<VariableBrowserViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes the variable browser with available variables from layout and data sources
    /// </summary>
    public void Initialize(DisplayLayout? layout, IEnumerable<DataSource>? dataSources)
    {
        Variables.Clear();

        // Add system variables
        AddSystemVariables();

        // Add data source variables
        if (dataSources != null)
        {
            AddDataSourceVariables(dataSources);
        }

        // Add layout element variables
        if (layout != null)
        {
            AddLayoutElementVariables(layout);
        }

        // Initialize filtered list
        UpdateFilteredVariables();

        _logger.LogInformation("Variable browser initialized with {Count} variables", Variables.Count);
    }

    /// <summary>
    /// Adds system variables (date, time, etc.)
    /// </summary>
    private void AddSystemVariables()
    {
        Variables.Add(new TemplateVariable
        {
            Name = "date",
            Category = "System",
            Description = "Current date",
            Example = "{{ date }}",
            Type = "DateTime",
            Value = DateTime.Now.ToShortDateString()
        });

        Variables.Add(new TemplateVariable
        {
            Name = "time",
            Category = "System",
            Description = "Current time",
            Example = "{{ time }}",
            Type = "DateTime",
            Value = DateTime.Now.ToShortTimeString()
        });

        Variables.Add(new TemplateVariable
        {
            Name = "datetime",
            Category = "System",
            Description = "Current date and time",
            Example = "{{ datetime }}",
            Type = "DateTime",
            Value = DateTime.Now.ToString()
        });

        Variables.Add(new TemplateVariable
        {
            Name = "now",
            Category = "System",
            Description = "Current timestamp",
            Example = "{{ now }}",
            Type = "DateTime",
            Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });

        Variables.Add(new TemplateVariable
        {
            Name = "hostname",
            Category = "System",
            Description = "Client hostname",
            Example = "{{ hostname }}",
            Type = "String",
            Value = "pi-display-01"
        });

        Variables.Add(new TemplateVariable
        {
            Name = "ip_address",
            Category = "System",
            Description = "Client IP address",
            Example = "{{ ip_address }}",
            Type = "String",
            Value = "192.168.0.178"
        });
    }

    /// <summary>
    /// Adds variables from data sources
    /// </summary>
    private void AddDataSourceVariables(IEnumerable<DataSource> dataSources)
    {
        foreach (var dataSource in dataSources)
        {
            // SQL data sources
            if (dataSource.Type == DataSourceType.SQL)
            {
                Variables.Add(new TemplateVariable
                {
                    Name = $"data.{dataSource.Name}",
                    Category = "Data Source",
                    Description = $"SQL query results from '{dataSource.Name}'",
                    Example = $"{{{{ for row in data.{dataSource.Name} }}}}\n  {{{{ row.ColumnName }}}}\n{{{{ end }}}}",
                    Type = "Array",
                    Value = "(SQL Data)"
                });
            }
            // API data sources
            else if (dataSource.Type == DataSourceType.REST)
            {
                Variables.Add(new TemplateVariable
                {
                    Name = $"api.{dataSource.Name}",
                    Category = "Data Source",
                    Description = $"API response from '{dataSource.Name}'",
                    Example = $"{{{{ api.{dataSource.Name}.field_name }}}}",
                    Type = "Object",
                    Value = "(API Data)"
                });
            }
        }
    }

    /// <summary>
    /// Adds variables from layout elements
    /// </summary>
    private void AddLayoutElementVariables(DisplayLayout layout)
    {
        foreach (var element in layout.Elements)
        {
            if (element.Type == "text" || element.Type == "datetime")
            {
                var elementName = element.Name.Replace(" ", "_").ToLower();
                Variables.Add(new TemplateVariable
                {
                    Name = $"element.{elementName}",
                    Category = "Layout Element",
                    Description = $"Reference to element '{element.Name}'",
                    Example = $"{{{{ element.{elementName}.content }}}}",
                    Type = "Element",
                    Value = element.Name
                });
            }
        }
    }

    /// <summary>
    /// Updates the filtered variables based on search text
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        UpdateFilteredVariables();
    }

    /// <summary>
    /// Updates filtered variables list
    /// </summary>
    private void UpdateFilteredVariables()
    {
        FilteredVariables.Clear();

        var searchLower = SearchText?.ToLower() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(searchLower)
            ? Variables
            : Variables.Where(v =>
                v.Name.ToLower().Contains(searchLower) ||
                v.Category.ToLower().Contains(searchLower) ||
                v.Description.ToLower().Contains(searchLower));

        foreach (var variable in filtered.OrderBy(v => v.Category).ThenBy(v => v.Name))
        {
            FilteredVariables.Add(variable);
        }
    }

    /// <summary>
    /// Updates the variable syntax when selection changes
    /// </summary>
    partial void OnSelectedVariableChanged(TemplateVariable? value)
    {
        if (value != null)
        {
            VariableSyntax = value.Example;
        }
        else
        {
            VariableSyntax = string.Empty;
        }
    }

    /// <summary>
    /// Copies the selected variable syntax to clipboard
    /// </summary>
    [RelayCommand]
    private void CopyToClipboard()
    {
        if (SelectedVariable != null)
        {
            try
            {
                System.Windows.Clipboard.SetText(SelectedVariable.Example);
                _logger.LogInformation("Copied variable syntax to clipboard: {Syntax}", SelectedVariable.Example);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy to clipboard");
            }
        }
    }

    private bool CanCopyToClipboard() => SelectedVariable != null;

    /// <summary>
    /// Inserts the variable at cursor position (via dialog result)
    /// </summary>
    [RelayCommand]
    private void InsertVariable()
    {
        if (SelectedVariable != null)
        {
            _logger.LogInformation("Insert variable: {Name}", SelectedVariable.Name);
            // Dialog result will be handled by caller
        }
    }

    private bool CanInsertVariable() => SelectedVariable != null;
}

/// <summary>
/// Represents a template variable
/// </summary>
public partial class TemplateVariable : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _example = string.Empty;

    [ObservableProperty]
    private string _type = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}
