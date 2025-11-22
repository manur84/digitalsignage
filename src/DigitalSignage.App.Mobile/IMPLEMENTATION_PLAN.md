# Implementierungsplan: iOS Mobile App für Digital Signage System

**Version:** 1.5
**Datum:** 2025-11-20
**Letzte Änderung:** 2025-11-21 (Phase 3.8 Device Detail Page - COMPLETE ✅)
**Technologie-Empfehlung:** .NET MAUI (plattformübergreifend iOS/Android)
**Projektordner:** `src/DigitalSignage.App.Mobile/`

## Implementation Status

**Server Extensions (Phase 2):**
- ✅ **Phase 2.1**: Database Schema (MobileAppRegistration) - **COMPLETE** (2025-11-21)
- ✅ **Phase 2.2**: WebSocket Protocol Extension - **COMPLETE** (2025-11-21)
- ✅ **Phase 2.3**: MobileAppService - **COMPLETE** (2025-11-21)
- ✅ **Phase 2.4**: WebSocketCommunicationService Extended - **COMPLETE** (2025-11-21)
- ✅ **Phase 2.5**: Admin UI for Mobile App Management - **COMPLETE** (2025-11-21)
- ✅ **Phase 2.6**: Auto-Discovery Service (Server-Side) - **COMPLETE** (2025-11-21)

**Mobile App (Phase 1 & 3):**
- ✅ **Phase 1.2**: MAUI Project Setup - **COMPLETE** (2025-11-21)
- ✅ **Phase 1.3**: Project Structure - **COMPLETE** (2025-11-21)
- ✅ **Phase 3.1**: MAUI Configuration - **COMPLETE** (2025-11-21)
- ✅ **Phase 3.2**: Models - **COMPLETE** (2025-11-21)
- ✅ **Phase 3.3**: Core Services (SecureStorage, Auth, Discovery, WebSocket) - **COMPLETE** (2025-11-21)
- ✅ **Phase 3.4**: ViewModels (Base, Login, DeviceList) - **COMPLETE** (2025-11-21)
- ✅ **Phase 3.5**: Views (LoginPage, DeviceListPage) - **COMPLETE** (2025-11-21)
- ✅ **Phase 3.6**: MauiProgram DI Configuration - **COMPLETE** (2025-11-21)
- ✅ **Phase 3.7**: AppShell Navigation - **COMPLETE** (2025-11-21)
- ✅ **Phase 3.8**: Device Detail Page with Remote Controls - **COMPLETE** (2025-11-21)
- ⏳ **Phase 3.9**: Layout Assignment UI - **PENDING**
- ⏳ **Phase 3.10**: Biometric Authentication - **PENDING**
- ⏳ **Phase 3.11**: Push Notifications - **PENDING**

---

## Executive Summary

Dieser Plan beschreibt die vollständige Implementierung einer mobilen App zur Verwaltung und Steuerung des Digital Signage Systems. Die App ermöglicht Administratoren, Clients zu überwachen, Remote-Befehle auszuführen und Layouts zuzuweisen - alles von einem iOS-Gerät aus.

**Kernmerkmale:**
- **Auto-Discovery:** Automatisches Finden von Servern im lokalen Netzwerk (mDNS/Bonjour)
- Echtzeit-Überwachung aller registrierten Clients
- Remote-Steuerung (Restart, Screenshot, Lautstärke, Display)
- Sicherer Autorisierungs-Workflow (Server-Admin muss App-Zugriff freigeben)
- Layout-Zuweisung und Zeitplan-Verwaltung
- Push-Benachrichtigungen für kritische Ereignisse
- Offline-Modus mit lokalem Cache

**Geschätzte Gesamtdauer:** 4-6 Wochen (1 Entwickler, Vollzeit)

---

## Phase 1: Technologie-Evaluation & Projekt-Setup

### 1.1 Technologie-Entscheidung

**Empfehlung: .NET MAUI**

**Vorteile:**
- ✅ Maximaler Code-Sharing mit bestehendem .NET 8 Backend (Models, DTOs, Business Logic)
- ✅ Ein Codebase für iOS + Android + Windows Mobile
- ✅ C# Expertise bereits vorhanden im Projekt
- ✅ Native Performance und UI
- ✅ MVVM Pattern (wie WPF Server)
- ✅ Hot Reload für schnellere Entwicklung
- ✅ Direkte Nutzung von .NET Libraries (System.Text.Json, System.Net.WebSockets)

**Nachteile:**
- ⚠️ Größere App-Größe als native Swift
- ⚠️ MAUI ist relativ neu (.NET 8), potenzielle Bugs

**Alternative: Native Swift**
- Vorteile: Kleinere App-Größe, 100% native iOS-Features, bessere Performance
- Nachteile: Komplett neuer Code, kein Code-Sharing, zweite Sprache im Projekt, separate Android-App nötig

**Entscheidung: .NET MAUI** (wegen Code-Sharing und einheitlichem Tech-Stack)

**Komplexität:** 🟢 Niedrig (Setup)
**Dauer:** 0.5 Tage

---

### 1.2 Entwicklungsumgebung einrichten

**Voraussetzungen:**
- Visual Studio 2022 (Windows) oder Visual Studio for Mac 2022+ / Rider 2024+
- .NET 8 SDK installiert
- Xcode 15+ (für iOS-Entwicklung)
- Apple Developer Account (für Gerätetests und App Store)
- iOS Simulator oder physisches iOS-Gerät

**Schritte:**
1. .NET MAUI Workload installieren:
   ```bash
   dotnet workload install maui
   ```

2. iOS-Entwicklungstools installieren (macOS):
   ```bash
   xcode-select --install
   dotnet workload install ios
   ```

3. Projekt erstellen:
   ```bash
   cd src/
   dotnet new maui -n DigitalSignage.App.Mobile
   ```

4. Solution-Datei aktualisieren:
   ```bash
   dotnet sln DigitalSignage.sln add src/DigitalSignage.App.Mobile/DigitalSignage.App.Mobile.csproj
   ```

