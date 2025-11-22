# GitHub Copilot Instructions für Digital Signage

Du bist ein C# WPF und iOS Code-Experte und hilfst bei der Entwicklung und Verbesserung dieses Digital Signage Projekts.

## 🎯 Projektübersicht

**Digital Signage System** - Ein professionelles System zur Verwaltung und Anzeige digitaler Inhalte auf Raspberry Pi Displays.

### Technologie-Stack

**Server (Windows):**
- .NET 8 / C# 12
- WPF (Windows Presentation Foundation)
- Entity Framework Core 8 + SQLite
- CommunityToolkit.Mvvm (MVVM Framework)
- WebSocket-Server (SSL/TLS)
- Serilog (Structured Logging)

**Client (Raspberry Pi):**
- Python 3.9+
- PyQt5 (UI Framework)
- SQLite (Offline-Cache)
- systemd Service
- WebSocket-Client mit Auto-Reconnect

**Mobile App:**
- .NET MAUI
- Cross-platform (iOS, Android)

### Architektur

```
src/
├── DigitalSignage.Server/      # WPF Server-Anwendung
├── DigitalSignage.Core/        # Shared Models & Interfaces
├── DigitalSignage.Data/        # EF Core Data Layer
├── DigitalSignage.Client.RaspberryPi/  # Python Client
└── DigitalSignage.App.Mobile/  # MAUI Mobile App
```

## 💻 Code-Richtlinien

### C# / WPF Best Practices

**MVVM Pattern strikt befolgen:**
```csharp
// ✅ RICHTIG: ViewModel mit Commands und Properties
public class MainViewModel : ObservableObject
{
    private string _title;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public ICommand SaveCommand { get; }
    
    public MainViewModel()
    {
        SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
    }
}

// ❌ FALSCH: Business Logic in Code-Behind
private void Button_Click(object sender, EventArgs e)
{
    // Keine Business Logic hier!
}
```

**Dependency Injection verwenden:**
```csharp
public class LayoutService
{
    private readonly ILogger<LayoutService> _logger;
    private readonly AppDbContext _context;
    
    public LayoutService(ILogger<LayoutService> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }
}
```

**Async/Await für alle I/O-Operationen:**
```csharp
// ✅ RICHTIG: Vollständig async
public async Task<List<Device>> GetDevicesAsync(CancellationToken ct = default)
{
    await using var context = await _contextFactory.CreateDbContextAsync(ct);
    return await context.Devices.ToListAsync(ct);
}

// ❌ FALSCH: Sync-over-Async
public List<Device> GetDevices()
{
    return GetDevicesAsync().Result; // DEADLOCK-GEFAHR!
}
```

### Python Best Practices

**PEP 8 Standard befolgen:**
```python
# ✅ RICHTIG: snake_case, Type Hints, Docstrings
from typing import Optional, Dict
import logging

logger = logging.getLogger(__name__)

class DeviceManager:
    """Manages device information and monitoring."""
    
    async def get_device_info(self) -> Dict[str, Any]:
        """Get current device information.
        
        Returns:
            Dict containing device metrics.
        """
        try:
            return {
                "cpu_usage": psutil.cpu_percent(),
                "memory_usage": psutil.virtual_memory().percent
            }
        except Exception as e:
            logger.error(f"Failed to get device info: {e}")
            return {}
```

## 🚨 Kritische Fehler vermeiden

### 1. Thread-Safety (KRITISCH)

```csharp
// ❌ FALSCH: Dictionary ist nicht thread-safe
private readonly Dictionary<int, Client> _clients = new();

// ✅ RICHTIG: ConcurrentDictionary verwenden
private readonly ConcurrentDictionary<int, Client> _clients = new();
```

### 2. Resource Disposal (KRITISCH)

```csharp
// ❌ FALSCH: IDisposable nicht disposed
public void ProcessData(string json)
{
    var doc = JsonDocument.Parse(json); // MEMORY LEAK!
    // ...
}

// ✅ RICHTIG: using-Statement
public void ProcessData(string json)
{
    using var doc = JsonDocument.Parse(json);
    // ...
}

// ✅ RICHTIG: await using für async
public async Task ProcessDataAsync()
{
    await using var context = await _factory.CreateDbContextAsync();
    // ...
}
```

### 3. Fire-and-Forget Tasks (KRITISCH)

```csharp
// ❌ FALSCH: Task wird nicht awaited
_ = Task.Run(async () => await SaveToDatabase());

// ✅ RICHTIG: Task awaiten
await SaveToDatabaseAsync();
```

### 4. Async Void vermeiden

```csharp
// ❌ FALSCH: async void (außer Event Handler)
public async void LoadData() { }

// ✅ RICHTIG: async Task zurückgeben
public async Task LoadDataAsync() { }

// ✅ AKZEPTABEL: Nur für Event Handler
private async void OnButtonClick(object sender, EventArgs e)
{
    try
    {
        await LoadDataAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading data");
    }
}
```

### 5. Input Validation (WICHTIG)

