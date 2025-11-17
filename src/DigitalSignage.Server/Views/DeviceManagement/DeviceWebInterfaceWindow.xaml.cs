using System;
using System.Windows;
using System.Windows.Input;

namespace DigitalSignage.Server.Views.DeviceManagement
{
    public partial class DeviceWebInterfaceWindow : Window
    {
        private readonly string _url;

        public DeviceWebInterfaceWindow(string url)
        {
            InitializeComponent();
            _url = url;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                UrlText.Text = _url;
                Browser.Navigate(_url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Seite: {ex.Message}", "Web Interface", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Browser.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Aktualisieren: {ex.Message}", "Web Interface", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.F5)
            {
                Refresh_Click(sender, e);
            }
        }
    }
}