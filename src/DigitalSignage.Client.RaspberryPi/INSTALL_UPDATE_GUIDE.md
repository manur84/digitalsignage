# Digital Signage Client - Installation & Update Guide

## Overview

Das Digital Signage Client Installer-System wurde vereinfacht. **Es gibt jetzt nur noch ein Script**: `install.sh`

### Was ist neu?

- **Ein Script für alles**: `install.sh` erkennt automatisch, ob Installation oder Update nötig ist
- **Intelligente Erkennung**: Prüft vorhandene Installation und wählt den richtigen Modus
- **Config-Sicherheit**: Automatisches Backup/Restore der config.py bei Updates
- **Idempotent**: Kann mehrfach ausgeführt werden ohne Probleme
- **Farbiges Output**: Klare visuelle Unterscheidung von Erfolg/Warnung/Fehler

## Verwendung

### Frische Installation

```bash
cd /path/to/digitalsignage/src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh
```

**Das Script erkennt automatisch:**
- Keine vorhandene Installation → **INSTALL MODE**
- Führt vollständige Installation durch (10 Schritte)
- Installiert System-Packages, Python Dependencies, Service
- Konfiguriert Display-Modus (Production/Development)

### Update auf bestehendem System

```bash
cd /opt/digitalsignage-client/src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh
```

**Das Script erkennt automatisch:**
- Installation in `/opt/digitalsignage-client` vorhanden → **UPDATE MODE**
- Führt intelligentes Update durch (8 Schritte):
  1. Stoppt Service
  2. Sichert config.py
  3. Macht Git Pull
  4. Kopiert aktualisierte Dateien
  5. Updated Dependencies (nur wenn requirements.txt geändert)
  6. Stellt config.py wieder her
  7. Updated Service-Konfiguration
  8. Startet Service neu

## Automatische Erkennung

Das Script prüft folgende Indikatoren:

```
✓ Installation directory found: /opt/digitalsignage-client
✓ Git repository exists
✓ Virtual environment exists
✓ Configuration file exists
✓ Service installed
✓ Service running
```

Basierend darauf wählt es:
- **INSTALL MODE**: Wenn kein Installationsverzeichnis oder Service
- **UPDATE MODE**: Wenn beides vorhanden

## Features im Detail

### 1. Config Backup/Restore (UPDATE MODE)

```bash
# Automatisches Backup vor Update
Backing up configuration...
✓ Config backed up to: /tmp/digitalsignage-config-backup-1234567890.py

# ... Update-Schritte ...

# Automatisches Restore nach File-Copy
Restoring configuration...
✓ Configuration restored
```

### 2. Intelligente Dependency-Updates

```bash
# Prüft ob requirements.txt geändert wurde
Checking Python dependencies...
ℹ requirements.txt changed, updating dependencies...
✓ Dependencies updated

# Oder wenn keine Änderung:
ℹ No dependency changes detected
```

### 3. Git Integration

```bash
# Bei INSTALL: Pull vor Installation
Updating code from repository...
ℹ Git repository detected - updating to latest version
Current branch: main
✓ Code updated successfully

# Bei UPDATE: Pull für neueste Files
Updating code from repository...
ℹ Git repository detected
Current branch: main
✓ Code updated from git

Recent changes:
abc1234 Fix: Client connection issue
def5678 Feature: Add new element type
```

### 4. File-Kopier-Verification

```bash
Copying updated files...
  ✓ client.py
  ✓ config.py
  ✓ discovery.py
  ✓ device_manager.py
  ✓ display_renderer.py
  ✓ cache_manager.py
  ✓ watchdog_monitor.py
  ✓ status_screen.py
  ✓ web_interface.py
  ✓ start-with-display.sh
✓ Copied 10 files
```

Falls Dateien fehlen:
```bash
  ✗ Missing: client.py
✗ Missing required files: client.py
```

### 5. Service-Status-Tracking

```bash
# Nach UPDATE:
Starting service...
✓ Service started successfully

Service Status:
● digitalsignage-client.service - Digital Signage Client
   Loaded: loaded (/etc/systemd/system/digitalsignage-client.service; enabled)
   Active: active (running) since ...
```

## Unterschiede INSTALL vs UPDATE

### Nur bei INSTALL (10 Schritte):

1. **System Packages installieren**
   ```bash
   Installing system dependencies...
   ✓ System dependencies installed
   ```

2. **Virtual Environment erstellen**
   ```bash
   Creating Python virtual environment...
   ✓ Virtual environment created
   ```

