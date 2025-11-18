# ✅ Priorisierte Abarbeitungs-Checkliste

**Digital Signage WPF Projekt - Code-Verbesserungen**
**Stand:** 2025-11-18
**Gesamt:** 195 Issues → 70-100 Stunden Aufwand

---

## 🔴 P0: CRITICAL - SOFORT (Heute/Morgen) - 8-12h

### Security (CRITICAL)

- [x] **SQL Injection beheben** (3h) 🔴
  - **Datei:** `src/DigitalSignage.Server/ViewModels/DataSourceViewModel.cs`
  - **Fix:** Strikte Identifier-Validierung (Regex) + Schema-basierte Tabellen/Spalten; Spalten werden aus Whitelist (`AvailableColumns`) gewählt; Tabellen- und Spaltennamen werden mit `[]` gequotet; WHERE/ORDER BY basic Keyword-Blocker.
  - **Verantwortlich:** Security Team + Backend Lead
  - **OWASP:** A03:2021 – Injection
  - **Tester:** SQL Injection Attack testen

- [x] **Connection String Injection beheben** (1h) 🔴
  - **Datei:** `src/DigitalSignage.Data/Services/SqlDataService.cs`
  - **Fix:** `SqlConnectionStringBuilder`-basierte Sanitisierung + Whitelist-Rebuild (`SanitizeConnectionString`) vor `ExecuteQueryAsync`/`TestConnectionAsync` + Pooling-Settings
  - **Verantwortlich:** Security Team
  - **Tester:** Sonderzeichen in Username/Password testen

- [x] **SSH Command Injection beheben** (2h) 🔴
  - **Datei:** `src/DigitalSignage.Server/Services/RemoteClientInstallerService.cs:ForceFreshInstallAsync, IsServiceActiveSafeAsync`
  - **Fix:** Base64-Encoding für Passwörter und Decoding auf Remote statt direkter Shell-Interpolation (`echo $B64 | base64 -d | sudo -S ...`)
  - **Verantwortlich:** DevOps + Security
  - **Tester:** Malicious password strings testen

### Resource Leaks (CRITICAL)

- [x] **JsonDocument Leaks fixen** (30min) 🔴 ⚡ QUICK WIN
  - **Dateien:**
    - `src/DigitalSignage.Data/Services/SqlDataService.cs:360`
    - `src/DigitalSignage.Data/Services/SqlDataService.cs:431`
    - `src/DigitalSignage.Server/ViewModels/AlertRuleEditorViewModel.cs:107`
  - **Fix:** `using var jsonDocument = JsonDocument.Parse(...)`
  - **Verantwortlich:** Backend Developer
  - **Tester:** Memory Profiler laufen lassen (sollte keine JsonDocument-Leaks mehr zeigen)

- [x] **Timer Resource Leak beheben** (15min) 🔴 ⚡ QUICK WIN
  - **Datei:** `src/DigitalSignage.Server/ViewModels/DeviceDetailViewModel.cs:20,132`
  - **Fix:** IDisposable implementieren, Timer disposen
  - **Verantwortlich:** UI Developer
  - **Tester:** Fenster 10x öffnen/schließen, Task Manager prüfen

- [x] **Ping Resource Leak beheben** (5min) 🔴 ⚡ QUICK WIN
  - **Datei:** `src/DigitalSignage.Server/ViewModels/DeviceDetailViewModel.cs:292`
  - **Fix:** `using var ping = new Ping();`
  - **Verantwortlich:** Network Developer
  - **Tester:** 100 Pings ausführen, Resource Monitor prüfen

- [x] **WebSocket Dictionary Disposal** (30min) 🔴
  - **Datei:** `src/DigitalSignage.Server/Services/WebSocketCommunicationService.cs:269`
  - **Fix:** Alle WebSockets vor Clear() disposen
  - **Verantwortlich:** Network Developer
  - **Tester:** Server während aktiver Connections stoppen

### Async/Await (CRITICAL für Datenkonsistenz)

