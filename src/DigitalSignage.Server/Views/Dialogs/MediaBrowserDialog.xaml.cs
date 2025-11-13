using System.Windows;
using System.Windows.Input;
using DigitalSignage.Server.ViewModels;
using DigitalSignage.Data.Entities;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Media Browser Dialog for selecting media files
/// </summary>
public partial class MediaBrowserDialog : Window
{
    private readonly ILogger<MediaBrowserDialog> _logger;

    /// <summary>
    /// Gets the selected media file
    /// </summary>
    public MediaFile? SelectedMedia { get; private set; }

    /// <summary>
    /// Initializes a new instance of the MediaBrowserDialog
    /// </summary>
    public MediaBrowserDialog(MediaBrowserViewModel viewModel, ILogger<MediaBrowserDialog> logger)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Loaded += async (s, e) =>
        {
            try
            {
                _logger.LogInformation("Loading media files for browser dialog...");
                await ((MediaBrowserViewModel)DataContext).LoadMediaAsync();
                _logger.LogInformation("Media files loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load media files");
                MessageBox.Show(
                    $"Failed to load media files:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        };

        _logger.LogDebug("MediaBrowserDialog initialized");
    }

    /// <summary>
    /// Handle Select button click
    /// </summary>
    private void Select_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MediaBrowserViewModel)DataContext;
        if (vm.SelectedMedia != null)
        {
            SelectedMedia = vm.SelectedMedia;
            _logger.LogInformation("Media file selected: {FileName}", SelectedMedia.OriginalFileName);
            DialogResult = true;
            Close();
        }
        else
        {
            _logger.LogWarning("Select clicked but no media file selected");
            MessageBox.Show(
                "Please select a media file first.",
                "No Selection",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// Handle Cancel button click
    /// </summary>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Media browser dialog cancelled");
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Handle DataGrid double-click to select media
    /// </summary>
    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var vm = (MediaBrowserViewModel)DataContext;
        if (vm.SelectedMedia != null)
        {
            _logger.LogInformation("Media file selected via double-click: {FileName}", vm.SelectedMedia.OriginalFileName);
            Select_Click(sender, e);
        }
    }
}
