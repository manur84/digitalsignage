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

#### Display-Konfiguration

Der Client unterstützt zwei Display-Modi:

**1. Produktionsmodus (mit physischem Display)**

Für Produktionsumgebungen mit HDMI-Display muss X11 automatisch starten:

```bash
# X11 Auto-Start konfigurieren
sudo /opt/digitalsignage-client/enable-autologin-x11.sh

# System neu starten
sudo reboot
```

Dies konfiguriert:
- Auto-Login zum Desktop
- Bildschirmschoner deaktiviert
- Energie-Management (DPMS) deaktiviert
- Mauszeiger wird automatisch versteckt
- X11 startet automatisch beim Booten

**2. Headless-Modus (ohne physisches Display)**

Für Test- und Entwicklungsumgebungen ohne physisches Display wird automatisch Xvfb (X Virtual Framebuffer) verwendet:

```bash
# Xvfb ist bereits installiert durch install.sh
# Der Client erkennt automatisch, ob ein Display vorhanden ist

# Manuelle Xvfb-Nutzung:
Xvfb :99 -screen 0 1920x1080x24 &
export DISPLAY=:99
```

Das Installationsskript (`start-with-display.sh`) erkennt automatisch:
- Wenn X11 läuft (DISPLAY=:0) → nutzt X11
- Wenn kein X11 läuft → startet Xvfb auf DISPLAY=:99

**Display-Status prüfen:**

```bash
# Diagnostic-Tool ausführen
sudo /opt/digitalsignage-client/diagnose.sh

# HDMI-Status prüfen (nur Raspberry Pi)
tvservice -s

# X11-Prozesse anzeigen
ps aux | grep X
```

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

### Client läuft mit Xvfb statt echtem Display

**Problem:** Der Client läuft mit virtuellem Display (Xvfb), obwohl ein HDMI-Display angeschlossen ist.

**Symptome:**
- Service läuft, aber Display zeigt nichts
- Logs zeigen "Display renderer created" aber kein Bild
- `ps aux | grep X` zeigt Xvfb :99 statt X :0

**Lösung:**

```bash
# 1. Real Display konfigurieren
cd ~/digitalsignage/src/DigitalSignage.Client.RaspberryPi
sudo bash configure-display.sh

# 2. System neu starten (erforderlich!)
sudo reboot

# 3. Nach Neustart: Display prüfen
echo $DISPLAY
# Sollte :0 anzeigen (nicht :99)

# 4. Service-Status prüfen
sudo systemctl status digitalsignage-client

# 5. Logs überprüfen
sudo journalctl -u digitalsignage-client -f
```

**Was macht configure-display.sh:**
- Aktiviert Auto-Login für den aktuellen Benutzer
- Konfiguriert X11 Auto-Start auf echtem Display
- Deaktiviert Bildschirmschoner und Energieverwaltung
- Versteckt Mauszeiger automatisch
- Aktualisiert Service für DISPLAY=:0

### Client verbindet nicht zum Server / Server findet Client nicht

**Problem:** Der Server kann den Client nicht finden oder der Client kann sich nicht verbinden.

**Diagnose:**

```bash
# Netzwerk-Diagnose ausführen
cd ~/digitalsignage/src/DigitalSignage.Client.RaspberryPi
sudo bash test-connection.sh
```

Das Diagnose-Tool prüft:
1. Netzwerkinterface und IP-Adresse
2. Gateway-Erreichbarkeit
3. DNS-Auflösung
4. Server Ping-Test
5. Server Port-Verbindung (TCP)
6. Client-Logs
7. Konfigurationsvalidierung

**Häufige Ursachen:**

**1. Falsche Server-IP in Konfiguration**

```bash
# Config bearbeiten
sudo nano /opt/digitalsignage-client/config.json

# Korrekte Server-IP eintragen:
{
  "server_host": "192.168.1.100",  # Windows-Server IP
  "server_port": 8080,
  "registration_token": "YOUR_TOKEN"
}

# Service neu starten
sudo systemctl restart digitalsignage-client
```

**2. Firewall blockiert Port 8080**

Windows Server:
```powershell
# PowerShell als Administrator:
New-NetFirewallRule -DisplayName "Digital Signage Server" `
  -Direction Inbound -LocalPort 8080 -Protocol TCP -Action Allow
```

Linux Server:
```bash
sudo ufw allow 8080/tcp
sudo ufw reload
```

**3. Verschiedene Netzwerke/Subnetze**

```bash
# Client-Netzwerk prüfen:
ip addr show | grep "inet "

# Windows Server IP prüfen:
# cmd: ipconfig

# Beide müssen im gleichen Subnet sein (z.B. 192.168.1.x)
```

**4. Server nicht gestartet oder nicht erreichbar**

```bash
# Von Client aus testen:
ping 192.168.1.100
telnet 192.168.1.100 8080

