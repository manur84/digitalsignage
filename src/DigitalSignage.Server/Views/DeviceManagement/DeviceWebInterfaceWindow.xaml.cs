using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace DigitalSignage.Server.Views.DeviceManagement
{
    public partial class DeviceWebInterfaceWindow : Window
    {
        private readonly string _url;
        private bool _initialized;

        public DeviceWebInterfaceWindow(string url)
        {
            InitializeComponent();
            _url = url;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UrlText.Text = _url;
            await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            if (_initialized) return;
            try
            {
                // Ensure environment
                var env = await CoreWebView2Environment.CreateAsync();
                await WebView.EnsureCoreWebView2Async(env);

                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                WebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                WebView.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;

                WebView.Source = new Uri(_url);
                _initialized = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Initialisieren WebView2: {ex.Message}", "Web Interface", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            UrlText.Text = e.Uri;
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                UrlText.Text = $"Fehler: {e.WebErrorStatus}";
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // Reserved for future use (JS postMessage)
        }

        private void CoreWebView2_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            MessageBox.Show($"WebView Prozessfehler: {e.ProcessFailedKind}", "Web Interface", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_initialized)
                {
                    await InitializeWebViewAsync();
                }
                else
                {
                    WebView.Reload();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Aktualisieren: {ex.Message}", "Web Interface", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WebView.CoreWebView2 != null && WebView.CoreWebView2.CanGoBack)
                {
                    WebView.CoreWebView2.GoBack();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Zurück: {ex.Message}", "Web Interface", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.F5)
            {
                Refresh_Click(sender, e);
            }
            else if (e.Key == Key.Back)
            {
                Back_Click(sender, e);
            }
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // Ctrl+Enter reload
                Refresh_Click(sender, e);
            }
            else if (e.Key == Key.R && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Refresh_Click(sender, e);
            }
            else if (e.Key == Key.L && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // Ctrl+L select URL text
                UrlText.Focus();
            }
            await Task.CompletedTask;
        }
    }
}