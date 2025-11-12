# Code TODO - Digital Signage Management System

Basierend auf dem Entwicklungsauftrag und dem aktuellen Code-Stand.

**Legende:**
- ✅ Vollständig implementiert
- ⚠️ Teilweise implementiert / Verbesserung nötig
- ❌ Nicht implementiert
- 🔴 Hohe Priorität
- 🟡 Mittlere Priorität
- 🟢 Niedrige Priorität

---

## TEIL 1: WINDOWS-ANWENDUNG (SERVER/MANAGER)

### 1.1 Hauptfunktionalitäten

#### Anzeigeverwaltung
- ✅ Grundlegende Layoutverwaltung (LayoutService implementiert)
- ✅ Versionsverwaltung (Version-Feld vorhanden)
- ✅ **Layout Templates System**
  - ✅ LayoutTemplate Entity mit Category Enum
  - ✅ Kategorien: RoomOccupancy, InformationBoard, Wayfinding, MenuBoard, WelcomeScreen, Emergency, Blank, Custom
  - ✅ Built-in Templates (können nicht gelöscht werden)
  - ✅ Template Metadaten: Name, Description, Thumbnail, Resolution
  - ✅ ElementsJson für vordefinierte Element-Layouts
  - ✅ Usage Tracking (LastUsedAt, UsageCount)
  - ✅ 11 Built-in Templates beim DB-Init:
    - **Blank Templates (5):**
      - Blank 1920x1080 (Full HD Landscape)
      - Blank 1080x1920 (Full HD Portrait)
      - Blank 1280x720 (HD)
      - Blank 3840x2160 (4K UHD Landscape)
      - Blank 2160x3840 (4K UHD Portrait)
    - **Content Templates (6):**
      - Simple Information Board
      - Room Occupancy Display (mit Template-Variablen)
      - Corporate Welcome Screen (mit date_format)
      - Digital Menu Board
      - Directory Wayfinding
      - Emergency Information
  - ✅ Template-Auswahl-Dialog in UI (Vollständig implementiert)
- ❌ 🟡 **Layout-Kategorien und Tags** für bessere Organisation
  - Kategorisierung in `DisplayLayout` Model
  - Filter- und Suchfunktion in UI

#### Visueller Designer
- ✅ **Designer-Canvas** - Vollständig funktional
  - ✅ DesignerCanvas Control mit Grid-Rendering
  - ✅ Drag-and-Drop Funktionalität für Elemente
  - ✅ Werkzeugleiste mit Element-Buttons (Text, Image, Rectangle)
  - ✅ Selektions- und Transformationshandles (ResizeAdorner)
  - ✅ DesignerItemControl für Element-Rendering
  - ✅ **Multi-Selektion** - Vollständig implementiert (NEU - 2025-11-11)
    - ✅ SelectionService für Multi-Selection Management
    - ✅ Ctrl+Click für Toggle-Selection
    - ✅ Shift+Click für Range-Selection
    - ✅ Selection Rectangle mit Maus-Drag
    - ✅ Bulk Operations (Delete, Duplicate, Move)
    - ✅ Selection Bounds Calculation
- ✅ **Ebenenmanagement** - Vollständig implementiert
  - ✅ Z-Index Move Up/Down Commands
  - ✅ Z-Index Eingabefeld in Properties Panel
  - ✅ Ebenenpalette mit visueller Darstellung (Layer Panel in Designer Tab)
  - ✅ Ebenen-Sichtbarkeit Toggle (IsVisible Property)
  - ✅ Layer List mit Type Icons und Z-Index Anzeige
  - ✅ Move Up/Down Buttons für Layers
  - ✅ Synchronisierte Selektion zwischen Canvas und Layer Panel
- ✅ **Raster und Ausrichtung** - Implementiert
  - ✅ Rasteranzeige im DesignerCanvas
  - ✅ Snap-to-Grid beim Verschieben
  - ✅ Konfigurierbare Grid-Größe
  - ✅ Grid Show/Hide Toggle
  - ❌ 🟡 Ausrichtungshilfslinien (Smart Guides)
  - ❌ 🟡 Objekt-Ausrichtungs-Funktionen (links, rechts, zentriert)
- ✅ **Eigenschaften-Panel** - Vollständig implementiert mit erweiterten Features
  - ✅ Position (X, Y) Eingabefelder
  - ✅ Größe (Width, Height) Eingabefelder
  - ✅ Z-Index mit Up/Down Buttons
  - ✅ Element-Name Eingabe
  - ✅ Layout Properties (Name, Resolution, Background)
  - ✅ Duplicate und Delete Buttons
  - ✅ Dynamische Anzeige basierend auf Selektion
  - ✅ **Rotation Eingabefeld mit Slider (0-360°)**
  - ✅ **Schrift-Einstellungen für Text** (FontFamily ComboBox, FontSize Slider, Bold/Italic Toggles)
  - ✅ **Farb-Picker mit Hex-Eingabe und Vorschau** (für Text Color, Fill Color, Border Color)
  - ✅ **Kontextsensitive Properties** (Text-spezifisch, Rectangle-spezifisch)
  - ❌ 🟡 Datenquellen-Bindung UI
- ✅ **Undo/Redo-System** - Vollständig implementiert mit Command Pattern
  - ✅ IUndoableCommand Interface definiert
  - ✅ CommandHistory mit Undo/Redo Stacks (Max 50 Einträge)
  - ✅ AddElementCommand, DeleteElementCommand implementiert
  - ✅ MoveElementCommand, ResizeElementCommand implementiert
  - ✅ ChangePropertyCommand, ChangeZIndexCommand implementiert
  - ✅ Undo/Redo Commands in DesignerViewModel (Ctrl+Z, Ctrl+Y ready)
  - ✅ HistoryChanged Event für UI-Updates
  - ✅ Integration in alle Designer-Operationen
- ❌ 🟡 **Element-Gruppierung**
  - Gruppe erstellen/auflösen
  - Gruppe als Einheit transformieren

#### SQL-Datenbankanbindung
- ✅ SqlDataService mit Basisfunktionalität
- ✅ Verbindungstest implementiert
- ✅ Parametrisierte Abfragen
- ✅ **Query-Builder mit visueller Unterstützung**
  - ✅ Tabellen-Browser mit Refresh
  - ✅ Spalten-Auswahl per Checkbox
  - ✅ WHERE-Klausel Builder
  - ✅ Visual SQL Editor mit Syntax-Highlighting
  - ✅ Connection Test
  - ✅ Query Execution und Results Preview
  - ❌ 🟡 JOIN-Unterstützung (UI-gestützt)
