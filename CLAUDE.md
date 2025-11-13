# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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
3. **SSH to Raspberry Pi Client:**
```bash
ssh pro@192.168.0.178
# Password: mr412393
```

4. **Update and test on Pi:**
```bash
# Pull latest changes
cd /opt/digitalsignage-client
sudo git pull

# Run update script
sudo ./update.sh

# Check service status
sudo systemctl status digitalsignage-client

# View logs
sudo journalctl -u digitalsignage-client -f
```

5. **Test the changes** on the actual hardware
6. **If issues found:** Fix locally, push to GitHub, repeat

**Quick SSH Command:**
```bash
sshpass -p 'mr412393' ssh pro@192.168.0.178
```

## Build and Run Commands

### Server (Windows .NET 8 WPF)

```bash
# Build solution
dotnet build DigitalSignage.sln

# Build Release
dotnet build -c Release

# Run server
dotnet run --project src/DigitalSignage.Server/DigitalSignage.Server.csproj

# Clean build
dotnet clean

# Restore packages
dotnet restore

# Run tests
dotnet test

# Publish standalone Windows executable
dotnet publish src/DigitalSignage.Server/DigitalSignage.Server.csproj -c Release -r win-x64 --self-contained
```

### Database Migrations (EF Core + SQLite)

```bash
cd src/DigitalSignage.Data

# Create migration
dotnet ef migrations add MigrationName --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj

# Apply migrations (automatic on server startup via DatabaseInitializationService)
dotnet ef database update --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj

# Remove last migration
dotnet ef migrations remove --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj
```

### Client (Raspberry Pi Python)

```bash
# Install as systemd service (creates venv automatically)
cd src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh

# Manual venv for development
python3 -m venv --system-site-packages venv
source venv/bin/activate
pip install -r requirements.txt

# Test mode
source venv/bin/activate
python client.py --test-mode

# Service management
sudo systemctl status digitalsignage-client
sudo systemctl restart digitalsignage-client
sudo systemctl stop digitalsignage-client
sudo journalctl -u digitalsignage-client -f

# Update client on Pi
cd /opt/digitalsignage-client
sudo git pull
sudo ./update.sh
```

## Architecture Overview

### Project Structure

```
digitalsignage/
├── src/
│   ├── DigitalSignage.Server/          # WPF Application (MVVM)
│   │   ├── Views/                      # XAML views
│   │   ├── ViewModels/                 # ViewModels (CommunityToolkit.Mvvm)
│   │   ├── Services/                   # Business logic
│   │   ├── Controls/                   # Custom controls (DesignerCanvas)
│   │   ├── Converters/                 # WPF value converters
│   │   └── Configuration/              # Settings
│   ├── DigitalSignage.Core/            # Shared models & interfaces
│   │   ├── Models/                     # DisplayElement, etc.
│   │   ├── Interfaces/                 # Service contracts
│   │   └── Helpers/                    # Utilities
│   ├── DigitalSignage.Data/            # Data layer (EF Core + SQLite)
│   │   ├── Entities/                   # Database entities
│   │   ├── Services/                   # DbContext, repositories
│   │   └── Migrations/                 # EF migrations
│   └── DigitalSignage.Client.RaspberryPi/  # Python client
│       ├── client.py                   # Main entry
│       ├── display_renderer.py         # PyQt5 renderer
│       ├── device_manager.py           # Hardware monitoring
│       ├── cache_manager.py            # SQLite offline cache
│       └── config.py                   # Configuration
```

### Key Technologies

**Server:**
- .NET 8 WPF with MVVM (CommunityToolkit.Mvvm)
- Entity Framework Core + SQLite
- WebSocket server (System.Net.WebSockets)
- Serilog logging
- Scriban template engine

**Client:**
- Python 3.9+ with PyQt5
- python-socketio for WebSocket
- SQLite for offline cache
- systemd service with watchdog

## Design Patterns

### MVVM Pattern (Server)

**ViewModels:**
- Use `[ObservableProperty]` for bindable properties
- Use `[RelayCommand]` for commands
- Example:
```csharp
public partial class MyViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [RelayCommand]
    private void DoSomething()
    {
        // Command logic
    }
}
```

**Views:**
- XAML files in `Views/` folder
- Data binding to ViewModel properties
- NO business logic in code-behind

