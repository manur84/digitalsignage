using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DigitalSignage.Server.Views;

public partial class SplashScreenWindow : Window, IDisposable
{
    private readonly string[] _logoCandidates;
    private bool _isClosed;

    public SplashScreenWindow()
    {
        InitializeComponent();

        _logoCandidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "digisign-logo.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "digisign-logo.png"),
        };

        LoadLogo();
    }

    public static SplashScreenWindow ShowSplash(string statusMessage)
    {
        var splash = new SplashScreenWindow();
        splash.SetStatus(statusMessage);
        splash.ShowActivated = false;
        splash.Show();
        splash.Topmost = true;
        splash.Focusable = false;
        splash.UpdateLayout();
        return splash;
    }

    public void SetStatus(string message)
    {
        Dispatcher.Invoke(() => StatusText.Text = message);
    }

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
                Close();
            }
            catch
            {
                // Best effort only
            }
        });
    }

    private void LoadLogo()
    {
        var logoPath = _logoCandidates.FirstOrDefault(File.Exists);
        if (logoPath == null)
        {
            return; // keep text-only splash
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
            bitmap.EndInit();
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