- ❌ 🟡 **Stored Procedures Browser und Executor**
- ✅ **Daten-Refresh-Mechanismus**
  - ✅ DataRefreshService implementiert als BackgroundService
  - ✅ Polling-Timer basierend auf DataSource.RefreshInterval
  - ✅ Automatische Updates an aktive Clients
  - ❌ 🟡 Differenzielle Updates (nur geänderte Daten übertragen)
- ❌ 🟢 **SQL Service Broker Integration** für Event-basierte Updates
- ✅ **Connection Pooling** konfigurieren - Vollständig implementiert
  - ✅ ConnectionPoolSettings in appsettings.json
  - ✅ Automatische Pooling-Konfiguration in SqlDataService
  - ✅ MinPoolSize, MaxPoolSize, ConnectionTimeout, CommandTimeout
- ✅ **Query-Caching** implementieren - Vollständig implementiert
  - ✅ QueryCacheService mit SHA256-basierten Cache-Keys
  - ✅ Konfigurier bare TTL und Max-Einträge
  - ✅ LRU Eviction Strategy (10% bei Limit)
  - ✅ Cache Statistics (Hits, Misses, Hit Rate)
  - ✅ Cache-Invalidierung nach Pattern

#### Skalierbarkeit und Anpassung
- ✅ Resolution in DisplayLayout definiert
- ✅ **Vordefinierte Auflösungs-Templates**
  - ✅ Layout Templates mit verschiedenen Auflösungen
  - ✅ 1920x1080 (Full HD) Landscape & Portrait
  - ✅ 1280x720 (HD) Landscape
  - ✅ 3840x2160 (4K UHD) Landscape & Portrait
  - ✅ Resolution Objekt in LayoutTemplate Entity
  - ✅ Orientation Support (landscape/portrait)
  - ✅ 5 verschiedene Auflösungs-Templates verfügbar
  - ✅ Template-Auswahl-Dialog in UI (Vollständig implementiert)
- ❌ 🟡 **Responsive Design-Optionen**
  - Prozentuale Positionierung neben Pixel
  - Anchor-Points für Elemente
- ✅ **Zoom-Funktionalität** - Vollständig implementiert
  - ✅ Zoom-Slider in UI (25%-200%)
  - ✅ Zoom mit Mausrad (Strg + Mausrad)
  - ✅ Zoom-Level Anzeige
  - ✅ Fit to Screen / Reset Zoom Commands
  - ❌ 🟡 Zoom auf Auswahl

### 1.2 Creator-Interface Spezifikationen

#### Variablenplatzhalter
- ✅ Python Client kann {{Variable}} ersetzen
- ✅ **.NET Template-Engine** für Server-seitige Verarbeitung
  - ✅ Scriban Template Engine integriert (TemplateService)
  - ✅ Formatierungs-Optionen: {{date_format Datum "dd.MM.yyyy"}}
  - ✅ Berechnete Felder: {{Wert1 + Wert2}}
  - ✅ Fallback-Werte: {{Variable ?? "Default"}}
  - ✅ Bedingungen: {{if}}...{{else}}...{{end}}
  - ✅ Schleifen: {{for item in items}}...{{end}}
  - ✅ Custom Functions: date_format, number_format, upper, lower, default
  - ✅ Integration in ClientService und DataRefreshService
  - ✅ Umfassende Dokumentation (TEMPLATE_ENGINE.md)
- ❌ 🟡 **Variable-Browser** in UI
  - Verfügbare Variablen anzeigen
  - Drag-and-Drop von Variablen in Textfelder

#### Medienmanagement
- ✅ **Zentrale Medienbibliothek** - Vollständig implementiert (Backend + UI)
  - ✅ MediaFile Entity mit vollständigen Metadaten
  - ✅ MediaType Enum (Image, Video, Audio, Document, Other)
  - ✅ EnhancedMediaService mit Datenbank-Integration
  - ✅ File Validation (Größe, Typ, Extension)
  - ✅ SHA256 Hash für Duplikat-Erkennung
  - ✅ Access Tracking (LastAccessedAt, AccessCount)
  - ✅ MIME Type Detection
  - ✅ Unterstützte Formate:
    - Bilder: JPG, PNG, GIF, BMP, WEBP, SVG
    - Videos: MP4, AVI, MOV, WMV, FLV, MKV, WEBM
    - Audio: MP3, WAV, OGG, FLAC, AAC, WMA
    - Dokumente: PDF, DOC/DOCX, XLS/XLSX, PPT/PPTX, TXT
  - ✅ 100 MB Max File Size
  - ✅ **MediaLibraryViewModel** mit vollständiger CRUD-Funktionalität
  - ✅ **Media Library Tab UI** (Upload, Filter, Search, Details Panel)
  - ✅ **Filter nach Medientyp** (All, Images, Videos, Audio, Documents)
  - ✅ **Suchfunktion** (OriginalFileName, Description, Tags)
  - ✅ **Upload Dialog** mit Multi-Select
  - ✅ **Delete Confirmation** Dialog
  - ✅ **Details Panel** mit Edit-Funktionen (Description, Tags, Category)
  - ✅ **FileSizeConverter** für formatierte Größenangaben
  - ✅ **Status Messages** für User Feedback
  - ❌ 🟡 Thumbnail-Generierung für Bildvorschau
- ❌ 🟡 **Bildbearbeitung**
  - Zuschneiden
  - Größenanpassung
  - Filter (Helligkeit, Kontrast, Sättigung)
- ❌ 🟡 **Symbolbibliothek**
  - Material Design Icons
  - FontAwesome Icons
  - SVG-Import
  - Farbänderung von Icons

#### Vorschau und Test
- ✅ **Live-Vorschau Tab** - Vollständig implementiert
  - ✅ Live-Vorschau mit aktuellem Layout
  - ✅ Testdaten-Simulation (JSON Editor)
  - ✅ Daten-Refresh Button für manuelle Updates
  - ✅ Auto-Refresh Toggle (alle 5 Sekunden)
  - ✅ Vollständige Template Engine Integration
  - ✅ Zoom-Funktionen (Fit, Reset)
  - ❌ 🟡 Daten-Simulator mit automatisch wechselnden Werten
  - ❌ 🟡 Vollbild-Vorschau
  - ❌ 🟢 Multi-Monitor-Vorschau
  - ❌ 🟢 Export als Bild (PNG/PDF)

