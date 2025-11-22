# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## 🚨 CRITICAL WORKFLOW - ALWAYS FOLLOW

### GitHub Push After EVERY Change

**MANDATORY: Push to GitHub after EVERY single modification!**

```bash
# After ANY code changes:
source .env  # Load GitHub token
git add -A
git commit -m "Description of changes

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"
git push
```

**GitHub Token Configuration:**
- Token stored in `.env` file (NOT committed to git)
- Use: `source .env` before git operations
- `.env` format:
```
GITHUBTOKEN=your_token_here
GITHUBREPO=https://github.com/manur84/digitalsignage.git
```

### Raspberry Pi Client Testing Workflow

**When making changes to Python client code:**

1. **Make changes** to Python client files
2. **PUSH TO GITHUB** (mandatory!)
3. **SSH to Raspberry Pi:**
```bash
sshpass -p 'mr412393' ssh pro@192.168.0.178
```

4. **Update and test:**
```bash
cd /opt/digitalsignage-client
sudo git pull
sudo ./update.sh
sudo journalctl -u digitalsignage-client -f
```

5. **Test on actual hardware** - verify display output on HDMI monitor
6. **If issues:** Fix locally → push to GitHub → repeat

---

## Build and Run Commands

### Server (Windows .NET 8 WPF)

```bash
# Build & Run
dotnet build DigitalSignage.sln
dotnet run --project src/DigitalSignage.Server/DigitalSignage.Server.csproj

# Release Build
dotnet build -c Release

# Tests
dotnet test

# Publish standalone Windows executable
dotnet publish src/DigitalSignage.Server/DigitalSignage.Server.csproj -c Release -r win-x64 --self-contained
```

### Database Migrations (EF Core + SQLite)

```bash
cd src/DigitalSignage.Data

# Create migration
dotnet ef migrations add MigrationName --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj

# Apply migrations (automatic on server startup)
dotnet ef database update --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj

# Remove last migration
dotnet ef migrations remove --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj
```

### Client (Raspberry Pi Python)

```bash
# INITIAL INSTALLATION on Raspberry Pi:
# 1. Clone repository to home directory (NOT /opt!)
cd ~
git clone https://github.com/manur84/digitalsignage.git
cd digitalsignage/src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh
# → This copies files to /opt/digitalsignage-client and installs service

# UPDATE existing installation:
# The repository should be in your home directory, NOT in /opt!
cd ~/digitalsignage  # Or wherever you cloned it
git pull
cd src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh  # Auto-detects UPDATE mode, preserves config

# Service management
sudo systemctl status|restart|stop digitalsignage-client
sudo journalctl -u digitalsignage-client -f
```

---

## Project Architecture

### Solution Structure

```
digitalsignage/
├── DigitalSignage.sln                          # Visual Studio solution
├── src/
│   ├── DigitalSignage.Server/                  # WPF App (94 C# files, 18 XAML)
│   ├── DigitalSignage.Core/                    # Shared models & interfaces
│   ├── DigitalSignage.Data/                    # EF Core data layer
│   └── DigitalSignage.Client.RaspberryPi/      # Python client (11 files)
├── tests/
│   └── DigitalSignage.Tests/                   # Unit tests
├── CLAUDE.md                                   # This file
├── CODETODO.md                                 # Feature checklist (67KB)
└── REFACTORING_PLAN.md                         # Architecture refactoring
```

### Technology Stack

**Server (Windows):**
- .NET 8 / C# 12, WPF
- Entity Framework Core 8 + SQLite
- CommunityToolkit.Mvvm
- Serilog (Logging)
- HttpListener (WebSocket Server)
- Scriban (Template Engine)

**Client (Raspberry Pi):**
- Python 3.9+, PyQt5
- SQLite (Offline Cache)
- systemd Service
- psutil, aiohttp

**Communication:**
- WebSocket (Port 8080-8083/8888/9000)
- SSL/TLS with self-signed certificate
- Token-based authentication
- Auto-reconnect with exponential backoff

