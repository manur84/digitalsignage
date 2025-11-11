using DigitalSignage.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for managing multi-selection in the designer
/// </summary>
public class SelectionService : INotifyPropertyChanged
{
    private readonly ObservableCollection<DisplayElement> _selectedElements = new();
    private DisplayElement? _primarySelection;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Gets the collection of selected elements
    /// </summary>
    public ReadOnlyObservableCollection<DisplayElement> SelectedElements { get; }

    /// <summary>
    /// Gets the primary selected element (the last one selected)
    /// </summary>
    public DisplayElement? PrimarySelection
    {
        get => _primarySelection;
        private set
        {
            if (_primarySelection != value)
            {
                _primarySelection = value;
                OnPropertyChanged(nameof(PrimarySelection));
            }
        }
    }

    /// <summary>
    /// Gets whether multiple elements are selected
    /// </summary>
    public bool HasMultipleSelections => _selectedElements.Count > 1;

    /// <summary>
    /// Gets whether any elements are selected
    /// </summary>
    public bool HasSelection => _selectedElements.Count > 0;

    /// <summary>
    /// Gets the number of selected elements
    /// </summary>
    public int SelectionCount => _selectedElements.Count;

    public SelectionService()
    {
        SelectedElements = new ReadOnlyObservableCollection<DisplayElement>(_selectedElements);
        _selectedElements.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasMultipleSelections));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectionCount));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>
    /// Selects a single element (clears previous selection)
    /// </summary>
    public void SelectSingle(DisplayElement element)
    {
        _selectedElements.Clear();
        _selectedElements.Add(element);
        PrimarySelection = element;
    }

    /// <summary>
    /// Adds an element to the selection (multi-select)
    /// </summary>
    public void AddToSelection(DisplayElement element)
    {
        if (!_selectedElements.Contains(element))
        {
            _selectedElements.Add(element);
            PrimarySelection = element;
        }
    }

    /// <summary>
    /// Removes an element from the selection
    /// </summary>
    public void RemoveFromSelection(DisplayElement element)
    {
        _selectedElements.Remove(element);

        if (_selectedElements.Count > 0)
        {
            PrimarySelection = _selectedElements.Last();
        }
        else
        {
            PrimarySelection = null;
        }
    }

    /// <summary>
    /// Toggles an element's selection state
    /// </summary>
    public void ToggleSelection(DisplayElement element)
    {
        if (_selectedElements.Contains(element))
        {
            RemoveFromSelection(element);
        }
        else
        {
            AddToSelection(element);
        }
    }

    /// <summary>
    /// Selects multiple elements
    /// </summary>
    public void SelectMultiple(IEnumerable<DisplayElement> elements)
    {
        _selectedElements.Clear();

        foreach (var element in elements)
        {
            _selectedElements.Add(element);
        }

        if (_selectedElements.Count > 0)
        {
            PrimarySelection = _selectedElements.Last();
        }
    }

    /// <summary>
    /// Selects a range of elements between two elements (Shift+Click behavior)
    /// </summary>
    public void SelectRange(DisplayElement fromElement, DisplayElement toElement, IEnumerable<DisplayElement> allElements)
    {
        var elementList = allElements.ToList();
        var fromIndex = elementList.IndexOf(fromElement);
        var toIndex = elementList.IndexOf(toElement);

        if (fromIndex == -1 || toIndex == -1)
        {
            return;
        }

        var startIndex = Math.Min(fromIndex, toIndex);
        var endIndex = Math.Max(fromIndex, toIndex);

        _selectedElements.Clear();

        for (int i = startIndex; i <= endIndex; i++)
        {
            _selectedElements.Add(elementList[i]);
        }

        PrimarySelection = toElement;
    }

    /// <summary>
    /// Selects elements within a rectangular area
    /// </summary>
    public void SelectInRectangle(double x, double y, double width, double height, IEnumerable<DisplayElement> allElements)
    {
        _selectedElements.Clear();

        foreach (var element in allElements)
        {
            if (IsElementInRectangle(element, x, y, width, height))
            {
                _selectedElements.Add(element);
            }
        }

        if (_selectedElements.Count > 0)
        {
            PrimarySelection = _selectedElements.Last();
        }
    }

    /// <summary>
    /// Clears all selections
    /// </summary>
    public void ClearSelection()
    {
        _selectedElements.Clear();
        PrimarySelection = null;
    }

    /// <summary>
    /// Checks if an element is selected
    /// </summary>
    public bool IsSelected(DisplayElement element)
    {
        return _selectedElements.Contains(element);
    }

    /// <summary>
    /// Gets the bounding box of all selected elements
    /// </summary>
    public (double X, double Y, double Width, double Height)? GetSelectionBounds()
    {
        if (_selectedElements.Count == 0)
        {
            return null;
        }

        var minX = _selectedElements.Min(e => e.Position.X);
        var minY = _selectedElements.Min(e => e.Position.Y);
        var maxX = _selectedElements.Max(e => e.Position.X + e.Size.Width);
        var maxY = _selectedElements.Max(e => e.Position.Y + e.Size.Height);

        return (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Checks if an element intersects with a rectangle
    /// </summary>
    private bool IsElementInRectangle(DisplayElement element, double x, double y, double width, double height)
    {
        var elementX = element.Position.X;
        var elementY = element.Position.Y;
        var elementWidth = element.Size.Width;
        var elementHeight = element.Size.Height;

        // Check for intersection
        return elementX < x + width &&
               elementX + elementWidth > x &&
               elementY < y + height &&
               elementY + elementHeight > y;
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
