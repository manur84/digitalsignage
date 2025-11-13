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
3. **SSH to Raspberry Pi:**
```bash
ssh pro@192.168.0.178
# Password: mr412393

# Or quick command:
sshpass -p 'mr412393' ssh pro@192.168.0.178
```

4. **Update and test:**
```bash
cd /opt/digitalsignage-client
sudo git pull
sudo ./update.sh

# Monitor logs
sudo journalctl -u digitalsignage-client -f

# Check service
sudo systemctl status digitalsignage-client
```

5. **Test on actual hardware** - verify display output on HDMI monitor
6. **If issues:** Fix locally → push to GitHub → repeat

---

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
# Install as systemd service
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

---

## Project Architecture

### Solution Structure (3 C# Projects + 1 Python Client)

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
├── DESIGNER_IMPROVEMENTS_PLAN.md               # Designer enhancements
└── REFACTORING_PLAN.md                         # Architecture refactoring
```

### Server Application Structure

```
DigitalSignage.Server/
├── Assets/                      # Images, icons, resources
├── Behaviors/                   # WPF attached behaviors
├── Commands/                    # Undo/Redo command pattern
│   ├── IUndoableCommand.cs
│   ├── CommandHistory.cs
│   ├── AddElementCommand.cs
│   ├── DeleteElementCommand.cs
│   ├── MoveElementCommand.cs
│   ├── ResizeElementCommand.cs
│   ├── ChangePropertyCommand.cs
│   └── ChangeZIndexCommand.cs
├── Configuration/               # App configuration classes
│   ├── ServerSettings.cs
│   ├── QueryCacheSettings.cs
│   └── ConnectionPoolSettings.cs
├── Controls/                    # Custom WPF controls
│   ├── DesignerCanvas.cs       # Main designer canvas
│   ├── DesignerItemControl.cs  # Element rendering
│   ├── ResizeAdorner.cs        # Resize handles
│   ├── AlignmentGuidesAdorner.cs
│   ├── ResizableElement.cs
│   └── ColorPicker.xaml
├── Converters/                  # 18 Value Converters
│   ├── BoolToVisibilityConverter.cs
│   ├── BoolToVisibilityStringConverter.cs
│   ├── BoolToOnOffStringConverter.cs
│   ├── InverseBoolToVisibilityConverter.cs
│   ├── InverseBooleanConverter.cs
│   ├── NullToBooleanConverter.cs
│   ├── StringFormatConverter.cs
│   ├── FileSizeConverter.cs
│   ├── MediaTypeToIconConverter.cs
│   ├── MediaTypeToStringConverter.cs
│   ├── ElementTypeIconConverter.cs
│   ├── HexToColorConverter.cs
│   ├── StatusToColorConverter.cs
│   ├── TestResultToColorConverter.cs
│   ├── FontWeightToBoolConverter.cs
│   ├── FontStyleToBoolConverter.cs
│   └── TypeEqualsConverter.cs
├── Helpers/                     # Utility classes
│   ├── RelayCommand.cs
│   └── UndoRedoManager.cs
├── Services/                    # 21 Business services (see below)
├── ViewModels/                  # 15 ViewModels (see below)
├── Views/                       # XAML views & dialogs
│   ├── Common/
│   ├── DataSources/
│   ├── Designer/
│   │   ├── DesignerToolbar.xaml
│   │   ├── DesignerStatusBar.xaml
│   │   ├── ToolboxPanel.xaml
│   │   ├── PropertiesPanel.xaml (27KB)
│   │   └── LayersPanel.xaml (13KB)
│   ├── DeviceManagement/
│   ├── Dialogs/
│   │   ├── MediaBrowserDialog.xaml
│   │   ├── GridConfigDialog.xaml
│   │   ├── KeyboardShortcutsDialog.xaml
│   │   ├── NewLayoutDialog.xaml
│   │   ├── LayoutSelectionDialog.xaml
│   │   ├── TemplateSelectionWindow.xaml
│   │   ├── ScreenshotWindow.xaml
│   │   └── DatabaseConnectionDialog.xaml
│   └── MainWindow.xaml (154KB - main app window)
├── App.xaml.cs                  # DI configuration & startup
├── Program.cs                   # URL ACL checks
└── appsettings.json            # Server configuration
```

### Python Client Structure

```
DigitalSignage.Client.RaspberryPi/
├── client.py                    # Main entry point
├── display_renderer.py          # PyQt5 layout rendering
├── cache_manager.py             # SQLite offline cache
├── device_manager.py            # Hardware monitoring
├── config.py                    # Configuration management
├── watchdog_monitor.py          # systemd watchdog
├── status_screen.py             # Status UI overlays
├── discovery.py                 # Server auto-discovery
├── remote_log_handler.py        # Remote logging
├── web_interface.py             # Flask web UI
├── test_status_screens.py       # Testing utility
├── requirements.txt             # Python dependencies
├── config.json                  # Client configuration
├── install.sh                   # Installation script
├── start-with-display.sh        # Display management
├── diagnose.sh                  # Diagnostic tool
├── enable-autologin-x11.sh      # Production setup
├── configure-display.sh         # Display config
├── test-connection.sh           # Network testing
├── update.sh                    # Client updater
├── fix-installation.sh          # Repair script
└── digitalsignage-client.service # systemd unit file
```

---

## Server Services (21 Services)

### Core Communication & Client Management

**1. WebSocketCommunicationService.cs**
- HttpListener-based WebSocket server
- URL ACL checking with automatic fallback to localhost
- SSL/TLS support with certificate binding
- Connection management (ConcurrentDictionary)
- Events: MessageReceived, ClientConnected, ClientDisconnected
- Port auto-selection: 8080 → 8081 → 8082 → 8083 → 8888 → 9000

**2. ClientService.cs**
- Client registry (in-memory + database persistence)
- Token-based registration authentication
- Client status tracking (Online, Offline, Error)
- Layout assignment and updates
- Events: ClientConnected, ClientDisconnected, ClientStatusChanged

**3. MessageHandlerService.cs**
- WebSocket message routing & processing
- Handles: REGISTER, HEARTBEAT, DISPLAY_UPDATE, COMMAND, STATUS_REPORT, SCREENSHOT
- Screenshot event handling
- Command execution (RESTART, SCREENSHOT, SCREEN_ON/OFF, SET_VOLUME, CLEAR_CACHE)
- Configuration updates (UPDATE_CONFIG)

### Background Services (IHostedService)

**4. DatabaseInitializationService.cs**
- Applies EF Core migrations on startup
- Seeds default admin user
- Seeds 11 layout templates
- Ensures database exists

**5. HeartbeatMonitoringService.cs**
- Monitors client heartbeats every 30s
- Marks clients offline after 120s timeout
- Triggers ClientDisconnected events

**6. DataRefreshService.cs**
- Polls SQL data sources at configured intervals
- Sends data updates to clients via WebSocket
- Caches query results via QueryCacheService

**7. DiscoveryService.cs**
- UDP broadcast discovery service
- Broadcasts server info on port 8888
- Allows clients to auto-discover server

**8. MdnsDiscoveryService.cs**
- mDNS/Zeroconf service advertisement
- Service type: `_digitalsignage._tcp`
- Cross-platform discovery

**9. AlertMonitoringService.cs**
- Monitors alert rules (client offline, data source errors)
- Triggers alerts based on conditions
- Background alert checking

### Data & Template Services

**10. LayoutService.cs**
- Layout CRUD operations
- Layout versioning and metadata
- Element management

**11. TemplateService.cs**
- Scriban template engine integration
- Server-side variable substitution
- Template rendering with data sources
- Built-in helper functions (date_format, etc.)

**12. EnhancedMediaService.cs**
- Media library management
- SHA256 hash-based deduplication
- File upload/download with MIME type detection
- 100MB file size limit
- Access tracking (LastAccessedAt, AccessCount)
- Supported: Images (JPG/PNG/GIF/BMP/WEBP/SVG), Videos, Audio, Documents

**13. DataSourceRepository.cs**
- SQL data source CRUD operations
- Connection string management
- Query validation

**14. SqlDataService.cs**
- Dapper-based parameterized query execution
- Supports: SQL Server, PostgreSQL, MySQL, SQLite
- Query result caching
- Connection pooling

### Utility Services

**15. QueryCacheService.cs**
- In-memory query result caching
- Configurable TTL (default 300s)
- LRU eviction policy (max 1000 entries)
- Cache statistics

**16. AuthenticationService.cs**
- User password hashing (SHA256, should use BCrypt/Argon2)
- API key validation
- Token-based client registration

**17. AlertService.cs**
- Alert rule CRUD
- Alert triggering and acknowledgment
- Alert history tracking

**18. LogStorageService.cs**
- Stores client logs to database
- Log querying and filtering

**19. UISink.cs**
- Serilog custom sink for live UI logs
- ObservableCollection for WPF binding
- Max 2000 messages in memory

**20. AlignmentService.cs**
- Designer alignment helpers
- Align elements: left, right, top, bottom, center
- Snap-to-grid functionality
- Alignment guides rendering

**21. SelectionService.cs**
- Designer element selection management
- Multi-select support
- Primary/secondary selection tracking
- Selection change events
- ReadOnlyObservableCollection<DisplayElement>

---

## ViewModels (15 ViewModels)

All use **CommunityToolkit.Mvvm** with `[ObservableProperty]` and `[RelayCommand]`.

**1. MainViewModel.cs** - Application root
- Hosts all child ViewModels
- Client list management
- Server status tracking
- Communication event handling

**2. DesignerViewModel.cs** - Visual layout designer
- Element collection management
- Tool selection (select, text, image, rectangle, circle, qrcode, table, datetime)
- Zoom (25%-400%), grid, snap-to-grid
- Undo/Redo via CommandHistory
- Selection via SelectionService
- Commands: AddElement, DeleteSelected, DuplicateSelected, Undo, Redo, SaveLayout, LoadLayout
- Status message and unsaved changes tracking

**3. DeviceManagementViewModel.cs** - Device control panel
- Client list with status indicators (Online/Offline/Error)
- Remote commands: Restart Device, Restart App, Screenshot, Clear Cache
- Screen control: On/Off (via xset DPMS)
- Volume control: 0-100% (via amixer)
- Layout assignment via ComboBox
- Configuration updates (server host/port, SSL, fullscreen, log level)

**4. PreviewViewModel.cs** - Layout preview
- Real-time layout rendering with test data
- Data source selection for preview
- Refresh on layout changes
- Zoom controls

**5. DataSourceViewModel.cs** - Data source management
- Data source CRUD
- Connection testing
- Query builder (UI pending)

**6. SchedulingViewModel.cs** - Layout scheduling
- Time-based layout switching
- Day-of-week scheduling (Mon-Sun)
- Client/group targeting
- Priority management (1-100)
- Valid date range (ValidFrom-ValidUntil)

**7. MediaLibraryViewModel.cs** - Media browser
- File upload/download
- Category filtering (Images/Videos/Audio/Documents)
- Tag management
- Preview generation
- Deduplication detection

**8. LogViewerViewModel.cs** - Historical log viewer
- Log filtering by level, source, date
- Export to file
- Search functionality

**9. LiveLogsViewModel.cs** - Real-time log streaming
- Displays logs from UISink ObservableCollection
- Auto-scroll
- Level filtering (Debug/Info/Warning/Error)

**10. ScreenshotViewModel.cs** - Screenshot display
- Shows client screenshots (base64 PNG data)
- Save to file
- Zoom/pan controls

**11. MediaBrowserViewModel.cs** - Media selection dialog
- Browse media library
- Filter by type
- Search by filename
- Select for layout elements (Image elements)

**12. NewLayoutViewModel.cs** - New layout dialog
- Layout name, description
- Resolution selection (1920x1080, 1280x720, 3840x2160, etc.)
- Background color/image selection

**13. LayoutSelectionViewModel.cs** - Layout picker dialog
- List all layouts
- Preview thumbnails
- Filter/search

**14. TemplateSelectionViewModel.cs** - Template picker
- Display 11 built-in templates
- Category filtering (Blank, RoomOccupancy, InformationBoard, Wayfinding, MenuBoard, WelcomeScreen, Emergency)
- Usage tracking (LastUsedAt, UsageCount)

**15. GridConfigViewModel.cs** - Grid settings dialog
- Grid size (5-100px, default 10px)
- Grid color picker
- Grid visibility toggle
- Snap-to-grid toggle
- Grid style (Dots/Lines)

---

## Database Entities (9 Entities + 3 Core Models)

### Entities (Data/Entities/)

**1. User.cs** - User accounts
- Username (unique), Email (unique), PasswordHash, Role
- LastLogin, IsActive, CreatedAt
- Indexes: Username, Email

**2. ApiKey.cs** - API authentication
- Name, KeyHash, UserId
- ExpiresAt, LastUsedAt, UsageCount, IsActive
- Indexes: KeyHash, ExpiresAt, IsActive

**3. ClientRegistrationToken.cs** - Token-based registration
- Token (unique), Description, CreatedByUserId
- MaxUses, UsageCount, ExpiresAt, IsUsed
- AllowedMacAddresses (comma-separated)
- AutoAssignGroup, AutoAssignLocation
- Indexes: Token, ExpiresAt, IsUsed

**4. MediaFile.cs** - Media library
- FileName, OriginalFileName, FilePath, ThumbnailPath
- Type (Image/Video/Audio/Document), MimeType, FileSize
- Hash (SHA256, unique), Description, Tags, Category
- UploadedAt, UploadedByUserId, LastAccessedAt, AccessCount
- Indexes: FileName, Type, Category, Hash, Tags, UploadedAt

**5. LayoutTemplate.cs** - Pre-built templates
- Name, Description
- Category (Blank/RoomOccupancy/InformationBoard/Wayfinding/MenuBoard/WelcomeScreen/Emergency/Custom)
- Resolution (JSON), BackgroundColor, BackgroundImage, ElementsJson (JSON)
- ThumbnailPath, IsBuiltIn, IsPublic
- CreatedAt, CreatedByUserId, LastUsedAt, UsageCount
- Indexes: Name, Category, IsBuiltIn, CreatedAt

**6. LayoutSchedule.cs** - Time-based scheduling
- Name, Description, LayoutId
- ClientId, ClientGroup (targeting)
- StartTime, EndTime (TimeOnly)
- DaysOfWeek (comma-separated: Mon,Tue,Wed,Thu,Fri,Sat,Sun)
- ValidFrom, ValidUntil (DateOnly)
- Priority (1-100), IsActive
- Indexes: LayoutId, ClientId, ClientGroup, IsActive, Priority, StartTime, ValidFrom, ValidUntil

**7. AuditLog.cs** - Change tracking
- Action (Create/Update/Delete), EntityType, EntityId
- UserId, IpAddress, Changes (JSON)
- Timestamp
- Indexes: Timestamp, Action, EntityType+EntityId

**8. Alert.cs** - Alert instances
- Title, Message
- Severity (Info/Warning/Error/Critical)
- EntityType, EntityId, AlertRuleId
- TriggeredAt, AcknowledgedAt, ResolvedAt
- IsAcknowledged, IsResolved
- Indexes: TriggeredAt, IsAcknowledged, IsResolved, Severity, EntityType+EntityId

**9. AlertRule.cs** - Alert definitions
- Name, Description
- RuleType (ClientOffline/DataSourceError/HighCpuUsage/LowDiskSpace/ScreenshotFailed)
- Severity, Threshold, CheckInterval
- NotifyEmails (comma-separated), NotifyWebhook
- IsEnabled, CreatedAt
- Indexes: IsEnabled, RuleType, Severity

### Core Models (stored directly via DbContext)

**RaspberryPiClient** (Core/Models/RaspberryPiClient.cs)
- Id, Name, MacAddress, IpAddress
- Status (Online/Offline/Error)
- Group, Location, AssignedLayoutId
- DeviceInfo (JSON: model, OS version, IP, MAC, CPU, memory, disk, temperature, screen resolution)
- Schedules (JSON array), Metadata (JSON)
- LastSeen, RegisteredAt, IsEnabled

**DisplayLayout** (Core/Models/DisplayLayout.cs)
- Id, Name, Description, Version
- Resolution (JSON: Width, Height)
- BackgroundColor, BackgroundImage
- Elements (JSON array of DisplayElement)
- DataSources (JSON), Metadata (JSON)
- Created, Modified, IsActive

**DataSource** (Core/Models/DataSource.cs)
- Id, Name, Type (SqlServer/PostgreSQL/MySQL/SQLite/StaticData)
- ConnectionString, Query, Parameters (JSON)
- RefreshInterval (seconds), Enabled
- Metadata (JSON), LastRefresh

---

## Python Client Files (11 Files)

**1. client.py** - Main WebSocket client
- Connects to server via WebSocket
- Handles messages: REGISTER, HEARTBEAT, DISPLAY_UPDATE, COMMAND, UPDATE_CONFIG
- Auto-reconnection with exponential backoff
- Auto-discovery via mDNS/UDP
- systemd watchdog keep-alive
- Command-line args: --test-mode, --server, --port

**2. display_renderer.py** - PyQt5 rendering engine
- Renders DisplayLayout with all element types
- Elements: Text, Image, Rectangle, Circle, QR Code, Table, DateTime
- Background color/image support
- Status screens: connecting, disconnected, error
- Element lifecycle management (create, update, destroy)
- QTimer for DateTime auto-update

**3. cache_manager.py** - SQLite offline cache
- Caches layouts in local SQLite database
- Automatic fallback when server disconnected
- Cache metadata: timestamp, version, layout_id
- CRUD operations: save, load, list, delete

**4. device_manager.py** - Hardware monitoring
- System info: CPU usage, memory, disk, temperature (via psutil)
- Raspberry Pi model detection (/proc/device-tree/model)
- Network info: IP address, MAC address
- Display control: screen on/off (xset DPMS), HDMI power (vcgencmd)
- Volume control: amixer (0-100%)
- System restart: systemctl reboot

**5. config.py** - Configuration dataclass
- Client ID (UUID-based persistent ID)
- Server connection: host, port, endpoint, SSL settings
- Registration token for authentication
- Display settings: fullscreen, log level
- Auto-discovery: enabled/disabled
- Remote logging: enabled, batch size (50), interval (5s)
- Methods: load(), save(), update_from_server(), from_env()

**6. watchdog_monitor.py** - systemd watchdog
- Sends sd_notify WATCHDOG=1 keep-alive
- Monitors application health
- systemd auto-restart on failure

**7. status_screen.py** - PyQt5 status overlays
- ConnectingScreen (animated spinner)
- DisconnectedScreen (retry counter)
- ErrorScreen (error message display)
- Fullscreen overlays with transparency

**8. discovery.py** - Server auto-discovery
- mDNS/Zeroconf client (service type: _digitalsignage._tcp)
- UDP broadcast listener (port 8888)
- Automatic server detection and connection
- Fallback to configured server if discovery fails

**9. remote_log_handler.py** - Remote logging
- Python logging.Handler implementation
- Sends logs to server via WebSocket
- Batching: 50 messages per batch, 5s interval
- Level filtering (DEBUG/INFO/WARNING/ERROR)

**10. web_interface.py** - Flask web UI
- Local web server for management (port 5000)
- Status page: system info, service status
- Configuration page: edit config.json
- Restart button, logs viewer
- Auto-starts in background thread

**11. test_status_screens.py** - Testing utility
- Tests status screen rendering
- Command-line selection: connecting/disconnected/error
- PyQt5 QApplication for visual testing

---

## Client Shell Scripts (8 Scripts)

**1. install.sh** - Client installation
- Installs system dependencies (apt: PyQt5, Xvfb, amixer, etc.)
- Creates venv with --system-site-packages flag
- Installs Python requirements.txt
- Verifies PyQt5 accessibility
- Installs systemd service (digitalsignage-client.service)
- Enables service auto-start

**2. start-with-display.sh** - Display management wrapper
- Auto-detects DISPLAY environment
- If X11 running (DISPLAY=:0): use real display
- If no X11: start Xvfb on DISPLAY=:99 (headless)
- Launches client.py with correct DISPLAY
- Used by systemd service

**3. diagnose.sh** - Diagnostic tool
- Checks Python 3 installation
- Tests PyQt5 import
- Verifies all requirements.txt packages
- Tests DISPLAY variable and X server
- Checks service status and logs
- Network connectivity test

**4. enable-autologin-x11.sh** - Production HDMI setup
- Configures autologin for pi user
- Enables X11 on real display (HDMI)
- Disables screensaver and DPMS
- Hides mouse cursor (unclutter)
- Sets up autostart for digital signage

**5. configure-display.sh** - Display configuration
- HDMI settings and resolution
- Monitor detection
- Display mode selection

**6. test-connection.sh** - Network diagnostics
- Tests server connectivity (ping, telnet)
- WebSocket handshake test
- DNS resolution check
- Firewall detection

**7. update.sh** - Client updater
- Stops digitalsignage-client service
- Pulls latest changes from git
- Shows git log of changes
- Updates systemd service file
- Reloads systemd daemon
- Starts service
- Shows status

**8. fix-installation.sh** - Installation repair
- Fixes common installation issues
- Reinstalls Python dependencies
- Repairs venv if broken
- Fixes permissions
- Resets configuration to defaults

---

## Configuration Files

### Server: appsettings.json

**ConnectionStrings:**
- DefaultConnection: `Data Source=digitalsignage.db` (SQLite)

**ServerSettings:**
- Host: `0.0.0.0` (all interfaces)
- Port: `8080` (auto-select if occupied: 8081, 8082, 8083, 8888, 9000)
- AutoSelectPort: `true`
- Endpoint: `/ws/`
- UseSSL: `false`
- CertificatePath: `null`
- CertificatePassword: `null`
- MaxMessageSize: `1048576` (1MB)
- ClientHeartbeatTimeout: `90` (seconds)

**QueryCacheSettings:**
- Enabled: `true`
- DefaultCacheDuration: `300` (seconds)
- MaxCacheSize: `1000` (entries)
- CleanupInterval: `60` (seconds)

**ConnectionPoolSettings:**
- MinPoolSize: `5`
- MaxPoolSize: `100`
- ConnectionTimeout: `30` (seconds)

**Serilog:**
- MinimumLevel: Debug
- WriteTo: Console, Debug, File (daily rolling)
- File retention: 30 days (info), 90 days (errors)
- File size limit: 100MB with rollover
- Enrichers: FromLogContext, WithMachineName, WithThreadId

### Client: config.py

**Dataclass fields:**
- client_id: UUID (persistent)
- server_host: `"192.168.1.100"` (server IP)
- server_port: `8080`
- server_endpoint: `"/ws/"`
- registration_token: `"YOUR_TOKEN"`
- use_ssl: `false`
- verify_ssl: `true`
- fullscreen: `true`
- log_level: `"INFO"`
- enable_discovery: `true` (mDNS/UDP auto-discovery)
- enable_remote_logging: `true`
- remote_log_batch_size: `50`
- remote_log_interval: `5.0` (seconds)

**Methods:**
- `load()` - Load from config.json
- `save()` - Save to config.json
- `update_from_server(config_data)` - Update from server UPDATE_CONFIG message
- `from_env()` - Load from environment variables

---

## Architectural Patterns

### 1. MVVM (Model-View-ViewModel)
- Views: XAML with zero code-behind logic
- ViewModels: CommunityToolkit.Mvvm with `[ObservableProperty]` and `[RelayCommand]`
- Data binding: Two-way with UpdateSourceTrigger=PropertyChanged
- No business logic in Views

### 2. Dependency Injection
- Microsoft.Extensions.DependencyInjection
- Configured in App.xaml.cs
- Service lifetimes: Singleton (services), Scoped (DbContext), Transient (ViewModels)
- DbContextFactory for ViewModel usage

### 3. Repository Pattern
- Service layer abstracts data access
- Interfaces: ILayoutService, IClientService
- Async/await throughout
- EF Core + Dapper for queries

### 4. Command Pattern (Undo/Redo)
- IUndoableCommand interface
- Commands: Add, Delete, Move, Resize, ChangeProperty, ChangeZIndex
- CommandHistory with stack-based undo/redo
- Registered in DesignerViewModel

### 5. Observer Pattern
- Events: ClientConnected, ClientDisconnected, MessageReceived, SelectionChanged
- ObservableCollection for WPF binding
- PropertyChanged via ObservableObject
- INotifyPropertyChanged implementation

### 6. Template Method Pattern
- Scriban template engine
- Server-side variable substitution: `{{Variable}}`
- Client-side runtime substitution for DateTime
- Formatters: `{{date_format DateValue "dd.MM.yyyy"}}`

### 7. Strategy Pattern
- Multiple data source types: SqlServer, PostgreSQL, MySQL, SQLite, StaticData
- Media type handlers: Image, Video, Audio, Document
- Different query execution strategies (Dapper vs EF Core)

### 8. Singleton Pattern
- Services registered as Singleton in DI container
- In-memory caches: QueryCacheService, client registry
- SelectionService, AlignmentService

### 9. Background Service Pattern
- IHostedService for long-running tasks
- Managed lifecycle via .NET Generic Host
- Services: DatabaseInitializationService, DataRefreshService, HeartbeatMonitoringService, AlertMonitoringService

### 10. Message-Based Communication
- JSON messages over WebSocket
- Message types: REGISTER, HEARTBEAT, DISPLAY_UPDATE, COMMAND, STATUS_REPORT, SCREENSHOT, UPDATE_CONFIG
- Async message handling
- Offline queue with retry

---

## Dependencies

### Server NuGet Packages

**UI & MVVM:**
- CommunityToolkit.Mvvm 8.2.2

**Database:**
- Microsoft.EntityFrameworkCore 8.0.0
- Microsoft.EntityFrameworkCore.Sqlite 8.0.0
- Microsoft.EntityFrameworkCore.SqlServer 8.0.0
- Microsoft.EntityFrameworkCore.Design 8.0.0
- Dapper 2.1.24

**Communication:**
- System.Net.WebSockets (built-in)
- Makaretu.Dns.Multicast 0.27.0 (mDNS)

**Dependency Injection & Hosting:**
- Microsoft.Extensions.DependencyInjection 9.0.0
- Microsoft.Extensions.Hosting 9.0.0

**Logging:**
- Serilog 4.2.0
- Serilog.Extensions.Hosting 9.0.0
- Serilog.Settings.Configuration 9.0.0
- Serilog.Sinks.File 6.0.0
- Serilog.Sinks.Console 6.0.0
- Serilog.Sinks.Debug 3.0.0

**Template Engine:**
- Scriban 6.5.0

**JSON:**
- Newtonsoft.Json 13.0.3

**Utilities:**
- System.ServiceProcess.ServiceController 8.0.0

### Python Requirements

```
websocket-client>=1.6.0     # WebSocket client
requests>=2.31.0            # HTTP requests
psutil>=5.9.6               # Hardware monitoring
pillow>=10.3.0              # Image processing
qrcode>=7.4.2               # QR code generation
zeroconf>=0.70.0            # mDNS/Zeroconf
netifaces>=0.11.0           # Network interfaces
qasync>=0.23.0              # Async Qt event loop
flask>=2.3.0                # Web interface
```

**System Packages (apt):**
- python3-pyqt5
- python3-pyqt5.qtsvg
- python3-pyqt5.qtmultimedia
- libqt5multimedia5-plugins
- xvfb (virtual X server)
- x11-utils (xset)
- alsa-utils (amixer)

---

## Critical WPF Binding Rules

### 1. DisplayElement Indexer Binding

**MUST use `OnPropertyChanged("Item[]")` with empty brackets:**
```csharp
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