- [x] **Fire-and-Forget Tasks beheben** (1h) 🔴
  - **Datei:** `src/DigitalSignage.Server/ViewModels/AlertsViewModel.cs:716,767`
  - **Fix:** Tasks tracken, bei Disposal awaiten
  - **Verantwortlich:** MVVM Developer
  - **Tester:** Filter schnell ändern, dann Fenster sofort schließen

---

### ✅ P0 Abschluss-Checkliste

- [ ] Alle 8 Critical Fixes implementiert
- [ ] Security Scan durchgeführt (keine Critical Findings)
- [ ] Memory Profiler zeigt keine Leaks
- [ ] Git Commit + Push



---

## 🟡 P1: HIGH - Diese Woche (30-40h)

### Performance (HIGH - Hot Paths!)

- [x] **File I/O: Logdatei-Streaming** (1h) 🔥
  - **Datei:** `src/DigitalSignage.Server/Services/SystemDiagnosticsService.cs:438`
  - **Problem:** Liest 10+ MB Logdatei komplett in Memory
  - **Fix:** File.ReadLines() + Single-Pass Counting
  - **Impact:** 80-95% weniger Memory
  - **Verantwortlich:** Backend Developer
  - **Tester:** Große Logdatei (10+ MB) generieren, Memory Usage messen

- [x] **Multiple LINQ Iterations optimieren** (30min) 🔥
  - **Datei:** `src/DigitalSignage.Server/ViewModels/DeviceManagementViewModel.cs:736-767`
  - **Problem:** 4-5x Count() auf selber Collection
  - **Fix:** Single-Pass mit switch Statement
  - **Impact:** 75% schneller bei 100+ Clients
  - **Verantwortlich:** MVVM Developer
  - **Tester:** 100+ Clients registrieren, UI-Responsiveness prüfen

- [x] **UndoRedoManager refactoren** (1h) 🔥
  - **Datei:** `src/DigitalSignage.Server/Helpers/UndoRedoManager.cs:37-46`
  - **Problem:** O(5n) Operation bei jedem Command
  - **Fix:** LinkedList statt Stack
  - **Impact:** 95% schneller
  - **Verantwortlich:** MVVM Developer
  - **Tester:** 100+ Undo Operations, Performance messen

- [x] **Reflection Caching in PropertyChangeCommand** (30min) 🔥
  - **Datei:** `src/DigitalSignage.Server/Helpers/UndoRedoManager.cs:130-134`
  - **Fix:** PropertyInfo cachen
  - **Impact:** 10-50x schnellere Undo/Redo
  - **Verantwortlich:** MVVM Developer

- [x] **Multiple DB Queries optimieren** (30min) 🔥
  - **Datei:** `src/DigitalSignage.Server/Services/SystemDiagnosticsService.cs:97-100`
  - **Fix:** Task.WhenAll für parallele Queries
  - **Impact:** 50-70% schneller
  - **Verantwortlich:** Backend Developer

### Async/Await (HIGH)

- [x] **Blocking I/O in BackupService** (30min)
  - **Datei:** `src/DigitalSignage.Server/Services/BackupService.cs:71,78,87`
  - **Fix:** Task.Run für File.Copy/Delete
  - **Verantwortlich:** Backend Developer

- [x] **Blocking I/O in EnhancedMediaService** (15min)
  - **Datei:** `src/DigitalSignage.Server/Services/EnhancedMediaService.cs:117`
  - **Fix:** Task.Run für File.Delete
  - **Verantwortlich:** Backend Developer

- [x] **Fake Async in LayoutService** (30min)
  - **Datei:** `src/DigitalSignage.Server/Services/LayoutService.cs:213-244`
  - **Fix:** Echte Async-Implementierung beibehalten; zusätzlich sichere JsonSettings
  - **Verantwortlich:** Backend Developer

- [x] **Fake Async in MediaService** (15min)
  - **Datei:** `src/DigitalSignage.Server/Services/MediaService.cs:135-171`
  - **Fix:** Echte Async-Implementierung (Delete via Task.Run, Directory listing via Task.Run)
  - **Verantwortlich:** Backend Developer

- [x] **Blocking I/O in DiagnosticsViewModel** (30min)
  - **Datei:** `src/DigitalSignage.Server/ViewModels/DiagnosticsViewModel.cs:186-212`
  - **Fix:** File.Delete Loop mit Task.Run
  - **Verantwortlich:** UI Developer