# Wenn Ping funktioniert aber telnet nicht:
# → Server-Anwendung läuft nicht oder Port ist falsch
```

**5. Registration Token fehlt oder falsch**

```bash
# Token in Config eintragen:
sudo nano /opt/digitalsignage-client/config.json

{
  "registration_token": "YOUR_REGISTRATION_TOKEN",
  ...
}

# Token muss auf Server konfiguriert sein
# Server: Settings → Client Registration → Tokens
```

### Layout wird nicht aktualisiert

1. **Client-Status in Server-UI prüfen**
   - Server-Anwendung → Devices Tab
   - Client sollte "Online" Status haben
   - Letzte Verbindung sollte aktuell sein

2. **Logs ansehen:**
   ```bash
   sudo journalctl -u digitalsignage-client -f
   ```

3. **Layout neu zuweisen**
   - Server → Devices → Client auswählen
   - "Assign Layout" auswählen
   - Layout aus Dropdown wählen
   - "Apply" klicken

4. **Cache leeren (falls Problem weiterhin besteht)**
   ```bash
   sudo systemctl stop digitalsignage-client
   rm -rf ~/.digitalsignage/cache/*
   sudo systemctl start digitalsignage-client
   ```

### SQL-Verbindung schlägt fehl

1. **Connection String prüfen**
   - Server-Anwendung → Data Sources Tab
   - "Test Connection" Button verwenden

2. **SQL Server erreichbar?**
   ```bash
   # Von Windows aus testen:
   telnet localhost 1433
   ```

3. **Firewall-Regeln für SQL Server**
   ```powershell
   New-NetFirewallRule -DisplayName "SQL Server" `
     -Direction Inbound -LocalPort 1433 -Protocol TCP -Action Allow
   ```

4. **SQL Server Browser läuft?**
   - Services.msc → SQL Server Browser → Started

5. **Authentication Mode**
   - SQL Server muss Mixed Mode Authentication verwenden
   - SSMS → Server Properties → Security → SQL Server and Windows Authentication

### Display zeigt nur schwarzen Bildschirm

1. **HDMI-Verbindung prüfen**
   ```bash
   # Raspberry Pi:
   tvservice -s
   # Sollte aktives HDMI-Display zeigen
   ```

2. **X11 läuft?**
   ```bash
   echo $DISPLAY
   # Sollte :0 anzeigen

   ps aux | grep X
   # Sollte X-Server Prozess zeigen
   ```

3. **Client-Service läuft?**
   ```bash
   sudo systemctl status digitalsignage-client
   ```

4. **GPU Memory erhöhen (bei Raspberry Pi)**
   ```bash
   sudo raspi-config
   # Advanced Options → Memory Split → 128 oder 256
   sudo reboot
   ```

### Performance-Probleme / Hohe CPU-Last

1. **Resolution reduzieren**
   ```json
   # Layout mit niedrigerer Auflösung verwenden
   # z.B. 1280x720 statt 1920x1080
   ```

2. **Update-Intervall für Daten erhöhen**
   - Server → Data Sources → Refresh Interval erhöhen

3. **Komplexe Layouts vereinfachen**
   - Weniger Elemente verwenden
   - Tabellen mit weniger Zeilen

4. **Raspberry Pi übertakten (vorsichtig!)**
   ```bash
   sudo raspi-config
   # Performance Options → Overclock
   ```

### Service startet nicht / Crasht sofort

1. **Logs analysieren:**
   ```bash
   sudo journalctl -u digitalsignage-client -n 100 --no-pager
   ```

2. **Manuellen Test durchführen:**
   ```bash
   cd /opt/digitalsignage-client
   sudo -u pi ./venv/bin/python3 client.py --test
   ```

3. **PyQt5-Installation prüfen:**
   ```bash
   /opt/digitalsignage-client/venv/bin/python3 -c "import PyQt5; print('OK')"
   ```

4. **Permissions prüfen:**
   ```bash
   ls -la /opt/digitalsignage-client/
   # Alle Dateien sollten dem richtigen User gehören
   ```

5. **Fix-Script ausführen:**
   ```bash
   sudo /opt/digitalsignage-client/fix-installation.sh
   ```

### Schnelle Diagnose

```bash
# All-in-One Diagnostic:
cd ~/digitalsignage/src/DigitalSignage.Client.RaspberryPi

# 1. Display-Check
echo "Display: $DISPLAY"
ps aux | grep -E "X|Xvfb"

# 2. Service-Check
sudo systemctl status digitalsignage-client

# 3. Network-Check
sudo bash test-connection.sh

# 4. Logs
sudo journalctl -u digitalsignage-client -n 50
```

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