### 2. Numeric Properties Must Be Double

```csharp
// ✅ Correct
BorderThickness = 2.0
FontSize = 24.0

// ❌ Wrong (causes InvalidCastException)
BorderThickness = 2
FontSize = 24
```

### 3. ComboBox Binding

**Use `SelectedValue` with `SelectedValuePath`:**
```xml
<!-- ✅ Correct -->
<ComboBox SelectedValue="{Binding Element.[FontFamily], Mode=TwoWay}"
          SelectedValuePath="Content">
    <ComboBoxItem Content="Arial"/>
    <ComboBoxItem Content="Verdana"/>
</ComboBox>

<!-- ❌ Wrong (stores ComboBoxItem object instead of string) -->
<ComboBox SelectedItem="{Binding Element.[FontFamily]}">
    <ComboBoxItem>Arial</ComboBoxItem>
</ComboBox>
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
`InvalidOperationException: TwoWay binding doesn't work with readonly property 'SelectionCount'`

### 5. Converter Registration

**All converters MUST be registered in App.xaml:**
```xml
<Application.Resources>
    <converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
    <converters:BoolToVisibilityStringConverter x:Key="BoolToVisibilityStringConverter"/>
    <converters:BoolToOnOffStringConverter x:Key="BoolToOnOffStringConverter"/>
    <!-- ... all 18 converters ... -->
</Application.Resources>
```

