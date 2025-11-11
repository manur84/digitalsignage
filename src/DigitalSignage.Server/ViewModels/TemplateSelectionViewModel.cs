using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Template Selection Dialog
/// </summary>
public partial class TemplateSelectionViewModel : ObservableObject
{
    private readonly DigitalSignageDbContext _dbContext;
    private readonly ILogger<TemplateSelectionViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<LayoutTemplate> _templates = new();

    [ObservableProperty]
    private LayoutTemplate? _selectedTemplate;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Event raised when the dialog should close
    /// </summary>
    public event EventHandler<bool>? CloseRequested;

    public TemplateSelectionViewModel(
        DigitalSignageDbContext dbContext,
        ILogger<TemplateSelectionViewModel> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        // Load templates when constructed
        _ = LoadTemplatesAsync();
    }

    /// <summary>
    /// Load all available templates from the database
    /// </summary>
    private async Task LoadTemplatesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading templates...";

        try
        {
            _logger.LogInformation("Loading layout templates from database");

            // Query templates ordered by category and usage count
            var templates = await _dbContext.LayoutTemplates
                .OrderBy(t => t.Category)
                .ThenByDescending(t => t.UsageCount)
                .ThenBy(t => t.Name)
                .ToListAsync();

            _logger.LogInformation("Loaded {Count} templates", templates.Count);

            Templates.Clear();
            foreach (var template in templates)
            {
                Templates.Add(template);
            }

            StatusMessage = $"Loaded {templates.Count} templates";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load templates");
            StatusMessage = $"Error loading templates: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Command to select a template and close the dialog
    /// </summary>
    [RelayCommand]
    private async Task SelectTemplate(LayoutTemplate? template)
    {
        if (template == null)
        {
            _logger.LogWarning("SelectTemplate called with null template");
            return;
        }

        try
        {
            _logger.LogInformation("Template selected: {TemplateName} (ID: {TemplateId})",
                template.Name, template.Id);

            SelectedTemplate = template;

            // Update usage statistics
            template.LastUsedAt = DateTime.UtcNow;
            template.UsageCount++;

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Updated usage statistics for template {TemplateName}", template.Name);

            // Close dialog with success
            CloseRequested?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select template {TemplateName}", template.Name);
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Command to cancel and close the dialog
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _logger.LogInformation("Template selection cancelled");
        SelectedTemplate = null;
        CloseRequested?.Invoke(this, false);
    }

    /// <summary>
    /// Command to refresh the template list
    /// </summary>
    [RelayCommand]
    private async Task Refresh()
    {
        _logger.LogInformation("Refreshing template list");
        await LoadTemplatesAsync();
    }
}
