## Project Overview

The system consists of:
- **Windows Server Application**: WPF/.NET 8, MVVM pattern, Entity Framework Core, WebSocket communication
- **Raspberry Pi Clients**: Python 3.9+, PyQt5, WebSocket client, SQLite cache
- **Current Completion**: ~40% fully implemented, 15% partially implemented, 45% not implemented

## Code Standards

### C# / .NET Standards
- Follow Microsoft C# Coding Conventions strictly
- Always use async/await for I/O operations
- Write XML documentation for all public APIs
- Utilize nullable reference types
- Use dependency injection via Microsoft.Extensions.DependencyInjection
- Implement proper error handling with try-catch blocks
- Use Serilog for structured logging

### Python Standards
- Follow PEP 8 strictly
- Use Type Hints for all functions
- Write comprehensive docstrings
- Utilize async/await for I/O operations
- Handle exceptions gracefully with proper logging

## Current System Architecture

### Already Implemented (✅)
- **Designer Canvas**: Fully functional drag-and-drop with grid, snap, resize
- **Device Management Tab**: Complete UI with remote commands, volume control, layout assignment
- **Template System**: 11 built-in templates with categories
- **Scriban Template Engine**: Variable replacement, formatting, conditions, loops
- **Client Registration**: Token-based with MAC identification
- **SSL/TLS**: Full encryption support with documentation
- **Offline Cache**: SQLite-based fallback for clients
- **systemd Service**: Auto-start and watchdog monitoring
- **Entity Framework Core**: Database context with all entities
- **Dependency Injection**: Fully configured DI container
- **Media Library Backend**: EnhancedMediaService with hash-based deduplication

### Key Technologies in Use
- **Backend**: .NET 8, WPF, EF Core, CommunityToolkit.Mvvm, Scriban, Serilog
- **Frontend**: PyQt5, python-socketio, SQLite
- **Communication**: WebSocket (System.Net.WebSockets / python-socketio)
- **Database**: SQL Server (main), SQLite (client cache)

## Priority System

When working on tasks, ALWAYS follow this priority order:

### 🔴 HIGH PRIORITY - MVP Features
1. **EF Core Migrations** - Apply database schema
2. **Datenquellen-Tab UI** - Data source management interface
3. **Query-Builder** - Visual SQL query construction
4. **Layout Scheduling** - Time-based layout switching
5. **Auto-Discovery** - UDP broadcast for device detection

### 🟡 MEDIUM PRIORITY - Production Features
1. **Undo/Redo System** - Command pattern implementation
2. **Media Browser UI** - Visual media library
3. **Remote Log Viewer** - Real-time log streaming
4. **Alert System** - Error notifications
5. **Layer Management Palette** - Visual layer organization

### 🟢 LOW PRIORITY - Enhancement Features
1. **MSI Installer** - WiX Toolset deployment
2. **REST API** - Third-party integration
3. **Widgets** - Weather, RSS, social media
4. **Touch Support** - Tablet optimization
5. **Cloud Sync** - Multi-site deployment

## Development Approach

### When Implementing Features

1. **Check Existing Code First**
```csharp
   // Always review related services and models
   // Check: Services/, Models/, ViewModels/, Data/
```

2. **Follow MVVM Pattern**
```csharp
   // ViewModel
   public partial class FeatureViewModel : ViewModelBase
   {
       [ObservableProperty]
       private string _property;
       
       [RelayCommand]
       private async Task ExecuteFeatureAsync()
       {
           // Implementation with proper error handling
           try
           {
               await _service.PerformActionAsync();
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Error in feature");
               ShowError("Operation failed");
           }
       }
   }
```

3. **Service Layer Pattern**
```csharp
   public class FeatureService : IFeatureService
   {
       private readonly DigitalSignageDbContext _context;
       private readonly ILogger _logger;
       
       public async Task ProcessAsync(CancellationToken ct)
       {
           // Always use async EF Core methods
           return await _context.Entities
               .Where(e => e.IsActive)
               .ToListAsync(ct);
       }
   }
```

4. **Database Integration**
```csharp
   // Add new entity
   public class NewFeature
   {
       public Guid Id { get; set; }
       public string Name { get; set; } = string.Empty;
       public DateTime CreatedAt { get; set; }
       // Follow existing patterns
   }
   
   // Update DbContext
   public DbSet NewFeatures { get; set; }
   
   // Configure in OnModelCreating
   modelBuilder.Entity(entity =>
   {
       entity.HasKey(e => e.Id);
       entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
       // Follow existing configuration patterns
   });
```

5. **Client Communication**
```python
   # Python client handler
   async def handle_new_feature(self, data: Dict[str, Any]) -> None:
       """Handle new feature message from server."""
       try:
           # Validate data
           if not self._validate_feature_data(data):
               return
               
           # Process feature
           await self._process_feature(data)
           
           # Update UI if needed
           self.update_signal.emit(data)
           
       except Exception as e:
           logger.error(f"Feature handling error: {e}")
           # Fallback to cached data if available
```

