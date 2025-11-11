# Quick Start Guide

## In 5 Minuten zur ersten Anzeige

### Schritt 1: Server starten (Windows)

```bash
# Repository klonen
git clone https://github.com/yourusername/digitalsignage.git
cd digitalsignage

# Build und Start
dotnet restore
dotnet run --project src/DigitalSignage.Server/DigitalSignage.Server.csproj
```

Die Anwendung öffnet sich automatisch.

### Schritt 2: Erstes Layout erstellen

1. **Neues Layout**: `File` → `New Layout`
2. **Name vergeben**: "Willkommen"
3. **Textelement hinzufügen**:
   - Tool "T" in der linken Leiste wählen
   - Auf Canvas klicken
   - Text eingeben: "Willkommen bei Digital Signage"
   - Schriftgröße auf 48 setzen

4. **Speichern**: `File` → `Save`

### Schritt 3: Raspberry Pi Client einrichten

```bash
# Auf dem Raspberry Pi
git clone https://github.com/yourusername/digitalsignage.git
cd digitalsignage/src/DigitalSignage.Client.RaspberryPi

# Installation (erstellt automatisch eine virtuelle Python-Umgebung)
sudo ./install.sh

# Konfiguration
sudo nano /opt/digitalsignage-client/config.py
```

Tragen Sie die IP-Adresse Ihres Windows-Servers ein:

```python
# Server connection settings
SERVER_HOST = "192.168.1.100"
SERVER_PORT = 8080
```

Starten Sie den Client:

```bash
sudo systemctl start digitalsignage-client
```

**Hinweis:** Die Installation erstellt automatisch eine virtuelle Python-Umgebung unter `/opt/digitalsignage-client/venv` mit `--system-site-packages` Flag. Dies ist erforderlich für Python 3.11+ und ermöglicht sowohl die Isolation von pip-Paketen als auch den Zugriff auf system-installierte Pakete wie PyQt5.

### Schritt 4: Layout zuweisen

1. In der Server-App: Wechseln Sie zum **Devices** Tab
2. Sie sehen Ihren Raspberry Pi in der Liste
3. Klicken Sie auf den Client
4. Wählen Sie "Assign Layout" → "Willkommen"

Der Client zeigt jetzt Ihr Layout an!

## Nächste Schritte

### Dynamische Inhalte hinzufügen

1. **Datenquelle erstellen**:
   - Gehen Sie zum **Data Sources** Tab
   - Klicken Sie "Add Data Source"
   - Konfigurieren Sie Ihre SQL-Verbindung

2. **Variablen verwenden**:
   - Erstellen Sie ein Textelement
   - Verwenden Sie: `{{variableName}}`
   - Die Variable wird automatisch mit Daten gefüllt

### Weitere Elemente

- **Bilder**: Ziehen Sie das Bild-Tool auf den Canvas
- **QR-Codes**: Für interaktive Anzeigen
- **Formen**: Für Design-Elemente
- **Tabellen**: Für strukturierte Daten

### Zeitpläne erstellen

Zeigen Sie unterschiedliche Layouts zu verschiedenen Zeiten:

1. Wählen Sie einen Client
2. Klicken Sie "Add Schedule"
3. Definieren Sie Zeit und Layout
4. Speichern

## Troubleshooting

**Client verbindet nicht?**
- Firewall-Einstellungen prüfen
- IP-Adresse korrekt?
- Server läuft?

**Layout wird nicht angezeigt?**
- Logs prüfen: `sudo journalctl -u digitalsignage-client -f`
- Layout erneut zuweisen

## Hilfe

- [Vollständige Dokumentation](README.md)
- [API-Referenz](docs/API.md)
- [GitHub Issues](https://github.com/yourusername/digitalsignage/issues)
