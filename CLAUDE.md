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
# Install as systemd service
cd src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh

# Service management
sudo systemctl status|restart|stop digitalsignage-client
sudo journalctl -u digitalsignage-client -f

# Update client on Pi
cd /opt/digitalsignage-client
sudo git pull
sudo ./update.sh
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
├── Services/                    # 21 Business services
├── ViewModels/                  # 15 ViewModels
├── Views/                       # XAML views & dialogs
├── App.xaml.cs                  # DI configuration & startup
└── appsettings.json            # Server configuration
```

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

## Server Services (21 Services)

### Core Services

1. **WebSocketCommunicationService** - WebSocket server, SSL/TLS support, connection management
2. **ClientService** - Client registry, token-based registration, status tracking
3. **LayoutService** - Layout CRUD, JSON serialization, scheduling integration
4. **MediaService** - Media library, SHA256 deduplication, thumbnail generation
5. **DeviceControlService** - Remote commands (Restart, Screenshot, Volume, etc.)
6. **ScheduleService** - Time-based layout scheduling, recurring schedules
7. **DataSourceService** - SQL/API data sources, Scriban integration
8. **TemplateService** - 11 built-in templates, Scriban rendering
9. **CommandHistoryService** - Undo/Redo for designer
10. **BackgroundUpdateService** - Automatic data refresh (5min interval)
11. **HeartbeatMonitoringService** - Client health monitoring (30s timeout)
12. **DatabaseInitializationService** - Auto migrations, seed data
13. **ScreenshotService** - Remote screenshot capture
14. **CertificateService** - SSL certificate generation
15. **AlertService** - System alerts (UI pending)
16. **StatisticsService** - Usage analytics
17. **LoggingService** - Centralized logging
18. **SettingsService** - Application settings
19. **NetworkDiscoveryService** - mDNS/UDP auto-discovery
20. **BackupService** - Database backup
21. **UpdateService** - Auto-update mechanism

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
sudo git clone https://github.com/manur84/digitalsignage.git /opt/digitalsignage-client
cd /opt/digitalsignage-client/src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh
sudo nano /opt/digitalsignage-client/config.json  # Configure
sudo systemctl start digitalsignage-client
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
cd /opt/digitalsignage-client
sudo git pull && sudo ./update.sh
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