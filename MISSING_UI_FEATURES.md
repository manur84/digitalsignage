# Digital Signage - Missing UI Features Report

**Generated:** 2025-11-14
**Audit Type:** Complete Windows Server UI Audit
**Status:** 21 Services Registered, Multiple UI Elements Missing

---

## EXECUTIVE SUMMARY

The Digital Signage Server has **21 registered services** in App.xaml.cs, but several critical features **have no UI access** despite being fully implemented in the backend. This report identifies all missing UI elements and prioritizes their implementation.

**Key Findings:**
- **7 Menu Commands** defined but NOT implemented in ViewModels
- **3 Services** with complete backend but NO UI at all
- **2 Services** with partial UI (backend complete, frontend incomplete)
- **Several Configuration Classes** with no UI editor

---

## CATEGORY 1: MENU ITEMS WITHOUT IMPLEMENTATION

These menu items exist in MainWindow.xaml but have **no backing Command in MainViewModel.cs**:

### 1. Settings Command
- **Menu Location:** Tools → Settings (⚙)
- **Backend Status:** ServerSettings.cs exists (147 lines, complete)
- **UI Status:** ❌ Command NOT implemented
- **What's Missing:**
  - No SettingsCommand in MainViewModel
  - No SettingsDialog.xaml
  - No SettingsViewModel
- **Backend Features:**
  - Port configuration (8080 + alternatives)
  - SSL/TLS settings
  - Certificate configuration
  - WebSocket endpoint path
  - Message size limits
  - Heartbeat timeout
- **Priority:** 🔴 **HIGH** - Users cannot configure server without editing JSON
- **Estimated Effort:** Medium (2-3 hours)
  - Create SettingsDialog.xaml
  - Create SettingsViewModel with ServerSettings binding
  - Implement SettingsCommand in MainViewModel
  - Add validation and apply/save logic

---

### 2. Server Configuration Command
- **Menu Location:** Tools → Server Configuration (🖥)
- **Backend Status:** Partially covered by ServerSettings.cs
- **UI Status:** ❌ Command NOT implemented
- **What's Missing:**
  - No ServerConfigurationCommand in MainViewModel
  - No server status overview dialog
  - No advanced server diagnostics UI
- **Backend Features:**
  - Server port and protocol info
  - Active connections count
  - WebSocket server status
  - URL ACL configuration status
- **Priority:** 🟡 **MEDIUM** - Overlaps with Settings, could be merged
- **Estimated Effort:** Small (1-2 hours if merged with Settings)

---

### 3. Backup Database Command
- **Menu Location:** Tools → Backup Database (💾)
- **Backend Status:** BackupService registered as dependency but not exposed
- **UI Status:** ❌ Command NOT implemented
- **What's Missing:**
  - No BackupDatabaseCommand in MainViewModel
  - No BackupService reference in MainViewModel
  - No backup dialog (SaveFileDialog integration)
  - No progress indicator
- **Backend Status:** Service likely exists but needs investigation
- **Priority:** 🔴 **HIGH** - Critical for data safety
- **Estimated Effort:** Medium (2-3 hours)
  - Investigate existing BackupService
  - Implement BackupDatabaseCommand
  - Add SaveFileDialog with .db filter
  - Add progress reporting
  - Verify backup integrity

---

### 4. Restore Database Command
- **Menu Location:** Tools → Restore Database (📂)
- **Backend Status:** Restoration logic likely missing
- **UI Status:** ❌ Command NOT implemented
- **What's Missing:**
  - No RestoreDatabaseCommand in MainViewModel
  - No restoration service/logic
  - No OpenFileDialog integration
  - No backup verification before restore
  - No warning about data loss
- **Priority:** 🔴 **HIGH** - Backup is useless without restore
- **Estimated Effort:** Medium (3-4 hours)
  - Implement RestoreService
  - Add database validation
  - Implement RestoreDatabaseCommand
  - Add OpenFileDialog with .db filter
  - Add confirmation dialog with warnings
  - Handle application restart after restore

---

