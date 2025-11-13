using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// ViewModel for Grid Configuration Dialog
/// </summary>
public partial class GridConfigViewModel : ObservableObject
{
    [ObservableProperty]
    private int _gridSize = 10;

    [ObservableProperty]
    private string _gridColor = "#E0E0E0";

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _snapToGrid = true;

    [ObservableProperty]
    private bool _isDotGrid = true;

    [ObservableProperty]
    private bool _isLineGrid = false;

    /// <summary>
    /// Color preview for the grid color picker
    /// </summary>
    public Color GridColorPreview
    {
        get
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(GridColor);
            }
            catch
            {
                return Colors.LightGray;
            }
        }
    }

    public GridConfigViewModel()
    {
        // Default values already set
    }

    public GridConfigViewModel(int gridSize, string gridColor, bool showGrid, bool snapToGrid, bool isDotGrid)
    {
        GridSize = gridSize;
        GridColor = gridColor;
        ShowGrid = showGrid;
        SnapToGrid = snapToGrid;
        IsDotGrid = isDotGrid;
        IsLineGrid = !isDotGrid;
    }

    partial void OnGridColorChanged(string value)
    {
        OnPropertyChanged(nameof(GridColorPreview));
    }

    partial void OnIsDotGridChanged(bool value)
    {
        if (value)
        {
            IsLineGrid = false;
        }
    }

    partial void OnIsLineGridChanged(bool value)
    {
        if (value)
        {
            IsDotGrid = false;
        }
    }
}
