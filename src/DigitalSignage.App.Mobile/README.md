# Digital Signage Mobile App

.NET MAUI cross-platform mobile app for managing and controlling Digital Signage clients.

## Status: Enhanced ✨

The mobile app has been significantly improved with iOS compliance, modern design, and new features!

### Recent Improvements (2024-11)

#### ✅ Apple Guidelines Compliance
- **App Transport Security** improved (removed insecure arbitrary loads)
- **Privacy Manifest** complete with all required API declarations
- **Entitlements** properly configured (Keychain, Push Notifications)
- **Dark Mode** fully supported with automatic system theme detection

#### ✨ New Features
- **Settings Page** with preferences management
- **Dark Mode** toggle with live preview
- **Biometric Authentication** (Face ID/Touch ID)
- **Search & Filter** in device list
- **Modern Navigation** with Flyout menu

#### 🎨 Design Improvements
- Complete dark theme with optimized colors
- Modern UI following iOS Human Interface Guidelines
- Improved accessibility (44pt minimum touch targets)
- Smooth animations and transitions

## Features

### Core Features ✅

- **Server Discovery** (mDNS/Bonjour)
  - Automatic detection of Digital Signage servers on local network
  - Manual server entry as fallback
  - SSL/TLS support

- **Authentication**
  - Device registration with server
  - Secure token storage (Keychain)
  - Optional registration token
  - **NEW:** Biometric authentication (Face ID/Touch ID)

- **Device Management**
  - Real-time device list with status indicators
  - **NEW:** Search devices by name, IP, or location
  - **NEW:** Filter by status (All, Online, Offline, Warning, Error)
  - Pull-to-refresh for latest data
  - Device details with hardware metrics

- **Remote Control**
  - Restart device
  - Take screenshot
  - Volume control (up/down)
  - Screen control (on/off)
  - View hardware metrics (CPU, Memory, Disk, Temperature)

- **User Interface**
  - **NEW:** Dark mode support
  - Material-inspired design
  - **NEW:** Settings page
  - Flyout navigation menu
  - Responsive layouts

### Settings & Preferences 🆕

- **Dark Mode** - Toggle between light and dark themes
- **Biometric Auth** - Enable Face ID/Touch ID for secure access
- **Push Notifications** - Receive alerts for device events
- **Auto-Connect** - Automatically connect on app launch
- **Cache Management** - Clear offline data
- **Server Management** - Disconnect and manage connections

### Pending Features ⏳

- Layout assignment UI
- Schedule management
- Push notifications (full implementation)
- Bulk operations (multi-device commands)
- Advanced filtering and sorting
- iPad optimization (Split View)

## Architecture

### Technology Stack

- **.NET MAUI 8.0** - Cross-platform framework
- **C# 12** - Programming language
- **MVVM** - Architecture pattern (CommunityToolkit.Mvvm)
- **Zeroconf** - mDNS/Bonjour client
- **System.Net.WebSockets** - WebSocket communication
- **SecureStorage** - Secure credential storage (Keychain on iOS)

### Project Structure

```
DigitalSignage.App.Mobile/
├── Models/                    # Data models
├── Services/                  # Business logic
│   ├── AuthenticationService.cs (with Biometric support)
│   ├── SecureStorageService.cs
│   ├── ServerDiscoveryService.cs
│   ├── WebSocketService.cs
│   └── ApiService.cs
├── ViewModels/                # MVVM ViewModels
│   ├── BaseViewModel.cs
│   ├── LoginViewModel.cs
│   ├── DeviceListViewModel.cs (with Search/Filter)
│   ├── DeviceDetailViewModel.cs
│   └── SettingsViewModel.cs (NEW)
├── Views/                     # XAML Pages
│   ├── LoginPage.xaml
│   ├── DeviceListPage.xaml
│   ├── DeviceDetailPage.xaml
│   └── SettingsPage.xaml (NEW)
├── Converters/                # Value converters
│   ├── InvertedBoolConverter.cs
│   ├── DeviceStatusToColorConverter.cs
│   ├── FilterButtonColorConverter.cs (NEW)
│   └── ...
├── Platforms/                 # Platform-specific code
│   └── iOS/
│       ├── Info.plist (Enhanced)
│       ├── Entitlements.plist (Enhanced)
│       └── PrivacyInfo.xcprivacy (Complete)
├── Resources/                 # Images, fonts, styles
│   └── Styles/
│       ├── Colors.xaml (Dark Mode support)
│       └── Styles.xaml
├── AppShell.xaml             # Navigation (Flyout menu)
└── MauiProgram.cs            # DI configuration
```

## Building

### Prerequisites