3. **Service-Unit-File installieren**
   ```bash
   Installing systemd service...
   ✓ Service file installed
   ```

4. **Service enablen (autostart)**
   ```bash
   systemctl enable digitalsignage-client
   ✓ Service enabled
   ```

5. **Display-Konfiguration (Production/Development)**
   ```bash
   Select deployment mode:
     1) PRODUCTION MODE - For HDMI displays
     2) DEVELOPMENT MODE - For headless/testing
   ```

6. **Pre-Flight Check**
   ```bash
   Testing client startup before enabling service...
   ✓ Pre-flight check successful!
   ```

### Nur bei UPDATE (8 Schritte):

1. **Service stoppen**
   ```bash
   Stopping service...
   ✓ Service stopped
   ```

2. **Config backup**
   ```bash
   Backing up configuration...
   ✓ Config backed up to: /tmp/digitalsignage-config-backup-1234567890.py
   ```

3. **Git Pull**
   ```bash
   Updating code from repository...
   ✓ Code updated from git
   ```

4. **Config restore**
   ```bash
   Restoring configuration...
   ✓ Configuration restored
   ```

5. **Service neu starten**
   ```bash
   Starting service...
   ✓ Service started successfully
   ```

### Bei BEIDEN:

- Dateien kopieren
- Python Dependencies installieren (wenn nötig)
- Permissions setzen
- Status anzeigen

## Migration vom alten update.sh

### Das alte update.sh Script

**Ist jetzt deprecated** aber funktioniert noch:

```bash
sudo ./update.sh
```

Zeigt:
```
⚠️  WARNING: This script is DEPRECATED!

The update.sh script has been merged into install.sh
install.sh now intelligently detects whether to install or update.

Please use install.sh instead:
  sudo ./install.sh

Redirecting to install.sh in 3 seconds...
```

Dann führt es automatisch `install.sh` aus.

### Warum die Änderung?

**Vorher (2 Scripts):**
```bash
# Frische Installation
sudo ./install.sh

# Update
sudo ./update.sh  # User muss sich das merken!
```

**Jetzt (1 Script):**
```bash
# Beides:
sudo ./install.sh  # Erkennt automatisch was zu tun ist!
```

**Vorteile:**
- Einfacher für User (nur ein Befehl)
- Weniger Code-Duplikation
- Konsistentes Verhalten
- Automatisches Config-Backup bei Updates
- Besseres Error Handling

## Beispiel-Szenarien

### Szenario 1: Frische Installation auf neuem Pi

```bash
# 1. Repository klonen (wenn nicht via install.sh auf anderem System)
cd ~
git clone https://github.com/manur84/digitalsignage.git
cd digitalsignage/src/DigitalSignage.Client.RaspberryPi

# 2. Installation
sudo ./install.sh

# Output:
# Detecting installation status...
# ✗ No installation directory found
# ✗ Service not installed
#
# Mode: 📦 INSTALL
#
# [1/10] Updating package lists...
# [2/10] Installing system dependencies...
# ...
# ✓ INSTALLATION COMPLETE!
```

### Szenario 2: Update auf bestehendem System

```bash
# 1. Zum Installationsverzeichnis
cd /opt/digitalsignage-client/src/DigitalSignage.Client.RaspberryPi

# 2. Update
sudo ./install.sh

# Output:
# Detecting installation status...
# ✓ Installation directory found: /opt/digitalsignage-client
# ✓ Service installed
# ✓ Service running
#
# Mode: 🔄 UPDATE
#
# [1/8] Stopping service...
# [2/8] Backing up configuration...
# ...
# ✓ UPDATE COMPLETE!
```

### Szenario 3: Wiederholte Ausführung (Idempotenz)

```bash
# Mehrfach ausführen ohne Probleme
sudo ./install.sh  # Erstes Mal: UPDATE
sudo ./install.sh  # Zweites Mal: UPDATE (gleicher Zustand)
sudo ./install.sh  # Drittes Mal: UPDATE (gleicher Zustand)

# Jedes Mal:
# - Config wird gesichert und wiederhergestellt
# - Neueste Dateien werden kopiert
# - Service wird neu gestartet
# - Keine Fehler, konsistenter Endzustand
```

### Szenario 4: Git Workflow (Empfohlener Workflow)

