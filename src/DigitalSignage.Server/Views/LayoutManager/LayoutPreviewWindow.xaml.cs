using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Views.LayoutManager;

public partial class LayoutPreviewWindow : Window
{
    private readonly DisplayLayout _layout;
    private readonly List<string> _tempFiles = new();

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
            var htmlPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
            var html = BuildHtml(svgBase64);
            File.WriteAllText(htmlPath, html, Encoding.UTF8);
            _tempFiles.Add(htmlPath);

            // Using NavigateToString often strips resources; Navigate with file:// to keep base URL
            Browser.Navigate(new Uri(htmlPath));
            ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"SVG konnte nicht angezeigt werden: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private static string BuildHtml(string svgBase64)
    {
        var sb = new StringBuilder();
        sb.Append("""
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <style>
        html, body { margin:0; padding:0; width:100%; height:100%; background:#111; overflow:hidden;}
        object { width:100vw; height:100vh; display:block; }
    </style>
</head>
<body>
    <object type="image/svg+xml" data="data:image/svg+xml;base64,
""");
        sb.Append(svgBase64);
        sb.Append("""
"></object>
</body>
</html>
""");
        return sb.ToString();
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
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }
}
