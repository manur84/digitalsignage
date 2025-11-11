using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Models;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

public partial class DesignerViewModel : ObservableObject
{
    [ObservableProperty]
    private DisplayLayout? _currentLayout;

    [ObservableProperty]
    private DisplayElement? _selectedElement;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private string _selectedTool = "select";

    public ObservableCollection<DisplayElement> Elements { get; } = new();

    [RelayCommand]
    private void SelectTool(string tool)
    {
        SelectedTool = tool;
    }

    [RelayCommand]
    private void AddTextElement()
    {
        var textElement = new DisplayElement
        {
            Type = "text",
            Name = "Text Element",
            Position = new Position { X = 100, Y = 100 },
            Size = new Size { Width = 200, Height = 50 },
            Properties = new Dictionary<string, object>
            {
                ["Content"] = "Sample Text",
                ["FontFamily"] = "Arial",
                ["FontSize"] = 24,
                ["Color"] = "#000000"
            }
        };

        Elements.Add(textElement);
        SelectedElement = textElement;
    }

    [RelayCommand]
    private void AddImageElement()
    {
        var imageElement = new DisplayElement
        {
            Type = "image",
            Name = "Image Element",
            Position = new Position { X = 100, Y = 100 },
            Size = new Size { Width = 300, Height = 200 }
        };

        Elements.Add(imageElement);
        SelectedElement = imageElement;
    }

    [RelayCommand]
    private void DeleteSelectedElement()
    {
        if (SelectedElement != null)
        {
            Elements.Remove(SelectedElement);
            SelectedElement = null;
        }
    }

    [RelayCommand]
    private void MoveElementUp()
    {
        if (SelectedElement != null)
        {
            SelectedElement.ZIndex++;
        }
    }

    [RelayCommand]
    private void MoveElementDown()
    {
        if (SelectedElement != null)
        {
            SelectedElement.ZIndex--;
        }
    }
}
