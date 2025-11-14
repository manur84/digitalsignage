using System.Threading.Tasks;

namespace DigitalSignage.Core.Interfaces;

/// <summary>
/// Interface for showing dialogs to the user.
/// Decouples ViewModels from direct UI dependencies (MessageBox, etc.)
/// allowing for better testability and MVVM compliance.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows an information message to the user
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="title">The dialog title (optional, defaults to "Information")</param>
    Task ShowInformationAsync(string message, string? title = null);

    /// <summary>
    /// Shows an error message to the user
    /// </summary>
    /// <param name="message">The error message to display</param>
    /// <param name="title">The dialog title (optional, defaults to "Error")</param>
    Task ShowErrorAsync(string message, string? title = null);

    /// <summary>
    /// Shows a warning message to the user
    /// </summary>
    /// <param name="message">The warning message to display</param>
    /// <param name="title">The dialog title (optional, defaults to "Warning")</param>
    Task ShowWarningAsync(string message, string? title = null);

    /// <summary>
    /// Shows a confirmation dialog and returns the user's choice
    /// </summary>
    /// <param name="message">The confirmation message</param>
    /// <param name="title">The dialog title (optional, defaults to "Confirm")</param>
    /// <returns>True if user confirmed (Yes/OK), false otherwise</returns>
    Task<bool> ShowConfirmationAsync(string message, string? title = null);

    /// <summary>
    /// Shows a Yes/No/Cancel dialog and returns the user's choice
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="title">The dialog title (optional, defaults to "Confirm")</param>
    /// <returns>True for Yes, False for No, null for Cancel</returns>
    Task<bool?> ShowYesNoCancelAsync(string message, string? title = null);

    /// <summary>
    /// Shows a validation error message
    /// </summary>
    /// <param name="message">The validation error message</param>
    /// <param name="title">The dialog title (optional, defaults to "Validation Error")</param>
    Task ShowValidationErrorAsync(string message, string? title = null);
}