**Error if missing:**
`XamlParseException: Resource with name 'ConverterName' cannot be found`

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
    // Without this check, child elements can't receive mouse events!
}
```

**Without `e.Source == this` check:**
- DesignerCanvas captures ALL mouse events
- Element event handlers in MainWindow.xaml.cs never fire
- Elements cannot be selected or dragged

---

## Adding New Features

### New ViewModel

1. Create class inheriting `ObservableObject` (CommunityToolkit.Mvvm)
2. Use `[ObservableProperty]` for bindable properties
3. Use `[RelayCommand]` for commands
4. Register in `App.xaml.cs`:

```csharp
// In ConfigureServices
services.AddSingleton<YourViewModel>();
```

Example:
```csharp
public partial class YourViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Item> _items = new();

    [RelayCommand]
    private void DoSomething()
    {
        // Command logic
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        // Async command
    }

    private bool CanSave() => !string.IsNullOrEmpty(Name);
}
```

### New View

1. Add XAML file in `Views/` folder
2. Set DataContext in code-behind constructor:

```csharp
public YourView(YourViewModel viewModel)
{
    InitializeComponent();
    DataContext = viewModel;
}
```

3. Use data binding in XAML:
```xml
<Window x:Class="DigitalSignage.Server.Views.YourView">
    <Grid>
        <TextBox Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
        <Button Content="Save" Command="{Binding SaveCommand}"/>
    </Grid>