**Komplexität:** 🟢 Niedrig
**Dauer:** 0.5 Tage

---

### 1.3 Projektstruktur anlegen

```
src/DigitalSignage.App.Mobile/
├── DigitalSignage.App.Mobile.csproj
├── App.xaml                          # App-Level Styles & Resources
├── App.xaml.cs                       # App Startup & DI
├── AppShell.xaml                     # Navigation Shell
├── AppShell.xaml.cs
├── MauiProgram.cs                    # DI Container & Services
├── Models/                           # Datenmodelle
│   ├── Device.cs                     # Client-Gerät
│   ├── DeviceStatus.cs              # Enum: Online/Offline/Warning
│   ├── Layout.cs                     # Layout-Daten
│   ├── Schedule.cs                   # Zeitplan
│   ├── AppRegistration.cs           # App-Registrierung
│   ├── WebSocketMessage.cs          # WebSocket-Nachrichten
│   └── Permission.cs                 # Berechtigungen
├── Services/                         # Business Logic Services
│   ├── IWebSocketService.cs
│   ├── WebSocketService.cs          # WebSocket-Kommunikation
│   ├── IAuthenticationService.cs
│   ├── AuthenticationService.cs     # Token-Management, Biometrie
│   ├── IServerDiscoveryService.cs
│   ├── ServerDiscoveryService.cs    # Auto-Discovery (mDNS/Bonjour)
│   ├── IDeviceService.cs
│   ├── DeviceService.cs             # Client-Verwaltung
│   ├── ILayoutService.cs
│   ├── LayoutService.cs             # Layout-Verwaltung
│   ├── INotificationService.cs
│   ├── NotificationService.cs       # Push Notifications
│   ├── ISecureStorageService.cs
│   ├── SecureStorageService.cs      # Token/Zertifikat-Speicherung
│   ├── ICacheService.cs
│   └── CacheService.cs              # Offline-Cache (SQLite)
├── ViewModels/                       # MVVM ViewModels
│   ├── BaseViewModel.cs             # Basis-ViewModel
│   ├── LoginViewModel.cs
│   ├── DeviceListViewModel.cs
│   ├── DeviceDetailViewModel.cs
│   ├── LayoutListViewModel.cs
│   ├── ScheduleViewModel.cs
│   └── SettingsViewModel.cs
├── Views/                            # XAML Views/Pages
│   ├── LoginPage.xaml
│   ├── DeviceListPage.xaml
│   ├── DeviceDetailPage.xaml
│   ├── LayoutListPage.xaml
│   ├── SchedulePage.xaml
│   └── SettingsPage.xaml
├── Converters/                       # Value Converters
│   ├── StatusToColorConverter.cs
│   ├── BoolToVisibilityConverter.cs
│   └── BytesToImageConverter.cs
├── Behaviors/                        # XAML Behaviors
├── Controls/                         # Custom Controls
│   └── DeviceStatusIndicator.cs
├── Resources/                        # Ressourcen
│   ├── Fonts/
│   ├── Images/
│   ├── Styles/
│   │   ├── Colors.xaml
│   │   └── Styles.xaml
│   └── Raw/
│       └── ca.crt                    # Server SSL-Zertifikat
├── Platforms/                        # Plattform-spezifischer Code
│   ├── iOS/
│   │   ├── Info.plist
│   │   ├── Entitlements.plist
│   │   └── AppDelegate.cs
│   ├── Android/
│   └── Windows/
└── Data/
    ├── AppDatabase.cs               # SQLite-Datenbank
    └── Migrations/
```

**Komplexität:** 🟢 Niedrig
**Dauer:** 0.5 Tage

---

## Phase 2: Server-Erweiterungen (WPF Server) ✅ COMPLETE

### 2.1 Datenbankschema erweitern ✅ COMPLETE (2025-11-21)

**Neue Entitäten:**

**`src/DigitalSignage.Core/Models/MobileAppRegistration.cs`**
```csharp
namespace DigitalSignage.Core.Models;

/// <summary>
/// Represents a mobile app registration awaiting or granted authorization
/// </summary>
public class MobileAppRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Device name from mobile app (e.g., "iPhone 15 Pro")
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Device identifier (OS-generated unique ID)
    /// </summary>
    public string DeviceIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// App version (e.g., "1.0.5")
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// Platform: iOS, Android, etc.
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Registration status: Pending, Approved, Rejected, Revoked
    /// </summary>
    public AppRegistrationStatus Status { get; set; } = AppRegistrationStatus.Pending;

    /// <summary>
    /// Authentication token (null until approved)
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Granted permissions (comma-separated: "view,control,manage")
    /// </summary>
    public string Permissions { get; set; } = string.Empty;

    /// <summary>
    /// When the app first registered
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the registration was approved/rejected
    /// </summary>
    public DateTime? AuthorizedAt { get; set; }

    /// <summary>
    /// Admin who authorized the app
    /// </summary>
    public string? AuthorizedBy { get; set; }

    /// <summary>
    /// Last time the app connected
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    /// Optional notes from admin
    /// </summary>
    public string? Notes { get; set; }
}

public enum AppRegistrationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Revoked = 3
}

public enum AppPermission
{
    View = 1,        // View-only (device list, screenshots)
    Control = 2,     // Execute commands (restart, screenshot, volume)
    Manage = 4       // Assign layouts, schedules
}
```

**Entity Framework Konfiguration:**

**`src/DigitalSignage.Data/Configurations/MobileAppRegistrationConfiguration.cs`**
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Data.Configurations;

