using DigitalSignage.App.Mobile.ViewModels;

namespace DigitalSignage.App.Mobile.Views;

/// <summary>
/// Device list page showing all connected devices.
/// </summary>
public partial class DeviceListPage : ContentPage
{
	public DeviceListPage()
	{
		InitializeComponent();
	}

	protected override void OnHandlerChanged()
	{
		base.OnHandlerChanged();

		if (Handler?.MauiContext != null && BindingContext == null)
		{
			BindingContext = Handler.MauiContext.Services.GetService<DeviceListViewModel>();
		}
	}
}
