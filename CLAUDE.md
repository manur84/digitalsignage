# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Digital Signage Management System** - A comprehensive client-server digital signage solution consisting of:
- **Windows Server/Manager**: WPF/.NET 8 desktop application with visual designer for managing layouts, data sources, and clients
- **Raspberry Pi Clients**: Python 3.9+ client software with PyQt5 for display rendering
- **WebSocket Communication**: Real-time bidirectional communication with TLS/SSL encryption
- **Current Status**: ~40% implemented (core infrastructure complete, UI and advanced features in progress)

### Technology Stack

**Server (.NET 8):**
- WPF with MVVM pattern (CommunityToolkit.Mvvm)
- Entity Framework Core with SQL Server
- Scriban template engine for variable replacement
- Serilog for structured logging
- Microsoft.Extensions.DependencyInjection for DI
- System.Net.WebSockets for real-time communication

**Client (Python 3.9+):**
- PyQt5 for display rendering
- python-socketio for WebSocket communication
- SQLite for offline caching
- systemd service with watchdog monitoring
- psutil for hardware monitoring

## Environment Configuration

**Important:** GitHub credentials and repository information are stored in the `.env` file at the project root:

```bash
GITHUBTOKEN=<your-github-token>
GITHUBREPO=<repository-url>
```

**Setup Instructions:**
1. Copy `.env.example` to `.env`: `cp .env.example .env`
2. Edit `.env` and add your GitHub personal access token
3. Update the repository URL if different from the example
4. Configure git remote with token: `source .env && git remote set-url origin "https://${GITHUBTOKEN}@github.com/manur84/digitalsignage.git"`

**Git Operations:**
```bash
# Pull latest changes (token authentication configured)
git pull

# Push changes
git push

# Check remote configuration
git remote -v
```

**🚨 CRITICAL: Git Workflow - ALWAYS PUSH AFTER EVERY CHANGE! 🚨**

⚠️ **MANDATORY: Push to Git after EVERY single modification, fix, or feature!**

**THIS IS NOT OPTIONAL!** After making ANY changes to the codebase, you MUST follow this workflow:

```bash
# Step 1: Stage all changes
git add -A

# Step 2: Commit with descriptive message
git commit -m "Description of changes

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"

# Step 3: IMMEDIATELY push to remote - DO NOT SKIP THIS!
git push
```

**Why this is critical:**
- ✅ All work is backed up in the repository
- ✅ Changes are synchronized across development environments
- ✅ No work is lost if the local environment has issues
- ✅ Other team members/Claude instances have access to latest changes
- ✅ Continuous integration and deployment pipelines can run
- ✅ Version history is maintained properly

**❌ NEVER skip the git push step!**
**❌ NEVER wait to push multiple changes together!**
**✅ ALWAYS push immediately after completing ANY task or fixing ANY issue!**

**Examples of when to push:**
- Fixed a build error → `git push`
- Added a new feature → `git push`
- Updated documentation → `git push`
- Changed configuration → `git push`
- Modified a single line → `git push`
- **EVERYTHING** → `git push`

**Security Note:** The `.env` file contains sensitive credentials and must never be committed to version control. It's included in `.gitignore` to prevent accidental commits. The git remote URL includes the token for authentication, but it's not stored in the repository.

## Commands

### Building and Running

```bash
# Build the solution
dotnet build DigitalSignage.sln

# Build release version
dotnet build -c Release

# Publish for Windows x64 (self-contained)
dotnet publish src/DigitalSignage.Server/DigitalSignage.Server.csproj -c Release -r win-x64 --self-contained

# Run the server application (from solution root)
dotnet run --project src/DigitalSignage.Server/DigitalSignage.Server.csproj

# Run tests
dotnet test

# Generate EF Core migration
cd src/DigitalSignage.Data
dotnet ef migrations add MigrationName --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj

# Apply database migrations
dotnet ef database update --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj
```

