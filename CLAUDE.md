# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Digital Signage Management System** - A client-server digital signage solution consisting of:
- **Windows Server/Manager**: WPF/.NET 8 desktop application with visual designer
- **Raspberry Pi Clients**: Python 3.9+ PyQt5 display client
- **WebSocket Communication**: Real-time bidirectional communication
- **SQLite Database**: Cross-platform data persistence

## Build and Run Commands

### Server (Windows .NET 8 WPF)

```bash
# Build the solution
dotnet build DigitalSignage.sln

# Build in Release mode
dotnet build -c Release

# Run the server
dotnet run --project src/DigitalSignage.Server/DigitalSignage.Server.csproj

# Clean build artifacts
dotnet clean

# Restore NuGet packages
dotnet restore

# Run tests
dotnet test

# Publish self-contained Windows executable
dotnet publish src/DigitalSignage.Server/DigitalSignage.Server.csproj -c Release -r win-x64 --self-contained
```

### Database Migrations

```bash
# Navigate to Data project
cd src/DigitalSignage.Data

# Create new migration
dotnet ef migrations add MigrationName --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj

# Apply migrations (happens automatically on server startup via DatabaseInitializationService)
dotnet ef database update --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj

# Remove last migration
dotnet ef migrations remove --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj
```

### Client (Raspberry Pi Python)

```bash
# Install client as systemd service (creates venv automatically)
cd src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh

# Manual venv setup for development
python3 -m venv --system-site-packages venv
source venv/bin/activate
pip install -r requirements.txt

# Run client in test mode
source venv/bin/activate
python client.py --test-mode

# Service management
sudo systemctl status digitalsignage-client
sudo systemctl restart digitalsignage-client
sudo journalctl -u digitalsignage-client -f

# Update client on Raspberry Pi
cd /opt/digitalsignage-client
sudo git pull
sudo ./update.sh
```

## Architecture

### Solution Structure

```
digitalsignage/
├── src/
│   ├── DigitalSignage.Server/          # WPF Application (MVVM)
│   │   ├── Views/                      # XAML views
│   │   ├── ViewModels/                 # ViewModels with CommunityToolkit.Mvvm
│   │   ├── Services/                   # Business logic layer
│   │   ├── Controls/                   # Custom WPF controls
│   │   ├── Converters/                 # Value converters
│   │   └── Configuration/              # Settings classes
│   ├── DigitalSignage.Core/            # Shared models and interfaces
│   │   ├── Models/                     # Domain models (DisplayElement, etc.)
│   │   ├── Interfaces/                 # Service contracts
│   │   └── Helpers/                    # Utility classes
│   ├── DigitalSignage.Data/            # Data access layer
│   │   ├── Entities/                   # EF Core entities
│   │   ├── Services/                   # DbContext and repositories
│   │   └── Migrations/                 # EF Core migrations
│   └── DigitalSignage.Client.RaspberryPi/  # Python client
│       ├── client.py                   # Main entry point
│       ├── display_renderer.py         # PyQt5 rendering engine
│       ├── device_manager.py           # Hardware monitoring
│       ├── cache_manager.py            # SQLite offline cache
│       └── config.py                   # Configuration
└── docs/                               # Documentation
```

### Key Design Patterns

**MVVM Pattern:**
- All UI follows Model-View-ViewModel pattern
- Views defined in XAML (`.xaml` files in `Views/`)
- ViewModels use `CommunityToolkit.Mvvm` with `[ObservableProperty]` and `[RelayCommand]` attributes
- No business logic in code-behind (`.xaml.cs` files)
- Data binding between View and ViewModel

**Dependency Injection:**
- Configured in `App.xaml.cs` using `Microsoft.Extensions.DependencyInjection`
- ViewModels registered as Singleton or Transient
- Services registered with appropriate lifetime
- Background services registered as `IHostedService`

**Repository Pattern:**
- `DigitalSignageDbContext` for EF Core operations (SQLite)
- Service layer (`ClientService`, `LayoutService`, etc.) wraps data access
- Async/await used throughout for I/O operations

