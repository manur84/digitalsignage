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
  - ❌ 🟡 Multi-Selektion mit Ctrl/Shift
- ⚠️ **Ebenenmanagement** - Z-Index vorhanden mit grundlegender UI
  - ✅ Z-Index Move Up/Down Commands
  - ✅ Z-Index Eingabefeld in Properties Panel
  - ❌ 🟡 Ebenenpalette mit Drag-Reorder
  - ❌ 🟡 Ebenen-Sichtbarkeit Toggle
  - ❌ 🟡 Ebenen-Gruppierung
- ✅ **Raster und Ausrichtung** - Implementiert
  - ✅ Rasteranzeige im DesignerCanvas
  - ✅ Snap-to-Grid beim Verschieben
  - ✅ Konfigurierbare Grid-Größe
  - ✅ Grid Show/Hide Toggle
  - ❌ 🟡 Ausrichtungshilfslinien (Smart Guides)
  - ❌ 🟡 Objekt-Ausrichtungs-Funktionen (links, rechts, zentriert)
- ✅ **Eigenschaften-Panel** - Kontextsensitives Panel implementiert
  - ✅ Position (X, Y) Eingabefelder
  - ✅ Größe (Width, Height) Eingabefelder
  - ✅ Z-Index mit Up/Down Buttons
  - ✅ Element-Name Eingabe
  - ✅ Layout Properties (Name, Resolution, Background)
  - ✅ Duplicate und Delete Buttons
  - ✅ Dynamische Anzeige basierend auf Selektion
  - ❌ 🟡 Rotation Eingabefeld
  - ❌ 🟡 Schrift-Einstellungen für Text
  - ❌ 🟡 Farb-Picker
  - ❌ 🟡 Datenquellen-Bindung UI
- ❌ 🟡 **Undo/Redo-System** - Befehle in ViewModel vorhanden, nicht implementiert
  - Command Pattern für alle Operationen
  - Undo-Stack Management
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
- ❌ 🟡 **Connection Pooling** konfigurieren
- ❌ 🟡 **Query-Caching** implementieren

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
- ✅ **Zentrale Medienbibliothek**
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
  - ❌ Thumbnail-Generierung (UI-Feature)
  - ❌ Medienbrowser-UI
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
- ❌ 🔴 **Automatische Netzwerkerkennung**
  - UDP-Broadcast auf Port 5555
  - Discovery-Service im Server
  - Geräte-Discovery-UI
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
- ❌ 🟡 **Remote Log-Viewer**
  - LOG Nachrichtentyp implementieren
  - Log-Level Filter
  - Echtzeit-Log-Streaming
- ❌ 🟡 **Fehlerbenachrichtigungen**
  - Alert-System im Server
  - E-Mail/Push-Benachrichtigungen
  - Alert-Rules konfigurieren

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
  - ✅ Eigenschaften-Panel (300px rechts)
  - ✅ Grid-Anzeige mit Snap-to-Grid
  - ✅ Drag-and-Drop für Elemente
  - ✅ Resize-Handles mit ResizeAdorner
  - ❌ 🟡 Ebenen-Panel (separates Panel)
- ✅ **Geräte-Tab**
  - ✅ DataGrid mit Geräteliste (Name, IP, MAC, Group, Location, Status, Last Seen)
  - ✅ Geräte-Detail-Panel (300px rechts)
  - ✅ Status-Indikatoren (Online/Offline mit Farben)
  - ✅ Remote Commands: Restart Device, Restart App, Screenshot
  - ✅ Screen Control: Screen On/Off
  - ✅ Volume Control mit Slider
  - ✅ Layout Assignment mit ComboBox
  - ✅ Maintenance: Clear Cache
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
  - ✅ Testdaten-Simulator (JSON Editor)
  - ✅ Auto-Refresh Toggle (alle 5 Sekunden)
  - ✅ Zoom-Funktionen (Fit, Reset)
  - ❌ 🟡 Vollbild-Button

### 4.2 Responsive Design

- ❌ 🟡 **Touch-Unterstützung** für Tablets
  - Touch-Gesten für Zoom/Pan
  - Größere Touch-Targets
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

