using DigitalSignage.App.Mobile.ViewModels;

namespace DigitalSignage.App.Mobile.Views;

/// <summary>
/// Login page for server discovery and connection.
/// </summary>
public partial class LoginPage : ContentPage
{
	public LoginPage()
	{
		InitializeComponent();
	}

	protected override void OnHandlerChanged()
	{
		base.OnHandlerChanged();

		if (Handler?.MauiContext != null && BindingContext == null)
		{
			BindingContext = Handler.MauiContext.Services.GetService<LoginViewModel>();
		}
	}
}