public class MobileAppRegistrationConfiguration : IEntityTypeConfiguration<MobileAppRegistration>
{
    public void Configure(EntityTypeBuilder<MobileAppRegistration> builder)
    {
        builder.ToTable("MobileAppRegistrations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DeviceName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.DeviceIdentifier)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(e => e.DeviceIdentifier)
            .IsUnique();

        builder.Property(e => e.Token)
            .HasMaxLength(500);

        builder.HasIndex(e => e.Token)
            .IsUnique();

        builder.Property(e => e.Permissions)
            .HasMaxLength(200);
    }
}
```

**DbContext aktualisieren:**

**`src/DigitalSignage.Data/AppDbContext.cs`** (ergänzen)
```csharp
public DbSet<MobileAppRegistration> MobileAppRegistrations { get; set; } = null!;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ... existing configurations
    modelBuilder.ApplyConfiguration(new MobileAppRegistrationConfiguration());
}
```

**Migration erstellen:**
```bash
cd src/DigitalSignage.Data
dotnet ef migrations add AddMobileAppRegistrations --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj
```

**Komplexität:** 🟡 Mittel
**Dauer:** 1 Tag

---

### 2.2 WebSocket-Protokoll erweitern ✅ COMPLETE (2025-11-21)

**Neue Message Types:**

**`src/DigitalSignage.Core/Models/WebSocket/AppMessages.cs`**
```csharp
namespace DigitalSignage.Core.Models.WebSocket;

