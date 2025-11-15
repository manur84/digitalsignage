namespace DigitalSignage.Core.Interfaces;

/// <summary>
/// Service for processing Scriban templates with variable replacement
/// </summary>
public interface IScribanService
{
    /// <summary>
    /// Process a template string with provided data
    /// </summary>
    /// <param name="templateString">Template string containing variables like {{VariableName}}</param>
    /// <param name="data">Dictionary of variable values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processed string with variables replaced</returns>
    Task<string> ProcessTemplateAsync(
        string templateString,
        Dictionary<string, object> data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process multiple template strings with the same data
    /// </summary>
    /// <param name="templates">Dictionary of template strings</param>
    /// <param name="data">Dictionary of variable values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary with processed strings</returns>
    Task<Dictionary<string, string>> ProcessTemplatesAsync(
        Dictionary<string, string> templates,
        Dictionary<string, object> data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate a template string for syntax errors
    /// </summary>
    /// <param name="templateString">Template string to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool ValidateTemplate(string templateString);
}