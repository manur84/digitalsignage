using CommunityToolkit.Maui;
using DigitalSignage.App.Mobile.Services;
using DigitalSignage.App.Mobile.ViewModels;
using DigitalSignage.App.Mobile.Views;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.App.Mobile;

/// <summary>
/// MAUI program configuration and dependency injection setup.
/// </summary>
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Register Services (Singleton - live for app lifetime)
		builder.Services.AddSingleton<ISecureStorageService, SecureStorageService>();
		builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
		builder.Services.AddSingleton<IServerDiscoveryService, ServerDiscoveryService>();
		builder.Services.AddSingleton<IWebSocketService, WebSocketService>();

		// Register ViewModels (Transient - new instance each time)
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<DeviceListViewModel>();
		builder.Services.AddTransient<DeviceDetailViewModel>();

		// Register Views (Transient - new instance each time)
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<DeviceListPage>();
		builder.Services.AddTransient<DeviceDetailPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
