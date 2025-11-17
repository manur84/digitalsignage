using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Views.LayoutManager;

public partial class LayoutPreviewWindow : Window
{
    private readonly DisplayLayout _layout;
    private string? _tempFile;

    public LayoutPreviewWindow(DisplayLayout layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        InitializeComponent();
        Loaded += OnLoaded;
        MouseDoubleClick += (_, _) => Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TitleText.Text = $"{_layout.Name} ({_layout.SvgFileName ?? "SVG"})";

        var svgBase64 = _layout.SvgContentBase64;
        if (string.IsNullOrWhiteSpace(svgBase64))
        {
            ErrorText.Text = "Kein SVG-Inhalt im Layout vorhanden.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(svgBase64);
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.svg");
            File.WriteAllBytes(tempPath, bytes);
            _tempFile = tempPath;
            Browser.Navigate(new Uri(tempPath));
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"SVG konnte nicht angezeigt werden: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!string.IsNullOrWhiteSpace(_tempFile) && File.Exists(_tempFile))
        {
            try
            {
                File.Delete(_tempFile);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }
}
