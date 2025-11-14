using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Template Manager Window
/// Provides CRUD operations for layout templates
/// </summary>
public partial class TemplateManagerViewModel : ObservableObject
{
    private readonly IDbContextFactory<DigitalSignageDbContext> _contextFactory;
    private readonly ITemplateService _templateService;
    private readonly ILogger<TemplateManagerViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<LayoutTemplate> _templates = new();

    [ObservableProperty]
    private LayoutTemplate? _selectedTemplate;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string _editTemplateName = string.Empty;

    [ObservableProperty]
    private string _editTemplateDescription = string.Empty;

    [ObservableProperty]
    private LayoutTemplateCategory _editTemplateCategory;

    [ObservableProperty]
    private string _editTemplateContent = string.Empty;

    [ObservableProperty]
    private int _editTemplateWidth = 1920;

    [ObservableProperty]
    private int _editTemplateHeight = 1080;

    [ObservableProperty]
    private string _editTemplateBackgroundColor = "#FFFFFF";

    [ObservableProperty]
    private bool _validationErrors;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private string _previewContent = string.Empty;

    public ObservableCollection<LayoutTemplateCategory> AvailableCategories { get; }

    /// <summary>
    /// Event raised when the dialog should close
    /// </summary>
    public event EventHandler? CloseRequested;

    public TemplateManagerViewModel(
        IDbContextFactory<DigitalSignageDbContext> contextFactory,
        ITemplateService templateService,
        ILogger<TemplateManagerViewModel> logger)
    {
        _contextFactory = contextFactory;
        _templateService = templateService;
        _logger = logger;

        // Populate available categories
        AvailableCategories = new ObservableCollection<LayoutTemplateCategory>(
            Enum.GetValues<LayoutTemplateCategory>());

        // Load templates on initialization
        _ = LoadTemplatesAsync();
    }

    /// <summary>
    /// Load all templates from the database
    /// </summary>
    [RelayCommand]
    private async Task LoadTemplatesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading templates...";