</Window>
```

### New Converter

1. Create class implementing `IValueConverter` in `Converters/`
2. Register in `App.xaml` resources:

```xml
<Application.Resources>
    <converters:YourConverter x:Key="YourConverter"/>
</Application.Resources>
```

3. Use in XAML:
```xml
<TextBlock Text="{Binding Value, Converter={StaticResource YourConverter}}"/>
```

Example:
```csharp
public class YourConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Convert logic
        return convertedValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

### New Database Entity

1. Create entity class in `DigitalSignage.Data/Entities/`
2. Add `DbSet<T>` to `DigitalSignageDbContext.cs`:

```csharp
public DbSet<YourEntity> YourEntities => Set<YourEntity>();
```

3. Configure in `OnModelCreating` using Fluent API:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<YourEntity>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.HasIndex(e => e.Name);
    });
}
```

4. Create migration:
```bash
cd src/DigitalSignage.Data
dotnet ef migrations add AddYourEntity --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj
```

5. Migration applies automatically on next server startup via `DatabaseInitializationService`

### New WebSocket Message Type

1. Add message type constant to `MessageTypes` class (if needed)
2. Update `MessageHandlerService.cs` for server-side handling:

```csharp
private async Task HandleMessageAsync(string clientId, string messageJson)
{
    var message = JsonConvert.DeserializeObject<WebSocketMessage>(messageJson);

    switch (message.Type)
    {
        case "YOUR_MESSAGE_TYPE":
            await HandleYourMessageAsync(clientId, message);
            break;
    }
}

