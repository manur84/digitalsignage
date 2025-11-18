using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Professional splash screen with progress tracking and modern animations
/// </summary>
public partial class SplashScreenWindow : Window, IDisposable
{
    private readonly string[] _logoCandidates;
    private bool _isClosed;

    public SplashScreenViewModel ViewModel { get; }

    public SplashScreenWindow()
    {
        InitializeComponent();

        ViewModel = new SplashScreenViewModel();
        DataContext = ViewModel;

        _logoCandidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "digisign-logo.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "digisign-logo.png"),
        };

        LoadLogo();
    }

    /// <summary>
    /// Show splash screen with initial message
    /// </summary>
    public static SplashScreenWindow ShowSplash(string statusMessage)
    {
        var splash = new SplashScreenWindow();
        splash.ViewModel.UpdateProgress(0, statusMessage);
        splash.ShowActivated = false;
        splash.Show();
        splash.Topmost = true;
        splash.Focusable = false;
        splash.UpdateLayout();
        return splash;
    }

    /// <summary>
    /// Update progress with percentage and messages
    /// </summary>
    public void UpdateProgress(double progress, string statusMessage, string detailMessage = "")
    {
        Dispatcher.Invoke(() =>
        {
            ViewModel.UpdateProgress(progress, statusMessage, detailMessage);
        });
    }

    /// <summary>
    /// Animate progress smoothly to target
    /// </summary>
    public async Task AnimateProgressAsync(double targetProgress, string statusMessage, string detailMessage = "", int durationMs = 300)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            await ViewModel.AnimateProgressAsync(targetProgress, statusMessage, detailMessage, durationMs);
        });
    }

    /// <summary>
    /// Set indeterminate loading state
    /// </summary>
    public void SetIndeterminate(string statusMessage, string detailMessage = "")
    {
        Dispatcher.Invoke(() =>
        {
            ViewModel.SetIndeterminate(statusMessage, detailMessage);
        });
    }

    /// <summary>
    /// Update status message (backward compatibility)
    /// </summary>
    [Obsolete("Use UpdateProgress instead")]
    public void SetStatus(string message)
    {
        UpdateProgress(ViewModel.Progress, message);
    }

    /// <summary>
    /// Close splash screen safely
    /// </summary>
    public void CloseSafely()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        Dispatcher.Invoke(() =>
        {
            try
            {
                // Fade out animation before closing
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300)
                };

                fadeOut.Completed += (s, e) =>
                {
                    try
                    {
                        Close();
                    }
                    catch
                    {
                        // Best effort only
                    }
                };

                BeginAnimation(OpacityProperty, fadeOut);
            }
            catch
            {
                // If animation fails, close immediately
                try
                {
                    Close();
                }
                catch
                {
                    // Best effort only
                }
            }
        });
    }

    /// <summary>
    /// Load logo from available paths
    /// </summary>
    private void LoadLogo()
    {
        var logoPath = _logoCandidates.FirstOrDefault(File.Exists);
        if (logoPath == null)
        {
            return; // Keep text-only splash
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze(); // Freeze for better performance
            LogoImage.Source = bitmap;
        }
        catch
        {
            // Ignore logo failures and keep text-only splash
        }
    }

    public void Dispose()
    {
        CloseSafely();
    }
}
