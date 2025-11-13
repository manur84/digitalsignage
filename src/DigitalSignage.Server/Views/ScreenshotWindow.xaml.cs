using System.Windows;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

/// <summary>
/// Interaction logic for ScreenshotWindow.xaml
/// </summary>
public partial class ScreenshotWindow : Window
{
    public ScreenshotWindow(ScreenshotViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Subscribe to close request
        viewModel.CloseRequested += (sender, args) =>
        {
            Close();
        };
    }

    /// <summary>
    /// Static helper method to show screenshot window
    /// </summary>
    public static void ShowScreenshot(string clientName, string base64ImageData, ScreenshotViewModel viewModel)
    {
        // Load the screenshot data into the view model
        viewModel.LoadScreenshot(clientName, base64ImageData);

        // Create and show the window on the UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            var window = new ScreenshotWindow(viewModel);
            window.Show();
            window.Activate();
        });
    }
}