private async Task HandleYourMessageAsync(string clientId, WebSocketMessage message)
{
    // Handle message
}
```

3. Update `client.py` for client-side handling:

```python
async def on_message(self, data):
    if data['Type'] == 'YOUR_MESSAGE_TYPE':
        await self.handle_your_message(data)

async def handle_your_message(self, data):
    # Handle message
```

4. Test offline scenarios - ensure client caches/retries if disconnected

### New Service

1. Create interface in `DigitalSignage.Core/Interfaces/`
2. Implement in `DigitalSignage.Server/Services/` or `DigitalSignage.Data/Services/`
3. Register in `App.xaml.cs`:

```csharp
services.AddSingleton<IYourService, YourService>();
// or
services.AddScoped<IYourService, YourService>();
// or
services.AddTransient<IYourService, YourService>();
```

4. Inject via constructor:
```csharp
public class YourViewModel : ObservableObject
{
    private readonly IYourService _yourService;

    public YourViewModel(IYourService yourService)
    {
        _yourService = yourService;
    }
}
```

---

## Python Client - Virtual Environment

**Python 3.11+ requires venv due to PEP 668 "externally-managed-environment":**

```bash
# install.sh creates venv with --system-site-packages flag
python3 -m venv --system-site-packages venv

# This allows access to system-installed PyQt5 (via apt)
# while isolating other packages in venv
```

**Why `--system-site-packages`?**
- PyQt5 is large (~50MB) and complex to build
- Installing via apt is more reliable than pip
- `--system-site-packages` allows venv to use system PyQt5
- Other packages (websocket-client, qrcode, etc.) installed in venv

**System Packages (installed via apt):**
- python3-pyqt5 - Qt5 Python bindings
- python3-pyqt5.qtsvg - SVG support
- python3-pyqt5.qtmultimedia - Multimedia
- libqt5multimedia5-plugins - Codecs

**Venv Packages (from requirements.txt):**
- websocket-client, requests, psutil, pillow, qrcode, zeroconf, netifaces, qasync, flask

**Display Detection:**
- `start-with-display.sh` auto-detects DISPLAY environment variable
- If X11 running (DISPLAY=:0) → use real X11 display (HDMI)
- If no X11 → start Xvfb on DISPLAY=:99 (headless mode for testing)
- Xvfb provides virtual framebuffer for PyQt5 rendering without physical display

---

## Background Services

Registered as `IHostedService` in `App.xaml.cs`:

**DatabaseInitializationService**
- Runs on application startup
- Applies EF Core migrations automatically
- Seeds default admin user (username: admin, password: admin)
- Seeds 11 layout templates
- Ensures database file exists

**DataRefreshService**
- Background polling of SQL data sources
- Interval: configured per data source (default 60s)
- Sends DISPLAY_UPDATE messages to clients with fresh data
- Uses QueryCacheService for performance

**HeartbeatMonitoringService**
- Monitors client heartbeats every 30s
- Marks clients offline after 120s timeout (4 missed heartbeats)
- Triggers ClientDisconnected events
- Logs client status changes

**DiscoveryService** (UDP Broadcast)
- Broadcasts server info on UDP port 8888
- Allows clients to auto-discover server
- Broadcast message contains server IP, port, SSL status

**MdnsDiscoveryService** (Zeroconf)
- Advertises server via mDNS/Bonjour
- Service type: `_digitalsignage._tcp`
- Service name: `Digital Signage Server`
- Allows cross-platform discovery (Windows, Linux, macOS)

**AlertMonitoringService**
- Monitors alert rules in background
- Checks every N seconds (configured per rule)
- Triggers alerts when conditions met
- Types: ClientOffline, DataSourceError, HighCpuUsage, LowDiskSpace, ScreenshotFailed

---

## Logging

### Server (Serilog)

**Sinks:**
- Console (colored output)
- Debug (Visual Studio output window)
- File (daily rolling)
  - Info logs: `logs/digitalsignage-{Date}.log` (30 day retention)
  - Error logs: `logs/errors/digitalsignage-errors-{Date}.log` (90 day retention)
  - 100MB file size limit with rollover
- UISink (ObservableCollection for live UI display)

**Enrichers:**
- FromLogContext
- WithMachineName
- WithThreadId
- WithProperty("Application", "DigitalSignage.Server")

**Configuration:**
- Configured in `appsettings.json` under `Serilog` section
- MinimumLevel: Debug (change to Information for production)
- Override levels per namespace

**Usage:**
```csharp
private readonly ILogger<MyClass> _logger;

