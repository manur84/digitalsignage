using DigitalSignage.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace DigitalSignage.Server.Services;

/// <summary>
/// WPF implementation of IDialogService using MessageBox.
/// Ensures all dialogs are shown on the UI thread.
/// </summary>
public class DialogService : IDialogService
{
    private readonly ILogger<DialogService> _logger;

    public DialogService(ILogger<DialogService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task ShowInformationAsync(string message, string? title = null)
    {
        return ShowMessageBoxAsync(
            message,
            title ?? "Information",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <inheritdoc/>
    public Task ShowErrorAsync(string message, string? title = null)
    {
        _logger.LogError("Error dialog shown: {Message}", message);
        return ShowMessageBoxAsync(
            message,
            title ?? "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    /// <inheritdoc/>
    public Task ShowWarningAsync(string message, string? title = null)
    {
        _logger.LogWarning("Warning dialog shown: {Message}", message);
        return ShowMessageBoxAsync(
            message,
            title ?? "Warning",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    /// <inheritdoc/>
    public async Task<bool> ShowConfirmationAsync(string message, string? title = null)
    {
        var result = await ShowMessageBoxAsync(
            message,
            title ?? "Confirm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    /// <inheritdoc/>
    public async Task<bool?> ShowYesNoCancelAsync(string message, string? title = null)
    {
        var result = await ShowMessageBoxAsync(
            message,
            title ?? "Confirm",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return result switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null
        };
    }

    /// <inheritdoc/>
    public Task ShowValidationErrorAsync(string message, string? title = null)
    {
        return ShowMessageBoxAsync(
            message,
            title ?? "Validation Error",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    /// <summary>
    /// Shows a MessageBox on the UI thread and returns the result
    /// </summary>
    private Task<MessageBoxResult> ShowMessageBoxAsync(
        string message,
        string title,
        MessageBoxButton button,
        MessageBoxImage icon)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher == null)
        {
            _logger.LogError("Application dispatcher is null, cannot show dialog");
            return Task.FromResult(MessageBoxResult.None);
        }

        // Check if already on UI thread to avoid unnecessary context switch
        if (dispatcher.CheckAccess())
        {
            return Task.FromResult(MessageBox.Show(message, title, button, icon));
        }
        else
        {
            return dispatcher.InvokeAsync(() => MessageBox.Show(message, title, button, icon)).Task;
        }
    }
}
