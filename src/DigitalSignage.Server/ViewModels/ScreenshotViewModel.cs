using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public ScreenshotViewModel(ILogger<ScreenshotViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Load screenshot from base64 encoded image data
    /// </summary>
    public void LoadScreenshot(string clientName, string base64ImageData)
    {
        try
        {
            IsLoading = true;
            ClientName = clientName;
            Timestamp = DateTime.Now;
            WindowTitle = $"Screenshot - {ClientName} - {Timestamp:yyyy-MM-dd HH:mm:ss}";

            _logger.LogInformation("=== LoadScreenshot START ===");
            _logger.LogInformation("Client: {ClientName}", clientName);
            _logger.LogInformation("Base64 data length: {Length} characters", base64ImageData?.Length ?? 0);

            if (string.IsNullOrWhiteSpace(base64ImageData))
            {
                StatusMessage = "No image data received";
                _logger.LogWarning("Empty or null image data received for client {ClientName}", clientName);
                return;
            }

            // Log first 100 chars of base64 to verify it's image data
            var preview = base64ImageData.Length > 100 ? base64ImageData.Substring(0, 100) : base64ImageData;
            _logger.LogDebug("Base64 preview (first 100 chars): {Preview}...", preview);

            // Convert base64 to byte array
            var imageBytes = Convert.FromBase64String(base64ImageData);
            _logger.LogInformation("Successfully decoded base64 to {ByteCount} bytes ({KiloBytes} KB)",
                imageBytes.Length, imageBytes.Length / 1024);

            // Log first few bytes to verify PNG header (89 50 4E 47)
            if (imageBytes.Length > 4)
            {
                var header = string.Join(" ", imageBytes.Take(8).Select(b => b.ToString("X2")));
                _logger.LogDebug("Image header bytes: {Header}", header);
            }

            // Create BitmapImage from bytes using the UI thread
            BitmapImage? bitmap = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var ms = new MemoryStream(imageBytes);
                    ms.Position = 0; // Ensure we're at the start

                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // Load image into memory immediately
                    bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Important for cross-thread access

                    _logger.LogInformation("BitmapImage created: {Width}x{Height}, Format={Format}",
                        bitmap.PixelWidth, bitmap.PixelHeight, bitmap.Format);

                    // Now we can safely dispose the stream since OnLoad cached the image
                    ms.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create BitmapImage on UI thread");
                    throw;
                }
            });

            if (bitmap == null)
            {
                throw new InvalidOperationException("Failed to create BitmapImage - result was null");
            }

            // Set the property on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                ScreenshotImage = bitmap;
                _logger.LogInformation("ScreenshotImage property set, value is null: {IsNull}", ScreenshotImage == null);
            });

            StatusMessage = $"Screenshot loaded successfully ({imageBytes.Length / 1024} KB, {bitmap.PixelWidth}x{bitmap.PixelHeight})";
            _logger.LogInformation("=== LoadScreenshot SUCCESS ===");
        }
        catch (FormatException ex)
        {
            StatusMessage = "Invalid image data format";
            _logger.LogError(ex, "Failed to decode base64 image data for client {ClientName}", clientName);
            MessageBox.Show($"Failed to load screenshot: Invalid image data format\n\n{ex.Message}",
                "Screenshot Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading screenshot: {ex.Message}";
            _logger.LogError(ex, "=== LoadScreenshot FAILED === Error for client {ClientName}", clientName);
            _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
            MessageBox.Show($"Failed to load screenshot:\n\n{ex.Message}",
                "Screenshot Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            _logger.LogInformation("=== LoadScreenshot END (IsLoading={IsLoading}) ===", IsLoading);
        }
    }

    /// <summary>
    /// Save screenshot to file
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveScreenshot))]
    private void SaveScreenshot()
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

                MessageBox.Show($"Screenshot saved successfully to:\n\n{saveFileDialog.FileName}",
                    "Screenshot Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving screenshot: {ex.Message}";
            _logger.LogError(ex, "Failed to save screenshot");
            MessageBox.Show($"Failed to save screenshot:\n\n{ex.Message}",
                "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanSaveScreenshot() => ScreenshotImage != null && !IsLoading;

    /// <summary>
    /// Copy screenshot to clipboard
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopyToClipboard))]
    private void CopyToClipboard()
    {
        try
        {
            if (ScreenshotImage != null)
            {
                Clipboard.SetImage(ScreenshotImage);
                StatusMessage = "Screenshot copied to clipboard";
                _logger.LogInformation("Screenshot copied to clipboard for client {ClientName}", ClientName);

                MessageBox.Show("Screenshot copied to clipboard successfully!",
                    "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error copying to clipboard: {ex.Message}";
            _logger.LogError(ex, "Failed to copy screenshot to clipboard");
            MessageBox.Show($"Failed to copy screenshot to clipboard:\n\n{ex.Message}",
                "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