### 1.3 Raspberry Pi Geräteverwaltung

#### Geräteregistrierung
- ✅ **RegisterClientAsync vollständig implementiert**
  - ✅ Validierung von Registration Tokens (AuthenticationService)
  - ✅ MAC-basierte Client-Identifikation
  - ✅ Re-Registration bestehender Clients
  - ✅ Auto-Assignment von Group/Location via Token
  - ✅ Datenbank-Persistenz (EF Core)
  - ✅ In-Memory-Cache für Performance
  - ✅ RegistrationResponseMessage an Client
- ✅ **Python Client unterstützt Registration Token**
  - ✅ Configuration: registration_token in config.json
  - ✅ Environment Variable: DS_REGISTRATION_TOKEN
  - ✅ Handler für REGISTRATION_RESPONSE
  - ✅ Automatische Client-ID-Aktualisierung
- ✅ **Automatische Netzwerkerkennung** - Vollständig implementiert
  - ✅ UDP-Broadcast auf Port 5555
  - ✅ DiscoveryService als Background Service im Server
  - ✅ Automatische Antwort mit Server-Verbindungsdaten (IPs, Port, Protokoll)
  - ✅ Python DiscoveryClient mit ServerInfo dataclass
  - ✅ discovery.py Modul mit discover_servers() Funktion
  - ✅ auto_discover Config-Option für Zero-Configuration Setup
  - ✅ Discover Devices Button in Device Management UI
  - ✅ Environment Variables: DS_AUTO_DISCOVER, DS_DISCOVERY_TIMEOUT
- ❌ 🟡 **QR-Code-Pairing**
  - QR-Code generieren mit Verbindungsdaten
  - Client scannt QR-Code für Auto-Konfiguration
- ⚠️ **Gerätegruppierung**
  - ✅ Group und Location Felder in RaspberryPiClient
  - ✅ Auto-Assignment via Registration Token
  - ❌ Bulk-Operationen auf Gruppen

#### Geräteinformationen
- ✅ DeviceInfo mit umfangreichen Daten
- ✅ Python DeviceManager sammelt System-Infos
- ✅ Alle geforderten Felder vorhanden
- ❌ 🟡 **Geräte-Detail-Ansicht** in UI
  - Alle Infos übersichtlich anzeigen
  - Grafische Darstellung (CPU, Memory Charts)
  - Ping-Test Button

#### Verwaltungsfunktionen
- ✅ **ClientService vollständig implementiert**
  - ✅ SendCommandAsync mit Datenbank-Persistenz
  - ✅ AssignLayoutAsync mit DB-Update
  - ✅ UpdateClientStatusAsync mit async DB-Write
  - ✅ GetAllClientsAsync / GetClientByIdAsync
  - ✅ RemoveClientAsync
  - ✅ Initialization von DB-Clients beim Startup
- ✅ **HeartbeatMonitoringService implementiert**
  - ✅ Background Service für Timeout-Überwachung
  - ✅ 30s Check-Interval, 120s Timeout
  - ✅ Automatisches Markieren als Offline
  - ✅ Logging von Status-Änderungen
- ✅ Python Client unterstützt RESTART, SCREENSHOT, SCREEN_ON/OFF, SET_VOLUME
- ✅ **Zeitpläne für Layouts** - Vollständig implementiert
  - ✅ LayoutSchedule Entity mit vollständiger Konfiguration
  - ✅ Zeitplan-Editor UI (Priority, Start/End Date/Time, Days of Week)
  - ✅ SchedulingService mit Background Worker
  - ✅ Automatische Zeitplan-Ausführung (alle 60 Sekunden)
  - ✅ Priority-basierte Auswahl bei Überlappungen
  - ✅ Aktives Schedule Tracking
  - ✅ Client-seitige Zeitplan-Ausführung via DisplayUpdate Messages
  - ✅ Schedule Management UI (Add, Edit, Delete, Enable/Disable)
  - ❌ 🟡 Cron-Expression Support für komplexere Zeitpläne
- ✅ **Remote Log-Viewer** - Vollständig implementiert als "Logs Tab" (NEU - 2025-11-12)
  - ✅ Client-Filter ComboBox (zeigt alle verfügbaren Clients)
  - ✅ Log-Level Filter (Debug, Info, Warning, Error, Critical)
  - ✅ Echtzeit-Log-Streaming von Clients
  - ✅ DataGrid mit Time, Client, Level, Message
  - ✅ Color-coded Log Levels
  - ✅ Export Funktionalität
  - ✅ LogViewerViewModel mit vollständiger Fehlerbehandlung
  - ❌ 🟡 LOG Nachrichtentyp noch zu implementieren (aktuell andere Mechanismen)
- ✅ **Alert System** - Vollständig implementiert (NEU - 2025-11-11)
  - ✅ Alert und AlertRule Entities mit EF Core
  - ✅ AlertService mit Rules Engine
  - ✅ AlertMonitoringService (Background Service, prüft jede Minute)
  - ✅ Rule Types: DeviceOffline, HighCPU, HighMemory, LowDiskSpace, DataSourceError, HighErrorRate
  - ✅ Configurable Thresholds via JSON
  - ✅ Cooldown Period zur Vermeidung von Spam-Alerts
  - ✅ Alert Severity Levels (Info, Warning, Error, Critical)
  - ✅ Alert Acknowledge und Resolve Funktionen
  - ✅ Notification Channels Support (Placeholder für Email/SMS/Push)
  - ❌ UI für Alert Management (noch nicht implementiert)

### 1.4 Datenmanagement

#### SQL-Integration
- ✅ Grundlegende Funktionen implementiert
- ❌ 🟡 **Connection Pooling** optimieren
- ❌ 🟡 **Query-Caching** implementieren
  - In-Memory Cache mit Invalidierung
  - Cache-TTL konfigurierbar
- ❌ 🟡 **Transaktionsmanagement** für Batch-Updates

#### Daten-Mapping
- ❌ 🔴 **Visuelle Zuordnung SQL → UI-Elemente**
  - Mapping-Editor
  - Spalten-Browser
  - Automatische Typkonvertierung