## Specific Implementation Guidelines

### For Designer Features
- Use DesignerCanvas control as base
- Implement commands in DesignerViewModel
- Follow existing DesignerItemControl patterns
- Maintain grid-snap functionality

### For Data Source Integration
- Extend SqlDataService for new providers
- Use parameterized queries (SQL injection protection)
- Implement connection pooling
- Add query caching where appropriate

### For Device Management
- Use ClientService for all client operations
- Update RaspberryPiClient entity for new fields
- Maintain backward compatibility with existing clients
- Test with offline scenarios

### For Template System
- Leverage Scriban for variable replacement
- Store templates in LayoutTemplate entity
- Maintain category system
- Preserve built-in templates

### For WebSocket Communication
- Use existing WebSocketHandler
- Follow MessageType enum pattern
- Implement proper error handling
- Ensure message serialization compatibility

## Testing Requirements

For every feature:
1. Write unit tests for business logic
2. Test offline/online transitions
3. Verify WebSocket message handling
4. Check database migrations
5. Validate UI responsiveness
6. Test with multiple concurrent clients

## Common Patterns to Follow

### Repository Pattern (if needed)
```csharp
public interface IRepository where T : class
{
    Task<IEnumerable> GetAllAsync();
    Task GetByIdAsync(Guid id);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
}
```

### Command Implementation
```csharp
[RelayCommand(CanExecute = nameof(CanExecuteCommand))]
private async Task ExecuteCommandAsync()
{
    IsBusy = true;
    try
    {
        await _service.PerformOperationAsync();
        StatusMessage = "Success";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Command failed");
        StatusMessage = $"Error: {ex.Message}";
    }
    finally
    {
        IsBusy = false;
    }
}
```

### Python Service Pattern
```python
class FeatureManager:
    def __init__(self, config: Config, websocket_client: WebSocketClient):
        self.config = config
        self.client = websocket_client
        self._cache = {}
        
    async def initialize(self) -> None:
        """Initialize the feature manager."""
        await self._load_cached_data()
        
    async def process_update(self, data: Dict) -> None:
        """Process feature update from server."""
        self._validate_data(data)
        await self._update_cache(data)
        await self._apply_changes(data)
```

## File Organization

Always maintain the existing structure:
```
/DigitalSignageManager/
├── /Services/          # Business logic
├── /Models/           # Data models
├── /ViewModels/       # MVVM ViewModels
├── /Views/            # WPF XAML views
├── /Data/             # EF Core context
├── /Controls/         # Custom WPF controls
├── /Messages/         # WebSocket messages
└── /Utils/            # Helper classes

/raspberry-pi-client/
├── /src/
│   ├── main.py
│   ├── websocket_client.py
│   ├── display_manager.py
│   ├── cache_manager.py
│   └── /utils/
├── /config/
└── /tests/
```

## Deployment Considerations

When implementing features, always consider:
1. **Migration path** for existing installations
2. **Backward compatibility** with deployed clients
3. **Configuration management** (appsettings.json / config.py)
4. **Performance impact** on Raspberry Pi hardware
5. **Network reliability** (offline operation)
6. **Security implications** (authentication, encryption)

## Quick Reference: Current TODO Priorities

**IMMEDIATE FOCUS (Next Sprint):**
1. Apply EF Core migrations to create database
2. Complete Datenquellen-Tab UI with visual query builder
3. Implement layout scheduling system
4. Add media browser UI for existing MediaService
5. Create auto-discovery mechanism

**SKIP FOR NOW (Low Priority):**
- MSI Installer
- Cloud sync
- Mobile apps
- Advanced widgets
- A/B testing

## Commands for Common Tasks
```bash
# Run database migrations
dotnet ef database update

# Generate new migration
dotnet ef migrations add FeatureName

# Test Python client
python src/main.py --test-mode

# Build release
dotnet publish -c Release -r win-x64

# Install on Raspberry Pi
sudo ./install.sh
```

Remember: ALWAYS check the TODO list status before implementing. Many features are partially complete - build upon existing code rather than starting from scratch. The goal is to complete the MVP features (🔴) first to achieve a working system, then enhance with production features (🟡).
```

Dieser Agent-Prompt ist speziell auf dein Projekt zugeschnitten und berücksichtigt:

1. **Deine bestehende Code-Basis** (~40% fertig)
2. **Die spezifischen Technologien** (WPF/.NET 8, PyQt5, EF Core)
3. **Die Prioritäten** aus deiner TODO-Liste (🔴🟡🟢)
4. **Deine Code-Standards** (Microsoft C# Conventions, PEP 8)
5. **Die bereits implementierten Features** (Designer, Device Management, Templates)
6. **Die Architektur-Patterns** (MVVM, Services, DI)
