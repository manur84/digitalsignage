using System;
using System.Text;
using System.Windows;
using System.Windows.Input;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Views.LayoutManager;

public partial class LayoutPreviewWindow : Window
{
    private readonly DisplayLayout _layout;

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
            var html = BuildHtml(svgBase64);
            Browser.NavigateToString(html);
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"SVG konnte nicht angezeigt werden: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private static string BuildHtml(string base64)
    {
        var sb = new StringBuilder();
        sb.Append("""
<!DOCTYPE html><html><head><meta charset="utf-8">
<style>
html,body { margin:0; padding:0; width:100%; height:100%; background:#111; overflow:hidden;}
object { width:100%; height:100%; }
</style>
</head><body>
<object data="data:image/svg+xml;base64,
""");
        sb.Append(base64);
        sb.Append("""
" type="image/svg+xml"></object></body></html>
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
}
