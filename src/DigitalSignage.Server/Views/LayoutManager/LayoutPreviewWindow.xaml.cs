using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Views.LayoutManager;

public partial class LayoutPreviewWindow : Window
{
    private readonly DisplayLayout _layout;
    private readonly List<string> _tempFiles = new();
    private ImageSource? _imageSource;

    public LayoutPreviewWindow(DisplayLayout layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        MouseDoubleClick += (_, _) => Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TitleText.Text = $"{_layout.Name} ({_layout.SvgFileName ?? "Layout"})";

        var pngBase64 = _layout.PngContentBase64;
        if (string.IsNullOrWhiteSpace(pngBase64))
        {
            ErrorText.Text = "Kein Bildinhalt im Layout vorhanden.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(pngBase64);
            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            _imageSource = bitmap;
            DrawImage();
            ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Bild konnte nicht angezeigt werden: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => DrawImage();

    private void DrawImage()
    {
        if (_imageSource == null || PreviewCanvas == null) return;

        PreviewCanvas.Children.Clear();

        var img = new System.Windows.Controls.Image
        {
            Source = _imageSource,
            Stretch = Stretch.Uniform,
            Width = PreviewCanvas.ActualWidth > 0 ? PreviewCanvas.ActualWidth : double.NaN,
            Height = PreviewCanvas.ActualHeight > 0 ? PreviewCanvas.ActualHeight : double.NaN
        };

        PreviewCanvas.Children.Add(img);
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
