using System.Windows;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Server.ViewModels;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Settings Dialog for application configuration
/// </summary>
public partial class SettingsDialog : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly ILogger<SettingsDialog> _logger;
    private readonly IDialogService _dialogService;
    private bool _isClosing = false;

    /// <summary>
    /// Constructor for Settings Dialog
    /// </summary>
    public SettingsDialog(SettingsViewModel viewModel, IDialogService dialogService, ILogger<SettingsDialog> logger)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DataContext = _viewModel;

        _logger.LogInformation("Settings dialog opened");

        // Handle window closing event to check for unsaved changes
        Closing += OnWindowClosing;

        // Subscribe to property changes to auto-close on successful save
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.HasUnsavedChanges))
            {
                // Check if save was successful (no unsaved changes and success message)
                if (!_viewModel.HasUnsavedChanges && _viewModel.StatusMessage.Contains("successfully"))
                {
                    // Dialog result will be set when user clicks OK on success message
                }
            }
        };
    }

    /// <summary>
    /// Handle window closing event to check for unsaved changes
    /// </summary>
    private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Prevent re-entry if already processing a close request
        if (_isClosing)
        {
            return;
        }

        // Cancel the close first, then check if we can actually close
        e.Cancel = true;
        _isClosing = true;

        try
        {
            if (await _viewModel.CanClose())
            {
                _logger.LogInformation("Settings dialog closed");
                // Remove the event handler to avoid recursion
                Closing -= OnWindowClosing;
                // Set DialogResult to trigger close (safer than calling Close())
                DialogResult = true;
            }
            else
            {
                _logger.LogInformation("Settings dialog close cancelled due to unsaved changes");
                _isClosing = false; // Reset flag so user can try again
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during window closing");
            _isClosing = false;
            throw;
        }
    }

    /// <summary>
    /// Handle Cancel button click
    /// </summary>
    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.HasUnsavedChanges)
        {
            var result = await _dialogService.ShowConfirmationAsync(
                "You have unsaved changes. Are you sure you want to discard them?",
                "Unsaved Changes");

            if (!result)
            {
                return;
            }
        }

        _logger.LogInformation("Settings dialog cancelled");
        // Remove event handler before closing
        Closing -= OnWindowClosing;
        DialogResult = false;
    }
}
