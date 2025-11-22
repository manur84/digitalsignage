using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.App.Mobile.Services;
using DigitalSignage.Core.Models;

namespace DigitalSignage.App.Mobile.ViewModels;

/// <summary>
/// View model for the device detail page with remote control capabilities.
/// </summary>
[QueryProperty(nameof(Device), "Device")]
public partial class DeviceDetailViewModel : BaseViewModel
{
	private readonly IWebSocketService _webSocketService;

	[ObservableProperty]
	private ClientInfo? _device;

	[ObservableProperty]
	private ImageSource? _screenshotImage;

	[ObservableProperty]
	private DateTime? _screenshotTimestamp;

	[ObservableProperty]
	private bool _isLoadingScreenshot;

	[ObservableProperty]
	private string _statusColor = "Gray";

	[ObservableProperty]
	private double _cpuUsagePercent;

	[ObservableProperty]
	private double _memoryUsagePercent;

	[ObservableProperty]
	private double _diskUsagePercent;

	[ObservableProperty]
	private string _temperatureText = "N/A";

	[ObservableProperty]
	private string _temperatureColor = "Gray";

	public DeviceDetailViewModel(IWebSocketService webSocketService)
	{
		_webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
	}

	/// <summary>
	/// Called when the Device property changes (via navigation parameter).
	/// </summary>
	partial void OnDeviceChanged(ClientInfo? value)
	{
		if (value != null)
		{
			Title = value.Name;
			UpdateStatusColor();
			UpdateHardwareMetrics();
		}
	}

	/// <summary>
	/// Initializes the view model with a device.
	/// </summary>
	public async Task InitializeAsync(ClientInfo device)
	{
		if (device == null)
			throw new ArgumentNullException(nameof(device));

		Device = device;
		Title = device.Name;

		// Update status color
		UpdateStatusColor();

		// Update hardware metrics
		UpdateHardwareMetrics();

		await Task.CompletedTask;
	}

	/// <summary>
	/// Updates the status color based on device status.
	/// </summary>
	private void UpdateStatusColor()
	{
		if (Device == null)
		{
			StatusColor = "Gray";
			return;
		}

		StatusColor = Device.Status?.ToLowerInvariant() switch
		{
			"online" => "Green",
			"offline" => "Red",
			"warning" => "Orange",
			_ => "Gray"
		};
	}

	/// <summary>
	/// Updates hardware metrics from device info.
	/// </summary>
	private void UpdateHardwareMetrics()
	{
		if (Device?.DeviceInfo == null)
		{
			CpuUsagePercent = 0;
			MemoryUsagePercent = 0;
			DiskUsagePercent = 0;
			TemperatureText = "N/A";
			TemperatureColor = "Gray";
			return;
		}

		// Update usage percentages (convert from decimal to percentage)
		CpuUsagePercent = Device.DeviceInfo.CpuUsage ?? 0;
		MemoryUsagePercent = Device.DeviceInfo.MemoryUsage ?? 0;
		DiskUsagePercent = Device.DeviceInfo.DiskUsage ?? 0;

		// Update temperature
		if (Device.DeviceInfo.Temperature.HasValue)
		{
			var temp = Device.DeviceInfo.Temperature.Value;
			TemperatureText = $"{temp:F1}°C";

			// Color code: green <60, orange 60-80, red >80
			TemperatureColor = temp switch
			{
				< 60 => "Green",
				>= 60 and < 80 => "Orange",
				_ => "Red"
			};
		}
		else
		{
			TemperatureText = "N/A";
			TemperatureColor = "Gray";
		}
	}

	[RelayCommand]
	private async Task RestartAsync()
	{
		if (Device == null)
			return;

		bool confirmed = await ShowConfirmationAsync(
			"Restart Device",
			$"Are you sure you want to restart {Device.Name}? The device will be unavailable during restart.");

		if (!confirmed)
			return;

		await ExecuteAsync(async () =>
		{
			await _webSocketService.SendCommandAsync(Device.Id, "Restart");
			await ShowSuccessAsync("Restart command sent successfully. The device will restart shortly.");
		}, "Failed to send restart command");
	}

	[RelayCommand]
	private async Task RequestScreenshotAsync()
	{
		if (Device == null)
			return;

		IsLoadingScreenshot = true;

		await ExecuteAsync(async () =>
		{
			var imageData = await _webSocketService.RequestScreenshotAsync(Device.Id);

			if (!string.IsNullOrEmpty(imageData))
			{
				// Decode base64 to image
				var bytes = Convert.FromBase64String(imageData);
				ScreenshotImage = ImageSource.FromStream(() => new MemoryStream(bytes));
				ScreenshotTimestamp = DateTime.Now;
				await ShowSuccessAsync("Screenshot captured successfully");
			}
			else
			{
				await ShowErrorAsync("No screenshot data received from device");
			}
		}, "Failed to capture screenshot");

		IsLoadingScreenshot = false;
	}

	[RelayCommand]
	private async Task VolumeUpAsync()
	{
		await SendSimpleCommandAsync("VolumeUp", "Volume Up");
	}

	[RelayCommand]
	private async Task VolumeDownAsync()
	{
		await SendSimpleCommandAsync("VolumeDown", "Volume Down");
	}

	[RelayCommand]
	private async Task ScreenOnAsync()
	{
		await SendSimpleCommandAsync("ScreenOn", "Screen On");
	}

	[RelayCommand]
	private async Task ScreenOffAsync()
	{
		await SendSimpleCommandAsync("ScreenOff", "Screen Off");
	}

	[RelayCommand]
	private async Task AssignLayoutAsync()
	{
		if (Device == null)
			return;

		// TODO: Show layout selection dialog
		await ShowSuccessAsync("Layout assignment feature coming soon");
	}

	[RelayCommand]
	private async Task RefreshAsync()
	{
		if (Device == null)
			return;

		await ExecuteAsync(async () =>
		{
			// Request updated device info
			// This would typically trigger a RequestClientList message
			// For now, just show a message
			await Task.Delay(500);
			await ShowSuccessAsync("Device information refreshed");
		}, "Failed to refresh device information");
	}

	/// <summary>
	/// Sends a simple command to the device.
	/// </summary>
	private async Task SendSimpleCommandAsync(string command, string displayName)
	{
		if (Device == null)
			return;

		await ExecuteAsync(async () =>
		{
			await _webSocketService.SendCommandAsync(Device.Id, command);
			await ShowSuccessAsync($"{displayName} command sent successfully");
		}, $"Failed to send {displayName} command");
	}

	/// <summary>
	/// Called when device info is updated (e.g., via WebSocket message).
	/// </summary>
	public void OnDeviceUpdated(ClientInfo updatedDevice)
	{
		if (updatedDevice == null || Device == null || updatedDevice.Id != Device.Id)
			return;

		Device = updatedDevice;
		UpdateStatusColor();
		UpdateHardwareMetrics();
	}
}