**Dependency Injection:**
- Configured in `App.xaml.cs`
- Register ViewModels and Services
```csharp
services.AddSingleton<MyViewModel>();
services.AddScoped<MyService>();
```

### DisplayElement Custom Indexer Pattern

**Critical for WPF binding:**
```csharp
// DisplayElement.cs
public object? this[string key]
{
    get => _properties.TryGetValue(key, out var value) ? value : GetDefaultForKey(key);
    set
    {
        _properties[key] = value;
        OnPropertyChanged("Item[]");        // ⚠️ CRITICAL: Empty brackets!
        OnPropertyChanged($"Item[{key}]");
    }
}
```

**XAML Usage:**
```xml
<TextBox Text="{Binding SelectedElement.[FontFamily], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
```

### WebSocket Communication

**Message Types:**
- `REGISTER` - Client registration
- `HEARTBEAT` - Keep-alive (30s interval)
- `DISPLAY_UPDATE` - Layout update to client
- `COMMAND` - Remote commands (RESTART, SCREENSHOT, etc.)
- `STATUS_REPORT` - Client metrics

**Server:** `WebSocketCommunicationService`
**Client:** `python-socketio` with auto-reconnect

### Database (SQLite)

**Key Entities:**
- `DisplayLayout` - Layout definitions (JSON elements)
- `RaspberryPiClient` - Client devices
- `DataSource` - SQL data sources
- `MediaFile` - Media library (SHA256 deduplication)
- `ClientRegistrationToken` - Token-based registration

**Connection String:** `appsettings.json` → `ConnectionStrings:DefaultConnection`
**Auto-Migration:** `DatabaseInitializationService` applies migrations on startup

## Critical WPF Binding Rules

### 1. DisplayElement Indexer Binding

**MUST use `OnPropertyChanged("Item[]")` with empty brackets:**
```csharp
set
{
    _properties[key] = value;
    OnPropertyChanged("Item[]");  // ⚠️ Required for WPF indexer binding
}
```

### 2. Numeric Properties Must Be Double

```csharp
// ✅ Correct
BorderThickness = 2.0

// ❌ Wrong (causes InvalidCastException)
BorderThickness = 2
```

### 3. ComboBox Binding

**Use `SelectedValue` with `SelectedValuePath`:**
```xml
<!-- ✅ Correct -->
<ComboBox SelectedValue="{Binding Element.[FontFamily], Mode=TwoWay}"
          SelectedValuePath="Content">
    <ComboBoxItem Content="Arial"/>
</ComboBox>

<!-- ❌ Wrong (stores ComboBoxItem object) -->
<ComboBox SelectedItem="{Binding Element.[FontFamily]}">
```

### 4. Run.Text Binding Mode

**ALWAYS use `Mode=OneWay` for readonly properties:**
```xml
<!-- ✅ Correct -->
<Run Text="{Binding SelectionCount, Mode=OneWay}"/>

<!-- ❌ Wrong (causes InvalidOperationException) -->
<Run Text="{Binding SelectionCount}"/>
```

**Error without Mode=OneWay:**
`InvalidOperationException: TwoWay binding doesn't work with readonly property`

### 5. Converter Registration

**All converters MUST be registered in App.xaml:**
```xml
<Application.Resources>
    <converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
    <converters:BoolToVisibilityStringConverter x:Key="BoolToVisibilityStringConverter"/>
    <!-- etc. -->
</Application.Resources>
```

**Error if missing:**
`XamlParseException: Resource 'ConverterName' cannot be found`

### 6. DesignerCanvas Mouse Event Handling

**Critical fix for element selection:**
```csharp
private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    // ⚠️ CRITICAL: Only capture if clicking canvas itself, not child elements
    if (e.Source == this && Keyboard.Modifiers == ModifierKeys.None)
    {
        _selectionStartPoint = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }
    // Without this check, elements can't be selected!
}
```

## Adding New Features

### New ViewModel

1. Create class inheriting `ObservableObject`
2. Use `[ObservableProperty]` and `[RelayCommand]`
3. Register in `App.xaml.cs`:
```csharp
services.AddSingleton<YourViewModel>();
```

### New View

1. Add XAML in `Views/`
2. Set DataContext in constructor:
```csharp
public YourView(YourViewModel vm)
{
    InitializeComponent();
    DataContext = vm;
}
```

### New Converter