### Security (HIGH)

- [x] **XSS in Scriban Templates beheben** (15min) ⚡ QUICK WIN
  - **Datei:** `src/DigitalSignage.Server/Services/ScribanService.cs:67`
  - **Fix:** HTML-Encoding des Render-Outputs
  - **Verantwortlich:** Security Team
  - **Tester:** `<script>alert('XSS')</script>` in Template-Daten testen

- [x] **Path Traversal Validation verbessern** (2h)
  - **Datei:** `src/DigitalSignage.Server/Services/LayoutService.cs:489`
  - **Fix:** Whitelist + Path Verification + Reserved Names Check (Regex + Reserved Names, invalid chars, no traversal)
  - **Verantwortlich:** Security Team
  - **Tester:** `..`, `CON`, `file.json:hidden` als layoutId testen

- [x] **Insecure Deserialization absichern** (1h)
  - **Dateien:** `LayoutService.cs:287,371,444`
  - **Fix:** SafeSettings mit TypeNameHandling.None
  - **Verantwortlich:** Security Team
  - **Tester:** $type-Property in JSON versuchen

- [x] **Process.Start Input Validation** (1h)
  - **Dateien:** `MainViewModel.cs:Documentation`, `Program.cs:RestartNormally`
  - **Fix:** Path/URL Validation vor Process.Start
  - **Verantwortlich:** Security Team

### MVVM Architecture (HIGH)

- [ ] **ISynchronizationContext Service erstellen** (2h)
  - **Neue Datei:** `src/DigitalSignage.Server/Services/ISynchronizationContext.cs`
  - **Fix:** Interface + WpfSynchronizationContext Implementierung
  - **Verantwortlich:** MVVM Developer
  - **Testing:** Dependency Injection registrieren

- [ ] **Dispatcher Usage refactoren - AlertsViewModel** (1h)
  - **Datei:** `src/DigitalSignage.Server/ViewModels/AlertsViewModel.cs` (4x Dispatcher)
  - **Fix:** ISynchronizationContext injizieren und verwenden
  - **Verantwortlich:** MVVM Developer

- [ ] **Dispatcher Usage refactoren - DeviceManagementViewModel** (30min)
  - **Datei:** `src/DigitalSignage.Server/ViewModels/DeviceManagementViewModel.cs` (3x)
  - **Fix:** ISynchronizationContext verwenden
  - **Verantwortlich:** MVVM Developer

- [ ] **Dispatcher Usage refactoren - LogViewerViewModel** (1h)
  - **Datei:** `src/DigitalSignage.Server/ViewModels/LogViewerViewModel.cs` (7x)
  - **Fix:** ISynchronizationContext verwenden
  - **Verantwortlich:** MVVM Developer

- [ ] **Dispatcher Usage refactoren - Weitere ViewModels** (1h)
  - **Dateien:** DeviceDetailViewModel, DiscoveredDevicesViewModel, ServerManagementViewModel, ScreenshotViewModel
  - **Fix:** ISynchronizationContext verwenden
  - **Verantwortlich:** MVVM Developer

- [ ] **DatabaseConnectionDialog refactoren** (3h)
  - **Datei:** `src/DigitalSignage.Server/Views/DatabaseConnectionDialog.xaml.cs:12-186`
  - **Problem:** Komplette Business-Logik in Code-Behind
  - **Fix:** DatabaseConnectionViewModel erstellen
  - **Verantwortlich:** MVVM Developer
  - **Tester:** Dialog-Funktionalität komplett testen

- [ ] **MainWindow Log Handlers refactoren** (2h)
  - **Datei:** `src/DigitalSignage.Server/Views/MainWindow.xaml.cs:30-106`
  - **Problem:** CopyLogsToClipboard_Click, ShowLogDetails_Click in Code-Behind
  - **Fix:** Commands in MainViewModel
  - **Verantwortlich:** MVVM Developer