### Server Application Structure

```
DigitalSignage.Server/
├── Commands/                    # Undo/Redo command pattern
├── Configuration/               # App configuration
├── Controls/                    # Custom WPF controls (DesignerCanvas, etc.)
├── Converters/                  # 18 Value Converters
├── MessageHandlers/             # WebSocket message handlers (Handler Pattern)
│   ├── RegisterMessageHandler.cs              # Pi: Device registration
│   ├── HeartbeatMessageHandler.cs             # Pi: Heartbeat
│   ├── StatusReportMessageHandler.cs          # Pi: Status reports
│   ├── ScreenshotMessageHandler.cs            # Pi: Screenshots
│   ├── LogMessageHandler.cs                   # Pi: Log messages
│   ├── UpdateConfigResponseMessageHandler.cs  # Pi: Config updates
│   └── MobileApp/                             # Mobile app handlers
│       ├── AppRegisterMessageHandler.cs
│       ├── AppHeartbeatMessageHandler.cs
│       ├── RequestClientListMessageHandler.cs
│       ├── SendCommandMessageHandler.cs
│       ├── AssignLayoutMessageHandler.cs
│       ├── RequestScreenshotMessageHandler.cs
│       └── RequestLayoutListMessageHandler.cs
├── Services/                    # 22 Business services (including MobileAppConnectionManager)
├── ViewModels/                  # 15 ViewModels
├── Views/                       # XAML views & dialogs
├── App.xaml.cs                  # DI configuration & startup
└── appsettings.json            # Server configuration
```

### Message Handler Pattern (NEW)

The server uses the **Handler Pattern** (Strategy Pattern) for processing WebSocket messages:

**Architecture:**
- Each message type has a dedicated handler class implementing `IMessageHandler`
- `MessageHandlerFactory` routes incoming messages to the appropriate handler
- Handlers are registered in DI and resolved at runtime

**Benefits:**
- ✓ **Single Responsibility**: Each handler focuses on one message type
- ✓ **Testability**: Handlers can be unit tested independently
- ✓ **Maintainability**: Easy to add new message types (just add a new handler)
- ✓ **Clean Code**: WebSocketCommunicationService reduced from 1535 → 934 lines (-39%)

**Example Handler:**
```csharp
public class HeartbeatMessageHandler : MessageHandlerBase
{
    public override string MessageType => MessageTypes.Heartbeat;

    public override async Task HandleAsync(Message message, string connectionId, CancellationToken ct)
    {
        var heartbeat = message as HeartbeatMessage;
        await _clientService.UpdateClientStatusAsync(heartbeat.ClientId, heartbeat.Status);
    }
}
```

**Handler Registration (Automatic):**
```csharp
// ServiceCollectionExtensions.cs
services.AddTransient<IMessageHandler, HeartbeatMessageHandler>();
services.AddSingleton<MessageHandlerFactory>();  // Auto-discovers all handlers
```

**Message Routing:**
```csharp
// WebSocketCommunicationService.cs
var handler = _messageHandlerFactory.GetHandler(message.Type);
if (handler != null)
    await handler.HandleAsync(message, connectionId, cancellationToken);
```

**Migrated Services:**
- `MobileAppConnectionManager` - Manages mobile app WebSocket connections, tokens, app IDs

### Python Client Structure

```
DigitalSignage.Client.RaspberryPi/
├── client.py                    # Main entry point
├── display_renderer.py          # PyQt5 layout rendering
├── cache_manager.py             # SQLite offline cache
├── device_manager.py            # Hardware monitoring
├── config.py                    # Configuration
├── watchdog_monitor.py          # systemd watchdog
├── requirements.txt             # Dependencies
├── install.sh                   # Installation script
└── digitalsignage-client.service # systemd unit
```

---

## Server Services (22 Services)

### Core Services