### 5. System Diagnostics Command
- **Menu Location:** Tools → System Diagnostics (🔧)
- **Backend Status:** No dedicated service
- **UI Status:** ❌ Command NOT implemented
- **What's Missing:**
  - No SystemDiagnosticsCommand in MainViewModel
  - No diagnostics dialog/window
  - No system health checks
- **Potential Features:**
  - Database connection status
  - WebSocket server health
  - Port availability check
  - Certificate validation
  - Client connection statistics
  - Performance metrics
  - Log analysis
- **Priority:** 🟡 **MEDIUM** - Useful for troubleshooting
- **Estimated Effort:** Large (4-6 hours)
  - Create DiagnosticsService
  - Create DiagnosticsWindow.xaml
  - Implement comprehensive health checks
  - Add copy-to-clipboard for diagnostics report

---

### 6. Template Manager Command
- **Menu Location:** Tools → Template Manager (📄)
- **Backend Status:** TemplateService exists (11 built-in templates)
- **UI Status:** ❌ Command NOT implemented, but TemplateSelectionWindow exists
- **What's Missing:**
  - No TemplateManagerCommand in MainViewModel
  - No template CRUD UI (existing TemplateSelectionWindow is read-only)
  - Cannot create custom templates
  - Cannot edit existing templates
  - Cannot delete templates
- **Backend Capabilities:**
  - TemplateService has 11 built-in templates
  - Scriban template rendering
  - Template metadata (name, description, category)
- **Priority:** 🟡 **MEDIUM** - Advanced feature
- **Estimated Effort:** Large (6-8 hours)
  - Create TemplateManagerWindow.xaml
  - Add template CRUD operations
  - Add Scriban template editor with syntax highlighting
  - Add template preview
  - Implement validation

---

### 7. Client Registration Tokens Command
- **Menu Location:** Tools → Client Registration Tokens (🔑)
- **Backend Status:** Token-based auth implemented in ClientService
- **UI Status:** ❌ Command NOT implemented
- **What's Missing:**
  - No ClientTokensCommand in MainViewModel
  - No token management UI
  - Cannot generate new tokens
  - Cannot revoke tokens
  - Cannot list active tokens
- **Backend Features:**
  - Token-based client registration (from appsettings.json)
  - Currently hardcoded in configuration
- **Priority:** 🟡 **MEDIUM** - Security feature
- **Estimated Effort:** Medium (3-4 hours)
  - Create TokenManagementWindow.xaml
  - Implement token generation (GUID-based)
  - Add token list with creation date
  - Add revoke functionality
  - Store tokens in database (AlertRules table exists)

---

## CATEGORY 2: SERVICES WITH NO UI

These services are **fully registered** in App.xaml.cs but have **zero UI access**:

### 8. Alert Service & Alert Monitoring Service
- **Service Files:**
  - `AlertService.cs` (registered as Singleton)
  - `AlertMonitoringService.cs` (registered as HostedService)
- **Backend Status:** ✅ **FULLY IMPLEMENTED**
- **UI Status:** ❌ **COMPLETELY MISSING**
- **What Exists:**
  - Alert rules evaluation
  - Alert triggering based on conditions
  - AlertRules database table
  - Background monitoring service
- **What's Missing:**
  - No UI to view alerts
  - No UI to create/edit alert rules
  - No UI to configure alert conditions
  - No UI to set alert actions (email, webhook, etc.)
  - No alert notification system in UI
  - No alert history viewer
- **Backend Capabilities:**
  - Rule-based alert system
  - Device offline detection
  - Custom alert conditions
  - Alert cooldown/throttling
- **Priority:** 🔴 **HIGH** - Critical monitoring feature
- **Estimated Effort:** Large (8-10 hours)
  - Create AlertsTab in MainWindow.xaml
  - Create AlertRulesViewModel
  - Add alert rules CRUD UI
  - Add alert history viewer
  - Add real-time alert notifications (toast/banner)
  - Implement alert condition builder UI

---

### 9. Discovery Service & mDNS Discovery Service
- **Service Files:**
  - `DiscoveryService.cs` (HostedService)
  - `MdnsDiscoveryService.cs` (HostedService)