- ❌ 🟢 **Multi-Tenancy Support**
- ❌ 🟢 **Cloud-Synchronisation**
- ❌ 🟢 **Mobile App** (iOS/Android)
- ❌ 🟡 **REST API** für Drittanbieter
  - OpenAPI/Swagger Dokumentation
- ❌ 🟢 **Widget-System**
  - Wetter-Widget
  - RSS-Feed
  - Social Media Integration
- ❌ 🟢 **Analytics und Reporting**
  - View-Statistiken
  - Performance-Metriken
- ❌ 🟢 **A/B Testing** für Layouts
- ❌ 🟢 **Touch-Interaktivität** auf Clients

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

- ❌ 🟡 **GitHub Actions Pipeline**
  - Build + Test bei Push
  - Automatische Releases
- ❌ 🟡 **Code-Coverage-Reports**
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

- **Vollständig:** ~60%
  - Kommunikations-Infrastruktur
  - Grundlegende Datenmodelle
  - Service-Layer-Architektur
  - Python Client Display-Engine
  - **Designer-Tab vollständig funktional** ✅
    - Drag-and-Drop Canvas
    - Properties Panel mit Echtzeit-Bearbeitung
    - Raster und Snap-to-Grid
    - Resize-Handles für Elemente
    - Zoom-Funktionen
    - Element-Verwaltung (Add/Delete/Duplicate)
  - **Geräte-Tab vollständig funktional** ✅
    - Device Management UI mit Control Panel
    - Alle Remote Commands implementiert
    - Layout Assignment UI
    - Volume Control mit Slider
    - Status Monitoring
  - **Datenquellen-Tab vollständig funktional** ✅ (NEU)
    - Data Source Management UI mit Editor
    - Query Builder Integration
    - Connection Test und Data Preview
    - Database Persistence
  - **Vorschau-Tab vollständig funktional** ✅ (NEU)
    - Live Preview mit Template Engine
    - Test Data Simulator
    - Auto-Refresh Funktionalität
  - **Zeitplan-System vollständig funktional** ✅ (NEU)
    - Layout Scheduling mit Editor
    - Automatische Zeitplan-Ausführung
    - Priority-basierte Auswahl
  - **Zoom-Funktionalität vollständig implementiert** ✅ (NEU)
    - Zoom Slider und Mausrad-Support
    - Fit to Screen / Reset Zoom
  - Dependency Injection Setup
  - systemd Service + Watchdog
  - TLS/SSL-Verschlüsselung
  - Client-Offline-Cache

- **Teilweise:** ~10%
  - Ebenen-Management (Grundfunktionen, keine Palette)
  - Medien-Management (Backend vorhanden, UI fehlt)

- **Nicht implementiert:** ~30%
  - Erweiterte Designer-Features (Undo/Redo, Gruppierung)
  - Medien-Management UI
  - Auto-Discovery
  - Deployment-Tools (MSI-Installer)
  - Erweiterte Dokumentation

### Nächste Schritte (Quick Wins)

1. ✅ **Designer-Canvas** funktional machen (ABGESCHLOSSEN)
2. ✅ **Dependency Injection** im Server einrichten (ABGESCHLOSSEN)
3. ✅ **systemd Service** für Raspberry Pi Client (ABGESCHLOSSEN)
4. ✅ **TLS-Verschlüsselung** aktivieren (ABGESCHLOSSEN)
5. ✅ **Client-Offline-Cache** implementieren (ABGESCHLOSSEN)

**Neue Prioritäten:**
1. **Auto-Discovery (UDP Broadcast)** - Automatische Netzwerkerkennung für Clients
2. **Medien-Browser UI** - UI für zentrale Medienbibliothek (Backend bereits vorhanden)
3. **Undo/Redo-System** - Command Pattern für Designer-Operationen
4. **Ebenen-Palette** - Drag-Reorder, Sichtbarkeits-Toggle, Gruppierung
5. **Visuelle Daten-Mapping UI** - SQL-Spalten zu UI-Elementen zuordnen
6. **Remote Log-Viewer** - Echtzeit-Log-Streaming von Clients