1. **WebSocketCommunicationService** - WebSocket server, SSL/TLS support, connection management (uses Handler Pattern for message processing)
2. **MobileAppConnectionManager** - Mobile app connection state management (connections, tokens, app IDs)
3. **ClientService** - Client registry, token-based registration, status tracking
4. **LayoutService** - Layout CRUD, JSON serialization, scheduling integration
5. **MediaService** - Media library, SHA256 deduplication, thumbnail generation
6. **DeviceControlService** - Remote commands (Restart, Screenshot, Volume, etc.)
7. **ScheduleService** - Time-based layout scheduling, recurring schedules
8. **DataSourceService** - SQL/API data sources, Scriban integration
9. **TemplateService** - 11 built-in templates, Scriban rendering
10. **CommandHistoryService** - Undo/Redo for designer
11. **BackgroundUpdateService** - Automatic data refresh (5min interval)
12. **HeartbeatMonitoringService** - Client health monitoring (30s timeout)
13. **DatabaseInitializationService** - Auto migrations, seed data
14. **ScreenshotService** - Remote screenshot capture
15. **CertificateService** - SSL certificate generation
16. **AlertService** - System alerts (UI pending)
17. **StatisticsService** - Usage analytics
18. **LoggingService** - Centralized logging
19. **SettingsService** - Application settings
20. **NetworkDiscoveryService** - mDNS/UDP auto-discovery
21. **BackupService** - Database backup
22. **UpdateService** - Auto-update mechanism

### Message Handlers (13 Handlers)

**Pi Client Handlers (6):**
1. **RegisterMessageHandler** - Device registration
2. **HeartbeatMessageHandler** - Heartbeat processing
3. **StatusReportMessageHandler** - Status updates
4. **ScreenshotMessageHandler** - Screenshot handling
5. **LogMessageHandler** - Log messages
6. **UpdateConfigResponseMessageHandler** - Config update responses

**Mobile App Handlers (7):**
1. **AppRegisterMessageHandler** - App registration & authorization
2. **AppHeartbeatMessageHandler** - App heartbeat
3. **RequestClientListMessageHandler** - Device list requests
4. **SendCommandMessageHandler** - Command forwarding to devices
5. **AssignLayoutMessageHandler** - Layout assignment
6. **RequestScreenshotMessageHandler** - Screenshot requests
7. **RequestLayoutListMessageHandler** - Layout list requests

### ViewModels (15 ViewModels)

**Main:** MainViewModel, DesignerViewModel, DeviceManagementViewModel
**Designer:** PropertiesPanelViewModel, ToolboxViewModel, LayersViewModel
**Data:** DataSourcesViewModel, DatabaseConnectionViewModel
**Dialogs:** MediaBrowserViewModel, TemplateSelectionViewModel, LayoutSelectionViewModel
**Device:** DeviceListViewModel, DeviceDetailsViewModel, ScreenshotViewModel, LogsViewModel

---

## WebSocket Protocol

### Message Types (Server → Client)

```json
{"type": "ShowLayout", "data": {"layoutId": 123, "layout": {}}}
{"type": "UpdateElement", "data": {"layoutId": 123, "element": {}}}
{"type": "ExecuteCommand", "data": {"command": "Restart|Screenshot|VolumeUp|VolumeDown|ScreenOn|ScreenOff"}}
{"type": "Ping", "data": {}}
```

### Message Types (Client → Server)

```json
{"type": "Register", "data": {"hostname": "pi-01", "token": "xxx", "resolution": "1920x1080"}}
{"type": "Status", "data": {"deviceId": "guid", "status": "Online", "deviceInfo": {}}}
{"type": "Screenshot", "data": {"deviceId": "guid", "imageData": "base64-png"}}
{"type": "Pong", "data": {}}
```

---

## Code Style Guidelines

### C# (.NET 8)