- ❌ 🟡 **Aggregatfunktionen** (SUM, AVG, COUNT)
  - In Query-Builder integrieren

#### Caching-Strategie
- ✅ **Client-seitiger Cache** für Offline-Betrieb
  - ✅ Layout-Daten lokal speichern (SQLite)
  - ✅ Automatisches Fallback bei Verbindungsabbruch
  - ✅ Cache-Metadaten und Statistiken
- ❌ 🟡 **TTL für Cache-Einträge**
  - Cache-Alterung und automatische Bereinigung
- ❌ 🟡 **Differenzielle Updates**
  - Nur geänderte Daten übertragen
  - Delta-Komprimierung
- ❌ 🟡 **gzip-Komprimierung** für WebSocket-Nachrichten

---

## TEIL 2: RASPBERRY PI CLIENT-SOFTWARE

### 2.1 Kernfunktionalitäten

#### Display-Engine
- ✅ PyQt5 Rendering funktioniert
- ⚠️ **Alternative: Chromium-basiertes Rendering**
  - ❌ 🟢 CEF (Chromium Embedded Framework) evaluieren
  - ❌ 🟢 Electron-Alternative prüfen
- ❌ 🟡 **Anti-Burn-In-Schutz**
  - Pixel-Shifting Algorithmus
  - Screensaver nach Inaktivität

#### Systemintegration
- ✅ **systemd Service**
  - ✅ digitalsignage-client.service Unit-File erstellt
  - ✅ Auto-Restart bei Absturz (Restart=always)
  - ✅ Installation-Script (install.sh mit systemd Integration)
- ✅ **Watchdog**
  - ✅ WatchdogMonitor implementiert mit systemd Integration (watchdog_monitor.py)
  - ✅ Automatische Pings (halbes Watchdog-Intervall)
  - ✅ Status-Benachrichtigungen (ready, stopping, status)
  - ✅ Automatischer Neustart bei Freeze (60s timeout)
  - ✅ Service-File konfiguriert (Type=notify, WatchdogSec=60)
- ❌ 🟡 **Automatische Updates**
  - Update-Check-Mechanismus
  - Safe Rollback bei Fehlern
- ⚠️ **Konfigurations-Management**
  - ❌ 🔴 Web-Interface für lokale Konfiguration
  - ✅ config.py vorhanden

#### Datenempfang
- ✅ WebSocket-Verbindung funktioniert
- ❌ 🟡 **Fallback auf HTTP-Polling** bei WebSocket-Problemen
- ✅ **Lokale Datenpufferung**
  - ✅ SQLite-Cache für Layouts (CacheManager implementiert)
  - ✅ Offline-Modus mit automatischem Fallback
  - ✅ Cached Layout beim Startup wenn Server offline
  - ✅ Offline-Status in Heartbeat-Nachrichten
- ✅ **TLS/SSL-Verschlüsselung**
  - ✅ Server unterstützt HTTPS/WSS via ServerSettings
  - ✅ Client unterstützt WSS mit SSL-Verifikation
  - ✅ Konfigurierbare SSL-Einstellungen (appsettings.json / config.py)
  - ✅ Umfassende SSL Setup Dokumentation (SSL_SETUP.md)
  - ✅ Support für Self-Signed und CA-Zertifikate
  - ✅ Reverse Proxy Konfigurationsbeispiele (nginx, IIS)

### 2.2 Kommunikationsprotokoll

#### Nachrichtentypen
- ✅ REGISTER, HEARTBEAT, DISPLAY_UPDATE, STATUS_REPORT, COMMAND, SCREENSHOT
- ❌ 🟡 **LOG-Nachrichtentyp**
  - Log-Ereignisse an Server senden
  - Log-Level (DEBUG, INFO, WARNING, ERROR)

#### Fehlerbehandlung
- ✅ Automatische Wiederverbindung implementiert
- ✅ **Offline-Modus mit gecachten Daten**
  - ✅ Letzte bekannte Layouts anzeigen
  - ✅ Offline-Indikator (offline_mode Flag)
  - ✅ Automatischer Wechsel bei Disconnect
- ❌ 🟡 **Fehler-Queue**
  - Failed Messages aufbewahren
  - Retry bei Reconnect
- ❌ 🟡 **Degraded Mode**
  - Bei Teilausfällen (z.B. nur statische Elemente zeigen)

---

## TEIL 3: TECHNISCHE ARCHITEKTUR

### 3.1 Windows-Anwendung

- ✅ WPF mit .NET 8
- ✅ MVVM Pattern (CommunityToolkit.Mvvm)
- ✅ **Dependency Injection Container** konfiguriert
  - ✅ Microsoft.Extensions.DependencyInjection
  - ✅ App.xaml.cs mit IHost
  - ✅ Service-Registrierung (alle Services + Background Services)
- ✅ **Entity Framework Core** für Datenbank
  - ✅ DigitalSignageDbContext erstellt mit allen Entitäten
  - ✅ Fluent API Konfiguration (JSON columns, relationships, indexes)
  - ✅ Automatische Migrations bei Startup (DatabaseInitializationService)
  - ✅ Default Admin User Seeding
  - ✅ Connection String in appsettings.json konfigurierbar
  - ✅ Retry-Logik und Connection Pooling
  - ✅ Development vs Production Konfiguration
- ❌ 🟢 **SignalR statt WebSocket** evaluieren
  - Einfachere RPC-Semantik
- ✅ **Serilog** für strukturiertes Logging
  - ✅ File Sink mit Rolling Files (täglich, 30 Tage Retention)
  - ✅ Separate Error-Logs (90 Tage Retention)
  - ✅ Console und Debug Sinks
  - ✅ Log-Levels aus appsettings.json konfigurierbar
  - ✅ Enrichment (Machine Name, Thread ID, Source Context)
  - ✅ File Size Limits und Roll-over (100 MB)
- ⚠️ **Unit Tests** - Grundstruktur vorhanden
  - ❌ 🟡 Test-Coverage auf >70% erhöhen
  - ❌ 🟡 Integration Tests für Services
  - ❌ 🟡 UI-Tests mit TestStack.White

### 3.2 Raspberry Pi Client

