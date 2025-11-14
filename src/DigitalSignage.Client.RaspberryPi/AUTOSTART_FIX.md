# Raspberry Pi Client Autostart Fix

## Problem (Original)

Wenn der Raspberry Pi neu startet:
1. Der Systemd-Service läuft (Status: active)
2. **Aber das Display zeigt nur den Desktop** - kein PyQt5 Fenster sichtbar
3. Erst nach manuellem Service-Restart (`sudo systemctl restart digitalsignage-client`) wird das Display korrekt geladen

## Root Cause Analysis

### Timing-Problem beim Boot

**Sequenz beim Boot:**
1. Systemd startet `digitalsignage-client.service`
2. Service wartet nur 2 Sekunden (`ExecStartPre=/bin/sleep 2`)
3. **X11 ist noch nicht bereit!** (X11-Start dauert 5-15 Sekunden auf Pi)
4. `start-with-display.sh` findet kein X11-Display
5. Script fällt zurück auf Xvfb (virtuelles Display)
6. PyQt5-Fenster wird auf Xvfb gerendert **→ nicht sichtbar auf HDMI!**

**Nach manuellem Restart:**
1. X11 läuft bereits seit 30+ Sekunden
2. `start-with-display.sh` findet X11 auf :0
3. PyQt5-Fenster wird auf HDMI gerendert **→ funktioniert!**

### Betroffene Dateien

1. **digitalsignage-client.service** (Zeile 18):
   ```ini
   ExecStartPre=/bin/sleep 2  # ← ZU KURZ!
   ```

2. **start-with-display.sh** (Zeile 112):
   ```bash
   if xset q &>/dev/null; then
       # X11 detected
   else
       # Falls back to Xvfb ← PASSIERT BEIM BOOT!
   fi
   ```

3. **install.sh** (Zeile 391):
   ```bash
   ExecStartPre=/bin/sleep 2  # ← Backup service hat auch nur 2s
   ```

## Lösung: Multi-Layer-Fix

### 1. Systemd Service (digitalsignage-client.service)

**Änderung:**
```ini
# ALT (fehlerhaft):
After=network-online.target graphical.target
ExecStartPre=/bin/sleep 2

# NEU (fix):
After=network-online.target graphical.target multi-user.target
ExecStartPre=/bin/bash -c 'for i in {1..30}; do if DISPLAY=:0 xset q &>/dev/null 2>&1; then echo "X11 ready after $i seconds"; exit 0; fi; echo "Waiting for X11... ($i/30)"; sleep 1; done; echo "WARNING: X11 not detected, will use Xvfb fallback"; exit 0'
```

**Was macht das?**
- Wartet aktiv auf X11 (nicht nur blind 2 Sekunden sleep)
- Prüft jede Sekunde ob X11 bereit ist (bis zu 30 Sekunden)
- Zeigt Progress in Logs
- Fällt zurück auf Xvfb wenn X11 nicht verfügbar (headless)

### 2. Display Detection (start-with-display.sh)

**Änderung:**
```bash
# ALT (fehlerhaft):
if xset q &>/dev/null; then
    export DISPLAY=:0
fi

# NEU (fix):
DISPLAY_CANDIDATES=(":0" ":1" "${DISPLAY}")
X11_FOUND=false

for DISPLAY_TEST in "${DISPLAY_CANDIDATES[@]}"; do
    if [ -n "$DISPLAY_TEST" ] && DISPLAY="$DISPLAY_TEST" xset q &>/dev/null 2>&1; then
        export DISPLAY="$DISPLAY_TEST"
        X11_FOUND=true
        break
    fi
done
```

**Was macht das?**
- Testet mehrere Display-Nummern (:0, :1, $DISPLAY)
- Bessere Fehlerbehandlung (2>&1)
- Explizite DISPLAY-Variable für xset-Test

### 3. Installation (install.sh)

