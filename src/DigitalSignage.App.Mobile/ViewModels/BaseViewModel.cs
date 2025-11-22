using CommunityToolkit.Mvvm.ComponentModel;

namespace DigitalSignage.App.Mobile.ViewModels;

/// <summary>
/// Base view model for all view models in the application.
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
	[ObservableProperty]
	private bool _isBusy;

	[ObservableProperty]
	private string? _title;

	[ObservableProperty]
	private string? _errorMessage;

	/// <summary>
	/// Executes an async operation with error handling and busy state management.
	/// </summary>
	protected async Task ExecuteAsync(Func<Task> operation, string? errorMessage = null)
	{
		if (IsBusy)
			return;

		IsBusy = true;
		ErrorMessage = null;

		try
		{
			await operation();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error in {GetType().Name}: {ex.Message}");
			ErrorMessage = errorMessage ?? ex.Message;
			await ShowErrorAsync(ErrorMessage);
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Shows an error message to the user.
	/// </summary>
	protected virtual async Task ShowErrorAsync(string message)
	{
		if (Application.Current?.MainPage != null)
		{
			await Application.Current.MainPage.DisplayAlert("Error", message, "OK");
		}
	}

	/// <summary>
	/// Shows a success message to the user.
	/// </summary>
	protected virtual async Task ShowSuccessAsync(string message)
	{
		if (Application.Current?.MainPage != null)
		{
			await Application.Current.MainPage.DisplayAlert("Success", message, "OK");
		}
	}

	/// <summary>
	/// Shows a confirmation dialog.
	/// </summary>
	protected virtual async Task<bool> ShowConfirmationAsync(string title, string message)
	{
		if (Application.Current?.MainPage != null)
		{
			return await Application.Current.MainPage.DisplayAlert(title, message, "Yes", "No");
		}
		return false;
	}
}