/// <summary>
/// Mobile app registration request
/// </summary>
public class AppRegisterMessage
{
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceIdentifier { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
}

/// <summary>
/// Server response: authorization required
/// </summary>
public class AppAuthorizationRequiredMessage
{
    public Guid AppId { get; set; }
    public string Status { get; set; } = "pending"; // pending, approved, rejected
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Server response: app authorized
/// </summary>
public class AppAuthorizedMessage
{
    public string Token { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// App requests client list
/// </summary>
public class RequestClientListMessage
{
    public string? Filter { get; set; } // "online", "offline", "all"
}

/// <summary>
/// Server sends client list
/// </summary>
public class ClientListUpdateMessage
{
    public List<ClientInfo> Clients { get; set; } = new();
}

public class ClientInfo
{
    public Guid Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string Status { get; set; } = string.Empty; // Online, Offline, Warning
    public string Resolution { get; set; } = string.Empty;
    public DeviceInfoData? DeviceInfo { get; set; }
    public DateTime LastSeen { get; set; }
    public int? AssignedLayoutId { get; set; }
}

public class DeviceInfoData
{
    public double? CpuUsage { get; set; }
    public double? MemoryUsage { get; set; }
    public double? Temperature { get; set; }
    public double? DiskUsage { get; set; }
}

/// <summary>
/// App sends command to specific device
/// </summary>
public class SendCommandMessage
{
    public Guid TargetDeviceId { get; set; }
    public string Command { get; set; } = string.Empty; // Restart, Screenshot, etc.
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// App assigns layout to device
/// </summary>
public class AssignLayoutMessage
{
    public Guid DeviceId { get; set; }
    public int LayoutId { get; set; }
}

/// <summary>
/// Server notifies app of client status change
/// </summary>
public class ClientStatusChangedMessage
{
    public Guid DeviceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Request screenshot from device
/// </summary>
public class RequestScreenshotMessage
{
    public Guid DeviceId { get; set; }
}

/// <summary>
/// Screenshot response
/// </summary>
public class ScreenshotResponseMessage
{
    public Guid DeviceId { get; set; }
    public string ImageData { get; set; } = string.Empty; // Base64 PNG
    public DateTime CapturedAt { get; set; }
}
```

**WebSocketMessageType Enum erweitern:**

**`src/DigitalSignage.Core/Models/WebSocket/WebSocketMessage.cs`** (ergänzen)
```csharp
public enum WebSocketMessageType
{
    // Existing types
    Register,
    Status,
    ShowLayout,
    UpdateElement,
    ExecuteCommand,
    Screenshot,
    Ping,
    Pong,

    // NEW: Mobile app types
    AppRegister,
    AppAuthorizationRequired,
    AppAuthorized,
    RequestClientList,
    ClientListUpdate,
    SendCommand,
    AssignLayout,
    ClientStatusChanged,
    RequestScreenshot,
    ScreenshotResponse
}
```

**Komplexität:** 🟡 Mittel
**Dauer:** 1 Tag

---

### 2.3 MobileAppService erstellen ✅ COMPLETE (2025-11-21)

**Hinweis:** Vollständiger Code im Plan - siehe Abschnitt im generierten Dokument.

**Key Points:**
- `RegisterAppAsync()` - Erstellt Pending-Registrierung
- `ApproveAppAsync()` - Generiert Token, setzt Status auf Approved
- `RejectAppAsync()` / `RevokeAppAsync()` - Verwaltung von Ablehnungen/Widerruf
- `ValidateTokenAsync()` - Token-Validierung für eingehende Requests
- Sichere Token-Generierung mit `RandomNumberGenerator`

**Komplexität:** 🟡 Mittel
**Dauer:** 2 Tage

---

### 2.4 WebSocketCommunicationService erweitern

**Erweiterte Funktionalität:**
- Tracking von Mobile App Connections getrennt von Device Connections
- Handler für neue Message Types (AppRegister, RequestClientList, SendCommand, etc.)
- Berechtigungsprüfung vor Command-Ausführung
- Event `OnNewAppRegistration` für Admin UI Notifications
- Methode `NotifyMobileAppsClientStatusChanged()` für Broadcast an alle Apps

**Komplexität:** 🔴 Hoch
**Dauer:** 3 Tage

---

### 2.5 Admin UI für App-Verwaltung (WPF)

**ViewModel:** `MobileAppManagementViewModel`
- ObservableCollection von Registrierungen
- Commands: Approve, Reject, Revoke
- Permission Checkboxen (View, Control, Manage)
- Pending Count für Badge

**View:** `MobileAppManagementView.xaml`
- DataGrid mit App-Liste
- Status-Badge (Pending, Approved, Rejected, Revoked)
- Details-Panel mit Permissions und Actions
- Integration in MainWindow als neuer Tab

**Notification Badge:**
- Event-Handler für neue Registrierungen
- Toast-Benachrichtigung
- Badge-Count im Tab-Header

**Komplexität:** 🟡 Mittel
**Dauer:** 2 Tage

---

### 2.6 Auto-Discovery Service (Server) ✅ COMPLETE (2025-11-21)

**Zweck:** Server broadcastet seine Verfügbarkeit im lokalen Netzwerk, sodass die Mobile App ihn automatisch finden kann.

**Technologie-Wahl:**

**Option 1: mDNS/Bonjour (Empfohlen)**
- ✅ Standard für Service Discovery (Apple-native)
- ✅ Funktioniert plattformübergreifend (Windows, macOS, Linux, iOS, Android)
- ✅ Keine Firewall-Konfiguration nötig (Multicast)
- ✅ Automatische Namensauflösung
- ⚠️ Benötigt NuGet Package: `Makaretu.Dns.Multicast` oder `Zeroconf`

**Option 2: UDP Broadcast**
- ✅ Einfacher zu implementieren
- ✅ Keine zusätzlichen Dependencies
- ⚠️ Funktioniert nur im lokalen Subnet
- ⚠️ Firewall-Regeln notwendig

**Entscheidung: mDNS/Bonjour** (bessere Integration mit iOS)

---

#### Server-seitige Implementierung

**`src/DigitalSignage.Core/Models/ServerInfo.cs`**
```csharp
namespace DigitalSignage.Core.Models;

/// <summary>
/// Server information broadcast via mDNS for auto-discovery
/// </summary>
public class ServerInfo
{
    /// <summary>
    /// Server name (e.g., "DigitalSignage-DESKTOP-ABC")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Server hostname
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// WebSocket URL (e.g., "wss://192.168.1.100:8080")
    /// </summary>
    public string WebSocketUrl { get; set; } = string.Empty;

    /// <summary>
    /// Server version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Number of connected clients
    /// </summary>
    public int ConnectedClients { get; set; }

    /// <summary>
    /// Server IP addresses
    /// </summary>
    public List<string> IpAddresses { get; set; } = new();

    /// <summary>
    /// Server port
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Requires SSL/TLS
    /// </summary>
    public bool UsesSsl { get; set; }
}
```

**`src/DigitalSignage.Server/Services/INetworkDiscoveryService.cs`**
```csharp
namespace DigitalSignage.Server.Services;

public interface INetworkDiscoveryService
{
    /// <summary>
    /// Start broadcasting server availability via mDNS
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stop broadcasting
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get current server info
    /// </summary>
    ServerInfo GetServerInfo();
}
```

**`src/DigitalSignage.Server/Services/NetworkDiscoveryService.cs`**
```csharp
using Makaretu.Dns;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Broadcasts server availability via mDNS/Bonjour for auto-discovery
/// </summary>
public class NetworkDiscoveryService : BackgroundService, INetworkDiscoveryService
{
    private readonly ILogger<NetworkDiscoveryService> _logger;
    private readonly IWebSocketCommunicationService _webSocketService;
    private readonly ServiceDiscovery _serviceDiscovery;
    private ServiceProfile? _serviceProfile;

    // Service type for mDNS (follows DNS-SD naming convention)
    private const string ServiceType = "_digitalsignage._tcp";
    private const string ServiceDomain = "local";

    public NetworkDiscoveryService(
        ILogger<NetworkDiscoveryService> logger,
        IWebSocketCommunicationService webSocketService)
    {
        _logger = logger;
        _webSocketService = webSocketService;
        _serviceDiscovery = new ServiceDiscovery();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await StartAsync(stoppingToken);

        // Keep running until stopped
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public new async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var serverInfo = GetServerInfo();

            // Create service profile
            _serviceProfile = new ServiceProfile(
                instanceName: serverInfo.Name,
                serviceName: ServiceType,
                port: (ushort)serverInfo.Port,
                addresses: serverInfo.IpAddresses.Select(ip => IPAddress.Parse(ip))
            );

            // Add TXT records with metadata
            _serviceProfile.Resources.Add(new TXTRecord
            {
                Name = _serviceProfile.FullyQualifiedName,
                Strings =
                {
                    $"version={serverInfo.Version}",
                    $"ssl={serverInfo.UsesSsl}",
                    $"clients={serverInfo.ConnectedClients}",
                    $"url={serverInfo.WebSocketUrl}"
                }
            });

            // Advertise service
            _serviceDiscovery.Advertise(_serviceProfile);

            _logger.LogInformation(
                "mDNS service started: {ServiceName} on port {Port}",
                _serviceProfile.FullyQualifiedName,
                serverInfo.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start mDNS service");
        }

        await Task.CompletedTask;
    }

    public new async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_serviceProfile != null)
            {
                _serviceDiscovery.Unadvertise(_serviceProfile);
                _logger.LogInformation("mDNS service stopped");
            }

            _serviceDiscovery.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping mDNS service");
        }

        await Task.CompletedTask;
    }

    public ServerInfo GetServerInfo()
    {
        // Get server hostname
        var hostname = Dns.GetHostName();

        // Get local IP addresses
        var ipAddresses = Dns.GetHostEntry(hostname)
            .AddressList
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork) // IPv4 only
            .Select(ip => ip.ToString())
            .ToList();

        // Get WebSocket port from configuration
        var port = _webSocketService.CurrentPort; // Assuming this property exists
        var usesSsl = true; // Assuming SSL is always used

        // Get connected clients count
        var connectedClients = _webSocketService.ConnectedDeviceCount; // Assuming this exists

        // Build WebSocket URL (use first IP address)
        var protocol = usesSsl ? "wss" : "ws";
        var primaryIp = ipAddresses.FirstOrDefault() ?? "localhost";
        var webSocketUrl = $"{protocol}://{primaryIp}:{port}";

        return new ServerInfo
        {
            Name = $"DigitalSignage-{hostname}",
            Hostname = hostname,
            WebSocketUrl = webSocketUrl,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0",
            ConnectedClients = connectedClients,
            IpAddresses = ipAddresses,
            Port = port,
            UsesSsl = usesSsl
        };
    }

