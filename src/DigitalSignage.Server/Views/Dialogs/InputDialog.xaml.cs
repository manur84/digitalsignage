using System.Windows;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Simple input dialog for getting text input from user
/// </summary>
public partial class InputDialog : Window
{
    /// <summary>
    /// Gets or sets the message/prompt text
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the input text value
    /// </summary>
    public string InputText { get; set; }

    /// <summary>
    /// Creates a new InputDialog
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Prompt message</param>
    /// <param name="defaultValue">Default input value</param>
    public InputDialog(string title, string message, string defaultValue = "")
    {
        InitializeComponent();

        Title = title; // Using inherited Window.Title property
        Message = message;
        InputText = defaultValue;

        DataContext = this;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Focus the input textbox and select all text
        InputTextBox.Focus();
        InputTextBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(InputText))
        {
            MessageBox.Show(
                "Please enter a value.",
                "Input Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            InputTextBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
