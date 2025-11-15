using DigitalSignage.Server.ViewModels;
using System.Windows;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Dialog for configuring QR Code element properties
/// </summary>
public partial class QRCodePropertiesDialog : Window
{
    public QRCodePropertiesViewModel ViewModel { get; }

    public QRCodePropertiesDialog(QRCodePropertiesViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
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
