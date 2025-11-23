using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.App.Mobile.Services;
using DigitalSignage.Core.Models;

namespace DigitalSignage.App.Mobile.ViewModels;

/// <summary>
/// View model for the device list page.
/// Loads devices via REST API and updates via WebSocket.
/// </summary>
public partial class DeviceListViewModel : BaseViewModel
{
	private readonly IApiService _apiService;
	private readonly IWebSocketService? _webSocketService;

	[ObservableProperty]
	private ObservableCollection<ClientInfo> _devices = new();

	[ObservableProperty]
	private ObservableCollection<ClientInfo> _filteredDevices = new();

	[ObservableProperty]
	private ClientInfo? _selectedDevice;

	[ObservableProperty]
	private bool _isRefreshing;

	[ObservableProperty]
	private int _onlineDeviceCount;

	[ObservableProperty]
	private int _totalDeviceCount;

	[ObservableProperty]
	private string _searchText = string.Empty;

	[ObservableProperty]
	private string _selectedFilter = "All";

	public List<string> FilterOptions { get; } = new() { "All", "Online", "Offline", "Warning", "Error" };

	public DeviceListViewModel(IApiService apiService, IWebSocketService? webSocketService = null)
	{
		_apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
		_webSocketService = webSocketService; // Optional - for real-time updates

		Title = "Devices";
	}

	/// <summary>
	/// Loads devices when the view appears.
	/// </summary>
	public async Task InitializeAsync()
	{
		await LoadDevicesAsync();
	}

	[RelayCommand]
	private async Task RefreshAsync()
	{
		IsRefreshing = true;

		await ExecuteAsync(async () =>
		{
			await LoadDevicesAsync();
		}, "Failed to refresh device list");

		IsRefreshing = false;
	}

	[RelayCommand]
	private async Task DeviceSelectedAsync(ClientInfo? device)
	{
		if (device == null)
			return;

		// Navigate to device detail page with the selected device
		var navigationParameter = new Dictionary<string, object>
		{
			["Device"] = device
		};

		await Shell.Current.GoToAsync("devicedetail", navigationParameter);
	}

	[RelayCommand]
	private void SelectedFilter(string filter)
	{
		SelectedFilter = filter;
	}

	/// <summary>
	/// Loads devices from the server via REST API.
	/// </summary>
	private async Task LoadDevicesAsync()
	{
		try
		{
			// Fetch all devices from server
			var deviceDtos = await _apiService.GetDevicesAsync();

			// Convert DeviceDto to ClientInfo
			var clientInfoList = deviceDtos.Select(dto => new ClientInfo
			{
				Id = dto.Id,
				Name = dto.Name,
				IpAddress = dto.IpAddress,
				Status = ParseDeviceStatus(dto.Status),
				Resolution = dto.Resolution,
				LastSeen = dto.LastSeen,
				AssignedLayoutId = dto.CurrentLayoutId,
				AssignedLayoutName = dto.CurrentLayoutName,
				Location = dto.Location,
				DeviceInfo = new DeviceInfoData
				{
					CpuUsage = dto.CpuUsage,
					MemoryUsage = dto.MemoryUsage,
					Temperature = dto.Temperature,
					DiskUsage = dto.DiskUsage,
					OsVersion = dto.OperatingSystem,
					AppVersion = null // Not available in DeviceDto
				}
			}).ToList();

			// Update collection on UI thread
			MainThread.BeginInvokeOnMainThread(() =>
			{
				Devices.Clear();
				foreach (var client in clientInfoList)
				{
					Devices.Add(client);
				}
				UpdateCounts();
			});
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error loading devices: {ex.Message}");
			// Collection remains unchanged on error
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

	private void UpdateCounts()
	{
		TotalDeviceCount = Devices.Count;
		OnlineDeviceCount = Devices.Count(d => d.Status == DeviceStatus.Online);
	}

	partial void OnDevicesChanged(ObservableCollection<ClientInfo> value)
	{
		UpdateCounts();
		ApplyFilters();
	}

	partial void OnSearchTextChanged(string value)
	{
		ApplyFilters();
	}

	partial void OnSelectedFilterChanged(string value)
	{
		ApplyFilters();
	}

	/// <summary>
	/// Applies search and filter to the device list
	/// </summary>
	private void ApplyFilters()
	{
		var filtered = Devices.AsEnumerable();

		// Apply status filter
		if (SelectedFilter != "All")
		{
			filtered = SelectedFilter switch
			{
				"Online" => filtered.Where(d => d.Status == DeviceStatus.Online),
				"Offline" => filtered.Where(d => d.Status == DeviceStatus.Offline),
				"Warning" => filtered.Where(d => d.Status == DeviceStatus.Warning),
				"Error" => filtered.Where(d => d.Status == DeviceStatus.Error),
				_ => filtered
			};
		}

		// Apply search text filter
		if (!string.IsNullOrWhiteSpace(SearchText))
		{
			var searchLower = SearchText.ToLowerInvariant();
			filtered = filtered.Where(d =>
				(d.Name?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(d.IpAddress?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(d.Location?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false));
		}

		// Update filtered collection on UI thread
		MainThread.BeginInvokeOnMainThread(() =>
		{
			FilteredDevices.Clear();
			foreach (var device in filtered)
			{
				FilteredDevices.Add(device);
			}
		});
	}
}