**Naming:**
- PascalCase: Classes, Methods, Properties
- camelCase: Parameters, local variables
- _camelCase: Private fields (underscore prefix)
- UPPER_CASE: Constants

**Best Practices:**
```csharp
// Use nullable reference types
#nullable enable

// Async all I/O
public async Task<Device?> GetDeviceAsync(Guid id)
{
    return await _context.Devices.FindAsync(id);
}

// Structured logging
_logger.LogInformation("Device {DeviceId} connected", deviceId);

// DI in constructor
public MyService(ILogger<MyService> logger, AppDbContext context)
{
    _logger = logger;
    _context = context;
}
```

### Python (PEP 8)

**Naming:**
- snake_case: Functions, variables, modules
- PascalCase: Classes
- UPPER_CASE: Constants

**Best Practices:**
```python
from typing import Optional, Dict
import logging

logger = logging.getLogger(__name__)

class DeviceManager:
    """Manages device information."""
    
    async def get_device_info(self) -> Dict[str, Any]:
        """Get device information."""
        try:
            return {"cpu_usage": psutil.cpu_percent()}
        except Exception as e:
            logger.error(f"Failed: {e}")
            return {}
```

### XAML

**Best Practices:**
- Use data binding over code-behind
- Name controls only when needed
- Use StaticResource for styles/converters
- Prefer Command binding over event handlers

```xml
<TextBox Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
<Button Content="Save" Command="{Binding SaveCommand}" IsEnabled="{Binding CanSave}"/>
```

---

## 🚨 CRITICAL CODING ERRORS TO AVOID

**Based on audit findings in WORK.md - these errors have been found in the codebase and MUST be avoided in new code!**

### 1. ❌ CRITICAL: Thread-Safety Issues

**NEVER use Dictionary in multi-threaded contexts:**
```csharp
// ❌ BAD - Not thread-safe!
private readonly Dictionary<int, DateTime> _cache = new();

// In method accessed by multiple threads:
_cache[key] = value;  // RACE CONDITION!
```

**✅ ALWAYS use ConcurrentDictionary:**
```csharp
// ✅ GOOD - Thread-safe
private readonly ConcurrentDictionary<int, DateTime> _cache = new();

// Safe for concurrent access
_cache[key] = value;
```

**Thread-safe statistics updates:**
```csharp
// ❌ BAD - Not atomic!
_statistics.AddOrUpdate(
    key,
    new Stats { Hits = 1 },
    (_, stats) => { stats.Hits++; return stats; });  // READ-MODIFY-WRITE RACE!

// ✅ GOOD - Atomic increment
_statistics.AddOrUpdate(
    key,
    new Stats { Hits = 1 },
    (_, stats) => {
        Interlocked.Increment(ref stats.Hits);
        return stats;
    });
```

---

### 2. ❌ CRITICAL: Resource Leaks (IDisposable)

**ALWAYS dispose IDisposable resources:**

```csharp
// ❌ BAD - JsonDocument leak!
private Dictionary<string, JsonElement> ParseConfig(string json)
{
    var doc = JsonDocument.Parse(json);  // NOT DISPOSED!
    var dict = new Dictionary<string, JsonElement>();
    foreach (var prop in doc.RootElement.EnumerateObject())
        dict[prop.Name] = prop.Value;
    return dict;  // MEMORY LEAK!
}

// ✅ GOOD - Properly disposed
private Dictionary<string, JsonElement> ParseConfig(string json)
{
    if (string.IsNullOrWhiteSpace(json))
        return new Dictionary<string, JsonElement>();

    try
    {
        using var doc = JsonDocument.Parse(json);
        var dict = new Dictionary<string, JsonElement>();
        foreach (var prop in doc.RootElement.EnumerateObject())
            dict[prop.Name] = prop.Value.Clone();  // Clone for use outside using block!
        return dict;
    }
    catch (JsonException ex)
    {
        _logger.LogError(ex, "Error parsing configuration");
        return new Dictionary<string, JsonElement>();
    }
}
```

