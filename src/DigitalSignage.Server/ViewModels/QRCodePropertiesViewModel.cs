using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for QR Code Properties Dialog - configures QR code elements
/// </summary>
public partial class QRCodePropertiesViewModel : ObservableObject
{
    private readonly ILogger<QRCodePropertiesViewModel> _logger;

    [ObservableProperty]
    private string _content = "https://example.com";

    [ObservableProperty]
    private string _foregroundColor = "#000000";

    [ObservableProperty]
    private string _backgroundColor = "#FFFFFF";

    [ObservableProperty]
    private string _errorCorrectionLevel = "M";

    [ObservableProperty]
    private string _alignment = "Center";

    /// <summary>
    /// Gets whether the dialog can be saved (content is not empty)
    /// </summary>
    public bool CanSave => !string.IsNullOrWhiteSpace(Content);

    /// <summary>
    /// Error correction level options for UI binding
    /// </summary>
    public string[] ErrorCorrectionLevels => new[] { "L", "M", "Q", "H" };

    /// <summary>
    /// Alignment options for UI binding
    /// </summary>
    public string[] AlignmentOptions => new[] { "Left", "Center", "Right" };

    /// <summary>
    /// Help text explaining error correction levels
    /// </summary>
    public string ErrorCorrectionHelp =>
        "Error Correction Level:\n" +
        "• L (Low): ~7% error correction\n" +
        "• M (Medium): ~15% error correction (recommended)\n" +
        "• Q (Quartile): ~25% error correction\n" +
        "• H (High): ~30% error correction\n\n" +
        "Higher levels allow QR code to be scanned even if partially damaged,\n" +
        "but result in a denser QR code pattern.";

    public QRCodePropertiesViewModel(ILogger<QRCodePropertiesViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Parameterless constructor for XAML designer
    /// </summary>
    public QRCodePropertiesViewModel() : this(
        Microsoft.Extensions.Logging.Abstractions.NullLogger<QRCodePropertiesViewModel>.Instance)
    {
    }

    /// <summary>
    /// Called when content changes - updates CanSave
    /// </summary>
    partial void OnContentChanged(string value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    /// <summary>
    /// Loads properties from an existing DisplayElement (for editing)
    /// </summary>
    public void LoadFromElement(DisplayElement element)
    {
        if (element == null || element.Type.ToLower() != "qrcode")
            return;

        try
        {
            // Load QR code properties - support both 'Content' and 'Data' property names
            Content = element.GetProperty<string>("Content", element.GetProperty<string>("Data", "https://example.com"));
            ForegroundColor = element.GetProperty<string>("ForegroundColor", "#000000");
            BackgroundColor = element.GetProperty<string>("BackgroundColor", "#FFFFFF");

            // Support both property names for error correction
            ErrorCorrectionLevel = element.GetProperty<string>("ErrorCorrectionLevel",
                element.GetProperty<string>("ErrorCorrection", "M"));

            Alignment = element.GetProperty<string>("Alignment", "Center");

            _logger.LogInformation("Loaded properties from existing QR code element");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load properties from element");
        }
    }

    /// <summary>
    /// Applies properties to a DisplayElement
    /// </summary>
    public void ApplyToElement(DisplayElement element)
    {
        if (element == null)
            return;

        try
        {
            element.Type = "qrcode";

            // Set QR code properties - use both property names for compatibility
            element.SetProperty("Content", Content);
            element.SetProperty("Data", Content);  // Legacy property name
            element.SetProperty("ForegroundColor", ForegroundColor);
            element.SetProperty("BackgroundColor", BackgroundColor);
            element.SetProperty("ErrorCorrectionLevel", ErrorCorrectionLevel);
            element.SetProperty("ErrorCorrection", ErrorCorrectionLevel);  // Legacy property name
            element.SetProperty("Alignment", Alignment);

            // Set name based on content (truncate if too long)
            var contentPreview = Content.Length > 30 ? Content.Substring(0, 27) + "..." : Content;
            element.Name = $"QR Code - {contentPreview}";

            _logger.LogInformation("Applied properties to QR code element");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply properties to element");
        }
    }
}