- ✅ Python 3.9+
- ✅ PyQt5
- ✅ python-socketio
- ❌ 🟡 **Flask/FastAPI** für lokale API
  - Konfigurations-Endpunkte
  - Status-Endpunkte
  - Webinterface für lokale Verwaltung
- ❌ 🟡 **RPi.GPIO** für Hardware-Steuerung
  - LED-Status-Anzeige
  - Hardware-Button für Neustart
- ❌ 🔴 **supervisor** für Process Management
  - Alternative: systemd (bereits geplant)

### 3.4 Sicherheitsanforderungen

- ✅ **TLS 1.2+ Verschlüsselung**
  - ✅ Server-seitiges SSL-Zertifikat (konfigurierbar)
  - ✅ Client-seitige Zertifikat-Validierung
  - ✅ Reverse Proxy Support (empfohlen für Produktion)
- ✅ **Authentifizierung**
  - ✅ AuthenticationService implementiert
  - ✅ API-Key-System (Erstellung, Validierung, Revokation)
  - ✅ Client-Registrierung mit Token
  - ✅ ClientRegistrationToken Entity (mit Restriktionen, MaxUses, Expiration)
  - ✅ User/Password Authentication
  - ✅ ApiKey Entity mit Usage Tracking
  - ✅ Password Hashing (SHA256, produktionsreif: BCrypt/Argon2 empfohlen)
  - ✅ Token Generation mit Secure RNG
- ❌ 🟡 **Rollbasierte Zugriffskontrolle (RBAC)**
  - User-Roles: Admin, Operator, Viewer
  - Berechtigungsprüfung in APIs
- ⚠️ **Audit-Logging**
  - ✅ AuditLog Entity erstellt mit vollständigen Feldern
  - ✅ Who-When-What Schema (User, Timestamp, Action, EntityType, EntityId)
  - ✅ JSON Changes Field für Before/After Werte
  - ❌ Automatische Change Tracking Interceptors (SaveChanges Override)
  - ❌ UI für Audit-Log-Anzeige
- ✅ SQL-Injection-Schutz (Parametrisierung)
- ✅ Input-Validierung (kürzlich hinzugefügt)
- ❌ 🟡 **Rate-Limiting**
  - Schutz vor Brute-Force
  - API-Request-Limits

---

## TEIL 4: BENUTZEROBERFLÄCHE

### 4.1 Windows-App UI-Struktur

- ✅ **Hauptfenster** - Vollständig implementiert
  - ✅ Menüleiste mit allen Befehlen
  - ✅ Tabbed Interface (Designer, Geräte, Datenquellen, Vorschau)
  - ✅ Statusleiste mit Server-Status und Client-Count
  - ❌ 🟡 Werkzeugleiste mit Icons (optional)
- ✅ **Designer-Tab**
  - ✅ Canvas mit Zoom/Pan
  - ✅ Werkzeugleiste (60px Sidebar)
  - ✅ **Layers Panel (250px, Grid Column 1)** - NEU implementiert
    - ✅ Layer List mit Type Icons
    - ✅ Z-Index Anzeige
    - ✅ Move Up/Down Buttons
    - ✅ Visibility Toggle (👁/🚫 Icons)
    - ✅ Synchronisierte Selektion mit Canvas
  - ✅ Eigenschaften-Panel (300px rechts)
  - ✅ Grid-Anzeige mit Snap-to-Grid
  - ✅ Drag-and-Drop für Elemente
  - ✅ Resize-Handles mit ResizeAdorner
  - ✅ **Zoom Controls Toolbar** - NEU implementiert
    - ✅ Zoom In/Out Buttons
    - ✅ Zoom Slider (25%-400%)
    - ✅ Zoom Level Display
    - ✅ Zoom to Fit Button
- ✅ **Geräte-Tab**
  - ✅ DataGrid mit Geräteliste (Name, IP, MAC, Group, Location, Status, Last Seen)
  - ✅ **Discover Devices Button** - NEU implementiert (UDP-Broadcast)
  - ✅ Geräte-Detail-Panel (300px rechts)
  - ✅ Status-Indikatoren (Online/Offline mit Farben)
  - ✅ Remote Commands: Restart Device, Restart App, Screenshot
  - ✅ Screen Control: Screen On/Off
  - ✅ Volume Control mit Slider
  - ✅ Layout Assignment mit ComboBox
  - ✅ Maintenance: Clear Cache
  - ✅ **Client Configuration Remote Update** - NEU implementiert
    - ✅ Server Host/Port konfigurierbar
    - ✅ SSL/TLS Settings
    - ✅ Full Screen Mode Toggle
    - ✅ Log Level konfigurierbar
    - ✅ Update-Command an Client mit Bestätigung
  - ✅ Status-Nachrichtenleiste
  - ✅ DeviceManagementViewModel mit vollständiger Fehlerbehandlung und Logging
- ✅ **Datenquellen-Tab** - Vollständig implementiert
  - ✅ Liste der konfigurierten Datenquellen (DataGrid)
  - ✅ Datenquellen-Editor (Connection String, Query, Refresh Interval)
  - ✅ Verbindungstest mit Status-Indikator
  - ✅ Vorschau der Daten (DataGrid mit Results)
  - ✅ Query Builder Integration
  - ✅ Add/Edit/Delete Datenquellen
  - ✅ Database Persistence (EF Core)
  - ✅ DataSourceManagementViewModel mit vollständiger Fehlerbehandlung
- ✅ **Vorschau-Tab** - Vollständig implementiert
  - ✅ Layout-Rendering mit Template Engine
  - ✅ Testdaten-Simulator mit Data Source Auswahl
  - ✅ Auto-Refresh Toggle mit Status-Anzeige
  - ✅ Clear Preview Button
  - ✅ Preview Canvas mit Layout-Hintergrund
  - ✅ Variable Substitution Preview
  - ✅ PreviewViewModel mit vollständiger Fehlerbehandlung
  - ❌ 🟡 Vollbild-Button
- ✅ **Scheduling-Tab** - Vollständig implementiert (NEU)
  - ✅ Schedule List (300px Sidebar) mit Add/Refresh Buttons
  - ✅ Schedule Editor mit vollständigem Form
    - ✅ Name, Description Felder
    - ✅ Layout Auswahl (ComboBox)
    - ✅ Start Time / End Time (HH:mm Format)
    - ✅ Days of Week (Komma-separiert oder *)
    - ✅ Priority Feld
    - ✅ IsActive Toggle
    - ✅ Client/Group Targeting (optional)
  - ✅ Save/Delete/Test Buttons
  - ✅ Status Message Display
  - ✅ SchedulingViewModel mit vollständiger Fehlerbehandlung