**Always dispose SemaphoreSlim, UdpClient, Ping, etc.:**
```csharp
// ❌ BAD - No disposal
public class MyService
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    // No Dispose method = RESOURCE LEAK!
}

// ✅ GOOD - Implement IDisposable
public class MyService : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}
```

---

### 3. ❌ CRITICAL: Fire-and-Forget Tasks

**NEVER use fire-and-forget tasks for critical operations:**

```csharp
// ❌ BAD - Database update may never complete!
_ = Task.Run(async () =>
{
    try
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        // Update database...
        await dbContext.SaveChangesAsync();  // May fail silently!
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed");  // Exception swallowed!
    }
}, cancellationToken);

// ✅ GOOD - Await the operation
try
{
    using var scope = _serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
    // Update database...
    await dbContext.SaveChangesAsync();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to update database");
    // Handle error appropriately (retry, throw, etc.)
}
```

**Fire-and-forget causes:**
- Silent failures
- Data inconsistency (in-memory vs database)
- No guarantee of completion
- Cannot be awaited or tracked
- Exceptions are swallowed

---

### 4. ❌ CRITICAL: Sync-over-Async Anti-Patterns

**NEVER block async code with sync calls:**

```csharp
// ❌ BAD - Blocks thread!
private void SaveLayout(Layout layout)
{
    _fileLock.Wait();  // BLOCKING! Thread pool starvation!
    try
    {
        var json = JsonConvert.SerializeObject(layout);
        File.WriteAllText(filePath, json);  // BLOCKING I/O!
    }
    finally
    {
        _fileLock.Release();
    }
}

// ✅ GOOD - Fully async
private async Task SaveLayoutAsync(Layout layout, CancellationToken ct = default)
{
    await _fileLock.WaitAsync(ct);
    try
    {
        var json = JsonConvert.SerializeObject(layout);
        await File.WriteAllTextAsync(filePath, json, ct);
    }
    finally
    {
        _fileLock.Release();
    }
}
```

**NEVER use .Result or .Wait():**
```csharp
// ❌ BAD - Can cause deadlocks!
var clients = _clientService.GetAllClientsAsync().Result;  // BLOCKING!

// ✅ GOOD - Await it
var clients = await _clientService.GetAllClientsAsync();
```

**NEVER use Thread.Sleep in async methods:**
```csharp
// ❌ BAD - Blocks thread for 500ms!
private PerformanceMetrics GetMetrics()
{
    var start = Process.GetCurrentProcess().TotalProcessorTime;
    Thread.Sleep(500);  // BLOCKING!
    var end = Process.GetCurrentProcess().TotalProcessorTime;
    // ...
}

// ✅ GOOD - Non-blocking delay
private async Task<PerformanceMetrics> GetMetricsAsync()
{
    var start = Process.GetCurrentProcess().TotalProcessorTime;
    await Task.Delay(500);  // NON-BLOCKING!
    var end = Process.GetCurrentProcess().TotalProcessorTime;
    // ...
}
```

---

### 5. ❌ HIGH: Async Void (Only for Event Handlers!)

**NEVER use async void except for event handlers:**

```csharp
// ❌ BAD - Async void method!
public async void ProcessData()  // Exceptions cannot be caught!
{
    await DoSomethingAsync();
}

// ✅ GOOD - Return Task
public async Task ProcessDataAsync()
{
    await DoSomethingAsync();
}

// ✅ ACCEPTABLE - Event handler only
private async void OnButtonClick(object sender, EventArgs e)
{
    try
    {
        await ProcessDataAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing data");
    }
}
```

**Why async void is dangerous:**
- Exceptions crash the application
- Cannot be awaited
- Cannot be unit tested
- No way to track completion

---

### 6. ❌ HIGH: Missing Async Disposal

**Use `await using` for IAsyncDisposable:**