### Python Client Commands

```bash
# Install client dependencies
cd src/DigitalSignage.Client.RaspberryPi
pip install -r requirements.txt

# Run client in test mode
python client.py --test-mode

# Install as systemd service on Raspberry Pi
sudo ./install.sh

# Check service status
sudo systemctl status digitalsignage-client

# View client logs
sudo journalctl -u digitalsignage-client -f
```

## Architecture Overview

### Project Structure

```
digitalsignage/
├── src/
│   ├── DigitalSignage.Server/       # WPF application (MVVM)
│   │   ├── Views/                   # XAML views
│   │   ├── ViewModels/              # ViewModels with CommunityToolkit.Mvvm
│   │   ├── Services/                # Business logic layer
│   │   ├── Controls/                # Custom WPF controls (DesignerCanvas, etc.)
│   │   ├── Converters/              # Value converters
│   │   └── Configuration/           # App configuration
│   ├── DigitalSignage.Core/         # Shared models and interfaces
│   │   ├── Models/                  # Domain models (messages, DTOs)
│   │   ├── Interfaces/              # Service contracts
│   │   └── Helpers/                 # Utility classes
│   ├── DigitalSignage.Data/         # Data access layer
│   │   ├── Entities/                # EF Core entities
│   │   ├── Services/                # Data services (EF Context, repositories)
│   │   └── DigitalSignageDbContext.cs
│   └── DigitalSignage.Client.RaspberryPi/  # Python client
│       ├── client.py                # Main entry point
│       ├── display_renderer.py      # PyQt5 rendering engine
│       ├── device_manager.py        # Hardware monitoring
│       ├── cache_manager.py         # SQLite offline cache
│       ├── watchdog_monitor.py      # systemd watchdog integration
│       └── config.py                # Configuration management
├── tests/
│   └── DigitalSignage.Tests/        # Unit tests
├── docs/                            # Comprehensive documentation
│   ├── ARCHITECTURE.md
│   ├── API.md
│   ├── SSL_SETUP.md
│   ├── TEMPLATE_ENGINE.md
│   └── DEPLOYMENT.md
└── scripts/                         # Build and deployment scripts
```

### Key Design Patterns

**MVVM Pattern:** All UI code follows Model-View-ViewModel pattern with:
- Views defined in XAML (in `Views/` folder)
- ViewModels using `CommunityToolkit.Mvvm` attributes (`[ObservableProperty]`, `[RelayCommand]`)
- Data binding between View and ViewModel
- No business logic in code-behind files

**Dependency Injection:** Configured in `App.xaml.cs` using `Microsoft.Extensions.DependencyInjection`:
- Services registered as Singleton, Scoped, or Transient
- ViewModels registered for automatic injection
- Background services registered as `IHostedService`

**Repository Pattern:** Data access abstracted through services:
- `DigitalSignageDbContext` for EF Core operations
- Service layer (`ClientService`, `LayoutService`, etc.) wraps data access
- Async/await used throughout for I/O operations

**Template Engine:** Scriban-based server-side template processing:
- Variables: `{{Variable}}`, `{{Data.Property}}`
- Formatting: `{{date_format DateValue "dd.MM.yyyy"}}`
- Conditionals: `{{if Condition}}...{{else}}...{{end}}`
- Loops: `{{for item in Items}}...{{end}}`
- Fallbacks: `{{Variable ?? "Default"}}`
- See `docs/TEMPLATE_ENGINE.md` for complete reference

## Database Architecture

### Entity Framework Core Setup

The project uses EF Core with SQL Server. Database context is in `src/DigitalSignage.Data/DigitalSignageDbContext.cs`.