- **.NET 8 SDK** or later
- **Visual Studio 2022** (Windows) or **Visual Studio for Mac** / **Rider**
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
- **NSFaceIDUsageDescription**: For biometric authentication

### Privacy Manifest (PrivacyInfo.xcprivacy)

Required for iOS 17+ App Store submission. Declares usage of:
- UserDefaults API (CA92.1)
- File Timestamp API (C617.1)
- System Boot Time API (35F9.1)
- Disk Space API (E174.1)

### App Transport Security

Configured to allow:
- Local networking only (secure)
- localhost for development
- No arbitrary loads (App Store compliant)

## Usage

### 1. Server Discovery

1. Ensure the Digital Signage server is running on the same network
2. Server must have mDNS/Bonjour enabled (NetworkDiscoveryService)
3. Launch the mobile app
4. Tap "Scan for Servers"
5. Select discovered server from list

### 2. Manual Connection

1. Tap "Manual Connection"
2. Enter server URL (e.g., `192.168.1.100:8080`)
3. Optionally enter registration token
4. Tap "Connect"

### 3. Server Approval

After registration, the server admin must approve the mobile app:
1. Server shows pending registration in MobileAppService
2. Admin approves via server UI
3. App receives approval notification and connects

### 4. Using Settings

1. Navigate to Settings via Flyout menu
2. Toggle Dark Mode, Biometric Auth, etc.
3. Manage server connection
4. Clear cache if needed

### 5. Search & Filter Devices

1. Use search bar to find devices by name, IP, or location
2. Use filter buttons to show only Online, Offline, etc.
3. Combine search and filter for precise results

## Development

### Adding a New Page

1. Create ViewModel in `ViewModels/`
2. Create XAML page in `Views/`
3. Register both in `MauiProgram.cs`:
   ```csharp
   builder.Services.AddTransient<MyViewModel>();
   builder.Services.AddTransient<MyPage>();
   ```
4. Add navigation route in `AppShell.xaml`

### Using Dark Mode

All UI elements support dark mode via `AppThemeBinding`:

```xml
<Label TextColor="{AppThemeBinding Light={StaticResource Gray900}, Dark={StaticResource DarkGray900}}" />
```

### Implementing Biometric Auth

```csharp
var available = await _authService.IsBiometricAuthAvailableAsync();
if (available)
{
    var success = await _authService.AuthenticateWithBiometricsAsync();
    if (success)
    {
        // User authenticated
    }
}
```

## Testing

### Manual Testing Checklist

- [x] Server discovery finds server
- [x] Manual connection works
- [x] Registration succeeds
- [x] WebSocket connects
- [x] Device list loads
- [x] Device details show correctly
- [x] Remote commands work
- [x] Screenshot capture works
- [x] Search and filter work
- [x] Settings save and load
- [x] Dark mode toggles correctly
- [x] Biometric auth works (on supported devices)
- [x] App survives background/foreground cycle
- [x] Network errors handled gracefully

## Apple App Store Submission

See [APP_STORE_CHECKLIST.md](APP_STORE_CHECKLIST.md) for complete submission guide.

### Key Requirements
- ✅ Privacy Manifest complete
- ✅ App Transport Security configured
- ✅ Permissions declared
- ✅ Dark Mode support
- ✅ Accessibility compliance
- ⏳ App icons (all sizes)
- ⏳ Screenshots
- ⏳ App Store metadata

## Known Issues

1. **Build on Linux**: MAUI workload not supported on Linux (build on macOS/Windows)
2. **mDNS Discovery**: May not work on networks with strict firewall rules
3. **Self-Signed Certificates**: Requires custom validation handler (development only)

## Security Best Practices

- ✅ Credentials stored in Keychain (iOS) / EncryptedSharedPreferences (Android)
- ✅ HTTPS enforced (App Transport Security)
- ✅ Certificate validation (configurable for development)
- ✅ Biometric authentication for sensitive actions
- ✅ No hardcoded secrets
- ✅ Token-based authentication with server

## Performance

- App launch: < 3 seconds (target)
- Device list load: < 2 seconds (target)
- Screenshot load: < 5 seconds (target)
- Memory usage: < 100 MB (typical)

## Next Steps

See `IMPLEMENTATION_PLAN.md` for detailed roadmap:

- [ ] Push notifications (full implementation)
- [ ] Layout assignment UI
- [ ] Schedule management
- [ ] Bulk operations UI
- [ ] Advanced filtering/search
- [ ] iPad optimization (Split View)
- [ ] Widget support (iOS Home Screen)
- [ ] Siri Shortcuts integration
- [ ] Offline mode improvements

## License

Same as parent project.

## Support

For issues and questions, please create a GitHub issue in the main repository.