**Custom Indexer Pattern for DisplayElement:**
- `DisplayElement` uses custom indexer `element["PropertyName"]` for WPF binding
- **Critical:** WPF indexer binding requires `OnPropertyChanged("Item[]")` with empty brackets
- Properties dictionary with type-safe defaults via `GetDefaultForKey()`
- All numeric defaults MUST be `Double` (e.g., `2.0` not `2`) for WPF compatibility

### WebSocket Communication Protocol

**Message Types:**
- `REGISTER` - Client registration with device info
- `REGISTRATION_RESPONSE` - Server response with client ID
- `HEARTBEAT` - Keep-alive (every 30s)
- `DISPLAY_UPDATE` - Layout updates sent to client
- `STATUS_REPORT` - Client metrics and status
- `COMMAND` - Remote commands (RESTART, SCREENSHOT, SCREEN_ON/OFF, SET_VOLUME, CLEAR_CACHE)
- `SCREENSHOT` - Screenshot data transfer
- `UPDATE_CONFIG` - Server sends configuration to client
- `UPDATE_CONFIG_RESPONSE` - Client confirms config update

**Implementation:**
- Server: `WebSocketCommunicationService` with in-memory client registry
- Client: `python-socketio` with automatic reconnection
- Offline mode: SQLite cache fallback when disconnected

### Database Architecture (SQLite)

**Key Entities:**
- `DisplayLayout` - Layout definitions with resolution, background, elements (JSON)
- `LayoutTemplate` - Pre-built templates with categories
- `RaspberryPiClient` - Client device registration and status
- `DataSource` - SQL data source configurations
- `MediaFile` - Media library with SHA256 deduplication
- `ClientRegistrationToken` - Token-based client registration
- `ApiKey` - API authentication with usage tracking
- `User` - User accounts with password hashing
- `AuditLog` - Change tracking

**Connection String:**
- Configured in `appsettings.json` under `ConnectionStrings:DefaultConnection`
- Default: `Data Source=digitalsignage.db`
- Migrations applied automatically at startup via `DatabaseInitializationService`

### Designer Canvas Implementation

**DesignerCanvas Control:**
- Custom Canvas control in `Controls/DesignerCanvas.cs`
- **Critical:** Mouse event handling requires `e.Source == this` check to prevent hijacking child element clicks
- Grid rendering with configurable size and snap-to-grid
- Selection rectangle for multi-select
- Drag-and-drop from toolbar

**Element Selection:**
- Event handlers in `MainWindow.xaml.cs`
- `Element_MouseLeftButtonDown` for selection
- `Element_MouseMove` for dragging
- `Element_MouseLeftButtonUp` to release
- Ctrl+Click for multi-selection
- **Important:** DesignerCanvas must NOT capture mouse on child element clicks

**Properties Panel:**
- Binds to `DesignerViewModel.SelectedElement`
- Two-way binding with `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged`
- ComboBox bindings use `SelectedValue` with `SelectedValuePath="Content"`
- Type-specific properties shown/hidden via converters

## Critical Implementation Details

### WPF Binding Requirements

**DisplayElement Indexer Binding:**
```csharp
// In DisplayElement.cs
public object? this[string key]
{
    get { /* ... */ }
    set
    {
        _properties[key] = value;
        OnPropertyChanged("Item[]");        // CRITICAL: Empty brackets!
        OnPropertyChanged($"Item[{key}]");  // Also fire specific property
    }
}
```

**XAML Binding Syntax:**
```xml
<!-- Correct -->
<TextBox Text="{Binding SelectedElement.[FontFamily], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>

<!-- ComboBox - use SelectedValue, not SelectedItem -->
<ComboBox SelectedValue="{Binding SelectedElement.[FontFamily], Mode=TwoWay}"
          SelectedValuePath="Content">
    <ComboBoxItem Content="Arial"/>
    <ComboBoxItem Content="Verdana"/>
</ComboBox>
```

**Type Conversion:**
- All numeric properties MUST be `Double` (not `Int32`)
- Example: `BorderThickness = 2.0` not `2`
- WPF throws `InvalidCastException` for Int32 to Double conversion

