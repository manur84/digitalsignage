using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DigitalSignage.Server.Views;

/// <summary>
/// UserControl for the Designer layers panel showing element hierarchy and Z-index management
/// </summary>
public partial class LayersPanel : UserControl
{
    public LayersPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles search text changes to filter the layers list
    /// </summary>
    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext == null) return;

        var searchText = SearchTextBox.Text.ToLower();
        var listBox = this.FindName("Layers") as ListBox;

        if (listBox?.ItemsSource != null)
        {
            var view = CollectionViewSource.GetDefaultView(listBox.ItemsSource);
            if (view != null)
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    view.Filter = null;
                }
                else
                {
                    view.Filter = item =>
                    {
                        if (item is Core.Models.DisplayElement element)
                        {
                            return element.Name.ToLower().Contains(searchText) ||
                                   element.Type.ToLower().Contains(searchText);
                        }
                        return false;
                    };
                }
            }
        }
    }
}