1. Implement `IValueConverter` in `Converters/`
2. Register in `App.xaml` resources
3. Use: `{Binding Value, Converter={StaticResource YourConverter}}`

### New Database Entity

1. Create in `DigitalSignage.Data/Entities/`
2. Add `DbSet<T>` to `DigitalSignageDbContext`
3. Configure in `OnModelCreating`
4. Create migration:
```bash
cd src/DigitalSignage.Data
dotnet ef migrations add AddYourEntity --startup-project ../DigitalSignage.Server
```

### New WebSocket Message

1. Add message type constant
2. Update `MessageHandlerService` (server)
3. Update `client.py` (client)
4. Test offline scenarios

## Python Client - Virtual Environment

**Python 3.11+ requires venv (PEP 668):**
```bash
# install.sh creates venv with --system-site-packages
python3 -m venv --system-site-packages venv

# System packages: PyQt5, psutil
# Venv packages: python-socketio, qrcode, pillow
```

**Display Detection:**
- `start-with-display.sh` auto-detects DISPLAY
- X11 running (DISPLAY=:0) → use X11
- No X11 → start Xvfb (DISPLAY=:99) headless mode

## Background Services

Registered as `IHostedService` in `App.xaml.cs`:
- `DatabaseInitializationService` - Apply EF migrations at startup
- `DataRefreshService` - Poll SQL sources, push updates
- `HeartbeatMonitoringService` - Monitor clients (120s timeout)

## Logging

**Server (Serilog):**
- Console, Debug, File sinks
- Rolling daily files (30 days retention)
- Live UI logs via UISink
- Config in `appsettings.json`

**Client (Python):**
- systemd journal integration
- View: `journalctl -u digitalsignage-client -f`

## Common Build Warnings (Ignorable)

These warnings are non-critical:
- CS8622: Nullable reference type mismatch (Touch handlers)
- CS8602/CS8603: Possible null reference (Converters)
- CS1998: Async method lacks await (Placeholder methods)
- CS0169/CS0414: Unused fields (Future features)

## Common Issues & Solutions

### XamlParseException on Startup

**Symptom:** `Resource 'ConverterName' cannot be found`
**Solution:** Register converter in `App.xaml` resources

**Symptom:** `TargetType mismatch` for Button/ToggleButton
**Solution:** Use `TargetType="ButtonBase"` for shared styles

### Property Changes Don't Update Canvas

**Root Cause:** Missing `OnPropertyChanged("Item[]")` in DisplayElement
**Solution:** Ensure indexer setter calls `OnPropertyChanged("Item[]")`

### Element Selection Not Working

**Root Cause:** DesignerCanvas capturing all mouse events
**Solution:** Add `e.Source == this` check before `CaptureMouse()`

### TwoWay Binding Error on Readonly Property

**Symptom:** `InvalidOperationException` on readonly property
**Solution:** Add `Mode=OneWay` to `Run.Text` bindings

### Client Service Crashes on Pi

**Diagnose:**
```bash
sudo journalctl -u digitalsignage-client -n 100 --no-pager
/opt/digitalsignage-client/venv/bin/python3 -c "import PyQt5; print('OK')"
```

**Fix:**
```bash
sudo /opt/digitalsignage-client/fix-installation.sh
```

## Testing Changes on Raspberry Pi

**Complete workflow:**

1. **Make code changes** (e.g., in `src/DigitalSignage.Client.RaspberryPi/`)
2. **Commit and push to GitHub:**
```bash
source .env
git add -A
git commit -m "Your changes"
git push
```

3. **SSH to Raspberry Pi:**
```bash
ssh pro@192.168.0.178
# Password: mr412393
```

4. **Update and test:**
```bash
cd /opt/digitalsignage-client
sudo git pull
sudo ./update.sh

# Monitor logs
sudo journalctl -u digitalsignage-client -f
```

5. **Verify:**
- Check service status: `sudo systemctl status digitalsignage-client`
- View display output on HDMI monitor
- Check for errors in logs

## Code Style

**C#:**
- Microsoft C# Coding Conventions
- Nullable reference types enabled
- `string.Empty` over `""`
- Async methods suffixed with `Async`
- XML docs for public APIs

**Python:**
- PEP 8 style guide
- Type hints for functions
- Docstrings for public functions
- `snake_case` naming

**XAML:**
- Data binding over code-behind
- Name controls only when needed
- StaticResource for styles/converters
- Grid/StackPanel for layouts
