namespace DigitalSignage.App.Mobile;

/// <summary>
/// Main application class.
/// </summary>
public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Set initial theme based on system preference
		UserAppTheme = AppTheme.Unspecified; // Follow system setting

		MainPage = new AppShell();
	}

	protected override async void OnStart()
	{
		base.OnStart();

		// Load theme preference from settings
		try
		{
			var secureStorage = Handler?.MauiContext?.Services.GetService<Services.ISecureStorageService>();
			if (secureStorage != null)
			{
				var darkModeStr = await secureStorage.GetAsync("DarkMode");
				if (!string.IsNullOrEmpty(darkModeStr))
				{
					UserAppTheme = darkModeStr == "true" ? AppTheme.Dark : AppTheme.Light;
				}
			}
		}
		catch
		{
			// Ignore errors loading theme preference
		}
	}
}