    public override void Dispose()
    {
        _serviceDiscovery?.Dispose();
        base.Dispose();
    }
}
```

**NuGet Package hinzufügen:**

**`src/DigitalSignage.Server/DigitalSignage.Server.csproj`** (ergänzen)
```xml
<ItemGroup>
  <PackageReference Include="Makaretu.Dns.Multicast" Version="0.27.0" />
</ItemGroup>
```

**DI Registrierung:**

**`src/DigitalSignage.Server/App.xaml.cs`** (ergänzen)
```csharp
// Register as singleton + hosted service
services.AddSingleton<INetworkDiscoveryService, NetworkDiscoveryService>();
services.AddHostedService(sp => (NetworkDiscoveryService)sp.GetRequiredService<INetworkDiscoveryService>());
```

**Firewall-Konfiguration (Windows):**

mDNS nutzt Port **5353 UDP**. Windows Firewall sollte automatisch funktionieren, aber zur Sicherheit:

```powershell
# PowerShell (als Administrator)
New-NetFirewallRule -DisplayName "mDNS (Digital Signage)" `
  -Direction Inbound -Protocol UDP -LocalPort 5353 -Action Allow
```

**Komplexität:** 🟡 Mittel
**Dauer:** 2 Tage

---

## Phase 3: Mobile App Implementierung (.NET MAUI)

### 3.1 Projekt konfigurieren

**Key Configuration:**
- Target Frameworks: `net8.0-ios` und `net8.0-android`
- NuGet Packages:
  - Microsoft.Maui.Controls 8.0
  - CommunityToolkit.Mvvm 8.2.2
  - CommunityToolkit.Maui 7.0
  - sqlite-net-pcl 1.8.116
- Entitlements: Keychain, Push Notifications
- Info.plist: Face ID Usage Description, Network Settings

**Komplexität:** 🟢 Niedrig
**Dauer:** 1 Tag

---

### 3.2 Models & DTOs

**App-spezifische Models:**
- `AppSettings` - Lokale Einstellungen
- `DeviceViewModel` - UI-freundliche Device-Darstellung mit Status-Colors

**Shared Models:**
- Direkte Nutzung von `DigitalSignage.Core` Models via Projekt-Referenz

**Komplexität:** 🟢 Niedrig
**Dauer:** 0.5 Tage

---

### 3.3 Services: Authentication & Secure Storage

**SecureStorageService:**
- Nutzt MAUI SecureStorage API
- Speichert: Token, ServerUrl, AppSettings
- Keychain-basiert auf iOS

**AuthenticationService:**
- App-Registrierung beim Server
- Biometrische Authentifizierung (Face ID/Touch ID)
- Platform-specific Code für iOS `LAContext`
- Persistent Device Identifier

**BiometricAuth (iOS):**
- Native LocalAuthentication Framework
- Unterscheidung FaceID vs. TouchID
- Async/Await Wrapper

**Komplexität:** 🟡 Mittel
**Dauer:** 2 Tage

---

### 3.4 Services: WebSocket Communication

**WebSocketService:**
- `ClientWebSocket` für Kommunikation
- SSL/TLS mit Self-Signed Certificate Support
- Auto-Reconnect Logic (TODO: ergänzen in vollständiger Implementation)
- Request/Response Pattern mit `TaskCompletionSource`
- Event-basierte Notifications (ConnectionStateChanged, DeviceStatusChanged)

**Key Methods:**
- `RegisterAsync()` - App-Registrierung
- `RequestClientListAsync()` - Client-Liste abrufen
- `SendCommandAsync()` - Remote-Befehle senden
- `AssignLayoutAsync()` - Layout zuweisen
- `RequestScreenshotAsync()` - Screenshot mit Timeout

**Komplexität:** 🔴 Hoch
**Dauer:** 3 Tage

---

### 3.5 Services: Server Discovery (Auto-Discovery)

**Zweck:** Automatisches Finden von Digital Signage Servern im lokalen Netzwerk.

#### iOS Implementation (mDNS/Bonjour)

**`src/DigitalSignage.App.Mobile/Services/IServerDiscoveryService.cs`**
```csharp
namespace DigitalSignage.App.Mobile.Services;

public interface IServerDiscoveryService
{
    /// <summary>
    /// Start scanning for servers
    /// </summary>
    Task StartScanningAsync();

    /// <summary>
    /// Stop scanning
    /// </summary>
    Task StopScanningAsync();

    /// <summary>
    /// Get discovered servers
    /// </summary>
    List<DiscoveredServer> DiscoveredServers { get; }

    /// <summary>
    /// Fired when a new server is discovered
    /// </summary>
    event EventHandler<DiscoveredServer>? ServerDiscovered;

    /// <summary>
    /// Fired when a server is lost (goes offline)
    /// </summary>
    event EventHandler<DiscoveredServer>? ServerLost;
}

public class DiscoveredServer
{
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string WebSocketUrl { get; set; } = string.Empty;
    public List<string> IpAddresses { get; set; } = new();
    public int Port { get; set; }
    public bool UsesSsl { get; set; }
    public string Version { get; set; } = string.Empty;
    public int ConnectedClients { get; set; }
    public DateTime DiscoveredAt { get; set; } = DateTime.Now;
    public int SignalStrength { get; set; } // For sorting (closer servers first)
}
```

**`src/DigitalSignage.App.Mobile/Services/ServerDiscoveryService.cs`**
```csharp
using Zeroconf;
using System.Collections.ObjectModel;

namespace DigitalSignage.App.Mobile.Services;

/// <summary>
/// Discovers Digital Signage servers via mDNS/Bonjour
/// </summary>
public class ServerDiscoveryService : IServerDiscoveryService
{
    private readonly ILogger<ServerDiscoveryService> _logger;
    private readonly ObservableCollection<DiscoveredServer> _discoveredServers = new();
    private CancellationTokenSource? _scanCancellationTokenSource;
    private bool _isScanning;

    private const string ServiceType = "_digitalsignage._tcp.local.";
    private const int ScanDurationSeconds = 5;

    public List<DiscoveredServer> DiscoveredServers => _discoveredServers.ToList();

    public event EventHandler<DiscoveredServer>? ServerDiscovered;
    public event EventHandler<DiscoveredServer>? ServerLost;

    public ServerDiscoveryService(ILogger<ServerDiscoveryService> logger)
    {
        _logger = logger;
    }

    public async Task StartScanningAsync()
    {
        if (_isScanning)
        {
            _logger.LogWarning("Already scanning for servers");
            return;
        }

        try
        {
            _isScanning = true;
            _scanCancellationTokenSource = new CancellationTokenSource();

            _logger.LogInformation("Starting server discovery scan...");

            // Clear old servers
            _discoveredServers.Clear();

            // Start continuous scanning
            await ScanContinuouslyAsync(_scanCancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during server discovery");
            _isScanning = false;
        }
    }

    public async Task StopScanningAsync()
    {
        if (!_isScanning)
            return;

        try
        {
            _scanCancellationTokenSource?.Cancel();
            _scanCancellationTokenSource?.Dispose();
            _scanCancellationTokenSource = null;
            _isScanning = false;

            _logger.LogInformation("Stopped server discovery scan");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping server discovery");
        }

        await Task.CompletedTask;
    }

    private async Task ScanContinuouslyAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Perform single scan
                var responses = await ZeroconfResolver.ResolveAsync(
                    ServiceType,
                    scanTime: TimeSpan.FromSeconds(ScanDurationSeconds),
                    cancellationToken: cancellationToken);

                foreach (var response in responses)
                {
                    ProcessDiscoveredService(response);
                }

                // Wait before next scan
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during mDNS scan");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private void ProcessDiscoveredService(IZeroconfHost host)
    {
        try
        {
            var service = host.Services.FirstOrDefault().Value;
            if (service == null)
                return;

            // Parse TXT records
            var txtRecords = service.Properties[0]; // Get TXT record dictionary
            var version = txtRecords.GetValueOrDefault("version", "unknown");
            var useSsl = bool.Parse(txtRecords.GetValueOrDefault("ssl", "true"));
            var clients = int.Parse(txtRecords.GetValueOrDefault("clients", "0"));
            var url = txtRecords.GetValueOrDefault("url", "");

            // Create server info
            var server = new DiscoveredServer
            {
                Name = host.DisplayName,
                Hostname = host.IPAddress, // Primary IP
                WebSocketUrl = url,
                IpAddresses = new List<string> { host.IPAddress },
                Port = service.Port,
                UsesSsl = useSsl,
                Version = version,
                ConnectedClients = clients,
                DiscoveredAt = DateTime.Now
            };

            // Check if already discovered
            var existing = _discoveredServers.FirstOrDefault(s => s.WebSocketUrl == server.WebSocketUrl);
            if (existing == null)
            {
                _discoveredServers.Add(server);
                _logger.LogInformation("Discovered server: {Name} at {Url}", server.Name, server.WebSocketUrl);
                ServerDiscovered?.Invoke(this, server);
            }
            else
            {
                // Update existing server info
                existing.ConnectedClients = server.ConnectedClients;
                existing.DiscoveredAt = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing discovered service: {Host}", host.DisplayName);
        }
    }

    // TODO: Implement cleanup for lost servers (haven't been seen in X minutes)
    private void CleanupLostServers()
    {
        var threshold = DateTime.Now.AddMinutes(-2);
        var lostServers = _discoveredServers.Where(s => s.DiscoveredAt < threshold).ToList();

        foreach (var server in lostServers)
        {
            _discoveredServers.Remove(server);
            _logger.LogInformation("Server lost: {Name}", server.Name);
            ServerLost?.Invoke(this, server);
        }
    }
}
```

**NuGet Package hinzufügen:**

**`src/DigitalSignage.App.Mobile/DigitalSignage.App.Mobile.csproj`** (ergänzen)
```xml
<ItemGroup>
  <PackageReference Include="Zeroconf" Version="3.6.11" />
</ItemGroup>
```

**DI Registrierung:**

**`src/DigitalSignage.App.Mobile/MauiProgram.cs`** (ergänzen)
```csharp
builder.Services.AddSingleton<IServerDiscoveryService, ServerDiscoveryService>();
```

**iOS Permissions:**

**`src/DigitalSignage.App.Mobile/Platforms/iOS/Info.plist`** (ergänzen)
```xml
<!-- Local Network Usage (required for mDNS/Bonjour on iOS 14+) -->
<key>NSLocalNetworkUsageDescription</key>
<string>Find Digital Signage servers on your local network</string>

<!-- Bonjour services -->
<key>NSBonjourServices</key>
<array>
    <string>_digitalsignage._tcp</string>
</array>
```

**Komplexität:** 🟡 Mittel
**Dauer:** 2 Tage

---

### 3.6 Services: Device & Layout Services

**TODO in Fortsetzung:**
- DeviceService - Client-Caching und Verwaltung
- LayoutService - Layout-Daten
- CacheService - SQLite Offline-Cache
- NotificationService - Push Notifications

---

### 3.7 ViewModels (MVVM)

**TODO in Fortsetzung:**
- BaseViewModel - INotifyPropertyChanged, IsBusy, etc.
- **LoginViewModel** - Server-Discovery, Auswahl, Manuelle Eingabe, Registrierung, Biometrie
- DeviceListViewModel - Liste, Filter, Refresh, Pull-to-Refresh
- DeviceDetailViewModel - Details, Commands, Screenshot
- LayoutListViewModel - Layout-Auswahl
- ScheduleViewModel - Zeitplan-Verwaltung
- SettingsViewModel - App-Einstellungen

**LoginViewModel Erweiterung für Auto-Discovery:**

```csharp
public partial class LoginViewModel : BaseViewModel
{
    private readonly IServerDiscoveryService _discoveryService;
    private readonly IAuthenticationService _authService;
    private readonly IWebSocketService _webSocketService;

    [ObservableProperty]
    private ObservableCollection<DiscoveredServer> _discoveredServers = new();

    [ObservableProperty]
    private DiscoveredServer? _selectedServer;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _showManualEntry;

    [ObservableProperty]
    private string _manualServerUrl = string.Empty;

    public LoginViewModel(
        IServerDiscoveryService discoveryService,
        IAuthenticationService authService,
        IWebSocketService webSocketService)
    {
        _discoveryService = discoveryService;
        _authService = authService;
        _webSocketService = webSocketService;

        // Subscribe to discovery events
        _discoveryService.ServerDiscovered += OnServerDiscovered;
        _discoveryService.ServerLost += OnServerLost;
    }

    [RelayCommand]
    private async Task StartScanAsync()
    {
        IsScanning = true;
        DiscoveredServers.Clear();
        await _discoveryService.StartScanningAsync();

        // Auto-stop after 30 seconds
        await Task.Delay(TimeSpan.FromSeconds(30));
        await StopScanAsync();
    }

    [RelayCommand]
    private async Task StopScanAsync()
    {
        await _discoveryService.StopScanningAsync();
        IsScanning = false;
    }

    [RelayCommand]
    private async Task ConnectToSelectedServerAsync()
    {
        if (SelectedServer == null)
            return;

        IsBusy = true;
        try
        {
            // Connect via WebSocket
            var connected = await _webSocketService.ConnectAsync(SelectedServer.WebSocketUrl);
            if (connected)
            {
                // Register app
                var result = await _authService.RegisterAsync(SelectedServer.WebSocketUrl);
                // Handle result...
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleManualEntry()
    {
        ShowManualEntry = !ShowManualEntry;
    }

    [RelayCommand]
    private async Task ConnectManuallyAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualServerUrl))
            return;

        // Validate URL format
        if (!ManualServerUrl.StartsWith("ws://") && !ManualServerUrl.StartsWith("wss://"))
        {
            // Show error
            return;
        }

        IsBusy = true;
        try
        {
            await _webSocketService.ConnectAsync(ManualServerUrl);
            // ...
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnServerDiscovered(object? sender, DiscoveredServer server)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DiscoveredServers.Add(server);
        });
    }

    private void OnServerLost(object? sender, DiscoveredServer server)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DiscoveredServers.Remove(server);
        });
    }
}
```

---

### 3.8 Views (XAML)

**TODO in Fortsetzung:**
- **LoginPage** - Server-Discovery, Server-Liste, Manuelle Eingabe, Connect, Status
- DeviceListPage - CollectionView, Search, Filter
- DeviceDetailPage - Tabs (Info, Control, Screenshot)
- LayoutListPage - Layout-Vorschau, Zuweisung
- SchedulePage - Kalender, Zeitslots
- SettingsPage - Dark Mode, Notifications, Biometrics, Logout

**LoginPage XAML Beispiel (mit Auto-Discovery):**

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2001/xaml"
             x:Class="DigitalSignage.App.Mobile.Views.LoginPage"
             Title="Connect to Server">
    <ScrollView>
        <StackLayout Padding="20" Spacing="20">
            <!-- Logo -->
            <Image Source="logo.png"
                   HeightRequest="100"
                   Aspect="AspectFit"/>

