using DigitalSignage.App.Mobile.ViewModels;
using DigitalSignage.Core.Models;

namespace DigitalSignage.App.Mobile.Views;

/// <summary>
/// Device detail page with remote control capabilities.
/// </summary>
public partial class DeviceDetailPage : ContentPage
{
	private readonly DeviceDetailViewModel _viewModel;

	public DeviceDetailPage(DeviceDetailViewModel viewModel)
	{
		InitializeComponent();

		_viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		BindingContext = _viewModel;
	}

	/// <summary>
	/// Called when the page appears.
	/// </summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Initialize the view model with device from navigation parameter
		if (BindingContext is DeviceDetailViewModel vm)
		{
			// Device will be set via QueryProperty
			await Task.CompletedTask;
		}
	}
}