```csharp
// ❌ BAD - Synchronous disposal of async resource!
public async Task<List<DataSource>> GetAllAsync()
{
    using var context = await _contextFactory.CreateDbContextAsync();
    return await context.DataSources.ToListAsync();
    // Disposes synchronously - may block!
}

// ✅ GOOD - Async disposal
public async Task<List<DataSource>> GetAllAsync()
{
    await using var context = await _contextFactory.CreateDbContextAsync();
    return await context.DataSources.ToListAsync();
}
```

---

### 7. ❌ HIGH: Weak Password Hashing

**NEVER use SHA256/MD5 for passwords:**

```csharp
// ❌ BAD - Vulnerable to rainbow tables!
public string HashPassword(string password)
{
    using var sha256 = SHA256.Create();
    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
    return Convert.ToBase64String(hash);  // NO SALT! NO ITERATIONS!
}

// ✅ GOOD - Use BCrypt
public string HashPassword(string password)
{
    return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
}

public bool VerifyPassword(string password, string hash)
{
    try
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
    catch
    {
        return false;
    }
}
```

---

### 8. ❌ MEDIUM: Missing Input Validation

**ALWAYS validate inputs:**

```csharp
// ❌ BAD - No validation!
public async Task<string> SaveMediaAsync(byte[] data, string fileName)
{
    var filePath = Path.Combine(_mediaDirectory, fileName);
    await File.WriteAllBytesAsync(filePath, data);  // What if data is null?
    return fileName;
}

// ✅ GOOD - Validate everything
public async Task<string> SaveMediaAsync(byte[] data, string fileName)
{
    if (data == null || data.Length == 0)
        throw new ArgumentException("Data cannot be null or empty", nameof(data));

    if (string.IsNullOrWhiteSpace(fileName))
        throw new ArgumentException("Filename cannot be empty", nameof(fileName));

    // Validate filename doesn't contain path traversal
    if (fileName.Contains("..") || Path.GetFileName(fileName) != fileName)
        throw new ArgumentException("Invalid filename", nameof(fileName));

    try
    {
        var filePath = Path.Combine(_mediaDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, data);
        _logger.LogDebug("Saved media file {FileName} ({Size} bytes)", fileName, data.Length);
        return fileName;
    }
    catch (IOException ex)
    {
        _logger.LogError(ex, "Failed to save media file {FileName}", fileName);
        throw new InvalidOperationException($"Failed to save media file {fileName}", ex);
    }
}
```

**Collection null checks:**
```csharp
// ❌ BAD - Will throw if null
public void AlignLeft(IEnumerable<DisplayElement> elements)
{
    var list = elements.ToList();  // NullReferenceException if null!
    // ...
}

// ✅ GOOD - Guard clause
public void AlignLeft(IEnumerable<DisplayElement> elements)
{
    if (elements == null)
        throw new ArgumentNullException(nameof(elements));

    var list = elements.ToList();
    if (list.Count < 2)
        return;
    // ...
}
```

---

### 9. ❌ MEDIUM: Inconsistent Disposal

**Always dispose in StopAsync AND in finally blocks:**

```csharp
// ❌ BAD - Only closes, doesn't dispose
public override Task StopAsync(CancellationToken cancellationToken)
{
    _logger.LogInformation("Stopping...");
    _udpListener?.Close();  // Missing Dispose()!
    return base.StopAsync(cancellationToken);
}

// ✅ GOOD - Close and dispose
public override Task StopAsync(CancellationToken cancellationToken)
{
    _logger.LogInformation("Stopping...");
    _udpListener?.Close();
    _udpListener?.Dispose();
    return base.StopAsync(cancellationToken);
}
```

---

### 10. ❌ LOW: Performance Issues

**Avoid multiple LINQ iterations:**

