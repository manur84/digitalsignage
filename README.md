# Digital Signage Management System

Ein umfassendes Digital Signage System bestehend aus einer Windows-Server-Anwendung und Raspberry Pi Clients zur Verwaltung und Anzeige digitaler Beschilderungen.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Python](https://img.shields.io/badge/Python-3.9+-blue.svg)

## 📋 Übersicht

Das Digital Signage Management System ermöglicht die zentrale Verwaltung und Steuerung von digitalen Anzeigen über ein Netzwerk. Es besteht aus:

- **Windows Server/Manager**: Desktop-Anwendung zur Verwaltung von Layouts, Datenquellen und Clients
- **Raspberry Pi Client**: Lightweight Client-Software zur Anzeige der Inhalte

### Hauptfunktionen

✨ **Visueller Designer**
- Drag-and-Drop Interface für Layout-Erstellung
- Textfelder, Bilder, Formen, QR-Codes, Tabellen
- Echtzeit-Vorschau

📊 **SQL-Datenbankanbindung**
- Dynamische Inhalte aus Microsoft SQL Server
- Parametrisierte Abfragen
- Automatische Datenaktualisierung

🖥️ **Geräteverwaltung**
- Zentrale Verwaltung aller Raspberry Pi Clients
- Echtzeit-Status-Monitoring
- Fernsteuerung (Neustart, Screenshots, etc.)

🔄 **Echtzeit-Kommunikation**
- WebSocket-basierte Client-Server-Kommunikation
- Automatische Wiederverbindung
- Offline-Modus mit lokalem Cache

## 🏗️ Architektur

```
┌─────────────────────────────────────────┐
│   Windows Server/Manager (WPF)          │
│   ┌─────────────────────────────────┐   │
│   │  Visual Designer                │   │
│   │  Device Management              │   │
│   │  Data Source Manager            │   │
│   └─────────────────────────────────┘   │
└──────────────┬──────────────────────────┘
               │ WebSocket (Port 8080)
               │
      ┌────────┴────────┐
      │                 │
┌─────▼─────┐    ┌─────▼─────┐
│ Pi Client │    │ Pi Client │
│  Display  │    │  Display  │
└───────────┘    └───────────┘
```

## 🚀 Installation

### Windows Server

#### Voraussetzungen
- Windows 10/11 oder Windows Server 2019+
- .NET 8.0 Runtime
- Microsoft SQL Server (optional)
- Mindestens 4 GB RAM
- 500 MB freier Festplattenspeicher

#### Installation
1. Download der neuesten Release von GitHub
2. MSI-Installer ausführen
3. Konfiguration der Datenbankverbindung
4. Server starten

```bash
# Oder via Kommandozeile (wenn .NET SDK installiert)
git clone https://github.com/yourusername/digitalsignage.git
cd digitalsignage
dotnet restore
dotnet build
dotnet run --project src/DigitalSignage.Server/DigitalSignage.Server.csproj
```

### Raspberry Pi Client

#### Voraussetzungen
- Raspberry Pi 3 oder neuer
- Raspberry Pi OS (Bullseye oder neuer)
- Python 3.9+ (Python 3.11+ wird mit virtueller Umgebung unterstützt)
- Netzwerkverbindung zum Server
- Display (HDMI)

#### Installation

```bash
# Repository klonen
git clone https://github.com/yourusername/digitalsignage.git
cd digitalsignage/src/DigitalSignage.Client.RaspberryPi

# Installationsscript ausführen (erstellt automatisch virtuelle Python-Umgebung)
chmod +x install.sh
sudo ./install.sh

# Konfiguration anpassen
sudo nano /opt/digitalsignage-client/config.py
```

Beispiel-Konfiguration:
```python
# Server connection settings
SERVER_HOST = "192.168.1.100"
SERVER_PORT = 8080

# Display settings
FULLSCREEN = True
LOG_LEVEL = "INFO"
```

Service starten:
```bash
sudo systemctl start digitalsignage-client
sudo systemctl enable digitalsignage-client
```

**Wichtig:** Die Installation erstellt automatisch eine virtuelle Python-Umgebung unter `/opt/digitalsignage-client/venv` mit `--system-site-packages` Flag. Dies ist erforderlich für Python 3.11+ (Debian Bookworm/Raspberry Pi OS 12+), um die "externally-managed-environment" Beschränkung zu umgehen und gleichzeitig Zugriff auf system-installierte Pakete zu ermöglichen:
- PyQt5 (python3-pyqt5, python3-pyqt5.qtsvg, python3-pyqt5.qtmultimedia)
- psutil (python3-psutil)

Die Installation überprüft automatisch, ob PyQt5 korrekt installiert wurde und aus der virtuellen Umgebung zugänglich ist. Alle anderen Abhängigkeiten werden isoliert in dieser Umgebung installiert.

## 📖 Verwendung

### 1. Layout erstellen

1. Server-Anwendung öffnen
2. "File" → "New Layout" wählen
3. Elemente aus der Werkzeugleiste per Drag & Drop platzieren
4. Eigenschaften im rechten Panel anpassen
5. Layout speichern

### 2. Datenquelle konfigurieren

1. "Data Sources" Tab öffnen
2. "Add Data Source" klicken
3. SQL-Verbindung konfigurieren:
   ```sql
   SELECT room_name, status, temperature
   FROM rooms
   WHERE room_id = @roomId
   ```
4. "Test Connection" ausführen
5. Speichern

### 3. Client registrieren

1. Raspberry Pi Client starten
2. Client erscheint automatisch in der "Devices" Ansicht
3. Layout zuweisen
4. Client zeigt Layout an

### 4. Variablen verwenden

In Textelementen können Variablen verwendet werden:

```
Raum: {{room.name}}
Status: {{room.status}}
Temperatur: {{room.temperature}}°C
```

## 🎨 Layout-Elemente

### Textfeld
- Statischer oder dynamischer Text
- Schriftart, -größe, -farbe anpassbar
- Text-Ausrichtung (links, zentriert, rechts)
- Variablen-Unterstützung

### Bild
- JPG, PNG, GIF, SVG unterstützt
- Skalierungsmodi: contain, cover, fill
- Dynamische Bildquellen möglich

### Formen
- Rechtecke, Kreise, Linien
- Füll- und Rahmenfarbe
- Transparenz

### QR-Code
- Dynamische QR-Code-Generierung
- Fehlerkorrektur-Level einstellbar
- Farben anpassbar

### Tabelle
- Dynamische Daten aus SQL
- Spalten konfigurierbar
- Zebrastreifen-Design

### Datum/Zeit
- Live-Anzeige
- Formatierung anpassbar
- Zeitzone-Unterstützung

## 🔧 Konfiguration

### Server-Konfiguration

Die Konfiguration wird in `%APPDATA%\DigitalSignage\config.json` gespeichert:

```json
{
  "server": {
    "port": 8080,
    "host": "0.0.0.0"
  },
  "database": {
    "connectionString": "Server=localhost;Database=DigitalSignage;..."
  },
  "logging": {
    "level": "Information",
    "file": "logs/server.log"
  }
}
```

### Firewall-Regeln

Für den Server muss Port 8080 geöffnet werden:

```powershell
# Windows Firewall
New-NetFirewallRule -DisplayName "Digital Signage Server" `
  -Direction Inbound -LocalPort 8080 -Protocol TCP -Action Allow
```

## 🔌 API-Referenz

### WebSocket-Nachrichten

#### Client Registration
```json
{
  "Type": "REGISTER",
  "ClientId": "uuid",
  "MacAddress": "aa:bb:cc:dd:ee:ff",
  "IpAddress": "192.168.1.100",
  "DeviceInfo": { ... }
}
```

#### Display Update
```json
{
  "Type": "DISPLAY_UPDATE",
  "Layout": { ... },
  "Data": { ... },
  "ForceRefresh": false
}
```

#### Commands
```json
{
  "Type": "COMMAND",
  "Command": "RESTART|SCREENSHOT|SCREEN_ON|SCREEN_OFF",
  "Parameters": { ... }
}
```

## 🧪 Entwicklung

### Projekt-Struktur

```
digitalsignage/
├── src/
│   ├── DigitalSignage.Server/        # WPF-Anwendung
│   │   ├── Views/                    # XAML-Views
│   │   ├── ViewModels/               # ViewModels (MVVM)
│   │   └── Services/                 # Business-Logik
│   ├── DigitalSignage.Core/          # Shared Models
│   ├── DigitalSignage.Data/          # Daten-Layer
│   └── DigitalSignage.Client.RaspberryPi/  # Python Client
├── docs/                             # Dokumentation
└── tests/                            # Unit-Tests
```

### Entwicklungsumgebung

**Windows Server:**
- Visual Studio 2022 oder JetBrains Rider
- .NET 8.0 SDK
- SQL Server (optional, für Entwicklung)

**Raspberry Pi Client:**
- Python 3.9+
- PyCharm oder VS Code
- Virtual Environment empfohlen

### Tests ausführen

```bash
# .NET Tests
dotnet test

# Python Tests
cd src/DigitalSignage.Client.RaspberryPi
pytest
```

### Build erstellen

```bash
# Windows Server
dotnet publish -c Release -r win-x64 --self-contained

# Python Client Package
cd src/DigitalSignage.Client.RaspberryPi
python setup.py sdist bdist_wheel
```

## 📊 Performance

- **Server**: Unterstützt 100+ gleichzeitige Clients
- **Latenz**: < 100ms für Updates
- **CPU-Last Client**: < 20% im Idle
- **Speicher Client**: < 200MB RAM
- **Startzeit Client**: < 30 Sekunden

## 🔒 Sicherheit

- TLS 1.2+ verschlüsselte Kommunikation
- API-Key-basierte Authentifizierung
- SQL-Injection-Schutz durch parametrisierte Queries
- Input-Validierung auf Client und Server
- Audit-Logging aller Änderungen

## 🐛 Troubleshooting

### Client verbindet nicht zum Server

1. Firewall-Einstellungen prüfen
2. Server-IP in Client-Konfiguration korrekt?
3. Server läuft und ist erreichbar?

```bash
# Verbindung testen
telnet <server-ip> 8080
```

### Layout wird nicht aktualisiert

1. Client-Status in Server-UI prüfen
2. Logs ansehen:
   ```bash
   sudo journalctl -u digitalsignage-client -f
   ```
3. Layout neu zuweisen

### SQL-Verbindung schlägt fehl

1. Connection String prüfen
2. SQL Server erreichbar?
3. Firewall-Regeln für SQL Server
4. Authentifizierung korrekt?

## 🗺️ Roadmap

### Version 2.0 (geplant)
- [ ] Multi-Tenancy Support
- [ ] Cloud-Synchronisation
- [ ] Mobile App für Verwaltung
- [ ] REST API für Drittanbieter
- [ ] Video-Unterstützung
- [ ] Touch-Interaktivität
- [ ] Wetter-Widget
- [ ] Social Media Integration
- [ ] Analytics Dashboard
- [ ] A/B Testing für Layouts

## 🤝 Beitragen

Beiträge sind willkommen! Bitte lesen Sie [CONTRIBUTING.md](CONTRIBUTING.md) für Details.

1. Fork des Projekts erstellen
2. Feature-Branch erstellen (`git checkout -b feature/AmazingFeature`)
3. Änderungen committen (`git commit -m 'Add some AmazingFeature'`)
4. Branch pushen (`git push origin feature/AmazingFeature`)
5. Pull Request erstellen

## 📄 Lizenz

Dieses Projekt ist unter der MIT-Lizenz lizenziert - siehe [LICENSE](LICENSE) Datei für Details.

## 👥 Autoren

- **Ihr Name** - *Initial work*

## 🙏 Danksagungen

- Community Toolkit MVVM
- SignalR Team
- Raspberry Pi Foundation
- PyQt5 Community

## 📞 Support

Bei Fragen oder Problemen:
- GitHub Issues: https://github.com/yourusername/digitalsignage/issues
- Email: support@example.com
- Dokumentation: https://docs.example.com

---

Made with ❤️ for Digital Signage
