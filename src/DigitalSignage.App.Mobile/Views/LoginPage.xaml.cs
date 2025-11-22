using DigitalSignage.App.Mobile.ViewModels;

namespace DigitalSignage.App.Mobile.Views;

/// <summary>
/// Login page for server discovery and connection.
/// </summary>
public partial class LoginPage : ContentPage
{
	public LoginPage(LoginViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