            <!-- Auto-Discovery Section -->
            <Frame BorderColor="{StaticResource Primary}">
                <StackLayout Spacing="10">
                    <Label Text="Discovered Servers"
                           FontSize="18"
                           FontAttributes="Bold"/>

                    <!-- Scan Button -->
                    <Button Text="{Binding IsScanning, Converter={StaticResource BoolToScanTextConverter}}"
                            Command="{Binding StartScanCommand}"
                            IsEnabled="{Binding IsScanning, Converter={StaticResource InvertBoolConverter}}"/>

                    <!-- Loading Indicator -->
                    <ActivityIndicator IsRunning="{Binding IsScanning}"
                                       IsVisible="{Binding IsScanning}"/>

                    <!-- Server List -->
                    <CollectionView ItemsSource="{Binding DiscoveredServers}"
                                    SelectionMode="Single"
                                    SelectedItem="{Binding SelectedServer}"
                                    HeightRequest="200">
                        <CollectionView.ItemTemplate>
                            <DataTemplate>
                                <Frame Padding="10"
                                       Margin="0,5"
                                       HasShadow="True">
                                    <StackLayout>
                                        <Label Text="{Binding Name}"
                                               FontSize="16"
                                               FontAttributes="Bold"/>
                                        <Label Text="{Binding WebSocketUrl}"
                                               FontSize="12"
                                               TextColor="Gray"/>
                                        <Label Text="{Binding ConnectedClients, StringFormat='{0} clients connected'}"
                                               FontSize="10"/>
                                    </StackLayout>
                                </Frame>
                            </DataTemplate>
                        </CollectionView.ItemTemplate>
                        <CollectionView.EmptyView>
                            <Label Text="No servers found. Tap 'Scan' to search."
                                   HorizontalOptions="Center"
                                   TextColor="Gray"/>
                        </CollectionView.EmptyView>
                    </CollectionView>

