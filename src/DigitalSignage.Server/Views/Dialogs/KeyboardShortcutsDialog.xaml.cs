using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DigitalSignage.Server.Views.Dialogs;

/// <summary>
/// Interaction logic for KeyboardShortcutsDialog.xaml
/// </summary>
public partial class KeyboardShortcutsDialog : Window
{
    public KeyboardShortcutsDialog()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchTextBox.Text.ToLower();

        foreach (var child in ShortcutsPanel.Children.OfType<Grid>())
        {
            if (child.Children.Count >= 2 && child.Children[1] is TextBlock descriptionBlock)
            {
                var description = descriptionBlock.Text.ToLower();
                child.Visibility = description.Contains(searchText) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // Also handle category headers
        foreach (var child in ShortcutsPanel.Children.OfType<TextBlock>())
        {
            // Keep category headers always visible
            child.Visibility = Visibility.Visible;
        }
    }
}
