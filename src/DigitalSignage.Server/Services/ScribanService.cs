using DigitalSignage.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Scriban;
using System.Collections.Concurrent;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for processing Scriban templates with variable replacement
/// </summary>
public class ScribanService : IScribanService
{
    private readonly ILogger<ScribanService> _logger;
    private readonly ConcurrentDictionary<string, Template> _templateCache = new();

    public ScribanService(ILogger<ScribanService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Process a template string with provided data
    /// </summary>
    public async Task<string> ProcessTemplateAsync(
        string templateString,
        Dictionary<string, object> data,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateString))
        {
            return templateString;
        }

        try
        {
            // Check if template contains any variables
            if (!templateString.Contains("{{"))
            {
                return templateString;
            }

            // Try to get cached template or parse new one
            var template = _templateCache.GetOrAdd(templateString, ts =>
            {
                var parsed = Template.Parse(ts);
                if (parsed.HasErrors)
                {
                    _logger.LogWarning("Template parsing errors: {Errors}",
                        string.Join(", ", parsed.Messages));
                }
                return parsed;
            });

            // Process template with data
            var context = new Scriban.Runtime.ScriptObject();
            if (data != null)
            {
                foreach (var kvp in data)
                {
                    context.SetValue(kvp.Key, kvp.Value, false);
                }
            }

            var templateContext = new Scriban.TemplateContext();
            templateContext.PushGlobal(context);

            var result = await template.RenderAsync(templateContext);

            _logger.LogDebug("Successfully processed template with {DataCount} variables", data?.Count ?? 0);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process template: {Template}", templateString);
            // Return original string if processing fails
            return templateString;
        }
    }

    /// <summary>
    /// Process multiple template strings with the same data
    /// </summary>
    public async Task<Dictionary<string, string>> ProcessTemplatesAsync(
        Dictionary<string, string> templates,
        Dictionary<string, object> data,
        CancellationToken cancellationToken = default)
    {
        if (templates == null || templates.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var results = new Dictionary<string, string>();

        foreach (var kvp in templates)
        {
            results[kvp.Key] = await ProcessTemplateAsync(kvp.Value, data, cancellationToken);
        }

        return results;
    }

    /// <summary>
    /// Validate a template string for syntax errors
    /// </summary>
    public bool ValidateTemplate(string templateString)
    {
        if (string.IsNullOrWhiteSpace(templateString))
        {
            return true;
        }

        try
        {
            var template = Template.Parse(templateString);
            return !template.HasErrors;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Template validation failed for: {Template}", templateString);
            return false;
        }
    }
}