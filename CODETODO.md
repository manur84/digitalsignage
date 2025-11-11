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
- ❌ 🔴 **Unterschiedliche Anzeigetypen** (Raumbelegung, Informationstafeln, Wegweiser)
  - `DisplayType` Enum zu Models hinzufügen
  - Layout-Templates für verschiedene Typen erstellen
  - Template-Auswahl-Dialog implementieren
- ❌ 🟡 **Layout-Kategorien und Tags** für bessere Organisation
  - Kategorisierung in `DisplayLayout` Model
  - Filter- und Suchfunktion in UI

#### Visueller Designer
- ⚠️ **Designer-Canvas** - Grundstruktur vorhanden, aber nicht funktional
  - ❌ 🔴 Drag-and-Drop Funktionalität implementieren
  - ❌ 🔴 Werkzeugleiste mit Element-Buttons erstellen
  - ❌ 🔴 Selektions- und Transformationshandles
  - ❌ 🔴 Multi-Selektion mit Ctrl/Shift
- ⚠️ **Ebenenmanagement** - Z-Index vorhanden, aber keine UI
  - ❌ 🔴 Ebenenpalette mit Drag-Reorder
  - ❌ 🟡 Ebenen-Sichtbarkeit Toggle
  - ❌ 🟡 Ebenen-Gruppierung
- ⚠️ **Raster und Ausrichtung** - Properties in ViewModel, aber nicht implementiert
  - ❌ 🔴 Rasteranzeige im Canvas
  - ❌ 🔴 Snap-to-Grid beim Verschieben
  - ❌ 🟡 Ausrichtungshilfslinien (Smart Guides)
  - ❌ 🟡 Objekt-Ausrichtungs-Funktionen (links, rechts, zentriert)
- ❌ 🔴 **Eigenschaften-Panel** - Kontextsensitives Panel für ausgewählte Elemente
  - Position, Größe, Rotation Eingabefelder
  - Schrift-Einstellungen für Text
  - Farb-Picker
  - Datenquellen-Bindung UI
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
- ❌ 🔴 **Query-Builder mit visueller Unterstützung**
  - Tabellen-Browser
  - Spalten-Auswahl per Checkbox
  - WHERE-Klausel Builder
  - JOIN-Unterstützung
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
- ❌ 🔴 **Vordefinierte Auflösungs-Templates**
  - 1920x1080, 1280x720, 3840x2160, Portrait-Modi
  - Template-Auswahl beim Erstellen
- ❌ 🟡 **Responsive Design-Optionen**
  - Prozentuale Positionierung neben Pixel
  - Anchor-Points für Elemente
- ⚠️ **Zoom-Funktionalität** - Befehle in ViewModel, nicht implementiert
  - ❌ 🔴 Zoom-Slider in UI
  - ❌ 🔴 Zoom mit Mausrad
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
- ❌ 🔴 **Zentrale Medienbibliothek**
  - Medien-Datenbank (Dateien + Metadaten)
  - Upload-Funktion
  - Thumbnail-Generierung
  - Medienbrowser-UI
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
- ⚠️ ViewModel hat Vorschau-Befehle, aber nicht implementiert
  - ❌ 🔴 Live-Vorschau mit Testdaten
  - ❌ 🟡 Daten-Simulator mit wechselnden Werten
  - ❌ 🟡 Vollbild-Vorschau
  - ❌ 🟢 Multi-Monitor-Vorschau
  - ❌ 🟢 Export als Bild (PNG/PDF)

### 1.3 Raspberry Pi Geräteverwaltung

#### Geräteregistrierung
- ✅ RegisterClientAsync implementiert
- ❌ 🔴 **Automatische Netzwerkerkennung**
  - UDP-Broadcast auf Port 5555
  - Discovery-Service im Server
  - Geräte-Discovery-UI
- ❌ 🟡 **QR-Code-Pairing**
  - QR-Code generieren mit Verbindungsdaten
  - Client scannt QR-Code für Auto-Konfiguration