- ✅ **Media Library Tab** - Vollständig implementiert (NEU)
  - ✅ Toolbar mit Upload/Refresh Buttons
  - ✅ Filter nach MediaType (All/Image/Video/Audio/Document)
  - ✅ Search TextBox mit Placeholder
  - ✅ Clear Filter Button
  - ✅ Media DataGrid mit Spalten:
    - ✅ Type Icon, File Name, Type, Size, Dimensions, Uploaded, Access Count
  - ✅ Details Panel (350px rechts)
    - ✅ Thumbnail Placeholder
    - ✅ File Information Display
    - ✅ Editable Fields: Description, Tags, Category
    - ✅ Update/Delete Buttons
  - ✅ Status Message Bar
  - ✅ MediaLibraryViewModel mit vollständiger Fehlerbehandlung
- ✅ **Logs Tab** - Vollständig implementiert (NEU)
  - ✅ Toolbar mit Filtern
    - ✅ Client Filter ComboBox
    - ✅ Log Level Checkboxes (Debug, Info, Warning, Error, Critical)
    - ✅ Auto-scroll Toggle
    - ✅ Refresh/Clear/Export Buttons
  - ✅ Logs DataGrid mit Spalten:
    - ✅ Time, Client, Level (farbcodiert), Message
    - ✅ Row Background basierend auf Level
    - ✅ Text Wrapping mit Tooltip
  - ✅ Status Bar mit Quick Actions
    - ✅ All/None/Errors Only Buttons
  - ✅ LogViewerViewModel mit vollständiger Fehlerbehandlung
- ✅ **Live Debug Logs Tab** - Vollständig implementiert (NEU)
  - ✅ Dark Theme Console-Style (VS Code ähnlich)
  - ✅ Real-time Log Streaming ListBox
  - ✅ Auto-scroll Toggle
  - ✅ Clear Logs Button
  - ✅ Consolas Font für bessere Lesbarkeit
  - ✅ Virtualization für Performance
  - ✅ Status Bar mit Log Count
  - ✅ LiveLogsViewModel mit vollständiger Fehlerbehandlung

### 4.2 Responsive Design

- ✅ **Touch-Unterstützung** für Tablets - Vollständig implementiert (NEU - 2025-11-11)
  - ✅ Touch Event Handlers (TouchDown, TouchMove, TouchUp)
  - ✅ Manipulation Support (IsManipulationEnabled)
  - ✅ Pinch-to-Zoom Gesture (ManipulationDelta)
  - ✅ Two-Finger Pan Gesture
  - ✅ Single Touch Selection (alternative zu Maus)
  - ✅ Custom Routed Events (ZoomChanged, PanChanged)
  - ✅ Touch-Gesten in DesignerCanvas integriert
  - ❌ 🟡 Größere Touch-Targets (UI-Anpassung noch ausstehend)
- ⚠️ **Dark/Light Theme**
  - ❌ 🟡 Theme-Switcher implementieren
  - ❌ 🟡 Theme-Ressourcen erstellen

---

## TEIL 5: DEPLOYMENT UND INSTALLATION

### 5.1 Windows-Installer

- ❌ 🔴 **MSI-Installer mit WiX Toolset**
  - Projekt-Setup
  - .NET Runtime Check
  - Installationsordner
  - Start-Menü-Einträge
- ❌ 🟡 **Datenbank-Setup-Dialog**
  - Connection String Eingabe
  - Verbindungstest
  - Schema-Erstellung
- ❌ 🟡 **Windows-Dienst-Option**
  - Server als Service laufen lassen
- ❌ 🟡 **Firewall-Regeln**
  - Port 8080 automatisch öffnen

### 5.2 Raspberry Pi Setup

- ✅ **Installations-Script (Bash)**
  - ✅ Abhängigkeiten installieren (apt-get)
  - ✅ Python-Packages (pip)
  - ✅ systemd Service einrichten
  - ✅ Auto-Start konfigurieren
  - ✅ Benutzer-Erkennung für sudo
  - ✅ Konfigurationsverzeichnisse erstellen
  - ✅ Screen blanking deaktivieren
  - ✅ Cursor ausblenden
- ❌ 🟡 **Konfiguration**
  - Web-Interface für Erstkonfiguration
  - Oder: Interactive Setup-Script
- ❌ 🟡 **Update-Mechanismus**
  - apt-Repository oder
  - Custom Updater via Server

---

## TEIL 6: ERWEITERUNGEN UND ZUKUNFT (Niedrige Priorität)

### Geplante Features


- ❌ 🟢 **Widget-System**
  - Wetter-Widget
  - RSS-Feed
  - Social Media Integration
- ❌ 🟢 **Analytics und Reporting**
  - View-Statistiken
  - Performance-Metriken
- ❌ 🟢 **A/B Testing** für Layouts


---

## QUALITÄT & TESTING

### Code-Qualität

- ✅ Logging in Services implementiert (kürzlich hinzugefügt)
- ✅ Error Handling verbessert
- ✅ Input Validation hinzugefügt
- ❌ 🟡 **Code-Coverage > 70%**
  - Mehr Unit Tests schreiben
  - Integration Tests
- ❌ 🟡 **Sicherheits-Audit** (OWASP Top 10)
- ❌ 🟡 **Performance-Tests**
  - Lasttests mit 50+ Clients
  - Memory-Leak-Detection

### Dokumentation
ganz zum schluss
- ✅ README.md vorhanden
- ✅ API-Dokumentation (Partial)
- ❌ 🟡 **Benutzerhandbuch** erstellen
- ❌ 🟡 **Technische Dokumentation**
  - Architektur-Diagramme
  - Deployment-Guide
  - API-Referenz (OpenAPI)
- ❌ 🟡 **Code-Kommentare** vervollständigen
  - XML-Dokumentation für alle Public APIs

### CI/CD


- ❌ 🟡 **Automatisierte Security-Scans**

---

## PRIORISIERTE ROADMAP

### Phase 1: MVP (Minimum Viable Product) - 🔴 Hohe Priorität

