# Raspberry Pi Autostart Fix - Komplettlösung

## Problem
Nach einem Neustart des Raspberry Pi:
- ✅ Systemd Service läuft (Prozess ist aktiv)
- ❌ **Display zeigt NUR Desktop** - kein PyQt5 Fenster sichtbar
- ✅ Nach manuellem `sudo systemctl restart digitalsignage-client` funktioniert es

## Lösung - Was wurde gefixt?

### 1. Service-File: Erweiterte X11-Wartezeit
**Datei:** `digitalsignage-client.service`

- **Wartezeit:** 30s → 45s für X11 + Desktop Ready
- **Bessere Checks:** `xset` + `xdpyinfo` (Desktop vollständig geladen)
- **Extra Delay:** Wenn Desktop > 15s braucht, zusätzliche 5s warten

### 2. Display Renderer: Window-Activation
**Datei:** `display_renderer.py`

- Explizite `raise_()` + `activateWindow()` Calls
- Window-Flags: `WindowStaysOnTopHint`
- Fullscreen + Active State forciert

### 3. Client Startup: QTimer Re-Activation
**Datei:** `client.py`

- QTimer (2s delay) für Re-Activation nach Event-Loop Start
- Sichert Sichtbarkeit auch bei langsamer Desktop-Init

## Installation & Testing

### Für NEUE Installationen
```bash
cd /opt/digitalsignage-client/src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh
```

### Für BESTEHENDE Installationen (Update)
```bash
# 1. Zu GitHub pushen (vom Development-Rechner)
cd /var/www/html/digitalsignage
source .env
git add -A
git commit -m "Fix: Raspberry Pi Autostart-Problem"
git push

# 2. Auf Pi updaten
sshpass -p 'mr412393' ssh pro@192.168.0.178
cd /opt/digitalsignage-client
sudo git pull
sudo ./update.sh

# 3. Testen
sudo reboot
```

### Diagnose nach Reboot
```bash
# Automatisches Diagnose-Script
sudo /opt/digitalsignage-client/check-autostart.sh

# Service-Status
sudo systemctl status digitalsignage-client

# Logs
sudo journalctl -u digitalsignage-client -n 50
```

## Erwartete Log-Ausgaben (Erfolg)

```
Waiting for X11 and desktop environment to be ready...
X11 and desktop ready after 18 seconds (waited for desktop)
X11 display detected on :0
✓ Display is accessible and responding
Display renderer set to fullscreen with window activation
Ensuring display window is visible and on top...
Display window visibility ensured
```

## Troubleshooting

### Service läuft, Display nicht sichtbar
```bash
sudo /opt/digitalsignage-client/check-autostart.sh
sudo systemctl restart digitalsignage-client
```

### X11 nicht verfügbar beim Boot
```bash
# Auto-Login aktivieren
sudo raspi-config
# → System Options → Boot/Auto Login → Desktop Autologin
```

## Erwartete Boot-Zeit
- Raspberry Pi 4: ~20-30 Sekunden bis Display sichtbar
- Raspberry Pi 3: ~30-45 Sekunden bis Display sichtbar

## Verification Checklist
- [ ] Pi bootet automatisch zum Desktop
- [ ] Service startet automatisch
- [ ] PyQt5 Fenster **sofort sichtbar** auf HDMI (innerhalb 30-45s)
- [ ] Fenster ist Fullscreen
- [ ] Maus-Cursor versteckt
- [ ] Nach `sudo systemctl restart` funktioniert es
- [ ] Nach erneutem Reboot funktioniert es