```csharp
// ❌ FALSCH: Keine Validierung
public async Task SaveMediaAsync(byte[] data, string fileName)
{
    var path = Path.Combine(_mediaDir, fileName);
    await File.WriteAllBytesAsync(path, data);
}

// ✅ RICHTIG: Immer validieren
public async Task SaveMediaAsync(byte[] data, string fileName)
{
    if (data == null || data.Length == 0)
        throw new ArgumentException("Data cannot be null or empty", nameof(data));
    
    if (string.IsNullOrWhiteSpace(fileName))
        throw new ArgumentException("Filename required", nameof(fileName));
    
    // Path traversal verhindern
    if (fileName.Contains("..") || Path.GetFileName(fileName) != fileName)
        throw new ArgumentException("Invalid filename", nameof(fileName));
    
    var path = Path.Combine(_mediaDir, fileName);
    await File.WriteAllBytesAsync(path, data);
}
```

## 📋 Naming Conventions

### C#
- `PascalCase`: Classes, Methods, Properties, Public Fields
- `camelCase`: Parameters, Local Variables
- `_camelCase`: Private Fields (Underscore-Präfix)
- `UPPER_CASE`: Constants

### Python
- `snake_case`: Functions, Variables, Modules
- `PascalCase`: Classes
- `UPPER_CASE`: Constants

## 🔧 Häufige Aufgaben

### EF Core Migrations

```bash
cd src/DigitalSignage.Data
dotnet ef migrations add MigrationName --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj
dotnet ef database update --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj
```

### Build & Run

```bash
# Server bauen und ausführen
dotnet build DigitalSignage.sln
dotnet run --project src/DigitalSignage.Server/DigitalSignage.Server.csproj

# Tests ausführen
dotnet test

# Release Build
dotnet build -c Release
```

### Raspberry Pi Client Update

```bash
# SSH zum Pi
ssh pro@192.168.0.178

# Update durchführen
cd ~/digitalsignage
git pull
cd src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh

# Logs anzeigen
sudo journalctl -u digitalsignage-client -f
```

## 🔒 Sicherheit

### Passwort-Hashing

```csharp
// ❌ FALSCH: SHA256/MD5 für Passwörter
var hash = SHA256.ComputeHash(password);

// ✅ RICHTIG: BCrypt verwenden
var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
var isValid = BCrypt.Net.BCrypt.Verify(password, hash);
```

### Keine Secrets in Code

```csharp
// ❌ FALSCH: Hardcoded secrets
var token = "abc123def456";

// ✅ RICHTIG: Environment Variables oder appsettings.json
var token = Environment.GetEnvironmentVariable("REGISTRATION_TOKEN");
var token = _configuration["Security:RegistrationToken"];
```

## 📊 Logging

```csharp
// ✅ Structured Logging mit Serilog
_logger.LogInformation("Device {DeviceId} connected from {IpAddress}", 
    deviceId, ipAddress);

_logger.LogError(ex, "Failed to save layout {LayoutId}", layoutId);

// ❌ FALSCH: Console.WriteLine verwenden
Console.WriteLine($"Device {deviceId} connected");
```

## 🎨 WPF/XAML Patterns

### Data Binding

```xml
<!-- ✅ RICHTIG: Data Binding verwenden -->
<TextBox Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
<Button Content="Save" Command="{Binding SaveCommand}" 
        IsEnabled="{Binding CanSave}"/>

<!-- ❌ FALSCH: Event Handler in Code-Behind -->
<Button Content="Save" Click="Button_Click"/>
```

### Value Converter

```csharp
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? Visibility.Visible : Visibility.Collapsed;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (Visibility)value == Visibility.Visible;
    }
}
```

## 🔄 WebSocket-Protokoll

### Server → Client Nachrichten

```json
{"type": "ShowLayout", "data": {"layoutId": 123, "layout": {...}}}
{"type": "ExecuteCommand", "data": {"command": "Restart"}}
{"type": "Ping", "data": {}}
```

### Client → Server Nachrichten

```json
{"type": "Register", "data": {"hostname": "pi-01", "token": "xxx"}}
{"type": "Status", "data": {"deviceId": "guid", "status": "Online"}}
{"type": "Pong", "data": {}}
```

## 📝 Wichtige Regeln

**IMMER:**
- ✅ Async/Await für I/O-Operationen
- ✅ MVVM Pattern in WPF befolgen
- ✅ Dependency Injection verwenden
- ✅ Input validieren
- ✅ Exceptions loggen (nicht schlucken)
- ✅ IDisposable Ressourcen mit `using` freigeben
- ✅ Structured Logging mit Serilog
- ✅ XML-Kommentare für Public APIs

**NIEMALS:**
- ❌ Secrets in Code committen
- ❌ `.Result` oder `.Wait()` auf Tasks aufrufen
- ❌ `async void` (außer Event Handler)
- ❌ Dictionary in Multi-Threading-Szenarien
- ❌ Business Logic in Code-Behind
- ❌ Console.WriteLine in Production Code
- ❌ Exceptions ohne Logging schlucken
- ❌ SHA256/MD5 für Passwörter verwenden

## 🧪 Testing

- Vor Commits: Projekt bauen und testen
- Bei Python-Änderungen: Auf Raspberry Pi testen
- Bei DB-Änderungen: Migration erstellen und testen
- Bei WebSocket-Änderungen: Server UND Client testen

## 📚 Weitere Dokumentation

Siehe `CLAUDE.md` für detaillierte Informationen zu:
- Vollständige Architektur-Dokumentation
- Alle 21 Services und deren Verantwortlichkeiten
- Deployment-Anleitungen
- Performance-Optimierungen
- Debugging-Tipps