- ❌ 🟡 **Gerätegruppierung**
  - Gruppen nach Standort/Funktion
  - Bulk-Operationen auf Gruppen

#### Geräteinformationen
- ✅ DeviceInfo mit umfangreichen Daten
- ✅ Python DeviceManager sammelt System-Infos
- ✅ Alle geforderten Felder vorhanden
- ❌ 🟡 **Geräte-Detail-Ansicht** in UI
  - Alle Infos übersichtlich anzeigen
  - Grafische Darstellung (CPU, Memory Charts)
  - Ping-Test Button

#### Verwaltungsfunktionen
- ✅ SendCommandAsync grundlegend implementiert
- ✅ Python Client unterstützt RESTART, SCREENSHOT, SCREEN_ON/OFF, SET_VOLUME
- ❌ 🔴 **Zeitpläne für Layouts**
  - Schedule-Tabelle in Datenbank
  - Zeitplan-Editor UI
  - Cron-Expression Support
  - Client-seitige Zeitplan-Ausführung
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
- ❌ 🟡 **Entity Framework Core** für Datenbank
  - DbContext erstellen
  - Migrations
  - Repository Pattern
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
- ❌ 🔴 **Authentifizierung**
  - API-Key-System
  - Client-Registrierung mit Token
- ❌ 🟡 **Rollbasierte Zugriffskontrolle (RBAC)**
  - User-Roles: Admin, Operator, Viewer
  - Berechtigungsprüfung in APIs
- ❌ 🟡 **Audit-Logging**
  - Alle Änderungen protokollieren
  - Who-When-What
- ✅ SQL-Injection-Schutz (Parametrisierung)
- ✅ Input-Validierung (kürzlich hinzugefügt)
- ❌ 🟡 **Rate-Limiting**
  - Schutz vor Brute-Force
  - API-Request-Limits

---

## TEIL 4: BENUTZEROBERFLÄCHE

### 4.1 Windows-App UI-Struktur

- ⚠️ **Hauptfenster** - Grundstruktur in MainWindow.xaml
  - ✅ Menüleiste teilweise vorhanden
  - ❌ 🔴 Vollständige Menüleiste implementieren
  - ❌ 🔴 Werkzeugleiste mit Icons
  - ❌ 🔴 Tabbed Interface (Designer, Geräte, Datenquellen, Vorschau)
  - ❌ 🟡 Statusleiste mit Infos
- ❌ 🔴 **Designer-Tab**
  - Canvas mit Zoom/Pan
  - Werkzeugpalette
  - Eigenschafts-Panel
  - Ebenen-Panel
- ❌ 🔴 **Geräte-Tab**
  - DataGrid mit Geräteliste
  - Geräte-Detail-Ansicht
  - Status-Indikatoren (Online/Offline)
  - Befehls-Buttons
- ❌ 🔴 **Datenquellen-Tab**
  - Liste der konfigurierten Datenquellen
  - Datenquellen-Editor
  - Verbindungstest
  - Vorschau der Daten
- ❌ 🟡 **Vorschau-Tab**
  - Layout-Rendering
  - Testdaten-Auswahl
  - Vollbild-Button

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

- **Vollständig:** ~25%
  - Kommunikations-Infrastruktur
  - Grundlegende Datenmodelle
  - Service-Layer-Architektur
  - Python Client Display-Engine

- **Teilweise:** ~15%
  - UI-Grundgerüst
  - Datenbank-Integration
  - Geräte-Management

- **Nicht implementiert:** ~60%
  - Visueller Designer (UI)
  - Medien-Management
  - Erweiterte Features (Zeitpläne, Auto-Discovery)
  - Deployment-Tools
  - Sicherheits-Features
  - Dokumentation

### Nächste Schritte (Quick Wins)

1. **Designer-Canvas** funktional machen (höchste Priorität)
2. **Dependency Injection** im Server einrichten
3. **systemd Service** für Raspberry Pi Client
4. **TLS-Verschlüsselung** aktivieren
5. **Client-Offline-Cache** implementieren