- [ ] **LayoutPreviewWindow refactoren** (2h)
  - **Datei:** `src/DigitalSignage.Server/Views/LayoutManager/LayoutPreviewWindow.xaml.cs:14-107`
  - **Problem:** Rendering-Logik in Code-Behind
  - **Fix:** ViewModel mit BitmapSource Property
  - **Verantwortlich:** MVVM Developer

- [ ] **RegisterDiscoveredDeviceDialog refactoren** (1h)
  - **Datei:** `src/DigitalSignage.Server/Views/Dialogs/RegisterDiscoveredDeviceDialog.xaml.cs:33-74`
  - **Problem:** Model-Erstellung in Code-Behind
  - **Fix:** ViewModel mit Command
  - **Verantwortlich:** MVVM Developer

- [ ] **SplashScreenWindow refactoren** (1h)
  - **Datei:** `src/DigitalSignage.Server/Views/SplashScreenWindow.xaml.cs:14-92`
  - **Problem:** UI-Manipulation in Code-Behind
  - **Fix:** ViewModel mit Data Binding
  - **Verantwortlich:** UI Developer

### XAML (HIGH - Performance!)

- [x] **Virtualization für SchedulingTabControl ListBox** (15min) ⚡ QUICK WIN
  - **Datei:** `src/DigitalSignage.Server/Views/LayoutManager/SchedulingTabControl.xaml:29-45,209-231`
  - **Fix:** VirtualizingPanel.IsVirtualizing="True"
  - **Impact:** 90% weniger Memory bei 100+ Items
  - **Verantwortlich:** XAML Developer
  - **Tester:** 100+ Schedules erstellen, Memory messen

- [x] **Virtualization für LayoutManagerTabControl DataGrid** (15min) ⚡ QUICK WIN
  - **Datei:** `src/DigitalSignage.Server/Views/LayoutManager/LayoutManagerTabControl.xaml:43-59`
  - **Fix:** VirtualizationMode="Recycling"
  - **Verantwortlich:** XAML Developer

- [ ] **UpdateSourceTrigger für Such-Felder** (30min)
  - **Dateien:** Mehrere Views mit Search TextBoxes
  - **Fix:** UpdateSourceTrigger=PropertyChanged
  - **Verantwortlich:** XAML Developer

---

### ✅ P1 Wochenabschluss-Checkliste

- [ ] Alle Performance Hot-Paths optimiert (5 Issues)
- [ ] Alle Blocking I/O Probleme behoben (5 Issues)
- [ ] ISynchronizationContext Service implementiert + alle ViewModels refactored (7 Issues)
- [ ] Wichtigste Code-Behind-Logik in ViewModels verschoben (5 Dialoge)
- [ ] XAML Virtualization hinzugefügt (2 Controls)
- [ ] Security HIGH Fixes abgeschlossen (4 Issues)
- [ ] Unit Tests für neue Features
- [ ] Performance-Tests durchgeführt
- [ ] Code Review
- [ ] Git Commit + Push
- [ ] Release Notes aktualisiert

**Deadline:** Ende Woche 2

---

## 🟢 P2: MEDIUM - Nächste 2 Wochen (20-30h)

### Code-Duplikation (MEDIUM)

- [ ] **SendCommandAsync Pattern extrahieren** (30min) 📈
  - **Datei:** `src/DigitalSignage.Server/ViewModels/DeviceManagementViewModel.cs:195-389`
  - **Problem:** 7 identische Methoden (140 Zeilen)
  - **Fix:** ExecuteClientCommandAsync() Helper
  - **Einsparung:** 115 Zeilen
  - **Verantwortlich:** MVVM Developer

- [ ] **ViewModelExtensions.ExecuteSafeAsync() erstellen** (1h) 📈
  - **Neue Datei:** `src/DigitalSignage.Server/Helpers/ViewModelExtensions.cs`
  - **Problem:** 50+ try-catch-finally Blöcke
  - **Einsparung:** 168 Zeilen
  - **Verantwortlich:** MVVM Developer

- [ ] **Error Handling Pattern anwenden - AlertsViewModel** (30min)
  - **Fix:** ExecuteSafeAsync() verwenden
  - **Verantwortlich:** MVVM Developer