**Key Entities:**
- `DisplayLayout` - Layout definitions with resolution, background, and elements (JSON column)
- `LayoutTemplate` - Pre-built templates with categories and thumbnails
- `RaspberryPiClient` - Client device registration and status
- `DataSource` - SQL data source configurations
- `MediaFile` - Media library with SHA256 hash-based deduplication
- `ClientRegistrationToken` - Token-based client registration with restrictions
- `ApiKey` - API key authentication with usage tracking
- `User` - User accounts with password hashing
- `AuditLog` - Change tracking for compliance

**Configuration:**
- Connection string in `appsettings.json` under `ConnectionStrings:DefaultConnection`
- Automatic migrations applied at startup via `DatabaseInitializationService`
- Default admin user seeded on first run

## Key Architectural Decisions

### 1. WebSocket Communication Protocol

All client-server communication uses WebSocket with custom JSON message protocol:

**Message Types:**
- `REGISTER` - Client registration with MAC address and device info
- `REGISTRATION_RESPONSE` - Server response with client ID and configuration
- `HEARTBEAT` - Periodic keep-alive (every 30s)
- `DISPLAY_UPDATE` - Layout updates sent to client
- `STATUS_REPORT` - Client status and metrics
- `COMMAND` - Remote commands (RESTART, SCREENSHOT, SCREEN_ON/OFF, SET_VOLUME)
- `SCREENSHOT` - Screenshot data transfer

**Implementation:**
- Server: WebSocket server in `CommunicationService` with in-memory client registry
- Client: `python-socketio` with automatic reconnection and exponential backoff
- Offline mode: Client falls back to SQLite cache when disconnected

### 2. Client Registration and Authentication

**Token-Based Registration:**
- Clients register with a `ClientRegistrationToken` (configured in `config.json`)
- Token can specify auto-assignment of Group and Location
- Token has optional restrictions: MaxUses, ExpiresAt, AllowedMacAddresses
- MAC address used for client identification (re-registration supported)

**API Key Authentication:**
- Server API can be accessed with API keys
- Keys tracked with LastUsedAt, UsageCount, and optional ExpiresAt
- Implemented in `AuthenticationService`

### 3. Offline Resilience

**Client-Side Caching:**
- All layouts cached in SQLite database (`CacheManager`)
- Automatic fallback when server unreachable
- Cache includes layout data, timestamp, and metadata
- Client displays last known layout until reconnection

**Server-Side:**
- In-memory client registry with database persistence
- Heartbeat monitoring service (120s timeout) marks clients offline
- Database stores last known state for recovery

### 4. Designer Canvas Implementation

**Fully Functional Visual Designer:**
- Custom `DesignerCanvas` control with grid rendering
- Drag-and-drop element placement from toolbar
- `DesignerItemControl` for individual element rendering
- `ResizeAdorner` for transformation handles
- Snap-to-grid with configurable grid size (default 10px)
- Properties panel with real-time element editing
- Z-index management with up/down commands
- Element duplication and deletion
- Zoom functionality (commands defined, UI pending)

**Element Types Supported:**
- Text fields (with variable substitution)
- Images (JPG, PNG, GIF, BMP, WEBP, SVG)
- Rectangles and shapes
- Tables (data-bound to SQL sources)
- QR codes (dynamic generation)
- Date/time (live updates)

### 5. Template System

**11 Built-in Templates:**
- 5 Blank templates (various resolutions: HD, Full HD, 4K in landscape/portrait)
- 6 Content templates (Information Board, Room Occupancy, Welcome Screen, Menu Board, Wayfinding, Emergency)
- Categories: RoomOccupancy, InformationBoard, Wayfinding, MenuBoard, WelcomeScreen, Emergency, Blank, Custom
- Templates stored as `LayoutTemplate` entities with metadata
- Usage tracking (LastUsedAt, UsageCount)
- Template selection UI pending implementation

### 6. Media Library

