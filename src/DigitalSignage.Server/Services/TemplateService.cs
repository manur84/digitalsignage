using DigitalSignage.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for processing templates with variable replacement
/// Uses Scriban template engine for advanced templating features
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly ILogger<TemplateService> _logger;
    private readonly TemplateContext _defaultContext;

    public TemplateService(ILogger<TemplateService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure default template context
        _defaultContext = new TemplateContext
        {
            // Enable member renaming (e.g., snake_case to PascalCase)
            MemberRenamer = member => member.Name,
            // Enable strict mode for better error messages
            StrictVariables = false // Allow undefined variables
        };

        // Register custom functions if needed
        var scriptObject = new ScriptObject();

        // Add utility functions
        scriptObject.Import(typeof(TemplateFunctions));

        _defaultContext.PushGlobal(scriptObject);
    }

    public async Task<string> ProcessTemplateAsync(
        string templateString,
        Dictionary<string, object> data,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateString))
        {
            _logger.LogWarning("ProcessTemplateAsync called with empty template string");
            return string.Empty;
        }

        if (data == null)
        {
            _logger.LogWarning("ProcessTemplateAsync called with null data, using empty dictionary");
            data = new Dictionary<string, object>();
        }

        try
        {
            _logger.LogDebug("Processing template with {DataCount} variables", data.Count);

            // Parse template
            var template = Template.Parse(templateString);

            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages);
                _logger.LogError("Template parsing errors: {Errors}", errors);
                return templateString; // Return original on error
            }

            // Create context for this render
            var context = new TemplateContext(_defaultContext);

            // Add data to context
            var scriptObject = new ScriptObject();
            foreach (var kvp in data)
            {
                if (kvp.Key != null)
                {
                    scriptObject[kvp.Key] = kvp.Value;
                }
            }
            context.PushGlobal(scriptObject);

            // Render template
            var result = await template.RenderAsync(context);

            _logger.LogDebug("Template processed successfully, result length: {Length}", result.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process template");
            return templateString; // Return original on error
        }
    }

    public async Task<Dictionary<string, string>> ProcessTemplatesAsync(
        Dictionary<string, string> templates,
        Dictionary<string, object> data,
        CancellationToken cancellationToken = default)
    {
        if (templates == null || templates.Count == 0)
        {
            _logger.LogWarning("ProcessTemplatesAsync called with empty templates");
            return new Dictionary<string, string>();
        }

        var results = new Dictionary<string, string>();

        foreach (var kvp in templates)
        {
            var processed = await ProcessTemplateAsync(kvp.Value, data, cancellationToken);
            results[kvp.Key] = processed;
        }

        _logger.LogInformation("Processed {Count} templates", results.Count);

        return results;
    }

    public bool ValidateTemplate(string templateString)
    {
        if (string.IsNullOrWhiteSpace(templateString))
        {
            return false;
        }

        try
        {
            var template = Template.Parse(templateString);

            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages);
                _logger.LogWarning("Template validation failed: {Errors}", errors);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during template validation");
            return false;
        }
    }
}

/// <summary>
/// Custom functions available in templates
/// </summary>
public static class TemplateFunctions
{
    /// <summary>
    /// Format a date with the specified format string
    /// Usage: {{ date_format my_date "dd.MM.yyyy" }}
    /// </summary>
    public static string DateFormat(object value, string format)
    {
        if (value == null) return string.Empty;

        if (value is DateTime dateTime)
        {
            return dateTime.ToString(format);
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString(format);
        }

        if (DateTime.TryParse(value.ToString(), out var parsedDate))
        {
            return parsedDate.ToString(format);
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Format a number with the specified format string
    /// Usage: {{ number_format my_number "N2" }}
    /// </summary>
    public static string NumberFormat(object value, string format)
    {
        if (value == null) return string.Empty;

        if (value is int || value is long || value is decimal || value is double || value is float)
        {
            return string.Format($"{{0:{format}}}", value);
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Convert value to uppercase
    /// Usage: {{ upper my_string }}
    /// </summary>
    public static string Upper(string value)
    {
        return value?.ToUpper() ?? string.Empty;
    }

    /// <summary>
    /// Convert value to lowercase
    /// Usage: {{ lower my_string }}
    /// </summary>
    public static string Lower(string value)
    {
        return value?.ToLower() ?? string.Empty;
    }

    /// <summary>
    /// Get default value if the input is null or empty
    /// Usage: {{ default my_value "Default Text" }}
    /// </summary>
    public static object Default(object value, object defaultValue)
    {
        if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
        {
            return defaultValue;
        }
        return value;
    }
}
