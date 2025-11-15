using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the New Layout Dialog
/// </summary>
public partial class NewLayoutViewModel : ObservableObject
{
    private readonly ILogger<NewLayoutViewModel> _logger;

    [ObservableProperty]
    private string _layoutName = "New Layout";

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _backgroundColor = "#FFFFFF";

    [ObservableProperty]
    private string? _category;

    [ObservableProperty]
    private string _tags = string.Empty;

    [ObservableProperty]
    private ResolutionOption? _selectedResolution;

    public ObservableCollection<ResolutionOption> AvailableResolutions { get; } = new();

    public ObservableCollection<string> PredefinedCategories { get; } = new()
    {
        "Marketing",
        "Operations",
        "Emergency",
        "Information",
        "Wayfinding",
        "Menu",
        "Welcome"
    };

    /// <summary>
    /// Event raised when the dialog should close
    /// </summary>
    public event EventHandler<bool>? CloseRequested;

    public NewLayoutViewModel(ILogger<NewLayoutViewModel> logger)
    {
        _logger = logger;

        // Populate available resolutions
        AvailableResolutions.Add(new ResolutionOption("Full HD Landscape (1920x1080)", 1920, 1080));
        AvailableResolutions.Add(new ResolutionOption("HD Landscape (1280x720)", 1280, 720));
        AvailableResolutions.Add(new ResolutionOption("4K Landscape (3840x2160)", 3840, 2160));
        AvailableResolutions.Add(new ResolutionOption("WXGA Landscape (1280x800)", 1280, 800));
        AvailableResolutions.Add(new ResolutionOption("XGA (1024x768)", 1024, 768));
        AvailableResolutions.Add(new ResolutionOption("WSVGA Landscape (1024x600)", 1024, 600)); // Raspberry Pi 7" display
        AvailableResolutions.Add(new ResolutionOption("Full HD Portrait (1080x1920)", 1080, 1920));
        AvailableResolutions.Add(new ResolutionOption("HD Portrait (720x1280)", 720, 1280));
        AvailableResolutions.Add(new ResolutionOption("4K Portrait (2160x3840)", 2160, 3840));

        // Default to Full HD Landscape
        SelectedResolution = AvailableResolutions[0];
    }

    /// <summary>
    /// Command to create the layout and close the dialog
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        if (SelectedResolution == null)
        {
            _logger.LogWarning("Create called with no resolution selected");
            return;
        }

        _logger.LogInformation("Creating new layout: {LayoutName} ({Width}x{Height})",
            LayoutName, SelectedResolution.Width, SelectedResolution.Height);

        // Close dialog with success
        CloseRequested?.Invoke(this, true);
    }

    private bool CanCreate()
    {
        return !string.IsNullOrWhiteSpace(LayoutName) && SelectedResolution != null;
    }

    /// <summary>
    /// Notify that layout name changed to update CanExecute
    /// </summary>
    partial void OnLayoutNameChanged(string value)
    {
        CreateCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Notify that resolution changed to update CanExecute
    /// </summary>
    partial void OnSelectedResolutionChanged(ResolutionOption? value)
    {
        CreateCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Command to cancel and close the dialog
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _logger.LogInformation("New layout creation cancelled");
        CloseRequested?.Invoke(this, false);
    }
}

/// <summary>
/// Helper class for resolution selection
/// </summary>
public class ResolutionOption
{
    public string DisplayName { get; }
    public int Width { get; }
    public int Height { get; }

    public ResolutionOption(string displayName, int width, int height)
    {
        DisplayName = displayName;
        Width = width;
        Height = height;
    }

    public override string ToString() => DisplayName;
}