**Änderung:**
- Fallback-Service-Definition aktualisiert
- Neues Script `wait-for-x11.sh` kopiert
- Bessere Logging-Konfiguration

### 4. Neues Script: wait-for-x11.sh

Standalone-Script zum Testen der X11-Readiness:
```bash
sudo /opt/digitalsignage-client/wait-for-x11.sh
```

## Testing-Anleitung

### Phase 1: Lokal Testen (vor GitHub Push)

1. **Dateien prüfen:**
   ```bash
   cd /var/www/html/digitalsignage/src/DigitalSignage.Client.RaspberryPi

   # Service-Datei prüfen
   grep -A5 "ExecStartPre" digitalsignage-client.service

   # Start-Script prüfen
   grep -A10 "DISPLAY_CANDIDATES" start-with-display.sh
   ```

2. **Syntax-Test:**
   ```bash
   # Bash-Syntax prüfen
   bash -n digitalsignage-client.service
   bash -n start-with-display.sh
   bash -n wait-for-x11.sh
   bash -n install.sh
   ```

### Phase 2: Zu GitHub Pushen

**PFLICHT: Nach JEDER Änderung sofort pushen!**

```bash
cd /var/www/html/digitalsignage
source .env  # GitHub Token laden

git add -A
git commit -m "Fix: Raspberry Pi Client Autostart-Problem

Problem:
- Nach Neustart läuft Service, aber Display bleibt schwarz
- Erst nach manuellem Restart funktioniert es

Root Cause:
- Service startet bevor X11 bereit ist (nur 2s Wartezeit)
- Fällt zurück auf Xvfb statt echtem HDMI-Display

Lösung:
- Systemd wartet aktiv auf X11 (bis zu 30s)
- Bessere Display-Erkennung mit Fallback
- Neues wait-for-x11.sh Script für Diagnostik

Betroffene Dateien:
- digitalsignage-client.service
- start-with-display.sh
- install.sh
- wait-for-x11.sh (neu)

Testing erforderlich auf Raspberry Pi!

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"

git push
```

### Phase 3: Auf Raspberry Pi Testen

**WICHTIG: Änderungen müssen zu GitHub gepusht sein!**

1. **SSH zum Pi:**
   ```bash
   sshpass -p 'mr412393' ssh pro@192.168.0.178
   ```

2. **Update vom Git:**
   ```bash
   cd /opt/digitalsignage-client
   sudo git pull
   ```

3. **Service neu installieren:**
   ```bash
   cd /opt/digitalsignage-client/src/DigitalSignage.Client.RaspberryPi
   sudo ./install.sh
   ```

4. **Service-Status prüfen:**
   ```bash
   sudo systemctl status digitalsignage-client
   ```

5. **Logs in Echtzeit beobachten:**
   ```bash
   sudo journalctl -u digitalsignage-client -f
   ```

6. **HDMI-Display prüfen:**
   - Sollte PyQt5-Fenster anzeigen
   - Nicht nur Desktop

### Phase 4: Reboot-Test (KRITISCH!)

**Dies ist der eigentliche Test des Fixes:**

1. **System neu starten:**
   ```bash
   sudo reboot
   ```

2. **Nach Neustart: SSH erneut verbinden:**
   ```bash
   sshpass -p 'mr412393' ssh pro@192.168.0.178
   ```

3. **Startup-Logs analysieren:**
   ```bash
   # Systemd Service Log
   sudo journalctl -u digitalsignage-client -b -n 100

   # Startup-Script Log
   sudo cat /var/log/digitalsignage-client-startup.log
   ```

4. **Auf wichtige Log-Einträge achten:**
   ```
   ✓ Erfolgreich:
   "X11 ready after 5 seconds"
   "✓ X11 display detected on :0"
   "Using DISPLAY=:0"

   ✗ Problem:
   "WARNING: X11 not detected, will use Xvfb fallback"
   "Starting virtual framebuffer (Xvfb)"
   ```