- [ ] **Error Handling Pattern anwenden - DeviceManagementViewModel** (30min)
  - **Fix:** ExecuteSafeAsync() verwenden
  - **Verantwortlich:** MVVM Developer

- [ ] **Dialog Opening Pattern extrahieren** (45min) 📈
  - **Datei:** `src/DigitalSignage.Server/ViewModels/MainViewModel.cs:107-224`
  - **Problem:** 4 ähnliche Dialog-Methoden (120 Zeilen)
  - **Fix:** Generische ShowDialogAsync<TViewModel, TDialog>()
  - **Einsparung:** 80 Zeilen
  - **Verantwortlich:** MVVM Developer

- [ ] **CollectionExtensions erstellen** (30min)
  - **Neue Datei:** `src/DigitalSignage.Server/Helpers/CollectionExtensions.cs`
  - **Fix:** ReplaceAll(), AddRange(), RemoveRange()
  - **Einsparung:** 30 Zeilen
  - **Verantwortlich:** MVVM Developer

- [ ] **ValidationExtensions erstellen** (30min)
  - **Neue Datei:** `src/DigitalSignage.Server/Helpers/ValidationExtensions.cs`
  - **Fix:** ValidateRangeAsync(), ValidateRequiredAsync()
  - **Einsparung:** 40 Zeilen
  - **Verantwortlich:** MVVM Developer

- [ ] **WindowExtensions erstellen** (15min)
  - **Neue Datei:** `src/DigitalSignage.Server/Helpers/WindowExtensions.cs`
  - **Fix:** SetAsChildOfMainWindow()
  - **Einsparung:** 7 Zeilen
  - **Verantwortlich:** UI Developer

### Performance (MEDIUM)

- [ ] **QueryCacheService - Multiple Sum() optimieren** (30min)
  - **Datei:** `src/DigitalSignage.Server/Services/QueryCacheService.cs:133-148`
  - **Fix:** Single-Pass Aggregation
  - **Verantwortlich:** Backend Developer

- [ ] **RateLimitingService - GetAllStats optimieren** (30min)
  - **Datei:** `src/DigitalSignage.Server/Services/RateLimitingService.cs:191-216`
  - **Fix:** Single-Pass Counting
  - **Verantwortlich:** Backend Developer

- [x] **NetworkScannerService - .Any() in Loop** (15min)
  - **Datei:** `src/DigitalSignage.Server/Services/NetworkScannerService.cs:132-140`
  - **Fix:** HashSet für O(1) Lookup
  - **Verantwortlich:** Network Developer

### XAML (MEDIUM)

- [ ] **Button-Styles zentralisieren** (1h) 📈
  - **Dateien:** App.xaml, SettingsDialog.xaml, InputDialog.xaml, DeviceDetailWindow.xaml
  - **Problem:** PrimaryButton/SecondaryButton 4x dupliziert
  - **Fix:** Zentral in App.xaml definieren
  - **Einsparung:** 150 Zeilen
  - **Verantwortlich:** XAML Developer

- [ ] **TextBlock-Styles zentralisieren** (30min)
  - **Problem:** SectionHeader, Label, Value mehrfach definiert
  - **Fix:** App.xaml Resources
  - **Verantwortlich:** XAML Developer

- [ ] **Hardcoded Magic Numbers durch Resources ersetzen** (2h)
  - **Problem:** Margin="12", #2196F3, Width="100" überall
  - **Fix:** Spacing/Color/Size Constants in Resources
  - **Verantwortlich:** XAML Developer

- [ ] **BoolToVisibilityConverter reduzieren** (1h)
  - **Problem:** 50+ Instanzen, oft unnötig
  - **Fix:** StaticResource + DataTriggers verwenden
  - **Verantwortlich:** XAML Developer

### MVVM (MEDIUM)

- [ ] **InputDialog refactoren** (1h)
  - **Datei:** `src/DigitalSignage.Server/Views/Dialogs/InputDialog.xaml.cs:44-60`
  - **Fix:** Commands statt Click Events
  - **Verantwortlich:** MVVM Developer

