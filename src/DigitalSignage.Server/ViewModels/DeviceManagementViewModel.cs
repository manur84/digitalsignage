using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

public partial class DeviceManagementViewModel : ObservableObject
{
    private readonly IClientService _clientService;

    [ObservableProperty]
    private RaspberryPiClient? _selectedClient;

    [ObservableProperty]
    private bool _isLoading = false;

    public ObservableCollection<RaspberryPiClient> Clients { get; } = new();

    public DeviceManagementViewModel(IClientService clientService)
    {
        _clientService = clientService;
        _ = LoadClientsAsync();
    }

    [RelayCommand]
    private async Task LoadClients()
    {
        IsLoading = true;
        try
        {
            var clients = await _clientService.GetAllClientsAsync();
            Clients.Clear();
            foreach (var client in clients)
            {
                Clients.Add(client);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RestartClient()
    {
        if (SelectedClient == null) return;

        await _clientService.SendCommandAsync(
            SelectedClient.Id,
            ClientCommands.Restart);
    }

    [RelayCommand]
    private async Task RestartClientApp()
    {
        if (SelectedClient == null) return;

        await _clientService.SendCommandAsync(
            SelectedClient.Id,
            ClientCommands.RestartApp);
    }

    [RelayCommand]
    private async Task TakeScreenshot()
    {
        if (SelectedClient == null) return;

        await _clientService.SendCommandAsync(
            SelectedClient.Id,
            ClientCommands.Screenshot);
    }

    [RelayCommand]
    private async Task AssignLayout(string layoutId)
    {
        if (SelectedClient == null) return;

        await _clientService.AssignLayoutAsync(SelectedClient.Id, layoutId);
    }

    [RelayCommand]
    private async Task RemoveClient()
    {
        if (SelectedClient == null) return;

        await _clientService.RemoveClientAsync(SelectedClient.Id);
        await LoadClientsAsync();
    }
}