**Run.Text Binding Mode:**
- `Run.Text` bindings default to TwoWay in some WPF versions
- **CRITICAL:** Always use `Mode=OneWay` for readonly properties
- Example: `<Run Text="{Binding SelectionCount, Mode=OneWay}"/>`
- Failure to do so causes: `InvalidOperationException: TwoWay binding doesn't work with readonly property`

### Converter Registration

**App.xaml Resource Registration:**
All converters MUST be registered in `App.xaml`:
```xml
<Application.Resources>
    <converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
    <converters:BoolToVisibilityStringConverter x:Key="BoolToVisibilityStringConverter"/>
    <converters:BoolToOnOffStringConverter x:Key="BoolToOnOffStringConverter"/>
    <!-- etc. -->
</Application.Resources>
```

**Common Error:**
`XamlParseException: "Resource with name 'ConverterName' cannot be found"`
→ Solution: Add converter to `App.xaml` resources

### Mouse Event Handling in DesignerCanvas

**Critical Fix:**
```csharp
private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    // CRITICAL: Only capture if clicking on canvas itself, not child elements
    if (e.Source == this && Keyboard.Modifiers == ModifierKeys.None)
    {
        _selectionStartPoint = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }
    // If clicking child element, do NOT capture - let event bubble to element handlers
}
```

Without this check, DesignerCanvas intercepts all mouse events and prevents element selection.

### Python Client - Virtual Environment

**Python 3.11+ Requirement:**
Due to PEP 668 "externally-managed-environment", Python packages must be installed in venv:

```bash
# install.sh creates venv with --system-site-packages flag
python3 -m venv --system-site-packages venv

# This allows access to system-installed PyQt5 while isolating other packages
# System packages: python3-pyqt5, python3-psutil
# Venv packages: python-socketio, qrcode, pillow, etc.
```

**Display Detection:**
- `start-with-display.sh` automatically detects DISPLAY environment
- If X11 running (DISPLAY=:0) → use X11
- If no X11 → start Xvfb on DISPLAY=:99 (headless mode)

### Background Services

Registered as `IHostedService` in `App.xaml.cs`:
- `DatabaseInitializationService` - Applies EF migrations at startup
- `DataRefreshService` - Polls SQL data sources, sends updates to clients
- `HeartbeatMonitoringService` - Monitors client heartbeats (120s timeout)

### Logging

**Serilog Configuration:**
- Console, Debug, and File sinks
- Rolling files: daily, 30 days retention (info), 90 days (errors)
- UISink for live logs in UI (`_liveLogMessages` ObservableCollection)
- Configured in `appsettings.json`

**Client Logging:**
- Python `logging` framework
- Integrated with systemd journal
- View with: `journalctl -u digitalsignage-client -f`

## Adding New Features

### Creating a New ViewModel

1. Create class inheriting from `ObservableObject` (CommunityToolkit.Mvvm)
2. Use `[ObservableProperty]` for bindable properties
3. Use `[RelayCommand]` for commands (support `CanExecute` with attribute parameter)
4. Register in DI container in `App.xaml.cs`:
```csharp
services.AddSingleton<YourViewModel>();
```

### Creating a New View

1. Add XAML file in `Views/` folder
2. Set DataContext in code-behind constructor:
```csharp
public YourView(YourViewModel viewModel)
{
    InitializeComponent();
    DataContext = viewModel;
}
```
3. Use data binding in XAML: `{Binding PropertyName}`

### Adding a New Converter

1. Create class implementing `IValueConverter` in `Converters/`
2. Register in `App.xaml` resources:
```xml
<converters:YourConverter x:Key="YourConverter"/>
```
3. Use in XAML:
```xml
<TextBlock Text="{Binding Value, Converter={StaticResource YourConverter}}"/>
```

### Adding Database Entity

1. Create entity class in `DigitalSignage.Data/Entities/`
2. Add `DbSet<YourEntity>` to `DigitalSignageDbContext.cs`
3. Configure in `OnModelCreating` using Fluent API
4. Generate migration:
```bash
cd src/DigitalSignage.Data
dotnet ef migrations add AddYourEntity --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj
```
5. Migration applies automatically on next server start

