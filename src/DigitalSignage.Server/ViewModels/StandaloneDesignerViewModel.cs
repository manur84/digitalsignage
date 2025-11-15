using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace DigitalSignage.Server.ViewModels;

/// <summary>
/// Lightweight view model that drives the standalone designer canvas.
/// Completely independent from the legacy DesignerViewModel so state does not bleed between tabs.
/// </summary>
public partial class StandaloneDesignerViewModel : ObservableObject
{
    private readonly ILayoutService _layoutService;
    private readonly ILogger<StandaloneDesignerViewModel> _logger;

    public ObservableCollection<DisplayElement> Elements { get; } = new();

    [ObservableProperty]
    private DisplayLayout _currentLayout = CreateDefaultLayout("Standalone Layout");

    [ObservableProperty]
    private DisplayElement? _selectedElement;

    [ObservableProperty]
    private double _canvasWidth = 1920;

    [ObservableProperty]
    private double _canvasHeight = 1080;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    public StandaloneDesignerViewModel(ILayoutService layoutService, ILogger<StandaloneDesignerViewModel> logger)
    {
        _layoutService = layoutService;
        _logger = logger;

        CurrentLayout.Elements ??= new List<DisplayElement>();

        if (CurrentLayout.Elements.Count > 0)
        {
            foreach (var element in CurrentLayout.Elements)
            {
                Elements.Add(element);
            }
        }

        CanvasWidth = CurrentLayout.Resolution.Width;
        CanvasHeight = CurrentLayout.Resolution.Height;
        HasUnsavedChanges = false;
    }

    [RelayCommand]
    private void CreateNewLayout()
    {
        CurrentLayout = CreateDefaultLayout($"Standalone Layout {DateTime.Now:HHmmss}");
        Elements.Clear();
        HasUnsavedChanges = false;
        CanvasWidth = CurrentLayout.Resolution.Width;
        CanvasHeight = CurrentLayout.Resolution.Height;
        _logger.LogInformation("Standalone designer created new layout {LayoutName}", CurrentLayout.Name);
    }

    [RelayCommand]
    private void AddTextElement()
    {
        var element = new DisplayElement
        {
            Type = "text",
            Name = $"Text {Elements.Count + 1}",
            Position = new Position { X = 100 + Elements.Count * 20, Y = 100 },
            Size = new Size { Width = 300, Height = 120 },
            ZIndex = Elements.Count + 1
        };
        element["Content"] = "New text";
        element["FontSize"] = 32.0;
        element["Color"] = "#111111";

        Elements.Add(element);
        CurrentLayout.Elements.Add(element);
        SelectedElement = element;
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void AddRectangleElement()
    {
        var element = new DisplayElement
        {
            Type = "rectangle",
            Name = $"Rectangle {Elements.Count + 1}",
            Position = new Position { X = 150 + Elements.Count * 25, Y = 200 },
            Size = new Size { Width = 250, Height = 150 },
            ZIndex = Elements.Count + 1
        };
        element["Fill"] = "#3498DB";
        element["Stroke"] = "#1B4F72";
        element["StrokeThickness"] = 3.0;

        Elements.Add(element);
        CurrentLayout.Elements.Add(element);
        SelectedElement = element;
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedElement == null)
            return;

        Elements.Remove(SelectedElement);
        CurrentLayout.Elements.Remove(SelectedElement);
        SelectedElement = null;
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private async Task SaveLayoutAsync()
    {
        try
        {
        CurrentLayout.Elements = Elements.ToList();
        CurrentLayout.Modified = DateTime.UtcNow;

            Result<DisplayLayout> result;
            if (string.IsNullOrWhiteSpace(CurrentLayout.Id))
            {
                CurrentLayout.Id = Guid.NewGuid().ToString();
                result = await _layoutService.CreateLayoutAsync(CurrentLayout);
            }
            else
            {
                result = await _layoutService.UpdateLayoutAsync(CurrentLayout);
            }

            if (result.IsSuccess)
            {
                HasUnsavedChanges = false;
                _logger.LogInformation("Standalone layout {LayoutName} saved successfully", result.Value.Name);
            }
            else
            {
                _logger.LogWarning("Failed to save standalone layout: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while saving standalone layout");
        }
    }

    public void UpdateElementPosition(DisplayElement element, double x, double y)
    {
        if (element == null) return;

        var clampedX = Math.Max(0, Math.Min(x, CanvasWidth - element.Size.Width));
        var clampedY = Math.Max(0, Math.Min(y, CanvasHeight - element.Size.Height));

        element.Position.X = clampedX;
        element.Position.Y = clampedY;
        HasUnsavedChanges = true;
    }

    public void UpdateCanvasSize(double width, double height)
    {
        CanvasWidth = width;
        CanvasHeight = height;
        CurrentLayout.Resolution.Width = (int)Math.Round(width);
        CurrentLayout.Resolution.Height = (int)Math.Round(height);
        HasUnsavedChanges = true;
    }

    partial void OnCanvasWidthChanged(double value)
    {
        if (value <= 0) return;
        CurrentLayout.Resolution.Width = (int)Math.Round(value);
        HasUnsavedChanges = true;
    }

    partial void OnCanvasHeightChanged(double value)
    {
        if (value <= 0) return;
        CurrentLayout.Resolution.Height = (int)Math.Round(value);
        HasUnsavedChanges = true;
    }

    private static DisplayLayout CreateDefaultLayout(string name)
    {
        return new DisplayLayout
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            Resolution = new Resolution { Width = 1920, Height = 1080 },
            Elements = new List<DisplayElement>()
        };
    }
}
