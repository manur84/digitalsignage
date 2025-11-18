using System.Windows;
using System.Windows.Controls;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views.SqlDataSources;

/// <summary>
/// Interaction logic for SqlDataSourcesTabControl.xaml
/// </summary>
public partial class SqlDataSourcesTabControl : UserControl
{
    public SqlDataSourcesTabControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles password changes and updates the ViewModel
    /// </summary>
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SqlDataSourcesViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.Password = passwordBox.Password;
        }
    }

    /// <summary>
    /// Handles authentication type selection changes
    /// </summary>
    private void AuthTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SqlDataSourcesViewModel viewModel && sender is ComboBox comboBox)
        {
            var isSqlAuth = comboBox.SelectedIndex == 1;
            viewModel.AuthType = isSqlAuth
                ? SqlAuthenticationType.SqlServerAuthentication
                : SqlAuthenticationType.WindowsAuthentication;

            // Enable/disable username and password fields
            UsernameTextBox.IsEnabled = isSqlAuth;
            PasswordBox.IsEnabled = isSqlAuth;
        }
    }
}