public MyClass(ILogger<MyClass> logger)
{
    _logger = logger;
}

_logger.LogInformation("Client {ClientId} connected from {IpAddress}", clientId, ipAddress);
_logger.LogError(ex, "Failed to process message from client {ClientId}", clientId);
```

### Client (Python logging)

**Configuration:**
- Python `logging` module
- Configured in `client.py`
- systemd journal integration

**Log Levels:**
- DEBUG, INFO, WARNING, ERROR, CRITICAL

**Handlers:**
- systemd journal (via `systemd.journal.JournalHandler`)
- Remote logging (via `RemoteLogHandler` to server)

**View Logs:**
```bash
# Real-time logs
sudo journalctl -u digitalsignage-client -f

# Last 100 lines
sudo journalctl -u digitalsignage-client -n 100

# Filter by level
sudo journalctl -u digitalsignage-client -p err

# Export to file
sudo journalctl -u digitalsignage-client > logs.txt
```

**Usage in Python:**
```python
import logging

logger = logging.getLogger(__name__)

logger.debug("Debug message")
logger.info("Client registered with server")
logger.warning("Low disk space: 5%")
logger.error("Failed to load layout", exc_info=True)
```

---

## Common Build Warnings (Ignorable)

These warnings are non-critical and can be safely ignored:

- **CS8622**: Nullable reference type mismatch (Touch event handlers)
- **CS8602/CS8603**: Possible null reference (Converter return values)
- **CS1998**: Async method lacks await operators (Placeholder methods for future features)
- **CS0169**: Unused field (Future features: `_clipboardElement`, `_lastPanPosition`, `_initialPinchDistance`)
- **CS0414**: Field assigned but never used (Grid settings: `_showGrid`, `_snapToGrid`, `_gridSize`)

**Note:** Build should have 0 errors. Warnings are acceptable but should be minimized in production.

---

## Common Issues & Solutions

### XamlParseException on Startup

**Symptom 1:** `Resource with name 'ConverterName' cannot be found`
**Solution:** Register converter in `App.xaml` resources section

**Symptom 2:** `TargetType 'Button' does not match element type 'ToggleButton'`
**Solution:** Use `TargetType="ButtonBase"` for styles shared by Button and ToggleButton

**Symptom 3:** Line number and position in XAML error
**Solution:** Check exact line in XAML file, look for missing resources or type mismatches

### Property Changes Don't Update Canvas

**Symptom:** Changing values in Properties Panel doesn't update visual elements on canvas
**Root Cause:** Missing `OnPropertyChanged("Item[]")` in DisplayElement indexer setter
**Solution:** Ensure indexer setter calls `OnPropertyChanged("Item[]")` with empty brackets

**File:** `DisplayElement.cs`
**Line:** Indexer setter

### Element Selection Not Working

**Symptom:** Cannot click or select elements on designer canvas
**Root Cause:** DesignerCanvas is capturing all mouse events before they reach child elements
**Solution:** Add `e.Source == this` check before `CaptureMouse()` in DesignerCanvas

**File:** `DesignerCanvas.cs`
**Method:** `OnMouseLeftButtonDown`

```csharp
if (e.Source == this && Keyboard.Modifiers == ModifierKeys.None)
{
    CaptureMouse();
}
```

### TwoWay Binding Error on Readonly Property

**Symptom:** `InvalidOperationException: TwoWay binding doesn't work with readonly property`
**Root Cause:** `Run.Text` bindings default to TwoWay mode but are bound to readonly properties
**Solution:** Add `Mode=OneWay` to all `Run.Text` bindings for readonly properties