**Ziel:** Funktionstüchtige Basis mit Kernfeatures

1. **Designer-Grundfunktionen**
   - Drag-and-Drop Canvas
   - Element-Erstellung (Text, Bild, Shape)
   - Eigenschaften-Panel
   - Speichern/Laden

2. **Geräte-Verwaltung**
   - Geräte-Liste mit Status
   - Layout-Zuweisung
   - Remote-Befehle

3. **Client-Stabilität**
   - ✅ systemd Service
   - ✅ Offline-Cache
   - ✅ TLS-Verschlüsselung

4. **Daten-Integration**
   - ✅ SQL-Datenquellen funktional
   - ✅ Auto-Refresh (DataRefreshService)
   - ✅ Variable-Ersetzung im Server (Scriban Template Engine)

### Phase 2: Erweiterungen - 🟡 Mittlere Priorität

**Ziel:** Produktionsreife Features

1. **Erweiterte Designer-Features**
   - Ebenen-Management UI
   - Undo/Redo
   - Vorlagen-System

2. **Medien-Management**
   - Medienbibliothek
   - Upload-Funktionalität

3. **Monitoring & Logs**
   - Remote Log-Viewer
   - Alert-System
   - Performance-Metriken

4. **Zeitpläne**
   - Layout-Scheduling
   - Zeitbasierte Anzeigen

### Phase 3: Professional Features - 🟢 Niedrige Priorität

**Ziel:** Enterprise-Features und Komfort

1. **Automatisierung**
   - Auto-Discovery
   - QR-Pairing
   - Auto-Updates

2. **Erweiterte Widgets**
   - Wetter, RSS, Social Media

3. **REST API & Integration**
   - Swagger-Doku
   - Webhooks

4. **Deployment-Verbesserungen**
   - MSI-Installer
   - Web-Konfiguration für Client

---

## ZUSAMMENFASSUNG

### Implementierungsstand

- **Vollständig:** ~85%
  - Kommunikations-Infrastruktur
  - Grundlegende Datenmodelle
  - Service-Layer-Architektur
  - Python Client Display-Engine mit Status Screens
  - WebSocket Communication mit TLS/SSL
  - **Designer-Tab vollständig funktional** ✅
    - Drag-and-Drop Canvas mit Selection Rectangle
    - **Layers Panel (250px Sidebar)** ✅ (NEU)
      - Visual Layer List mit Type Icons
      - Z-Index Display und Move Up/Down
      - Visibility Toggle (👁/🚫)
      - Synchronized Selection
    - Properties Panel mit Echtzeit-Bearbeitung
    - **Erweiterte Properties Panel** ✅ (NEU - 2025-11-11)
      - Rotation Control (0-360° mit Slider)
      - Font Settings (Family, Size, Bold, Italic)
      - Color Picker mit Hex-Eingabe und Vorschau
      - Kontextsensitive Properties (Text/Rectangle)
    - Raster und Snap-to-Grid
    - Resize-Handles für Elemente
    - **Zoom Controls Toolbar** ✅ (NEU)
      - Zoom In/Out Buttons, Slider (25%-400%)
      - Zoom Level Display, Zoom to Fit
    - Element-Verwaltung (Add/Delete/Duplicate)
    - **Undo/Redo-System** ✅ (NEU - 2025-11-11)
      - Command Pattern vollständig implementiert
      - CommandHistory mit 50 Einträgen
      - Keyboard Shortcuts ready (Ctrl+Z, Ctrl+Y)
    - **Multi-Selection** ✅ (NEU - 2025-11-11)
      - Ctrl+Click, Shift+Click, Selection Rectangle
      - SelectionService mit Bulk Operations
      - Selection Bounds Calculation
  - **Geräte-Tab vollständig funktional** ✅
    - Device Management UI mit Control Panel
    - **Auto-Discovery Button** ✅ (NEU - UDP Broadcast)
    - Alle Remote Commands implementiert
    - **Remote Client Configuration** ✅ (NEU)
      - Server Settings, SSL/TLS, Log Level
    - Layout Assignment UI
    - Volume Control mit Slider
    - Status Monitoring
  - **Datenquellen-Tab vollständig funktional** ✅
    - Data Source Management UI mit Editor
    - Query Builder Integration
    - Connection Test und Data Preview
    - Static Data Support (JSON)
    - Database Persistence
  - **Scheduling-Tab vollständig funktional** ✅ (NEU)
    - Schedule Management UI
    - Time-based Layout Switching
    - Client/Group Targeting
    - Priority System
  - **Media Library Tab vollständig funktional** ✅ (NEU)
    - Upload/Filter/Search Funktionalität
    - Details Panel mit Metadata
    - SHA256 Deduplizierung
    - Access Tracking
  - **Vorschau-Tab vollständig funktional** ✅
    - Template Engine Integration
    - Test Data Source Selector
  - **Logs Tab vollständig funktional** ✅ (NEU)
    - Client Filter, Log Level Filter
    - Export Funktionalität
    - Color-coded Levels
  - **Live Debug Logs Tab vollständig funktional** ✅ (NEU)
    - Real-time Server Log Streaming
    - Console-Style Dark Theme
    - Auto-scroll
    - Live Preview mit Template Engine
    - Test Data Simulator
    - Auto-Refresh Funktionalität
  - **Zeitplan-System vollständig funktional** ✅
    - Layout Scheduling mit Editor
    - Automatische Zeitplan-Ausführung
    - Priority-basierte Auswahl
  - **Media Library vollständig funktional** ✅ (NEU - 2025-11-11)
    - MediaLibraryViewModel mit CRUD
    - Upload mit Multi-Select
    - Filter nach Typ und Suche
    - Details Panel mit Edit-Funktionen
    - FileSizeConverter für Größenanzeige
    - Vollständige Backend-Integration
  - **Zoom-Funktionalität vollständig implementiert** ✅
    - Zoom Slider und Mausrad-Support
    - Fit to Screen / Reset Zoom
  - **Touch Support** ✅ (NEU - 2025-11-11)
    - Pinch-to-Zoom und Pan Gestures
    - Touch Event Handlers
    - Manipulation Support
  - **Connection Pooling & Query Caching** ✅ (NEU - 2025-11-11)
    - QueryCacheService mit SHA256 Keys
    - Automatische Pooling-Konfiguration
    - Cache Statistics und Invalidierung
  - **Alert System** ✅ (NEU - 2025-11-11)
    - AlertService mit Rules Engine
    - AlertMonitoringService Background Service
    - 6 Alert Rule Types implementiert
  - Dependency Injection Setup
  - systemd Service + Watchdog
  - TLS/SSL-Verschlüsselung
  - Client-Offline-Cache
  - Auto-Discovery (UDP Broadcast)