                    <!-- Connect Button -->
                    <Button Text="Connect to Selected Server"
                            Command="{Binding ConnectToSelectedServerCommand}"
                            IsEnabled="{Binding SelectedServer, Converter={StaticResource NullToBoolConverter}}"
                            BackgroundColor="{StaticResource Primary}"/>
                </StackLayout>
            </Frame>

            <!-- Manual Entry Section -->
            <StackLayout>
                <Button Text="{Binding ShowManualEntry, Converter={StaticResource BoolToManualEntryTextConverter}}"
                        Command="{Binding ToggleManualEntryCommand}"
                        BackgroundColor="Transparent"
                        TextColor="{StaticResource Primary}"/>

                <Frame IsVisible="{Binding ShowManualEntry}"
                       BorderColor="{StaticResource Primary}">
                    <StackLayout Spacing="10">
                        <Label Text="Manual Server Entry"/>
                        <Entry Placeholder="wss://192.168.1.100:8080"
                               Text="{Binding ManualServerUrl}"
                               Keyboard="Url"/>
                        <Button Text="Connect"
                                Command="{Binding ConnectManuallyCommand}"/>
                    </StackLayout>
                </Frame>
            </StackLayout>

            <!-- Status -->
            <Label Text="{Binding StatusMessage}"
                   IsVisible="{Binding StatusMessage, Converter={StaticResource StringNullOrEmptyBoolConverter}}"
                   TextColor="Red"
                   HorizontalOptions="Center"/>
        </StackLayout>
    </ScrollView>
</ContentPage>
```

---

## Phase 4: Testing & Refinement

**TODO in Fortsetzung:**
- Unit Tests (Services)
- Integration Tests (WebSocket)
- UI Tests (MAUI UITest)
- Manuelle Tests auf echten iOS-Geräten
- Performance-Optimierung
- Fehlerbehandlung & Edge Cases

---

## Phase 5: Deployment & App Store

**TODO in Fortsetzung:**
- Provisioning Profiles
- Code Signing
- App Icons & Screenshots
- App Store Metadaten
- TestFlight Beta-Tests
- App Store Submission
- CI/CD Pipeline (GitHub Actions)

---

## Prioritäten & Reihenfolge

**Empfohlene Umsetzungsreihenfolge:**

1. **Phase 2 (Server) zuerst:**
   - 2.1 Datenbankschema → Migration
   - 2.2 WebSocket-Protokoll
   - 2.3 MobileAppService
   - 2.4 WebSocketCommunicationService erweitern
   - 2.5 Admin UI
   - 2.6 Auto-Discovery Service (mDNS)
   - **Test:** Admin kann App-Registrierungen sehen und genehmigen, Server wird via mDNS broadcastet

2. **Phase 3 (Mobile App) - MVP:**
   - 3.1 Projekt-Setup
   - 3.2 Models
   - 3.3 Authentication Service (ohne Biometrie zunächst)
   - 3.4 WebSocket Service (Basic)
   - 3.5 Server Discovery Service (mDNS)
   - 3.7 LoginViewModel + DeviceListViewModel (Basic)
   - 3.8 LoginPage (mit Auto-Discovery UI) + DeviceListPage (Basic)
   - **Test:** App findet Server automatisch, zeigt Server-Liste, Admin genehmigt, App zeigt Client-Liste

3. **Phase 3 - Feature Completion:**
   - Device Commands
   - Screenshot-Funktion
   - Layout-Zuweisung
   - Biometrische Auth
   - Offline-Cache
   - Push Notifications

4. **Phase 4 & 5:**
   - Testing
   - Deployment

---

## Risiken & Herausforderungen

**Technische Risiken:**

1. **SSL/TLS mit Self-Signed Certificates:**
   - Problem: iOS blockiert unsichere Verbindungen
   - Lösung: Certificate Pinning oder Let's Encrypt für Server

2. **WebSocket Verbindungsstabilität:**
   - Problem: Mobile Netzwerke, App-Backgrounding
   - Lösung: Robuste Reconnect-Logic, Heartbeat

3. **MAUI Stabilität:**
   - Problem: MAUI ist relativ neu, potenzielle Bugs
   - Lösung: Aktuelle .NET 8 Version, Community Support

4. **App Store Review:**
   - Problem: Apple könnte App wegen bestimmter Features ablehnen
   - Lösung: Compliance mit Guidelines, kein Remote Code Execution

**Organisatorische Risiken:**

1. **iOS Development Umgebung:**
   - Benötigt: macOS, Xcode, Apple Developer Account ($99/Jahr)

2. **Zeitschätzung:**
   - 4-6 Wochen ist optimistisch bei Vollzeit
   - Puffer einplanen für Unvorhergesehenes

---

## Offene Fragen

1. **Soll die App auch auf Android laufen?**
   - MAUI unterstützt es, minimaler Mehraufwand
   - Empfehlung: Ja, für breitere Nutzbarkeit

2. **Mehrbenutzer-Support?**
   - Mehrere Admins mit verschiedenen Berechtigungen?
   - Aktuell: Ein Token pro App-Instanz

3. **Push Notifications Infrastructure:**
   - APNS (Apple Push Notification Service) Setup notwendig
   - Alternative: Polling

4. **Offline-Funktionalität:**
   - Wie viel Funktionalität offline?
   - Aktuell: Read-only Cache für Device-Liste

---

## Nächste Schritte

1. **Entscheidung treffen:**
   - MAUI vs. Native Swift
   - Scope festlegen (iOS-only vs. iOS+Android)

2. **Entwicklungsumgebung:**
   - macOS + Xcode installieren
   - Apple Developer Account erstellen

3. **Phase 2 starten:**
   - Server-Erweiterungen implementieren
   - Admin UI für App-Verwaltung

4. **MVP definieren:**
   - Minimal funktionsfähige Version festlegen
   - Inkrementell ausbauen

---

## Anhang: Weitere Features (Optional)

**Nice-to-Have Features für spätere Versionen:**

1. **Widget Support:**
   - iOS Home Screen Widget mit Device-Status

2. **Apple Watch App:**
   - Schnellzugriff auf wichtigste Befehle

3. **Shortcuts Integration:**
   - Siri Shortcuts für häufige Aktionen

4. **iPad-Optimierung:**
   - Split View für Liste + Details gleichzeitig

5. **3D Touch / Haptic Feedback:**
   - Quick Actions im App Icon

6. **Analytics:**
   - Usage Tracking, Crash Reporting

7. **Multi-Server Support:**
   - Verwaltung mehrerer Digital Signage Installationen

8. **Bulk Operations:**
   - Befehle an mehrere Clients gleichzeitig

---

**Plan-Ende** | Version 1.0 | Erstellt: 2025-11-20