**Files:** `DesignerStatusBar.xaml`, any XAML with Run.Text bindings

```xml
<Run Text="{Binding SelectionCount, Mode=OneWay}"/>
```

### Client Service Crashes on Raspberry Pi

**Diagnose:**
```bash
# Check logs
sudo journalctl -u digitalsignage-client -n 100 --no-pager

# Test PyQt5 import
/opt/digitalsignage-client/venv/bin/python3 -c "import PyQt5; print('OK')"

# Run diagnostic
cd /opt/digitalsignage-client
sudo ./diagnose.sh
```

**Common Causes:**
1. PyQt5 not installed or not accessible from venv
2. DISPLAY variable not set
3. Missing system dependencies (X11, Xvfb)
4. Permissions issues

**Fix:**
```bash
# Repair installation
sudo /opt/digitalsignage-client/fix-installation.sh

# Or reinstall
cd /opt/digitalsignage-client
sudo ./install.sh
```

### Port 8080 Already in Use

**Symptom:** Server fails to start with "Address already in use" error
**Solution:** Server has `AutoSelectPort: true` and will automatically try ports: 8081, 8082, 8083, 8888, 9000

**Manual fix:**
```bash
# Find process using port 8080
netstat -ano | findstr :8080

# Kill process (Windows)
taskkill /PID <process_id> /F
```

**Or:** Change port in `appsettings.json`

### Database Migration Errors

**Symptom:** EF Core migration fails or database schema mismatch
**Solution:** Migrations apply automatically on server startup via `DatabaseInitializationService`

**Manual migration:**
```bash
cd src/DigitalSignage.Data
dotnet ef database update --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj
```

**Reset database:**
```bash
# Delete database file
rm digitalsignage.db digitalsignage.db-wal digitalsignage.db-shm

# Run server - database will be recreated
dotnet run --project src/DigitalSignage.Server/DigitalSignage.Server.csproj
```

---

## Testing Changes on Raspberry Pi

**Complete workflow for client changes:**

1. **Make code changes** in `src/DigitalSignage.Client.RaspberryPi/`

2. **Test locally** (if possible):
```bash
cd src/DigitalSignage.Client.RaspberryPi
source venv/bin/activate
python client.py --test-mode
```

3. **Commit and push to GitHub:**
```bash
source .env
git add -A
git commit -m "Client: Your changes description

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"
git push
```