**EnhancedMediaService:**
- SHA256 hash-based deduplication (prevents duplicate uploads)
- MIME type detection and validation
- File size limit: 100 MB
- Access tracking (LastAccessedAt, AccessCount)
- Supported formats:
  - Images: JPG, PNG, GIF, BMP, WEBP, SVG
  - Videos: MP4, AVI, MOV, WMV, FLV, MKV, WEBM
  - Audio: MP3, WAV, OGG, FLAC, AAC, WMA
  - Documents: PDF, DOC/DOCX, XLS/XLSX, PPT/PPTX, TXT
- Media browser UI not yet implemented

### 7. Device Management

**Complete Device Management UI (DeviceManagementView):**
- DataGrid showing all clients with status indicators
- Device detail panel (300px right sidebar)
- Remote commands: Restart Device, Restart App, Screenshot, Clear Cache
- Screen control: On/Off
- Volume control with slider (0-100%)
- Layout assignment via ComboBox
- Real-time status updates via WebSocket
- Implemented in `DeviceManagementViewModel` with full error handling

### 8. Security Implementation

**TLS/SSL Encryption:**
- Server supports HTTPS/WSS via `ServerSettings` in appsettings.json
- Client supports SSL with configurable verification
- Reverse proxy support (nginx/IIS) for production deployments
- See `docs/SSL_SETUP.md` for configuration

**Authentication & Authorization:**
- User accounts with password hashing (SHA256, BCrypt/Argon2 recommended for production)
- API key system with revocation
- Token-based client registration
- Audit logging entity defined (UI pending)

**SQL Injection Protection:**
- All queries use parameterized statements
- Input validation on all service methods

### 9. Background Services

**Registered as IHostedService:**
- `DataRefreshService` - Polls SQL data sources at configured intervals, sends updates to clients
- `HeartbeatMonitoringService` - Monitors client heartbeats, marks offline after 120s
- `DatabaseInitializationService` - Applies migrations and seeds data at startup

### 10. Logging Strategy

**Serilog Configuration:**
- Console, Debug, and File sinks
- Daily rolling files (30 days retention for info, 90 days for errors)
- 100 MB file size limit with rollover
- Enrichers: Machine name, thread ID, source context
- Log levels configurable in appsettings.json

**Client Logging:**
- Python logging framework with systemd journal integration
- Logs available via `journalctl -u digitalsignage-client`

## Important Implementation Notes

### When Adding New Features

1. **Follow MVVM Strictly:**
   - Create ViewModel inheriting from `ViewModelBase` (or use `ObservableObject`)
   - Use `[ObservableProperty]` for bindable properties
   - Use `[RelayCommand]` for commands (support `CanExecute` with `CanExecute = nameof(MethodName)`)
   - Register ViewModel in DI container in `App.xaml.cs`

2. **Service Layer Pattern:**
   - Create interface in `DigitalSignage.Core/Interfaces`
   - Implement in `DigitalSignage.Server/Services` or `DigitalSignage.Data/Services`
   - Inject dependencies via constructor
   - Use async/await for all I/O operations
   - Add comprehensive error handling with try-catch
   - Log all operations using injected `ILogger<T>`

3. **Database Changes:**
   - Add/modify entity in `DigitalSignage.Data/Entities`
   - Add DbSet in `DigitalSignageDbContext.cs`
   - Configure in `OnModelCreating` using Fluent API
   - Generate migration: `dotnet ef migrations add FeatureName`
   - Apply migration: `dotnet ef database update`

4. **Client Communication:**
   - Add message type to enum if needed
   - Update `CommunicationService` for server-side handling
   - Update `client.py` for client-side handling
   - Ensure message serialization compatibility (JSON)
   - Test offline scenarios

5. **Template Variables:**
   - Server processes templates with Scriban in `TemplateService`
   - Use `{{Variable}}` syntax in text elements
   - Client performs additional runtime substitution for dynamic values
   - Validate template syntax before sending to clients

### Code Style Conventions

**C#:**
- Follow Microsoft C# Coding Conventions
- Use nullable reference types (`#nullable enable`)
- Use `string.Empty` instead of `""`
- Prefer expression-bodied members for simple properties/methods
- Use XML documentation comments for public APIs
- Async methods suffixed with `Async`

