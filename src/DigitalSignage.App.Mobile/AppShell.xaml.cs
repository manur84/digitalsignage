using DigitalSignage.App.Mobile.Views;

namespace DigitalSignage.App.Mobile;

/// <summary>
/// Application shell for navigation.
/// </summary>
public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Register routes for navigation
		Routing.RegisterRoute("devicedetail", typeof(DeviceDetailPage));
	}
}