```bash
# ENTWICKLUNG (Lokal auf Entwicklungsmaschine):
# 1. Code ändern
cd /var/www/html/digitalsignage
nano src/DigitalSignage.Client.RaspberryPi/client.py

# 2. PUSH TO GITHUB (PFLICHT!)
source .env
git add -A
git commit -m "Fix: Client connection bug"
git push

# DEPLOYMENT (Auf Raspberry Pi):
# 3. SSH zum Pi
sshpass -p 'mr412393' ssh pro@192.168.0.178

# 4. Update via install.sh
cd /opt/digitalsignage-client
sudo git pull  # Holt neuesten Code
cd src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh  # Erkennt UPDATE MODE, macht alles automatisch

# 5. Logs prüfen
sudo journalctl -u digitalsignage-client -f

# 6. HDMI-Monitor prüfen
# → Visuelle Verifizierung des Updates
```

## Error Handling

### Missing Files

```bash
Copying updated files...
  ✗ Missing: client.py
✗ Missing required files: client.py
```

**Lösung:**
- Git Pull prüfen: `git status`, `git pull`
- Dateien manuell prüfen: `ls -la`

### Service Start Failed

```bash
Starting service...
⚠ Service may have failed to start
Check status: sudo systemctl status digitalsignage-client
```

**Lösung:**
```bash
# Status prüfen
sudo systemctl status digitalsignage-client

# Logs anschauen
sudo journalctl -u digitalsignage-client -n 50

# Manuelle Tests
sudo systemctl stop digitalsignage-client
cd /opt/digitalsignage-client
./venv/bin/python3 client.py --test
```

### Git Pull Failed

```bash
Updating code from repository...
⚠ Git pull failed, continuing with current version
```

**Lösung:**
```bash
# Merge Conflicts?
git status

# Reset zu remote (ACHTUNG: Lokale Änderungen gehen verloren)
git reset --hard origin/main
git pull

# Dann install.sh erneut
sudo ./install.sh
```

### Dependency Update Failed

```bash
Checking Python dependencies...
✗ Failed to update dependencies
```

**Lösung:**
```bash
# Manuell installieren
cd /opt/digitalsignage-client
./venv/bin/pip install -r src/DigitalSignage.Client.RaspberryPi/requirements.txt

# Oder venv neu erstellen
rm -rf venv
python3 -m venv --system-site-packages venv
./venv/bin/pip install -r src/DigitalSignage.Client.RaspberryPi/requirements.txt
```

## Testing-Checkliste

Nach Installation/Update:

```bash
# 1. Service Status
sudo systemctl status digitalsignage-client
# → Sollte "active (running)" sein

# 2. Logs (keine Errors)
sudo journalctl -u digitalsignage-client -n 50 --no-pager
# → Keine kritischen Fehler

# 3. Config-Datei
cat /opt/digitalsignage-client/config.py
# → Deine Einstellungen sollten noch da sein

# 4. Web Interface (falls aktiviert)
curl http://localhost:8081/status
# → Sollte JSON-Response zurückgeben

# 5. HDMI-Display
# → Visuell prüfen ob Layout angezeigt wird
```

## Nützliche Befehle

```bash
# Status anzeigen
sudo systemctl status digitalsignage-client

# Logs in Echtzeit
sudo journalctl -u digitalsignage-client -f

# Service neu starten
sudo systemctl restart digitalsignage-client

# Service stoppen
sudo systemctl stop digitalsignage-client

# Diagnose-Script (falls vorhanden)
sudo /opt/digitalsignage-client/diagnose.sh

# Manueller Test-Modus
sudo systemctl stop digitalsignage-client
cd /opt/digitalsignage-client
sudo -u pro ./venv/bin/python3 client.py --test

# Update durchführen
cd /opt/digitalsignage-client/src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh
```

## Zusammenfassung

**Ein Script. Zwei Modi. Automatische Erkennung.**

```
                 install.sh
                     │
                     ▼
           ┌─────────────────┐
           │  Erkennung      │
           │  - Install Dir? │
           │  - Service?     │
           │  - Config?      │
           └─────────────────┘
                     │
         ┌───────────┴───────────┐
         ▼                       ▼
    INSTALL MODE           UPDATE MODE
    ────────────           ───────────
    10 Schritte            8 Schritte
    - Packages             - Stop Service
    - Venv                 - Backup Config
    - Service Install      - Git Pull
    - Display Config       - Copy Files
    - Enable Service       - Update Deps
                          - Restore Config
                          - Restart Service
```

**Bottom Line:**
Egal ob Installation oder Update - einfach `sudo ./install.sh` ausführen. Das Script macht den Rest.
