namespace DigitalSignage.App.Mobile;

/// <summary>
/// Main application class.
/// </summary>
public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		MainPage = new AppShell();
	}
}
