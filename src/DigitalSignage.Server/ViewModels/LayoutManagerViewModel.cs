using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace DigitalSignage.Server.ViewModels;

public partial class LayoutManagerViewModel : ObservableObject
{
    private readonly ILayoutService _layoutService;
    private readonly DeviceManagementViewModel _deviceManagementViewModel;
    private readonly ILogger<LayoutManagerViewModel> _logger;

    [ObservableProperty]
    private DisplayLayout? _selectedLayout;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Bereit";

    public ObservableCollection<DisplayLayout> Layouts { get; } = new();

    [ObservableProperty]
    private bool _canDelete;

    public LayoutManagerViewModel(
        ILayoutService layoutService,
        DeviceManagementViewModel deviceManagementViewModel,
        ILogger<LayoutManagerViewModel> logger)
    {
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _deviceManagementViewModel = deviceManagementViewModel ?? throw new ArgumentNullException(nameof(deviceManagementViewModel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _ = LoadLayoutsAsync();
    }

    [RelayCommand]
    private async Task LoadLayouts()
    {
        await LoadLayoutsAsync();
    }

    [RelayCommand]
    private async Task ImportSvg()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SVG Dateien (*.svg)|*.svg",
            Title = "SVG-Layout importieren",
            Multiselect = true
        };

        var result = dialog.ShowDialog();
        if (result != true)
        {
            return;
        }

        IsBusy = true;
        var successCount = 0;
        var failedCount = 0;

        foreach (var file in dialog.FileNames)
        {
            var imported = await ImportSingleSvgAsync(file);
            if (imported)
            {
                successCount++;
            }
            else
            {
                failedCount++;
            }
        }

        await LoadLayoutsAsync();
        _ = _deviceManagementViewModel.LoadLayoutsCommand.ExecuteAsync(null);

        StatusMessage = failedCount == 0
            ? $"SVG-Layout(s) importiert ({successCount})"
            : $"SVG-Import abgeschlossen: {successCount} ok, {failedCount} fehlgeschlagen";

        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(CanDeleteLayout))]
    private async Task DeleteLayout()
    {
        if (SelectedLayout == null) return;

        IsBusy = true;
        var layout = SelectedLayout;
        try
        {
            var result = await _layoutService.DeleteLayoutAsync(layout.Id);
            if (result.IsFailure)
            {
                StatusMessage = $"Löschen fehlgeschlagen: {result.ErrorMessage}";
                _logger.LogError("Failed to delete layout {LayoutId}: {Error}", layout.Id, result.ErrorMessage);
                return;
            }

            Layouts.Remove(layout);
            _logger.LogInformation("Deleted layout {LayoutId}", layout.Id);
            StatusMessage = $"Layout '{layout.Name}' gelöscht";

            _ = _deviceManagementViewModel.LoadLayoutsCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Löschen: {ex.Message}";
            _logger.LogError(ex, "DeleteLayout failed for {LayoutId}", layout.Id);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool CanDeleteLayout() => SelectedLayout != null && !IsBusy;

    public void OpenLayoutPreview(DisplayLayout? layout)
    {
        var target = layout ?? SelectedLayout;
        if (target == null) return;

        try
        {
            var window = new Views.LayoutManager.LayoutPreviewWindow(target);
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Vorschau konnte nicht geöffnet werden: {ex.Message}";
            _logger.LogError(ex, "Failed to open layout preview for {LayoutId}", target.Id);
        }
    }

    partial void OnSelectedLayoutChanged(DisplayLayout? value)
    {
        CanDelete = value != null;
        DeleteLayoutCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        DeleteLayoutCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadLayoutsAsync()
    {
        try
        {
            IsBusy = true;
            var result = await _layoutService.GetAllLayoutsAsync();
            if (result.IsFailure)
            {
                StatusMessage = $"Layouts konnten nicht geladen werden: {result.ErrorMessage}";
                _logger.LogError("Failed to load layouts: {Error}", result.ErrorMessage);
                return;
            }

            Layouts.Clear();
            foreach (var layout in result.Value.OrderBy(l => l.Name))
            {
                Layouts.Add(layout);
            }

            CanDelete = Layouts.Any();
            StatusMessage = $"Layouts geladen: {Layouts.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Laden der Layouts: {ex.Message}";
            _logger.LogError(ex, "Failed to load layouts");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> ImportSingleSvgAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("SVG file not found: {File}", filePath);
                return false;
            }

            var svgBytes = await File.ReadAllBytesAsync(filePath);
            if (svgBytes.Length == 0)
            {
                _logger.LogWarning("SVG file is empty: {File}", filePath);
                return false;
            }

            var fileName = Path.GetFileName(filePath);
            var layout = new DisplayLayout
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                Description = $"SVG-Layout importiert aus {fileName}",
                LayoutType = "svg",
                SvgContentBase64 = Convert.ToBase64String(svgBytes),
                SvgFileName = fileName,
                BackgroundColor = "#FFFFFF"
            };

            layout.Tags.Add("svg");

            var createResult = await _layoutService.CreateLayoutAsync(layout);
            if (createResult.IsFailure)
            {
                _logger.LogError("Failed to create SVG layout {Layout}: {Error}", fileName, createResult.ErrorMessage);
                return false;
            }

            _logger.LogInformation("Imported SVG layout {LayoutId} from {File}", createResult.Value.Id, fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import SVG layout from {File}", filePath);
            return false;
        }
    }
}