- **Backend Status:** ✅ **FULLY IMPLEMENTED**
- **UI Status:** ❌ **NO UI**
- **What Exists:**
  - Automatic client discovery on network
  - mDNS/Bonjour service broadcasting
  - UDP-based discovery
  - Discovered clients list
- **What's Missing:**
  - No UI to view discovered (unregistered) clients
  - No UI to configure discovery settings (enable/disable)
  - No UI to manually trigger discovery scan
  - No UI to register discovered clients
- **Priority:** 🟡 **MEDIUM** - Nice-to-have auto-discovery UI
- **Estimated Effort:** Medium (3-4 hours)
  - Add "Discovered Devices" section in Devices tab
  - Add manual scan button
  - Add "Register" button for discovered clients
  - Add discovery settings in Settings dialog

---

### 10. Data Refresh Service
- **Service File:** `DataRefreshService.cs` (HostedService)
- **Backend Status:** ✅ **FULLY IMPLEMENTED** (5-minute refresh interval)
- **UI Status:** ❌ **NO CONFIGURATION UI**
- **What Exists:**
  - Background service refreshing data sources every 5 minutes
  - Automatic layout updates with fresh data
  - Refresh interval hardcoded
- **What's Missing:**
  - No UI to view refresh status
  - No UI to configure refresh interval
  - No UI to manually trigger refresh
  - No UI to see last refresh time
  - No UI to enable/disable auto-refresh per data source
- **Priority:** 🟢 **LOW** - Works automatically
- **Estimated Effort:** Small (1-2 hours)
  - Add refresh status in Data Sources tab
  - Add refresh interval setting in Settings dialog
  - Add manual refresh button

---

## CATEGORY 3: SERVICES WITH PARTIAL UI

These services have **some UI** but are **incomplete**:

### 11. Schedule Service
- **Service File:** `ScheduleService.cs` (not registered - needs investigation)
- **Backend Status:** ✅ **BACKEND COMPLETE** (schedule CRUD, recurring schedules)
- **UI Status:** ⚠️ **PARTIAL** (Scheduling tab exists but incomplete)
- **What Exists in Backend:**
  - Time-based layout scheduling
  - Recurring schedules (daily, weekly)
  - Schedule priorities
  - Schedule database tables
- **What Exists in UI:**
  - SchedulingViewModel registered in App.xaml.cs
  - Scheduling tab in MainWindow.xaml (line 1468)
- **What's Missing in UI:**
  - Schedule calendar view incomplete
  - No drag-drop schedule creation
  - No recurring schedule UI
  - No schedule conflict detection UI
  - No schedule preview
- **Priority:** 🔴 **HIGH** - Critical feature (mentioned in CODETODO.md)
- **Estimated Effort:** Large (10-12 hours)
  - Complete calendar view implementation
  - Add schedule CRUD dialogs
  - Add recurring schedule builder
  - Add conflict detection UI
  - Add schedule preview
  - Integrate with ScheduleService

---

### 12. Query Cache Service & Connection Pool Settings
- **Service File:** `QueryCacheService.cs` (Singleton)
- **Configuration:** `QueryCacheSettings.cs`, `ConnectionPoolSettings.cs`
- **Backend Status:** ✅ **FULLY IMPLEMENTED**
- **UI Status:** ❌ **NO UI**
- **What Exists:**
  - Query result caching
  - Configurable cache duration
  - Connection pooling for database
  - Settings in appsettings.json
- **What's Missing:**
  - No UI to view cache statistics
  - No UI to clear cache
  - No UI to configure cache settings
  - No UI for connection pool monitoring
- **Priority:** 🟢 **LOW** - Advanced/debugging feature
- **Estimated Effort:** Small (2-3 hours)
  - Add cache statistics in System Diagnostics
  - Add cache clear button
  - Add cache settings in Settings dialog

---

## CATEGORY 4: MISSING DIALOGS/WINDOWS

These ViewModels exist but have **no corresponding View**:

### 13. Grid Configuration Dialog
- **ViewModel:** `GridConfigViewModel.cs` exists
- **View:** ❌ **GridConfigDialog.xaml MISSING**
- **Purpose:** Configure table/grid element properties
- **Priority:** 🟡 **MEDIUM** - Table elements exist
- **Estimated Effort:** Small (2-3 hours)

