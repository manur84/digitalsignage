using DigitalSignage.App.Mobile.ViewModels;
using DigitalSignage.Core.Models;

namespace DigitalSignage.App.Mobile.Views;

/// <summary>
/// Device detail page with remote control capabilities.
/// </summary>
public partial class DeviceDetailPage : ContentPage
{
	public DeviceDetailPage()
	{
		InitializeComponent();
	}

	protected override void OnHandlerChanged()
	{
		base.OnHandlerChanged();

		if (Handler?.MauiContext != null && BindingContext == null)
		{
			BindingContext = Handler.MauiContext.Services.GetService<DeviceDetailViewModel>();
		}
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