**Python:**
- Follow PEP 8 strictly
- Use type hints for all function signatures
- Write docstrings for all public functions/classes
- Use `async`/`await` for I/O operations
- Use `snake_case` for variables and functions

### Testing Strategy

**Unit Tests:**
- Test project: `tests/DigitalSignage.Tests`
- Test business logic in services
- Mock dependencies using interfaces
- Use xUnit or NUnit framework

**Integration Tests:**
- Test WebSocket communication end-to-end
- Test database operations with in-memory provider
- Test offline/online transitions

**Manual Testing:**
- Designer canvas operations (drag, resize, snap)
- Device commands (restart, screenshot)
- Layout assignment and display
- Offline mode with cached data

## Current Implementation Status

**Fully Implemented (✅):**
- Designer Canvas with full drag-and-drop functionality
- Device Management UI with remote control
- Template system with 11 built-in templates
- Scriban template engine integration
- Client registration with token-based authentication
- TLS/SSL encryption support
- Offline cache with SQLite
- systemd service with watchdog
- Entity Framework Core with migrations
- Dependency injection setup
- Media library backend (EnhancedMediaService)
- Background services (DataRefresh, HeartbeatMonitoring)
- Logging infrastructure (Serilog)

**Partially Implemented (⚠️):**
- EF Core migrations (schema defined, not yet applied to production DB)
- Layer management (Z-index commands exist, visual palette pending)
- Preview functionality (commands defined, rendering pending)

**Not Implemented (❌):**
- Data Sources tab UI (query builder, connection test UI)
- Layout scheduling system
- Media browser UI
- Auto-discovery (UDP broadcast)
- Undo/Redo system
- MSI installer
- Remote log viewer
- Touch support

## Priority Guidance

Follow this priority order when implementing features:

### 🔴 HIGH PRIORITY - MVP Features
1. Apply EF Core migrations to create database schema
2. Implement Data Sources tab UI with visual query builder
3. Add layout scheduling system (time-based layout switching)
4. Implement auto-discovery for device detection
5. Build media browser UI for existing MediaService

### 🟡 MEDIUM PRIORITY - Production Features
1. Undo/Redo system using command pattern
2. Layer management palette with visual organization
3. Remote log viewer with real-time streaming
4. Alert system for error notifications
5. Query caching and connection pooling optimization

### 🟢 LOW PRIORITY - Enhancement Features
1. MSI installer with WiX Toolset
2. REST API for third-party integration
3. Widgets (weather, RSS, social media)
4. Touch support for tablets
5. Cloud synchronization

## Troubleshooting

**Build Errors:**
- Ensure .NET 8 SDK installed: `dotnet --version`
- Restore packages: `dotnet restore`
- Clean solution: `dotnet clean`

**Database Connection Issues:**
- Check connection string in `appsettings.json`
- Verify SQL Server is running and accessible
- Test connection with SQL Management Studio

**Client Connection Issues:**
- Verify firewall allows port 8080 (or configured port)
- Check `server_host` and `server_port` in client `config.json`
- Test WebSocket connectivity: `telnet <server-ip> 8080`
- Review client logs: `sudo journalctl -u digitalsignage-client -f`

**WebSocket Communication:**
- Enable verbose logging in both server and client
- Check for TLS/SSL certificate issues if using WSS
- Verify message serialization (JSON format)

## Additional Resources

- `README.md` - Project overview and installation instructions
- `docs/ARCHITECTURE.md` - Detailed system architecture
- `docs/API.md` - WebSocket message protocol reference
- `docs/SSL_SETUP.md` - SSL/TLS configuration guide
- `docs/TEMPLATE_ENGINE.md` - Scriban template syntax and examples
- `docs/DEPLOYMENT.md` - Production deployment guide
- `CODETODO.md` - Detailed feature checklist and implementation status