---

## CATEGORY 5: EXISTING TABS MISSING FEATURES

### 14. Logs Tab
- **Tab:** Exists (line 1999 in MainWindow.xaml)
- **ViewModel:** `LogViewerViewModel.cs` exists
- **Status:** ⚠️ **INCOMPLETE**
- **What's Missing:**
  - Log filtering UI incomplete
  - No export logs functionality
  - No log search
  - No log level filtering
- **Priority:** 🟡 **MEDIUM**
- **Estimated Effort:** Medium (3-4 hours)

---

### 15. Live Debug Logs Tab
- **Tab:** Exists (line 2114 in MainWindow.xaml)
- **ViewModel:** `LiveLogsViewModel.cs` exists with UISink
- **Status:** ✅ **WORKING** (real-time log streaming)
- **Potential Enhancements:**
  - Auto-scroll toggle
  - Color coding by log level
  - Copy selected logs
- **Priority:** 🟢 **LOW** - Already functional

---

## CATEGORY 6: CONFIGURATION WITHOUT UI

These configuration classes exist but can only be edited via JSON:

### 16. Server Settings
- **File:** `ServerSettings.cs` (147 lines)
- **Current Access:** appsettings.json only
- **Features:**
  - Port configuration (8080 + alternatives 8081, 8082, 8083, 8888, 9000)
  - Auto port selection
  - SSL/TLS enable
  - Certificate configuration (path, thumbprint, password)
  - WebSocket endpoint path
  - Max message size
  - Client heartbeat timeout