### Adding WebSocket Message Type

1. Add message type to enum (if needed)
2. Update `MessageHandlerService` for server-side handling
3. Update `client.py` for client-side handling
4. Ensure JSON serialization compatibility
5. Test offline scenarios (client should cache/retry)

## Git Workflow

**CRITICAL: Always push after every change!**

```bash
# After making ANY changes:
git add -A
git commit -m "Description

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"
git push
```

**Environment Configuration:**
- GitHub credentials stored in `.env` (not committed)
- `.env` format:
```
GITHUBTOKEN=your_token
GITHUBREPO=https://github.com/user/repo.git
```
- Configure git remote with token:
```bash
source .env && git remote set-url origin "https://${GITHUBTOKEN}@github.com/manur84/digitalsignage.git"
```

## Common Issues and Solutions

### Build Warnings (Non-Critical)

Common warnings that can be safely ignored:
- CS8622: Nullable reference type mismatch (Touch event handlers)
- CS8602/CS8603: Possible null reference (Converter return values)
- CS1998: Async method lacks await operators (placeholder methods)
- CS0169/CS0414: Unused fields (future features, clipboard)

### XamlParseException on Startup

**Symptom:** `Resource 'ConverterName' cannot be found`
**Solution:** Register converter in `App.xaml` resources section

**Symptom:** `TargetType mismatch` for Button vs ToggleButton
**Solution:** Use `TargetType="ButtonBase"` for shared styles

### Property Changes Don't Update Canvas

**Symptom:** Changing Properties Panel values doesn't update visual elements
**Root Cause:** Missing `OnPropertyChanged("Item[]")` in DisplayElement indexer
**Solution:** Ensure indexer setter calls `OnPropertyChanged("Item[]")` with empty brackets

### Element Selection Not Working

**Symptom:** Cannot click elements on canvas
**Root Cause:** DesignerCanvas capturing all mouse events
**Solution:** Check `e.Source == this` before `CaptureMouse()` in DesignerCanvas

### Client Service Crashes on Raspberry Pi

**Diagnose:**
```bash
sudo journalctl -u digitalsignage-client -n 100 --no-pager
/opt/digitalsignage-client/venv/bin/python3 -c "import PyQt5; print('OK')"
```

**Fix:**
```bash
sudo /opt/digitalsignage-client/fix-installation.sh
```

## Project Status

**Current Implementation: ~95% Complete**

**Fully Implemented:**
- ✅ Visual Designer with drag-and-drop
- ✅ Device Management with remote control
- ✅ Template system (11 built-in templates)
- ✅ Scriban template engine integration
- ✅ Client registration with tokens
- ✅ TLS/SSL encryption support
- ✅ Offline cache (SQLite)
- ✅ systemd service with watchdog
- ✅ Media library backend (EnhancedMediaService)
- ✅ Professional Toolbar and Status Bar
- ✅ Properties Panel with all element types
- ✅ Multi-selection and multi-move
- ✅ QR Code properties UI
- ✅ DateTime auto-update on clients
- ✅ Media Browser Dialog

**Remaining Tasks:**
- ⚠️ Data Sources UI (query builder)
- ⚠️ Layout scheduling system
- ⚠️ Auto-discovery (UDP broadcast)
- ⚠️ Undo/Redo system
- ⚠️ MSI installer

## Code Style

**C#:**
- Follow Microsoft C# Coding Conventions
- Use nullable reference types (`#nullable enable`)
- Prefer `string.Empty` over `""`
- Async methods suffixed with `Async`
- Use XML documentation for public APIs

**Python:**
- Follow PEP 8
- Use type hints for function signatures
- Write docstrings for public functions/classes
- Use `snake_case` for variables and functions

**XAML:**
- Use data binding instead of code-behind
- Name controls only when necessary (`x:Name`)
- Use StaticResource for styles and converters
- Organize with Grid or StackPanel layouts
