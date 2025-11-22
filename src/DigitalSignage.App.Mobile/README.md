# Digital Signage Mobile App

.NET MAUI cross-platform mobile app for managing and controlling Digital Signage clients.

## Status: MVP Complete ✅

The mobile app MVP (Minimum Viable Product) has been implemented with core functionality:

- **Auto-Discovery**: mDNS/Bonjour server discovery on local network
- **Authentication**: Secure registration with server
- **WebSocket Communication**: Real-time bidirectional communication
- **Device List**: View all connected devices (basic)
- **Secure Storage**: Token and settings stored securely

## Features

### Implemented ✅

- **Server Discovery** (mDNS/Bonjour)
  - Automatic detection of Digital Signage servers on local network
  - Manual server entry as fallback
  - SSL/TLS support

- **Authentication**
  - Device registration with server
  - Secure token storage
  - Optional registration token

- **WebSocket Communication**
  - Real-time connection to server
  - Auto-reconnect logic
  - Message handling infrastructure

- **Basic UI**
  - Login page with server discovery
  - Device list page (basic)
  - MVVM architecture
  - Material-inspired design

### Pending ⏳

- Device Details page
- Remote control commands (Restart, Screenshot, etc.)
- Layout assignment
- Schedule management
- Push notifications
- Biometric authentication (Face ID/Touch ID)
- Advanced filtering and search

## Architecture

### Technology Stack

- **.NET MAUI 8.0** - Cross-platform framework
- **C# 12** - Programming language
- **MVVM** - Architecture pattern
- **CommunityToolkit.Mvvm** - MVVM helpers
- **Zeroconf** - mDNS/Bonjour client
- **System.Net.WebSockets** - WebSocket communication
- **SecureStorage** - Secure credential storage

### Project Structure

```
DigitalSignage.App.Mobile/
├── Models/                    # Data models
│   ├── AppSettings.cs
│   └── DiscoveredServer.cs
├── Services/                  # Business logic
│   ├── ISecureStorageService.cs
│   ├── SecureStorageService.cs
│   ├── IAuthenticationService.cs
│   ├── AuthenticationService.cs
│   ├── IServerDiscoveryService.cs
│   ├── ServerDiscoveryService.cs
│   ├── IWebSocketService.cs
│   └── WebSocketService.cs
├── ViewModels/                # MVVM ViewModels
│   ├── BaseViewModel.cs
│   ├── LoginViewModel.cs
│   └── DeviceListViewModel.cs
├── Views/                     # XAML Pages
│   ├── LoginPage.xaml
│   └── DeviceListPage.xaml
├── Converters/                # Value converters
│   ├── InvertedBoolConverter.cs
│   └── DeviceStatusToColorConverter.cs
├── Platforms/                 # Platform-specific code
│   ├── iOS/
│   │   ├── Info.plist
│   │   ├── AppDelegate.cs
│   │   └── Program.cs
│   └── Android/
│       ├── AndroidManifest.xml
│       ├── MainActivity.cs
│       └── MainApplication.cs
├── Resources/                 # Images, fonts, styles
│   ├── Fonts/
│   ├── Images/
│   ├── Styles/
│   │   ├── Colors.xaml
│   │   └── Styles.xaml
│   ├── AppIcon/
│   └── Splash/
├── App.xaml                   # Application resources
├── AppShell.xaml             # Navigation shell
└── MauiProgram.cs            # DI configuration
```

## Building

### Prerequisites

- **.NET 8 SDK** or later
- **Visual Studio 2022** (Windows) or **Visual Studio for Mac** (macOS) or **Rider**
- **Xcode 15+** (for iOS development)
- **macOS** (required for iOS builds)

### Install MAUI Workload

```bash
dotnet workload install maui
```

### Build

```bash
cd src/DigitalSignage.App.Mobile
dotnet build
```

### Run on iOS Simulator

```bash
dotnet build -t:Run -f net8.0-ios
```

### Run on Android Emulator

```bash
dotnet build -t:Run -f net8.0-android
```

## iOS-Specific Configuration

### Info.plist Permissions

The following permissions are required:

- **NSLocalNetworkUsageDescription**: For mDNS/Bonjour server discovery
- **NSBonjourServices**: `_digitalsignage._tcp` service type
- **NSFaceIDUsageDescription**: For biometric authentication (future feature)

### Privacy Manifest (PrivacyInfo.xcprivacy)

Required for iOS 17+ App Store submission. Declares usage of UserDefaults API.

## Usage

### 1. Server Discovery

1. Ensure the Digital Signage server is running on the same network
2. Server must have mDNS/Bonjour enabled (NetworkDiscoveryService)
3. Launch the mobile app
4. Tap "Scan for Servers"
5. Select discovered server from list

### 2. Manual Connection

1. Tap "Or enter server manually"
2. Enter server IP and port (e.g., `192.168.1.100:8080`)
3. Optionally enter registration token
4. Tap "Connect"

### 3. Server Approval

After registration, the server admin must approve the mobile app:
1. Server shows pending registration in MobileAppService
2. Admin approves/denies via server UI (future feature)
3. App receives approval notification via WebSocket

## Development

### Adding a New Page

1. Create ViewModel in `ViewModels/`
2. Create XAML page in `Views/`
3. Register both in `MauiProgram.cs`
4. Add navigation route in `AppShell.xaml`

### Adding a New Service

1. Create interface `IMyService.cs` in `Services/`
2. Create implementation `MyService.cs`
3. Register in `MauiProgram.cs`:
   ```csharp
   builder.Services.AddSingleton<IMyService, MyService>();
   ```

### Dependency Injection

All services and ViewModels use DI. Constructor injection:

```csharp
public LoginViewModel(
    IServerDiscoveryService discoveryService,
    IAuthenticationService authService)
{
    _discoveryService = discoveryService;
    _authService = authService;
}
```

## Testing

### Unit Tests (Pending)

```bash
dotnet test tests/DigitalSignage.App.Mobile.Tests/
```

### Manual Testing Checklist

- [ ] Server discovery finds server
- [ ] Manual connection works
- [ ] Registration succeeds
- [ ] WebSocket connects
- [ ] Settings are saved securely
- [ ] App survives background/foreground cycle
- [ ] Network errors handled gracefully

## Known Issues

1. **Fonts**: Placeholder font files - replace with actual OpenSans fonts
2. **Icons**: Placeholder SVG icons - replace with proper design
3. **Build on Linux**: MAUI workload not supported on Linux (build on macOS/Windows)

## Next Steps

See `IMPLEMENTATION_PLAN.md` for detailed roadmap:

- [ ] Device Details page with remote controls
- [ ] Layout assignment UI
- [ ] Schedule management
- [ ] Push notifications
- [ ] Biometric authentication
- [ ] Offline mode improvements
- [ ] Advanced filtering/search
- [ ] Dark mode support

## License

Same as parent project.