5. **HDMI-Display prüfen:**
   - **Sofort nach Boot** sollte PyQt5-Fenster erscheinen
   - **Kein manueller Restart** nötig

6. **Web-Interface prüfen:**
   ```bash
   # Von lokalem Rechner:
   curl http://192.168.0.178:5000/api/status
   ```

### Phase 5: Diagnostik bei Problemen

Wenn Display immer noch schwarz bleibt:

1. **X11-Readiness manuell testen:**
   ```bash
   sudo /opt/digitalsignage-client/wait-for-x11.sh
   ```

2. **X11-Status prüfen:**
   ```bash
   # Ist X11 überhaupt gestartet?
   ps aux | grep X

   # Welche Displays sind verfügbar?
   DISPLAY=:0 xset q
   DISPLAY=:1 xset q

   # X11-Logs prüfen
   cat /var/log/Xorg.0.log
   ```

3. **Auto-Login prüfen:**
   ```bash
   # Sollte "B4" sein (Desktop Auto-Login)
   sudo raspi-config nonint get_boot_behaviour
   ```

4. **Service Environment prüfen:**
   ```bash
   sudo systemctl show digitalsignage-client | grep Environment
   ```

5. **Manueller Start-Test:**
   ```bash
   # Service stoppen
   sudo systemctl stop digitalsignage-client

   # Manuell starten
   sudo -u pro /opt/digitalsignage-client/start-with-display.sh
   ```

## Erwartete Ergebnisse

### Vor dem Fix:
- ❌ Nach Neustart: Service läuft, Display schwarz
- ❌ Logs: "Starting virtual framebuffer (Xvfb)"
- ✓ Nach manuellem Restart: Funktioniert

### Nach dem Fix:
- ✓ Nach Neustart: Service läuft, Display zeigt sofort Content
- ✓ Logs: "X11 ready after 5 seconds"
- ✓ Logs: "✓ X11 display detected on :0"
- ✓ Kein manueller Restart nötig

## Rollback-Plan

Falls der Fix Probleme verursacht:

1. **Alte Service-Datei wiederherstellen:**
   ```bash
   cd /opt/digitalsignage-client
   sudo git checkout HEAD~1 digitalsignage-client.service
   sudo cp digitalsignage-client.service /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl restart digitalsignage-client
   ```

2. **Oder: Komplette alte Version wiederherstellen:**
   ```bash
   cd /opt/digitalsignage-client
   sudo git log --oneline -5  # Finde vorherigen Commit
   sudo git checkout <commit-hash>
   sudo ./install.sh
   ```

## Zusammenfassung der Änderungen

### digitalsignage-client.service
- ✅ Hinzugefügt: `multi-user.target` zu After
- ✅ Ersetzt: `sleep 2` mit aktivem X11-Wait-Loop (30s)
- ✅ Verbessert: Logging für Debugging

### start-with-display.sh
- ✅ Hinzugefügt: Multi-Display-Testing (:0, :1, $DISPLAY)
- ✅ Verbessert: Fehlerbehandlung mit 2>&1
- ✅ Hinzugefügt: Bessere Logging-Messages

### install.sh
- ✅ Aktualisiert: Fallback-Service-Definition
- ✅ Hinzugefügt: wait-for-x11.sh kopieren
- ✅ Hinzugefügt: Permissions setzen

### wait-for-x11.sh (NEU)
- ✅ Standalone-Script für X11-Readiness-Test
- ✅ Verwendbar für Diagnostik
- ✅ Exit-Code 0 für systemd-Kompatibilität

## Nächste Schritte

1. ✅ Änderungen zu GitHub pushen
2. ⏳ Auf Pi testen (SSH + git pull)
3. ⏳ Reboot-Test durchführen
4. ⏳ HDMI-Display verifizieren
5. ⏳ Bei Erfolg: Dokumentation aktualisieren
6. ⏳ Bei Fehler: Logs sammeln, analysieren, fixen
