using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DigitalSignage.Server.Controls;

public partial class ColorPicker : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(string),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata("#000000", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

    public string SelectedColor
    {
        get => (string)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public ColorPicker()
    {
        InitializeComponent();
        UpdatePreviewBrush();
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPicker picker)
        {
            picker.UpdatePreviewBrush();
        }
    }

    private void UpdatePreviewBrush()
    {
        if (ColorPreviewBorder != null)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(SelectedColor ?? "#000000");
                ColorPreviewBorder.Background = new SolidColorBrush(color);
            }
            catch
            {
                ColorPreviewBorder.Background = new SolidColorBrush(Colors.Black);
            }
        }
    }

    private void ColorPreview_Click(object sender, MouseButtonEventArgs e)
    {
        ColorPopup.IsOpen = true;
    }

    private void PresetColor_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Background is SolidColorBrush brush)
        {
            SelectedColor = brush.Color.ToString();
            ColorPopup.IsOpen = false;
        }
    }
}
