using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for the Screenshot Window
/// </summary>
public partial class ScreenshotViewModel : ObservableObject
{
    private readonly ILogger<ScreenshotViewModel> _logger;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private BitmapImage? _screenshotImage;

    [ObservableProperty]
    private string _clientName = string.Empty;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;

    [ObservableProperty]
    private string _windowTitle = "Screenshot";

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Event raised when the window should close
    /// </summary>
    public event EventHandler? CloseRequested;

    public ScreenshotViewModel(ILogger<ScreenshotViewModel> logger, IDialogService dialogService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    /// <summary>
    /// Load screenshot from base64 encoded image data
    /// </summary>
    public void LoadScreenshot(string clientName, string base64ImageData)
    {
        try
        {
            _logger.LogInformation("=== LoadScreenshot START ===");
            _logger.LogInformation("Client: {ClientName}", clientName);
            _logger.LogInformation("Base64 data length: {Length} characters", base64ImageData?.Length ?? 0);

            if (string.IsNullOrEmpty(base64ImageData))
            {
                _logger.LogError("Base64 image data is null or empty!");
                StatusMessage = "Error: No image data received";
                return;
            }

            ClientName = clientName;
            Timestamp = DateTime.Now;
            WindowTitle = $"Screenshot - {ClientName} - {Timestamp:HH:mm:ss}";
            IsLoading = true;
            StatusMessage = "Decoding image data...";

            // Decode base64
            byte[] imageBytes = Convert.FromBase64String(base64ImageData);
            _logger.LogInformation("Decoded to {ByteCount} bytes ({KiloBytes} KB)",
                imageBytes.Length, imageBytes.Length / 1024);

            // Log image header (first 16 bytes)
            if (imageBytes.Length > 16)
            {
                var header = string.Join(" ", imageBytes.Take(16).Select(b => b.ToString("X2")));
                _logger.LogDebug("Image header bytes: {Header}", header);

                // Check for PNG signature: 89 50 4E 47 0D 0A 1A 0A
                if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 &&
                    imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                {
                    _logger.LogInformation("✓ Valid PNG header detected");
                }
                else
                {
                    _logger.LogWarning("⚠ Not a PNG image! Header: {Header}", header);
                }
            }

            // Create BitmapImage on UI thread - check if already on UI thread first
            var dispatcher = Application.Current.Dispatcher;

            Action createBitmap = () =>
            {
                try
                {
                    StatusMessage = "Creating bitmap image...";

                    // Create a COPY of the byte array to avoid disposal issues
                    byte[] imageCopy = new byte[imageBytes.Length];
                    Array.Copy(imageBytes, imageCopy, imageBytes.Length);

                    var ms = new MemoryStream(imageCopy);

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();

                    // NOW we can dispose the stream because OnLoad cached it
                    ms.Dispose();

                    // Freeze for cross-thread access
                    bitmap.Freeze();

                    _logger.LogInformation("BitmapImage created: {Width}x{Height}, Format={Format}, DpiX={DpiX}",
                        bitmap.PixelWidth, bitmap.PixelHeight, bitmap.Format, bitmap.DpiX);

                    ScreenshotImage = bitmap;
                    IsLoading = false;
                    StatusMessage = $"Screenshot loaded: {bitmap.PixelWidth}x{bitmap.PixelHeight} " +
                                  $"({imageBytes.Length / 1024} KB)";

                    _logger.LogInformation("ScreenshotImage property set successfully");
                    _logger.LogInformation("=== LoadScreenshot SUCCESS ===");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create BitmapImage");
                    IsLoading = false;
                    StatusMessage = $"Error loading image: {ex.Message}";
                }
            };

            if (dispatcher.CheckAccess())
            {
                createBitmap();
            }
            else
            {
                dispatcher.Invoke(createBitmap);
            }
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Invalid base64 string format");
            IsLoading = false;
            StatusMessage = "Error: Invalid image data format";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load screenshot");
            IsLoading = false;
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Save screenshot to file
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveScreenshot))]
    private async Task SaveScreenshot()
    {
        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "Save Screenshot",
                FileName = $"Screenshot_{ClientName}_{Timestamp:yyyyMMdd_HHmmss}.png",
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|All Files (*.*)|*.*",
                DefaultExt = ".png"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                _logger.LogInformation("Saving screenshot to {FilePath}", saveFileDialog.FileName);

                // Create encoder based on file extension
                BitmapEncoder encoder = Path.GetExtension(saveFileDialog.FileName).ToLower() switch
                {
                    ".jpg" or ".jpeg" => new JpegBitmapEncoder(),
                    ".png" => new PngBitmapEncoder(),
                    _ => new PngBitmapEncoder()
                };

                encoder.Frames.Add(BitmapFrame.Create(ScreenshotImage!));

                using var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create);
                encoder.Save(fileStream);

                StatusMessage = "Screenshot saved successfully";
                _logger.LogInformation("Screenshot saved to {FilePath}", saveFileDialog.FileName);

                await _dialogService.ShowInformationAsync($"Screenshot saved successfully to:\n\n{saveFileDialog.FileName}",
                    "Screenshot Saved");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving screenshot: {ex.Message}";
            _logger.LogError(ex, "Failed to save screenshot");
            await _dialogService.ShowErrorAsync($"Failed to save screenshot:\n\n{ex.Message}",
                "Save Error");
        }
    }

    private bool CanSaveScreenshot() => ScreenshotImage != null && !IsLoading;

    /// <summary>
    /// Copy screenshot to clipboard
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopyToClipboard))]
    private async Task CopyToClipboard()
    {
        try
        {
            if (ScreenshotImage != null)
            {
                Clipboard.SetImage(ScreenshotImage);
                StatusMessage = "Screenshot copied to clipboard";
                _logger.LogInformation("Screenshot copied to clipboard for client {ClientName}", ClientName);

                await _dialogService.ShowInformationAsync("Screenshot copied to clipboard successfully!",
                    "Copied");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error copying to clipboard: {ex.Message}";
            _logger.LogError(ex, "Failed to copy screenshot to clipboard");
            await _dialogService.ShowErrorAsync($"Failed to copy screenshot to clipboard:\n\n{ex.Message}",
                "Copy Error");
        }
    }

    private bool CanCopyToClipboard() => ScreenshotImage != null && !IsLoading;

    /// <summary>
    /// Close the window
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        _logger.LogDebug("Closing screenshot window for client {ClientName}", ClientName);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnScreenshotImageChanged(BitmapImage? value)
    {
        // Notify commands that depend on ScreenshotImage
        SaveScreenshotCommand.NotifyCanExecuteChanged();
        CopyToClipboardCommand.NotifyCanExecuteChanged();
    }
}
