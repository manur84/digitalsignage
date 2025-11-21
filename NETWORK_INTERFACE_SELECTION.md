# Network Interface Selection Feature

## Overview

This feature allows both the Windows Server and Raspberry Pi Client to select a preferred network interface for WebSocket server binding and auto-discovery.

## Server (Windows WPF)

### New Files

1. **`NetworkInterfaceInfo.cs`** (`DigitalSignage.Core/Models/`)
   - Model class representing network interface information
   - Properties: Name, Description, IpAddress, MacAddress, InterfaceType, IsOperational, Speed, IsLoopback

2. **`NetworkInterfaceService.cs`** (`DigitalSignage.Server/Services/`)
   - Service for detecting and managing network interfaces
   - `GetAllNetworkInterfaces()` - Returns list of all active IPv4 interfaces
   - `GetPreferredIPAddress(preferredInterface)` - Returns IP of preferred interface with fallback logic
   - `GetAllIPv4Addresses()` - Returns all non-loopback IPv4 addresses

### Modified Files

1. **`ServerSettings.cs`**
   - Added `PreferredNetworkInterface` property (string, nullable)
   - Can be interface name (e.g., "Ethernet", "Wi-Fi") or IP address
   - Empty/null = auto-select first available non-localhost interface

2. **`SettingsViewModel.cs`**
   - Added properties: `PreferredNetworkInterface`, `AvailableNetworkInterfaces`, `SelectedNetworkInterface`
   - Added `LoadNetworkInterfaces()` method
   - Added `RefreshNetworkInterfacesCommand`
   - Integrated with `SaveAsync()` to persist preferred interface

3. **`SettingsDialog.xaml`**
   - Added new "Network" tab
   - ComboBox for interface selection with detailed ItemTemplate
   - Refresh button to reload interfaces
   - Shows selected interface details (Name, IP, MAC, Type, Description)

4. **`ServiceCollectionExtensions.cs`**
   - Registered `NetworkInterfaceService` as Singleton in DI container

### Usage

1. Open Settings dialog in the WPF application
2. Navigate to "Network" tab
3. Select preferred network interface from dropdown
4. Click "Save"
5. Restart application for changes to take effect

The WebSocketCommunicationService and DiscoveryService will use the selected interface for binding and broadcasting.

## Client (Raspberry Pi Python)

### Modified Files

1. **`config.py`**
   - Added `preferred_network_interface: str = ""` field to Config dataclass
   - Empty string = auto-select (default behavior)
   - Example values: "eth0", "wlan0"

2. **`device_manager.py`**
   - Added `get_all_network_interfaces()` method
     - Returns dictionary mapping interface names to IP addresses
     - Uses psutil with netifaces fallback
     - Filters out loopback interfaces

   - Added `get_preferred_ip_address(preferred_interface)` method
     - Returns IP address of preferred interface
     - Supports exact match and partial match (e.g., "eth" matches "eth0")
     - Falls back to auto-select if preferred interface not found

### Usage

Edit `/opt/digitalsignage-client/config.json`:

```json
{
  "preferred_network_interface": "eth0"
}
```

Or leave empty for automatic selection:

```json
{
  "preferred_network_interface": ""
}
```

Restart the client service:

```bash
sudo systemctl restart digitalsignage-client
```

## Implementation Notes

### Server

- **Thread-safe**: NetworkInterfaceService uses .NET's `NetworkInterface.GetAllNetworkInterfaces()` which is thread-safe
- **Error handling**: All methods have proper try-catch blocks with logging
- **Fallback logic**: If preferred interface not found, automatically selects first available non-loopback interface
- **Match strategies**:
  1. Exact name match (case-insensitive)
  2. IP address match (exact)
  3. Partial name match (e.g., "Ethernet" matches "Ethernet 2")
  4. Fallback to first available

### Client

- **Compatibility**: Uses psutil (always available) with netifaces fallback
- **Error handling**: Comprehensive exception handling with logging
- **Backward compatible**: Empty string uses existing `get_ip_address()` auto-select logic
- **Match strategies**:
  1. Exact name match
  2. Prefix match (e.g., "eth" matches "eth0", "eth1")
  3. Fallback to auto-select

## Testing Checklist

- [x] ServerSettings loads/saves PreferredNetworkInterface
- [x] SettingsViewModel populates AvailableNetworkInterfaces
- [x] SettingsDialog UI shows network interfaces
- [x] NetworkInterfaceService registered in DI
- [ ] WebSocketCommunicationService uses preferred interface (optional - to be implemented)
- [ ] DiscoveryService uses preferred interface (optional - to be implemented)
- [ ] Python client respects preferred_network_interface config
- [ ] Build succeeds without errors
- [ ] Settings can be saved and loaded correctly

## Future Enhancements

1. **Automatic interface monitoring**: Detect when network interfaces go up/down and switch automatically
2. **Multi-interface binding**: Allow WebSocket server to bind to multiple interfaces simultaneously
3. **Interface priority list**: Instead of single preferred interface, allow ordered list with fallback
4. **Network performance metrics**: Show interface speed, latency, packet loss in UI
5. **Client auto-discovery enhancement**: Use preferred interface for UDP broadcast discovery

## Security Considerations

- No sensitive data stored in network interface preferences
- Interface selection does not bypass firewall rules or URL ACL requirements
- All network operations still subject to existing security constraints

## Performance Impact

- **Minimal**: Interface enumeration happens once at startup and on manual refresh
- **No runtime overhead**: Interface selection is a one-time configuration read
- **Async operations**: All I/O operations use async/await patterns

## Known Limitations

1. Changes require application restart to take effect
2. Server: Windows firewall rules still apply to selected interface
3. Server: URL ACL must be configured for the selected port
4. Client: Interface must have valid IPv4 address
5. Client: Loopback interface (127.0.0.1) is automatically excluded

## Support

For issues or questions:
- Check logs: Server (`logs/log-YYYYMMDD.txt`), Client (`sudo journalctl -u digitalsignage-client`)
- Verify interface exists: `ip addr show` (Linux) or `ipconfig /all` (Windows)
- Test connectivity: `ping` from client to server IP