- [ ] **DeviceWebInterfaceWindow Commands** (1h)
  - **Datei:** `src/DigitalSignage.Server/Views/DeviceManagement/DeviceWebInterfaceWindow.xaml.cs:20-141`
  - **Fix:** Commands für Buttons
  - **Verantwortlich:** MVVM Developer

- [ ] **SettingsDialog Window Closing** (1h)
  - **Datei:** `src/DigitalSignage.Server/Views/Dialogs/SettingsDialog.xaml.cs:53-87`
  - **Fix:** IDialogService oder Behavior
  - **Verantwortlich:** MVVM Developer

- [ ] **CloseRequested Pattern ersetzen** (2h)
  - **Dateien:** DeviceDetailViewModel.cs:365, ScreenshotViewModel.cs:252
  - **Fix:** IDialogService verwenden
  - **Verantwortlich:** MVVM Developer

- [ ] **MainWindow Click Events → Commands** (1h)
  - **Datei:** `src/DigitalSignage.Server/Views/MainWindow.xaml:37,248,250`
  - **Fix:** Exit/CopyLogs/ShowDetails Commands
  - **Verantwortlich:** MVVM Developer

---

### ✅ P2 Abschluss-Checkliste

- [ ] Code-Duplikation von 500+ auf <50 Zeilen reduziert
- [ ] 6 Extension-Klassen erstellt und dokumentiert
- [ ] XAML Styles komplett zentralisiert
- [ ] Alle MEDIUM Performance-Probleme behoben
- [ ] Wichtigste MVVM-Antipattern beseitigt
- [ ] Code Coverage >80%
- [ ] Architektur-Dokumentation aktualisiert
- [ ] Code Review
- [ ] Git Commit + Push

**Deadline:** Ende Woche 4

---

## ⚪ P3: LOW - Langfristig (10-15h)

### Code Quality (LOW)

- [ ] **ClientService DeviceInfo Merge refactoren** (30min)
  - **Datei:** `src/DigitalSignage.Server/Services/ClientService.cs:176-206`
  - **Fix:** MergeDeviceInfo() Helper-Methode
  - **Verantwortlich:** Backend Developer

- [ ] **DataRefreshService Dictionary Merge** (15min)
  - **Datei:** `src/DigitalSignage.Server/Services/DataRefreshService.cs:156-165`
  - **Fix:** LINQ SelectMany (optional)
  - **Verantwortlich:** Backend Developer

- [ ] **RelayCommand.cs löschen** (5min)
  - **Datei:** `src/DigitalSignage.Server/Helpers/RelayCommand.cs`
  - **Problem:** Duplikat zu CommunityToolkit.Mvvm
  - **Fix:** Datei löschen, nur CommunityToolkit verwenden
  - **Verantwortlich:** MVVM Developer

### XAML (LOW)

- [ ] **Simple Close Button Commands** (2h)
  - **Dateien:** TokenManagementWindow, GridConfigDialog, DataMappingDialog, etc.
  - **Fix:** Commands für Close/Cancel Buttons
  - **Verantwortlich:** XAML Developer

- [ ] **MouseDoubleClick → InputBindings** (30min)
  - **Datei:** `src/DigitalSignage.Server/Views/LayoutManager/LayoutManagerTabControl.xaml`
  - **Fix:** InputBindings statt Event
  - **Verantwortlich:** XAML Developer

- [ ] **XAML Design-System erstellen** (8h)
  - **Neue Datei:** `src/DigitalSignage.Server/Themes/DesignSystem.xaml`
  - **Fix:** Spacing (Small/Medium/Large), Color Palette, Font Sizes
  - **Verantwortlich:** UI/UX Designer + XAML Developer

- [ ] **DarkTheme.xaml erstellen** (4h)
  - **Neue Datei:** `src/DigitalSignage.Server/Themes/DarkTheme.xaml`
  - **Fix:** Komplettes Dark Theme
  - **Verantwortlich:** UI/UX Designer

### Security (LOW)

- [ ] **Hardcoded Placeholder entfernen** (5min)
  - **Datei:** `src/DigitalSignage.Server/ViewModels/TokenManagementViewModel.cs:182`
  - **Fix:** PasswordHash = string.Empty
  - **Verantwortlich:** Security Team

---
