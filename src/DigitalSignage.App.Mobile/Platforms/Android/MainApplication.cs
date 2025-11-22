using Android.App;
using Android.Runtime;

namespace DigitalSignage.App.Mobile;

/// <summary>
/// Android MainApplication.
/// </summary>
[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