4. **SSH to Raspberry Pi:**
```bash
ssh pro@192.168.0.178
# Password: mr412393
```

5. **Update and test:**
```bash
cd /opt/digitalsignage-client
sudo git pull

# Show what changed
git log -3 --oneline

# Run update script (stops service, updates, restarts)
sudo ./update.sh

# Monitor logs in real-time
sudo journalctl -u digitalsignage-client -f
```

6. **Verify:**
- Check service status: `sudo systemctl status digitalsignage-client`
- View display output on HDMI monitor
- Check for errors in logs
- Test specific functionality (e.g., layout rendering, commands)

7. **If issues found:**
- Check logs for errors
- Fix issues locally
- Push to GitHub again
- Repeat update on Pi

**Quick commands:**
```bash
# Restart service
sudo systemctl restart digitalsignage-client

# Stop service (for manual testing)
sudo systemctl stop digitalsignage-client

# Run manually for debugging
cd /opt/digitalsignage-client
./venv/bin/python3 client.py

# Check service status
sudo systemctl status digitalsignage-client

# View last 50 log lines
sudo journalctl -u digitalsignage-client -n 50 --no-pager
```

---

## Code Style Guidelines

### C# (.NET 8)

**Follow Microsoft C# Coding Conventions:**
- PascalCase for classes, methods, properties, public fields
- camelCase for private fields, local variables, parameters
- Prefix private fields with underscore: `_myField`
- Nullable reference types enabled (`#nullable enable`)
- Use `string.Empty` instead of `""`
- Async methods suffixed with `Async`
- XML documentation comments for public APIs
- Expression-bodied members for simple properties/methods

**Example:**
```csharp
public class MyService : IMyService
{
    private readonly ILogger<MyService> _logger;
    private string _cachedValue = string.Empty;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the value asynchronously
    /// </summary>
    public async Task<string> GetValueAsync(string key)
    {
        _logger.LogInformation("Getting value for key: {Key}", key);
        return await FetchFromDatabaseAsync(key);
    }

    private async Task<string> FetchFromDatabaseAsync(string key)
    {
        // Implementation
    }
}
```

### Python (3.9+)

**Follow PEP 8 Style Guide:**
- snake_case for functions, variables, modules
- PascalCase for classes
- UPPER_CASE for constants
- Type hints for all function signatures
- Docstrings for public functions/classes (Google style or NumPy style)
- Use `async`/`await` for I/O operations
- Maximum line length: 100 characters (flexible)

**Example:**
```python
from typing import Optional, Dict, List
import logging

logger = logging.getLogger(__name__)

class DeviceManager:
    """Manages device information and system commands.

    Attributes:
        hostname: The device hostname.
    """

    def __init__(self):
        self.hostname = platform.node()

    async def get_device_info(self) -> Dict[str, Any]:
        """Get comprehensive device information.

        Returns:
            Dictionary containing device metrics (CPU, memory, disk, etc.)
        """
        try:
            cpu_usage = psutil.cpu_percent(interval=1)
            memory = psutil.virtual_memory()

            return {
                "hostname": self.hostname,
                "cpu_usage": cpu_usage,
                "memory_total": memory.total,
                "memory_used": memory.used,
            }
        except Exception as e:
            logger.error(f"Failed to get device info: {e}")
            return {}
```

### XAML

**Best Practices:**
- Use data binding instead of code-behind manipulation
- Name controls only when absolutely needed (prefer bindings)
- Use `StaticResource` for styles and converters
- Organize layout with Grid or StackPanel
- Use `x:Name` only for controls accessed in code-behind
- Prefer `Command` binding over event handlers
- Use converters for value transformations
- Keep XAML files clean and readable (proper indentation)

**Example:**
```xml
<Window x:Class="DigitalSignage.Server.Views.MyView"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:converters="clr-namespace:DigitalSignage.Server.Converters"
        Title="My View" Height="450" Width="800">

    <Window.Resources>
        <converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
    </Window.Resources>

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBox Grid.Row="0"
                 Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                 Margin="0,0,0,10"/>

        <Button Grid.Row="1"
                Content="Save"
                Command="{Binding SaveCommand}"
                IsEnabled="{Binding CanSave}"
                Visibility="{Binding IsEditing, Converter={StaticResource BoolToVisibilityConverter}}"/>
    </Grid>
</Window>
```

---

## Project Status

**Implementation Status: ~95% Complete**

**Fully Implemented (✅):**
- Visual Designer with drag-and-drop, undo/redo, multi-select
- Device Management with remote control (restart, screenshot, volume, screen on/off)
- Template system (11 built-in templates)
- Scriban template engine integration
- Client registration with token-based authentication
- TLS/SSL encryption support
- Offline cache (SQLite) with automatic fallback
- systemd service with watchdog
- Media library with SHA256 deduplication
- Professional Toolbar and Status Bar
- Properties Panel with all element types
- QR Code properties UI
- DateTime auto-update on clients
- Media Browser Dialog
- WebSocket communication with auto-reconnect
- Background services (Database init, Data refresh, Heartbeat monitoring)
- Logging infrastructure (Serilog + systemd journal)
- Command pattern for undo/redo
- MVVM architecture with CommunityToolkit.Mvvm

**Partially Implemented (⚠️):**
- Data Sources UI (backend complete, UI query builder pending)
- Layout scheduling (backend complete, UI pending)
- Alert system (backend complete, UI pending)

**Not Implemented (❌):**
- Auto-discovery UI (backend complete via mDNS/UDP)
- MSI installer (build scripts needed)
- REST API for third-party integration
- Video element support
- Touch support for tablets
- Cloud synchronization

**Known Issues:**
- Build warnings (36) - mostly nullable reference types and unused fields
- No automated tests yet (tests/ directory exists but empty)

**Documentation:**
- ✅ CLAUDE.md (this file)
- ✅ CODETODO.md (67KB feature checklist)
- ✅ DESIGNER_IMPROVEMENTS_PLAN.md
- ✅ REFACTORING_PLAN.md
- ❌ User documentation (pending)
- ❌ API documentation (pending)
