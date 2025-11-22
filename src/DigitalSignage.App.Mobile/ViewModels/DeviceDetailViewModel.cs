using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.App.Mobile.Services;
using DigitalSignage.Core.Models;

namespace DigitalSignage.App.Mobile.ViewModels;

/// <summary>
/// View model for the device detail page with remote control capabilities.
/// Uses WebSocket for commands with REST API as fallback.
/// </summary>
[QueryProperty(nameof(Device), "Device")]
public partial class DeviceDetailViewModel : BaseViewModel
{
	private readonly IApiService _apiService;
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

	public DeviceDetailViewModel(IApiService apiService, IWebSocketService webSocketService)
	{
		_apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
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

		StatusColor = Device.Status switch
		{
			DeviceStatus.Online => "Green",
			DeviceStatus.Offline => "Red",
			DeviceStatus.Warning => "Orange",
			DeviceStatus.Error => "Red",
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
			await SendCommandWithFallbackAsync("Restart");
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
			string? imageData = null;

			// Try REST API first (more reliable for screenshots)
			try
			{
				var screenshotResponse = await _apiService.RequestScreenshotAsync(Device.Id);
				imageData = screenshotResponse.ImageBase64;
				ScreenshotTimestamp = screenshotResponse.CapturedAt;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"REST API screenshot failed, trying WebSocket: {ex.Message}");

				// Fallback to WebSocket
				try
				{
					imageData = await _webSocketService.RequestScreenshotAsync(Device.Id);
					ScreenshotTimestamp = DateTime.Now;
				}
				catch (Exception wsEx)
				{
					Console.WriteLine($"WebSocket screenshot also failed: {wsEx.Message}");
					throw; // Re-throw to be caught by ExecuteAsync
				}
			}

			if (!string.IsNullOrEmpty(imageData))
			{
				// Decode base64 to image
				var bytes = Convert.FromBase64String(imageData);
				ScreenshotImage = ImageSource.FromStream(() => new MemoryStream(bytes));
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
			// Fetch updated device info from REST API
			try
			{
				var deviceDetail = await _apiService.GetDeviceByIdAsync(Device.Id);

				// Update device information
				Device.Name = deviceDetail.Name;
				Device.Status = ParseDeviceStatus(deviceDetail.Status);
				Device.IpAddress = deviceDetail.IpAddress;
				Device.Location = deviceDetail.Location;
				Device.LastSeen = deviceDetail.LastSeen;
				Device.AssignedLayoutId = deviceDetail.CurrentLayoutId;
				Device.AssignedLayoutName = deviceDetail.CurrentLayoutName;

				if (Device.DeviceInfo == null)
					Device.DeviceInfo = new DeviceInfoData();

				Device.DeviceInfo.CpuUsage = deviceDetail.CpuUsage;
				Device.DeviceInfo.MemoryUsage = deviceDetail.MemoryUsage;
				Device.DeviceInfo.Temperature = deviceDetail.Temperature;
				Device.DeviceInfo.DiskUsage = deviceDetail.DiskUsage;
				Device.DeviceInfo.OsVersion = deviceDetail.OperatingSystem;

				UpdateStatusColor();
				UpdateHardwareMetrics();

				await ShowSuccessAsync("Device information refreshed");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error refreshing device info: {ex.Message}");
				throw;
			}
		}, "Failed to refresh device information");
	}

	/// <summary>
	/// Sends a simple command to the device.
	/// Uses WebSocket with REST API fallback.
	/// </summary>
	private async Task SendSimpleCommandAsync(string command, string displayName)
	{
		if (Device == null)
			return;

		await ExecuteAsync(async () =>
		{
			await SendCommandWithFallbackAsync(command);
			await ShowSuccessAsync($"{displayName} command sent successfully");
		}, $"Failed to send {displayName} command");
	}

	/// <summary>
	/// Sends a command to the device with fallback from WebSocket to REST API.
	/// </summary>
	private async Task SendCommandWithFallbackAsync(string command)
	{
		if (Device == null)
			throw new InvalidOperationException("Device is null");

		try
		{
			// Try WebSocket first (faster, real-time)
			await _webSocketService.SendCommandAsync(Device.Id, command);
			Console.WriteLine($"Command '{command}' sent via WebSocket");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"WebSocket command failed, trying REST API: {ex.Message}");

			// Fallback to REST API
			try
			{
				var response = await _apiService.SendCommandAsync(Device.Id, command);
				if (!response.Success)
					throw new InvalidOperationException(response.Message);

				Console.WriteLine($"Command '{command}' sent via REST API");
			}
			catch (Exception apiEx)
			{
				Console.WriteLine($"REST API command also failed: {apiEx.Message}");
				throw; // Re-throw to be caught by ExecuteAsync
			}
		}
	}

	/// <summary>
	/// Parses device status string to enum.
	/// </summary>
	private DeviceStatus ParseDeviceStatus(string status)
	{
		return status?.ToLowerInvariant() switch
		{
			"online" => DeviceStatus.Online,
			"offline" => DeviceStatus.Offline,
			"error" => DeviceStatus.Error,
			"warning" => DeviceStatus.Warning,
			_ => DeviceStatus.Offline
		};
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