        try
        {
            _logger.LogInformation("Loading layout templates from database");

            await using var context = await _contextFactory.CreateDbContextAsync();
            var templates = await context.LayoutTemplates
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
    /// Create a new template
    /// </summary>
    [RelayCommand]
    private void CreateNewTemplate()
    {
        _logger.LogInformation("Creating new template");

        // Reset edit fields
        SelectedTemplate = null;
        EditTemplateName = string.Empty;
        EditTemplateDescription = string.Empty;
        EditTemplateCategory = LayoutTemplateCategory.Custom;
        EditTemplateContent = "[]"; // Empty elements array
        EditTemplateWidth = 1920;
        EditTemplateHeight = 1080;
        EditTemplateBackgroundColor = "#FFFFFF";
        ValidationErrors = false;
        ValidationMessage = string.Empty;
        PreviewContent = string.Empty;

        IsEditMode = true;
        StatusMessage = "Creating new template";
    }

    /// <summary>
    /// Edit the selected template
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private void EditTemplate()
    {
        if (SelectedTemplate == null)
        {
            _logger.LogWarning("EditTemplate called with no template selected");
            return;
        }

        if (SelectedTemplate.IsBuiltIn)
        {
            _logger.LogWarning("Cannot edit built-in template: {TemplateName}", SelectedTemplate.Name);
            StatusMessage = "Built-in templates cannot be edited. Create a copy instead.";
            return;
        }

        _logger.LogInformation("Editing template: {TemplateName}", SelectedTemplate.Name);

        // Load template data into edit fields
        EditTemplateName = SelectedTemplate.Name;
        EditTemplateDescription = SelectedTemplate.Description;
        EditTemplateCategory = SelectedTemplate.Category;
        EditTemplateContent = SelectedTemplate.ElementsJson;
        EditTemplateWidth = SelectedTemplate.Resolution.Width;
        EditTemplateHeight = SelectedTemplate.Resolution.Height;
        EditTemplateBackgroundColor = SelectedTemplate.BackgroundColor ?? "#FFFFFF";
        ValidationErrors = false;
        ValidationMessage = string.Empty;
        PreviewContent = string.Empty;

        IsEditMode = true;
        StatusMessage = $"Editing template: {SelectedTemplate.Name}";
    }

    /// <summary>
    /// Save the current template (create or update)
    /// </summary>
    [RelayCommand]
    private async Task SaveTemplateAsync()
    {
        if (!ValidateTemplate())
        {
            return;
        }

        IsLoading = true;
        StatusMessage = "Saving template...";

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            if (SelectedTemplate == null)
            {
                // Create new template
                var newTemplate = new LayoutTemplate
                {
                    Name = EditTemplateName,
                    Description = EditTemplateDescription,
                    Category = EditTemplateCategory,
                    ElementsJson = EditTemplateContent,
                    Resolution = new Core.Models.Resolution
                    {
                        Width = EditTemplateWidth,
                        Height = EditTemplateHeight
                    },
                    BackgroundColor = EditTemplateBackgroundColor,
                    IsBuiltIn = false,
                    IsPublic = true,
                    CreatedAt = DateTime.UtcNow,
                    UsageCount = 0
                };

                context.LayoutTemplates.Add(newTemplate);
                _logger.LogInformation("Creating new template: {TemplateName}", newTemplate.Name);
            }
            else
            {
                // Update existing template
                var templateToUpdate = await context.LayoutTemplates
                    .FirstOrDefaultAsync(t => t.Id == SelectedTemplate.Id);

                if (templateToUpdate == null)
                {
                    StatusMessage = "Template not found in database";
                    _logger.LogError("Template {TemplateId} not found for update", SelectedTemplate.Id);
                    return;
                }

                templateToUpdate.Name = EditTemplateName;
                templateToUpdate.Description = EditTemplateDescription;
                templateToUpdate.Category = EditTemplateCategory;
                templateToUpdate.ElementsJson = EditTemplateContent;
                templateToUpdate.Resolution = new Core.Models.Resolution
                {
                    Width = EditTemplateWidth,
                    Height = EditTemplateHeight
                };
                templateToUpdate.BackgroundColor = EditTemplateBackgroundColor;

                _logger.LogInformation("Updating template: {TemplateName}", templateToUpdate.Name);
            }

            await context.SaveChangesAsync();
            StatusMessage = "Template saved successfully";

            // Reload templates
            await LoadTemplatesAsync();

            // Exit edit mode
            IsEditMode = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save template");
            StatusMessage = $"Error saving template: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Delete the selected template
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    private async Task DeleteTemplateAsync()
    {
        if (SelectedTemplate == null)
        {
            _logger.LogWarning("DeleteTemplate called with no template selected");
            return;
        }

        if (SelectedTemplate.IsBuiltIn)
        {
            _logger.LogWarning("Cannot delete built-in template: {TemplateName}", SelectedTemplate.Name);
            StatusMessage = "Built-in templates cannot be deleted";
            MessageBox.Show(
                "Built-in templates cannot be deleted.",
                "Cannot Delete",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete the template '{SelectedTemplate.Name}'?\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = "Deleting template...";

        try
        {
            _logger.LogInformation("Deleting template: {TemplateName} (ID: {TemplateId})",
                SelectedTemplate.Name, SelectedTemplate.Id);

            await using var context = await _contextFactory.CreateDbContextAsync();
            var templateToDelete = await context.LayoutTemplates
                .FirstOrDefaultAsync(t => t.Id == SelectedTemplate.Id);

            if (templateToDelete != null)
            {
                context.LayoutTemplates.Remove(templateToDelete);
                await context.SaveChangesAsync();

                _logger.LogInformation("Template deleted successfully: {TemplateName}", templateToDelete.Name);
                StatusMessage = "Template deleted successfully";

                // Reload templates
                await LoadTemplatesAsync();

                // Clear selection
                SelectedTemplate = null;
            }
            else
            {
                StatusMessage = "Template not found in database";
                _logger.LogWarning("Template {TemplateId} not found for deletion", SelectedTemplate.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete template");
            StatusMessage = $"Error deleting template: {ex.Message}";
            MessageBox.Show(
                $"Failed to delete template: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Duplicate the selected template
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDuplicate))]
    private async Task DuplicateTemplateAsync()
    {
        if (SelectedTemplate == null)
        {
            _logger.LogWarning("DuplicateTemplate called with no template selected");
            return;
        }

        IsLoading = true;
        StatusMessage = "Duplicating template...";

        try
        {
            _logger.LogInformation("Duplicating template: {TemplateName}", SelectedTemplate.Name);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var duplicate = new LayoutTemplate
            {
                Name = $"{SelectedTemplate.Name} (Copy)",
                Description = SelectedTemplate.Description,
                Category = SelectedTemplate.Category,
                ElementsJson = SelectedTemplate.ElementsJson,
                Resolution = new Core.Models.Resolution
                {
                    Width = SelectedTemplate.Resolution.Width,
                    Height = SelectedTemplate.Resolution.Height
                },
                BackgroundColor = SelectedTemplate.BackgroundColor,
                BackgroundImage = SelectedTemplate.BackgroundImage,
                IsBuiltIn = false, // Duplicates are never built-in
                IsPublic = SelectedTemplate.IsPublic,
                CreatedAt = DateTime.UtcNow,
                UsageCount = 0
            };

            context.LayoutTemplates.Add(duplicate);
            await context.SaveChangesAsync();

            _logger.LogInformation("Template duplicated successfully: {TemplateName}", duplicate.Name);
            StatusMessage = "Template duplicated successfully";

            // Reload templates
            await LoadTemplatesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to duplicate template");
            StatusMessage = $"Error duplicating template: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Cancel editing
    /// </summary>
    [RelayCommand]
    private void CancelEdit()
    {
        _logger.LogInformation("Cancelling template edit");
        IsEditMode = false;
        StatusMessage = "Edit cancelled";
    }

    /// <summary>
    /// Validate the template content using TemplateService
    /// </summary>
    [RelayCommand]
    private void ValidateTemplateContent()
    {
        if (string.IsNullOrWhiteSpace(EditTemplateContent))
        {
            ValidationErrors = true;
            ValidationMessage = "Template content cannot be empty";
            _logger.LogWarning("Template validation failed: Empty content");
            return;
        }

        try
        {
            // Validate JSON structure
            System.Text.Json.JsonDocument.Parse(EditTemplateContent);

            ValidationErrors = false;
            ValidationMessage = "Template content is valid JSON";
            StatusMessage = "Template validated successfully";
            _logger.LogInformation("Template validation successful");
        }
        catch (System.Text.Json.JsonException ex)
        {
            ValidationErrors = true;
            ValidationMessage = $"Invalid JSON: {ex.Message}";
            StatusMessage = "Template validation failed";
            _logger.LogWarning("Template validation failed: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Generate a preview of the template
    /// </summary>
    [RelayCommand]
    private async Task GeneratePreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(EditTemplateContent))
        {
            PreviewContent = "No template content to preview";
            return;
        }

        try
        {
            // Parse JSON to format it nicely
            var jsonDoc = System.Text.Json.JsonDocument.Parse(EditTemplateContent);
            var formatted = System.Text.Json.JsonSerializer.Serialize(
                jsonDoc,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            PreviewContent = $"Template: {EditTemplateName}\n" +
                           $"Category: {EditTemplateCategory}\n" +
                           $"Resolution: {EditTemplateWidth}x{EditTemplateHeight}\n" +
                           $"Background: {EditTemplateBackgroundColor}\n\n" +
                           $"Elements JSON:\n{formatted}";

            StatusMessage = "Preview generated";
            _logger.LogInformation("Template preview generated");
        }
        catch (Exception ex)
        {
            PreviewContent = $"Error generating preview: {ex.Message}";
            _logger.LogError(ex, "Failed to generate template preview");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Close the window
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        _logger.LogInformation("Closing Template Manager");
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Validate template before saving
    /// </summary>
    private bool ValidateTemplate()
    {
        if (string.IsNullOrWhiteSpace(EditTemplateName))
        {
            ValidationErrors = true;
            ValidationMessage = "Template name is required";
            StatusMessage = "Validation failed: Name required";
            return false;
        }

        if (EditTemplateName.Length > 100)
        {
            ValidationErrors = true;
            ValidationMessage = "Template name must be 100 characters or less";
            StatusMessage = "Validation failed: Name too long";
            return false;
        }

        if (string.IsNullOrWhiteSpace(EditTemplateContent))
        {
            ValidationErrors = true;
            ValidationMessage = "Template content is required";
            StatusMessage = "Validation failed: Content required";
            return false;
        }

        try
        {
            // Validate JSON structure
            System.Text.Json.JsonDocument.Parse(EditTemplateContent);
        }
        catch (System.Text.Json.JsonException ex)
        {
            ValidationErrors = true;
            ValidationMessage = $"Invalid JSON: {ex.Message}";
            StatusMessage = "Validation failed: Invalid JSON";
            return false;
        }

        if (EditTemplateWidth <= 0 || EditTemplateHeight <= 0)
        {
            ValidationErrors = true;
            ValidationMessage = "Resolution must be greater than 0";
            StatusMessage = "Validation failed: Invalid resolution";
            return false;
        }

        ValidationErrors = false;
        ValidationMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Check if edit or delete can be executed
    /// </summary>
    private bool CanEditOrDelete()
    {
        return SelectedTemplate != null && !SelectedTemplate.IsBuiltIn;
    }

    /// <summary>
    /// Check if duplicate can be executed
    /// </summary>
    private bool CanDuplicate()
    {
        return SelectedTemplate != null;
    }

    partial void OnSelectedTemplateChanged(LayoutTemplate? value)
    {
        // Update command can execute states
        EditTemplateCommand.NotifyCanExecuteChanged();
        DeleteTemplateCommand.NotifyCanExecuteChanged();
        DuplicateTemplateCommand.NotifyCanExecuteChanged();
    }
}
