using DigitalSignage.App.Mobile.ViewModels;

namespace DigitalSignage.App.Mobile.Views;

/// <summary>
/// Device list page showing all connected devices.
/// </summary>
public partial class DeviceListPage : ContentPage
{
	public DeviceListPage(DeviceListViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
