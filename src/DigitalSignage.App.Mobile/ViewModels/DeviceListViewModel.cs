using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Models;

namespace DigitalSignage.App.Mobile.ViewModels;

/// <summary>
/// View model for the device list page.
/// </summary>
public partial class DeviceListViewModel : BaseViewModel
{
	[ObservableProperty]
	private ObservableCollection<ClientInfo> _devices = new();

	[ObservableProperty]
	private ClientInfo? _selectedDevice;

	[ObservableProperty]
	private bool _isRefreshing;

	[ObservableProperty]
	private int _onlineDeviceCount;

	[ObservableProperty]
	private int _totalDeviceCount;

	public DeviceListViewModel()
	{
		Title = "Devices";
		LoadDevices();
	}

	[RelayCommand]
	private async Task RefreshAsync()
	{
		IsRefreshing = true;

		await ExecuteAsync(async () =>
		{
			// TODO: Fetch devices from server via WebSocket or HTTP
			await Task.Delay(1000); // Simulate network delay
			LoadDevices();
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

	private void LoadDevices()
	{
		// TODO: Load devices from server
		// For now, show empty state
		Devices.Clear();
		UpdateCounts();
	}

	private void UpdateCounts()
	{
		TotalDeviceCount = Devices.Count;
		OnlineDeviceCount = Devices.Count(d => d.Status == DeviceStatus.Online);
	}

	partial void OnDevicesChanged(ObservableCollection<ClientInfo> value)
	{
		UpdateCounts();
	}
}
