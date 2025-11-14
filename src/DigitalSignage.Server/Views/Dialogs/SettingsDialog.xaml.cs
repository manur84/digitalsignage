using System.Windows;
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

    /// <summary>
    /// Constructor for Settings Dialog
    /// </summary>
    public SettingsDialog(SettingsViewModel viewModel, ILogger<SettingsDialog> logger)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
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
    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_viewModel.CanClose())
        {
            e.Cancel = true;
            _logger.LogInformation("Settings dialog close cancelled due to unsaved changes");
        }
        else
        {
            _logger.LogInformation("Settings dialog closed");
        }
    }

    /// <summary>
    /// Handle Cancel button click
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Are you sure you want to discard them?",
                "Unsaved Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                return;
            }
        }

        _logger.LogInformation("Settings dialog cancelled");
        DialogResult = false;
        Close();
    }
}