```csharp
// ❌ BAD - Iterates 5+ times!
public LogStatistics GetStatistics()
{
    var allLogs = _allLogs.ToArray();
    return new LogStatistics
    {
        DebugCount = allLogs.Count(l => l.Level == LogLevel.Debug),
        InfoCount = allLogs.Count(l => l.Level == LogLevel.Info),
        WarningCount = allLogs.Count(l => l.Level == LogLevel.Warning),
        // ... 5 separate iterations!
    };
}

// ✅ GOOD - Single pass with GroupBy
public LogStatistics GetStatistics()
{
    var allLogs = _allLogs.ToArray();
    var levelCounts = allLogs
        .GroupBy(l => l.Level)
        .ToDictionary(g => g.Key, g => g.Count());

    return new LogStatistics
    {
        DebugCount = levelCounts.GetValueOrDefault(LogLevel.Debug, 0),
        InfoCount = levelCounts.GetValueOrDefault(LogLevel.Info, 0),
        WarningCount = levelCounts.GetValueOrDefault(LogLevel.Warning, 0),
        // ... single iteration!
    };
}
```

---

## Quick Error Checklist

Before committing code, check:

- [ ] **Thread-safety:** Used ConcurrentDictionary for shared state?
- [ ] **Resource disposal:** All IDisposable resources disposed with `using`?
- [ ] **Async disposal:** Used `await using` for IAsyncDisposable?
- [ ] **No fire-and-forget:** All async operations awaited?
- [ ] **No sync-over-async:** No `.Result`, `.Wait()`, `Thread.Sleep()` in async code?
- [ ] **No async void:** Only in event handlers, with try-catch?
- [ ] **Input validation:** All parameters validated (null checks, ranges, formats)?
- [ ] **Password hashing:** Using BCrypt, not SHA256/MD5?
- [ ] **Proper logging:** Structured logging with context, not Console.WriteLine?
- [ ] **Exception handling:** Try-catch with logging, not swallowing exceptions?

---

## Common Tasks

### Feature Development

1. Check CODETODO.md for existing specs
2. Follow MVVM pattern for WPF
3. Use Dependency Injection
4. Add XML comments for public APIs
5. Push to GitHub after completion

### Bug Fixing

1. Analyze logs (Server: logs/, Client: journalctl)
2. Reproduce (Server or Client side?)
3. Fix incrementally
4. Test Undo/Redo if designer affected
5. Verify WebSocket connection

### Database Changes

```bash
cd src/DigitalSignage.Data
dotnet ef migrations add MigrationName --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj
# Check generated code in Migrations/
git add -A && git commit && git push
# Migration applied automatically on server startup
```

---

## Debugging & Troubleshooting

### Server Debugging

```bash
# Build & Run
dotnet build
dotnet run --project src/DigitalSignage.Server/DigitalSignage.Server.csproj

# View logs (logs/log-YYYYMMDD.txt)
```

**Common Issues:**
- **URL ACL Error:** Auto-switches port (8080→8081→8082→8083→8888→9000)
- **Database Lock:** `rm digitalsignage.db-wal digitalsignage.db-shm`
- **Build Warnings:** 36 existing (nullable types), avoid adding more

### Client Debugging

```bash
# SSH to Pi
sshpass -p 'mr412393' ssh pro@192.168.0.178

# Real-time logs
sudo journalctl -u digitalsignage-client -f

# Service control
sudo systemctl status|restart|stop digitalsignage-client

# Manual test mode
sudo systemctl stop digitalsignage-client
cd /opt/digitalsignage-client
./venv/bin/python3 client.py --test-mode
```

**Common Issues:**
- **Connection Failed:** Check network, firewall, certificate
- **Layout Not Shown:** Check logs, clear cache (`rm data/cache.db`)
- **High CPU:** Reduce animations, refresh rate, element count

---

## Performance Tips

### Server

```csharp
// ✅ Good: Async + Projection
var devices = await _context.Devices
    .Where(d => d.Status == DeviceStatus.Online)
    .Select(d => new DeviceDto { Id = d.Id, Name = d.Name })
    .ToListAsync();

// ❌ Bad: Sync + Eager Loading
var devices = _context.Devices
    .Include(d => d.AssignedLayouts)
    .Include(d => d.Screenshots)
    .ToList();
```