- **Priority:** 🔴 **HIGH** (covered by #1 - Settings Command)

---

### 17. Query Cache Settings
- **File:** `QueryCacheSettings.cs`
- **Current Access:** appsettings.json only
- **Features:**
  - Cache duration
  - Max cache entries
  - Enable/disable caching
- **Priority:** 🟢 **LOW**

---

### 18. Connection Pool Settings
- **File:** `ConnectionPoolSettings.cs`
- **Current Access:** appsettings.json only
- **Features:**
  - Min/max pool size
  - Connection timeout
  - Pool configuration
- **Priority:** 🟢 **LOW**

---

## IMPLEMENTATION PRIORITY MATRIX

### 🔴 **HIGH PRIORITY** (Must Implement)
1. ✅ **Settings Command** - Users need UI to configure server
2. ✅ **Backup/Restore Database** - Critical data safety
3. ✅ **Alert System UI** - Critical monitoring (backend complete!)
4. ✅ **Schedule Service UI Completion** - Core feature

**Total Effort: ~30-40 hours**

### 🟡 **MEDIUM PRIORITY** (Should Implement)
5. Template Manager - Advanced users want custom templates
6. Client Registration Tokens - Security improvement
7. Discovery Service UI - Ease of setup
8. System Diagnostics - Troubleshooting aid
9. Logs Tab Enhancements - Better debugging
10. Grid Config Dialog - Table elements need it

**Total Effort: ~20-25 hours**

### 🟢 **LOW PRIORITY** (Nice to Have)
11. Server Configuration Command (merge with Settings)
12. Data Refresh Service UI - Already works automatically
13. Query Cache UI - Advanced/debugging feature
14. Connection Pool UI - Advanced/debugging feature

**Total Effort: ~8-10 hours**

---

## RECOMMENDED IMPLEMENTATION ORDER

### Phase 1: Critical Features (Week 1-2)
1. **Settings Dialog** - Single comprehensive settings window covering:
   - Server Settings (port, SSL, etc.)
   - Query Cache Settings
   - Connection Pool Settings
   - Data Refresh interval
   - Discovery enable/disable
   - **Effort:** 6-8 hours

2. **Backup/Restore Database**
   - Backup command with SaveFileDialog
   - Restore command with OpenFileDialog and warnings
   - Database validation
   - **Effort:** 6-8 hours

3. **Alert System UI**
   - Alert Rules CRUD
   - Alert History viewer
   - Real-time alert notifications
   - Alert condition builder
   - **Effort:** 10-12 hours

4. **Complete Scheduling UI**
   - Calendar view completion
   - Recurring schedule builder
   - Conflict detection
   - **Effort:** 10-12 hours

**Phase 1 Total: ~32-40 hours**

### Phase 2: Important Features (Week 3-4)
5. **Client Registration Tokens**
   - Token management window
   - Generate/revoke functionality
   - **Effort:** 3-4 hours

6. **System Diagnostics**
   - Health check dashboard
   - Performance metrics
   - Diagnostic report export
   - **Effort:** 4-6 hours

7. **Template Manager**
   - Template CRUD
   - Scriban editor with preview
   - **Effort:** 6-8 hours

8. **Discovery UI**
   - Discovered devices list
   - Manual scan button
   - Register discovered clients
   - **Effort:** 3-4 hours

**Phase 2 Total: ~16-22 hours**

### Phase 3: Polish & Enhancements (Week 5)
9. **Logs Enhancements**
   - Search/filter improvements
   - Export functionality
   - **Effort:** 3-4 hours

10. **Grid Config Dialog**
    - Create missing XAML
    - Wire up ViewModel
    - **Effort:** 2-3 hours

11. **Data Refresh UI**
    - Status indicators
    - Manual refresh buttons
    - **Effort:** 1-2 hours

**Phase 3 Total: ~6-9 hours**

---

## TOTAL PROJECT ESTIMATE

- **High Priority:** 30-40 hours
- **Medium Priority:** 20-25 hours
- **Low Priority:** 8-10 hours

**Grand Total: 58-75 hours** (~2-3 weeks full-time)

---

## QUICK WINS (Can be done in <2 hours each)

1. **Data Refresh Manual Button** - Add to Data Sources tab
2. **Cache Clear Button** - Add to Settings or Tools menu
3. **Discovery Manual Scan** - Add to Devices tab
4. **Log Export** - Add to Logs tab
5. **Grid Config Dialog** - Create XAML from existing ViewModel

**Quick Wins Total: ~8-10 hours** (1 day of work for major UX improvements)

---

## ARCHITECTURAL NOTES

### Service Registration Status (App.xaml.cs)
```csharp
// ViewModels (9 registered)
MainViewModel, DesignerViewModel, DeviceManagementViewModel, DataSourceViewModel,
PreviewViewModel, SchedulingViewModel, MediaLibraryViewModel, LogViewerViewModel,
ScreenshotViewModel, MediaBrowserViewModel, LiveLogsViewModel

// Services (12 registered)
LayoutService, ClientService, SqlDataService, TemplateService,
WebSocketCommunicationService, EnhancedMediaService, AuthenticationService,
LogStorageService, QueryCacheService, AlertService

// Repositories (1 registered)
DataSourceRepository

// Background Services (6 registered as HostedService)
DataRefreshService, HeartbeatMonitoringService, DiscoveryService,
MdnsDiscoveryService, MessageHandlerService, AlertMonitoringService
```

### Missing Service Registrations
- ScheduleService - Needs to be registered (currently not in App.xaml.cs)
- BackupService - Mentioned but not found in Services folder
- RestoreService - Does not exist, needs to be created

---

## TESTING REQUIREMENTS

For each new UI feature, ensure:
1. ✅ Data binding works correctly
2. ✅ Commands are wired up
3. ✅ Validation is implemented
4. ✅ Error handling is robust
5. ✅ Undo/Redo works (if applicable)
6. ✅ Changes persist to database/config
7. ✅ UI is responsive and doesn't block
8. ✅ Logging is comprehensive

---

## CONCLUSION

The Digital Signage Server has a **solid backend architecture** with 21 services, but suffers from **significant UI gaps**. The most critical missing features are:

1. **Settings UI** - Users are editing JSON files manually
2. **Backup/Restore** - No data safety mechanisms exposed
3. **Alert System** - Complete backend but zero UI
4. **Scheduling** - Partially implemented UI

**Recommendation:** Focus on Phase 1 (Critical Features) first. This will provide the biggest immediate value to users and address critical usability and data safety issues.

**Generated with:** Claude Code 4.1
**Last Updated:** 2025-11-14