- **Teilweise:** ~5%
  - Element-Gruppierung (Commands vorhanden, UI fehlt)

- **Nicht implementiert:** ~10%
  - Deployment-Tools (MSI-Installer, Windows Dienst)
  - Smart Guides (Ausrichtungshilfslinien im Designer)
  - Thumbnail-Generierung für Media Library
  - Alert Management UI (Backend vorhanden, UI fehlt)
  - Visuelle Daten-Mapping UI (SQL → UI-Elemente)
  - Element-Gruppierung UI
  - Theme Switcher (Dark/Light)
  - REST API mit Swagger
  - Widget-System (Wetter, RSS)
  - Audit-Log UI (Entity vorhanden, UI fehlt)
  - Erweiterte Dokumentation (Benutzerhandbuch)

### Nächste Schritte (Quick Wins)

1. ✅ **Designer-Canvas** funktional machen (ABGESCHLOSSEN)
2. ✅ **Dependency Injection** im Server einrichten (ABGESCHLOSSEN)
3. ✅ **systemd Service** für Raspberry Pi Client (ABGESCHLOSSEN)
4. ✅ **TLS-Verschlüsselung** aktivieren (ABGESCHLOSSEN)
5. ✅ **Client-Offline-Cache** implementieren (ABGESCHLOSSEN)
6. ✅ **Medien-Browser UI** - UI für zentrale Medienbibliothek (ABGESCHLOSSEN - 2025-11-11)
7. ✅ **Undo/Redo-System** - Command Pattern für Designer-Operationen (ABGESCHLOSSEN - 2025-11-11)
8. ✅ **Ebenen-Palette** - Layer Panel mit Visibility Toggle (ABGESCHLOSSEN - 2025-11-11)
9. ✅ **Erweiterte Properties Panel** - Rotation, Font Settings, Color Picker (ABGESCHLOSSEN - 2025-11-11)
10. ✅ **Connection Pooling & Query Caching** - SQL Performance-Optimierung (ABGESCHLOSSEN - 2025-11-11)
11. ✅ **Alert System** - Rules Engine mit Background Monitoring (ABGESCHLOSSEN - 2025-11-11)
12. ✅ **Multi-Selection im Designer** - Ctrl+Click, Shift+Click, Selection Rectangle (ABGESCHLOSSEN - 2025-11-11)
13. ✅ **Touch Support** - Pinch-to-Zoom, Pan Gestures für Tablets (ABGESCHLOSSEN - 2025-11-11)

**Neue Prioritäten (Stand: 2025-11-12):**

### 🔴 Hohe Priorität - Produktionsreife Features
1. **Alert Management UI Tab** - 🆕 FEHLT NOCH
   - UI für Alert Rules (Create/Edit/Delete)
   - Active Alerts Dashboard mit Real-time Updates
   - Alert History mit Filter/Search
   - Backend (AlertService, AlertMonitoringService) ✅ vorhanden
   - ViewModel und MainWindow.xaml Tab fehlen

2. **Visuelle Daten-Mapping UI** - 🆕 FEHLT NOCH
   - Drag-and-Drop Mapping SQL-Spalten → UI-Elemente
   - Visual Connection Builder (wie Power BI)
   - Template Variable Browser
   - Auto-Mapping Suggestions

3. **Element-Gruppierung UI** - 🆕 FEHLT NOCH
   - Gruppe erstellen/auflösen Commands
   - Gruppe als Einheit transformieren
   - Group-Hierarchie im Layer Panel
   - Verschachtelte Gruppierung

4. **MSI Installer** - 🆕 FEHLT NOCH
   - WiX Toolset Setup Project
   - .NET Runtime Check und Installation
   - Installationsordner Konfiguration
   - Start-Menü-Einträge und Desktop-Shortcut
   - Datenbank-Setup-Dialog (Connection String)

### 🟡 Mittlere Priorität - UX Verbesserungen
5. **Smart Guides (Ausrichtungshilfslinien)** - 🆕 FEHLT NOCH
   - Automatische Hilfslinien beim Verschieben
   - Snap-to-Guide Funktionalität
   - Abstand-Anzeigen zwischen Elementen
   - Zentrale Ausrichtungshilfen

6. **Thumbnail-Generierung für Media Library** - 🆕 FEHLT NOCH
   - Automatische Thumbnail-Erstellung bei Upload
   - Image Resizing mit System.Drawing
   - Video First-Frame Extraction
   - PDF First-Page Preview
   - Thumbnail Cache Management

7. **Theme Switcher (Dark/Light Mode)** - 🆕 FEHLT NOCH
   - Theme ResourceDictionary erstellen
   - Theme-Selector UI (ComboBox oder Toggle)
   - Theme Persistence in User Settings
   - Dynamisches Theme-Switching zur Laufzeit

8. **Audit-Log UI Tab** - 🆕 FEHLT NOCH
   - Audit-Log Viewer mit DataGrid
   - Filter nach User, Action, Entity Type
   - Diff-Viewer für Changes (JSON Before/After)
   - Export als CSV/Excel
   - Backend (AuditLog Entity) ✅ vorhanden

### 🟢 Niedrige Priorität - Nice-to-Have
9. **REST API mit Swagger** - 🆕 FEHLT NOCH
   - ASP.NET Core Web API Controller
   - Swagger/OpenAPI Dokumentation
   - JWT Authentication
   - Rate Limiting Middleware
   - API Versioning

10. **Widget-System** - 🆕 FEHLT NOCH
    - Wetter-Widget (OpenWeatherMap API)
    - RSS-Feed-Widget
    - Social Media Widgets (Twitter, Instagram)
    - Pluggable Widget Architecture
    - Widget Store/Browser

11. **Erweiterte Dokumentation** - 🆕 FEHLT NOCH
    - Benutzerhandbuch (PDF/Online)
    - Video-Tutorials
    - Deployment Guide erweitern
    - Troubleshooting Guide
    - API Documentation (wenn REST API implementiert)
