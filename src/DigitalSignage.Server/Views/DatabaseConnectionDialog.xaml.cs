using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DigitalSignage.Core.Models;
using DigitalSignage.Core.Interfaces;

namespace DigitalSignage.Server.Views;

public partial class DatabaseConnectionDialog : Window
{
    private readonly IDataService _dataService;
    public SqlConnectionConfig ConnectionConfig { get; private set; }

    public int AuthTypeIndex
    {
        get => ConnectionConfig.IntegratedSecurity ? 0 : 1;
        set => ConnectionConfig.IntegratedSecurity = value == 0;
    }

    public string Server
    {
        get => ConnectionConfig.Server;
        set
        {
            ConnectionConfig.Server = value;
            UpdateConnectionString();
        }
    }

    public string Database
    {
        get => ConnectionConfig.Database;
        set
        {
            ConnectionConfig.Database = value;
            UpdateConnectionString();
        }
    }

    public string Username
    {
        get => ConnectionConfig.Username;
        set
        {
            ConnectionConfig.Username = value;
            UpdateConnectionString();
        }
    }

    public int ConnectionTimeout
    {
        get => ConnectionConfig.ConnectionTimeout;
        set
        {
            ConnectionConfig.ConnectionTimeout = value;
            UpdateConnectionString();
        }
    }

    public bool Encrypt
    {
        get => ConnectionConfig.Encrypt;
        set
        {
            ConnectionConfig.Encrypt = value;
            UpdateConnectionString();
        }
    }

    public bool TrustServerCertificate
    {
        get => ConnectionConfig.TrustServerCertificate;
        set
        {
            ConnectionConfig.TrustServerCertificate = value;
            UpdateConnectionString();
        }
    }

    public string ConnectionString => ConnectionConfig.ToConnectionString();

    public DatabaseConnectionDialog(SqlConnectionConfig? config = null)
    {
        InitializeComponent();
        _dataService = App.GetService<IDataService>();

        ConnectionConfig = config ?? new SqlConnectionConfig
        {
            Server = "localhost",
            Database = "DigitalSignage",
            IntegratedSecurity = true,
            ConnectionTimeout = 30,
            Encrypt = true
        };

        DataContext = this;
        AuthTypeComboBox.SelectedIndex = AuthTypeIndex;
    }

    private void AuthType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (AuthTypeComboBox.SelectedIndex == 0)
        {
            SqlAuthPanel.Visibility = Visibility.Collapsed;
            ConnectionConfig.IntegratedSecurity = true;
        }
        else
        {
            SqlAuthPanel.Visibility = Visibility.Visible;
            ConnectionConfig.IntegratedSecurity = false;
        }
        UpdateConnectionString();
    }

    private void Password_Changed(object sender, RoutedEventArgs e)
    {
        ConnectionConfig.Password = PasswordBox.Password;
        UpdateConnectionString();
    }

    private void UpdateConnectionString()
    {
        // Trigger UI update
        GetBindingExpression(DataContextProperty)?.UpdateTarget();
    }

    // Event handler must be async void, but delegates to async Task method
    private async void TestConnection_Click(object sender, RoutedEventArgs e)
        => await TestConnectionAsync((Button)sender);

    private async Task TestConnectionAsync(Button button)
    {
        TestResultBorder.Visibility = Visibility.Collapsed;
        button.IsEnabled = false;

        try
        {
            var success = await _dataService.TestConnectionAsync(
                new DataSource { ConnectionString = ConnectionString });

            if (success)
            {
                ShowTestResult(true, "✓ Connection successful!");
            }
            else
            {
                ShowTestResult(false, "✗ Connection failed. Please check your settings.");
            }
        }
        catch (Exception ex)
        {
            ShowTestResult(false, $"✗ Error: {ex.Message}");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void ShowTestResult(bool success, string message)
    {
        TestResultBorder.Visibility = Visibility.Visible;
        TestResultBorder.Background = success
            ? new SolidColorBrush(Color.FromRgb(220, 255, 220))
            : new SolidColorBrush(Color.FromRgb(255, 220, 220));
        TestResultBorder.BorderBrush = success
            ? Brushes.LimeGreen
            : Brushes.Red;
        TestResultBorder.BorderThickness = new Thickness(1);

        TestResultText.Text = message;
        TestResultText.Foreground = success ? Brushes.DarkGreen : Brushes.DarkRed;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