### Client

```python
# ✅ Good: Batch updates
self.setUpdatesEnabled(False)
for element in elements:
    self._update_element(element)
self.setUpdatesEnabled(True)
self.update()

# ❌ Bad: Update after each element
for element in elements:
    self._update_element(element)
    self.update()
```

---

## Security

### Never Commit
- Passwords, tokens, API keys
- `.env` file
- Production config with real credentials

### Use Instead
```csharp
// appsettings.json for dev, environment variables for production
var token = Environment.GetEnvironmentVariable("REGISTRATION_TOKEN");
```

### Input Validation
```csharp
if (deviceId == Guid.Empty)
    return Result.Failure("Invalid device ID");
if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
    return Result.Failure("Invalid name");
```

---

## Deployment

### Server
- Build Release: `dotnet build -c Release`
- Configure URL ACL for production port
- Set up SSL certificate
- Configure firewall rules

### Client (New Pi)
```bash
# Clone to home directory (installer will copy to /opt)
cd ~
git clone https://github.com/manur84/digitalsignage.git
cd digitalsignage/src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh  # Copies to /opt/digitalsignage-client, installs service
sudo nano /opt/digitalsignage-client/config.py  # Configure server_host, token
sudo systemctl restart digitalsignage-client
```

---

## Project Status (~95% Complete)

**Fully Implemented (✅):**
- Visual Designer (drag-drop, undo/redo, multi-select)
- Device Management (remote control, screenshots)
- Template system (11 templates)
- Client registration (token-based auth)
- TLS/SSL encryption
- Offline cache (SQLite)
- systemd service with watchdog
- Media library (SHA256 deduplication)
- WebSocket communication (auto-reconnect)
- Background services
- Logging infrastructure
- MVVM architecture

**Partially Implemented (⚠️):**
- Data Sources UI (backend done, UI pending)
- Layout scheduling (backend done, UI pending)
- Alert system (backend done, UI pending)

**Not Implemented (❌):**
- Auto-discovery UI (backend done)
- MSI installer
- REST API
- Video element support
- Touch support
- Cloud synchronization

**Known Issues:**
- 36 build warnings (nullable types, unused fields)
- No automated tests yet

---

## Quick Reference

```bash
# === BUILD & RUN ===
dotnet build
dotnet run --project src/DigitalSignage.Server/DigitalSignage.Server.csproj
dotnet build -c Release

# === DATABASE ===
cd src/DigitalSignage.Data
dotnet ef migrations add MigrationName --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj

# === CLIENT (PI) ===
sshpass -p 'mr412393' ssh pro@192.168.0.178
cd ~/digitalsignage  # Repository location (NOT /opt!)
git pull
cd src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh  # Auto-detects UPDATE mode
sudo journalctl -u digitalsignage-client -f
sudo systemctl status|restart|stop digitalsignage-client

# === GIT (MANDATORY AFTER CHANGES) ===
source .env
git add -A
git commit -m "Description"
git push
```

---

## Important Rules

**DO:**
- ✅ Push to GitHub after EVERY change
- ✅ Test on Pi after Python changes
- ✅ Use async/await for I/O
- ✅ Follow MVVM pattern
- ✅ Log structured (not Console.WriteLine)
- ✅ Validate user input
- ✅ Use Dependency Injection

**DON'T:**
- ❌ Commit secrets (.env, tokens, passwords)
- ❌ Make breaking changes without plan
- ❌ Change DB schema without migrations
- ❌ Test on Pi without GitHub push
- ❌ Change WebSocket protocol without syncing server + client
- ❌ Use blocking I/O
- ❌ Swallow exceptions without logging

---

For detailed information, see CODETODO.md (feature checklist) and REFACTORING_PLAN.md (architecture).