# Testing Instructions - Auto-Discovery Improvements

## Änderungen Zusammenfassung

**Datum:** 2025-11-21

### Was wurde geändert:

1. **IP-Priorisierung verbessert** (`discovery.py`)
   - 192.168.x.x hat jetzt höchste Priorität
   - 10.x.x.x zweite Priorität
   - 172.16-31.x.x dritte Priorität
   - Localhost (127.x.x.x) wird komplett gefiltert

2. **Neuer API Endpoint** (`web_interface.py`)
   - `/api/discovered-servers` gibt alle entdeckten Server zurück
   - Timeout 3 Sekunden für Web-UI Responsiveness
   - Nutzt mDNS + UDP Broadcast Discovery

3. **Web-Interface erweitert** (`dashboard.html`)
   - Server-Auswahl Dropdown
   - Scan-Button für manuelle Discovery
   - Auto-Scan 2 Sekunden nach Seiten-Load
   - Anzeige der aktuell konfigurierten Server-IP

4. **Dokumentation** (`AUTO_DISCOVERY_ANALYSIS.md`)
   - Kompletter Auto-Discovery Flow dokumentiert
   - Problem-Analyse (localhost-Verbindung)
   - Lösungsansätze beschrieben

---

## Test-Szenarien auf Raspberry Pi

### Vorbereitung

1. **Auf Raspberry Pi verbinden:**
   ```bash
   sshpass -p 'mr412393' ssh pro@192.168.0.178
   ```

2. **Update durchführen:**
   ```bash
   cd ~/digitalsignage
   git pull
   cd src/DigitalSignage.Client.RaspberryPi
   sudo ./install.sh  # Erkennt UPDATE-Modus automatisch
   ```

3. **Service neustarten:**
   ```bash
   sudo systemctl restart digitalsignage-client
   ```

4. **Logs beobachten:**
   ```bash
   sudo journalctl -u digitalsignage-client -f
   ```

---

### Test 1: IP-Priorisierung

**Ziel:** Verifizieren dass 192.168.x.x bevorzugt wird

**Setup:**
- Server hat mehrere IPs (z.B. WLAN + Ethernet)
- Beispiel: 192.168.0.100 (WLAN) + 10.0.0.50 (VPN)

**Schritte:**
1. Client starten
2. In Logs nach "Best IP selected" suchen
3. Verify: 192.168.x.x wurde ausgewählt

**Erwartetes Log-Output:**
```
Valid IP: 192.168.0.100 (priority: 0)
Valid IP: 10.0.0.50 (priority: 1)
Filtered and prioritized IPs: ['192.168.0.100', '10.0.0.50']
Best IP selected: 192.168.0.100 (priority: 0)
```

**Erfolg:** ✅ wenn 192.168.x.x zuerst kommt

---

### Test 2: Localhost-Filterung

**Ziel:** Verifizieren dass localhost NIE verwendet wird

**Setup:**
- Server sendet IPs: ['127.0.0.1', '192.168.0.100', 'localhost']

**Schritte:**
1. Discovery triggern
2. In Logs nach "Filtering out loopback IP" suchen

**Erwartetes Log-Output:**
```
Filtering out loopback IP: 127.0.0.1
Filtering out loopback IP: localhost
Valid IP: 192.168.0.100 (priority: 0)
Filtered and prioritized IPs: ['192.168.0.100']
```

**Erfolg:** ✅ wenn nur 192.168.0.100 übrig bleibt

---

### Test 3: Web-Interface Server-Auswahl

**Ziel:** Verifizieren dass Web-UI Server anzeigt und auswählen kann

**Setup:**
- Client läuft
- Web-Interface auf Port 8081

**Schritte:**
1. Im Browser öffnen: `http://192.168.0.178:8081`
2. Zu "Settings" Tab navigieren
3. Warten auf Auto-Scan (2 Sekunden)
4. Verifizieren:
   - Dropdown zeigt entdeckte Server
   - Status-Text zeigt "✓ Found X server(s)"
   - IPs sind nach Priorität sortiert (192.168 zuerst)
   - "⭐ (Best)" ist bei erster IP

**Erfolg:** ✅ wenn Dropdown gefüllt ist und beste IP markiert ist

---

### Test 4: Manuelle Server-Auswahl

**Ziel:** User kann Server aus Dropdown wählen

**Schritte:**
1. Web-Interface öffnen → Settings
2. Auf "🔍 Scan" Button klicken
3. Server aus Dropdown auswählen
4. Verifizieren:
   - Server Host Input wurde ausgefüllt
   - Server Port wurde ausgefüllt
   - SSL Checkbox wurde gesetzt (wenn Server SSL nutzt)
   - "Currently configured" zeigt gewählten Server
5. Auf "💾 Save Settings" klicken
6. Bestätigen "Restart now?" → JA
7. Nach Restart: Logs prüfen ob Client sich mit gewähltem Server verbindet

**Erwartetes Log-Output:**
```
Updated server_host to 192.168.0.100
Settings updated and saved: server_host, server_port, use_ssl
Configuration saved to /opt/digitalsignage-client/config.json
```

**Erfolg:** ✅ wenn Client sich nach Restart mit gewähltem Server verbindet

---

### Test 5: API Endpoint `/api/discovered-servers`

**Ziel:** API gibt korrekte Daten zurück

**Schritte:**
1. Via SSH auf Pi:
   ```bash
   curl http://localhost:8081/api/discovered-servers | python3 -m json.tool
   ```

2. Verifizieren Response:
   ```json
   {
     "success": true,
     "servers": [
       {
         "server_name": "Desktop-PC",
         "ips": ["192.168.0.100", "10.0.0.50"],
         "port": 8080,
         "protocol": "wss",
         "ssl_enabled": true,
         "endpoint_path": "ws/",
         "urls": [
           "wss://192.168.0.100:8080/ws/",
           "wss://10.0.0.50:8080/ws/"
         ],
         "primary_url": "wss://192.168.0.100:8080/ws/"
       }
     ],
     "count": 1,
     "timestamp": "2025-11-21T..."
   }
   ```

**Erfolg:** ✅ wenn:
- `success: true`
- IPs sind sortiert (192.168 zuerst)
- Keine localhost IPs vorhanden
- `primary_url` zeigt beste IP

---

### Test 6: Mehrere Netzwerk-Interfaces

**Ziel:** Client funktioniert mit WLAN + Ethernet gleichzeitig

**Setup:**
- Raspberry Pi hat WLAN (wlan0) + Ethernet (eth0) aktiv
- Server ist über beide Interfaces erreichbar

**Schritte:**
1. Netzwerk-Status prüfen:
   ```bash
   ip addr show | grep "inet "
   ```

2. Discovery starten
3. In Logs prüfen welches Interface verwendet wird

**Erwartetes Log-Output:**
```
Using interface eth0 (IP: 192.168.0.178) for mDNS discovery
Using eth0 broadcast address: 192.168.0.255
Sent discovery broadcast to 192.168.0.255:5555
```

**Erfolg:** ✅ wenn Discovery beide Interfaces nutzt und Server findet

---

### Test 7: Auto-Discovery Fallback

**Ziel:** Verifizieren Fallback-Logik wenn Discovery fehlschlägt

**Setup:**
- Server ist offline ODER
- Firewall blockt Discovery-Ports

**Schritte:**
1. Server stoppen
2. Client starten
3. Logs beobachten

**Erwartetes Log-Output:**
```
AUTO-DISCOVERY MODE ENABLED
Discovery scan #1/10 starting...
No server found, retrying in 2s...
Discovery scan #2/10 starting...
...
Discovery scan #10/10 starting...
AUTO-DISCOVERY FAILED after 10 attempts
FALLBACK: Disabling auto_discover and trying configured server...
Configured server: <server_host>:<server_port>
```

**Erfolg:** ✅ wenn Client nach 10 Versuchen fallback macht

**WICHTIG:** Verify dass Client NICHT zu localhost verbindet!

---

## Erwartete Probleme & Lösungen

### Problem: Discovery findet keine Server

**Mögliche Ursachen:**
1. Server ist offline
2. Firewall blockt Port 5555 (UDP) oder 5353 (mDNS)
3. Server und Client in verschiedenen Subnetzen

**Lösung:**
```bash
# Server-seitig prüfen:
# - UDP Port 5555 offen?
# - mDNS Service läuft?

# Client-seitig prüfen:
sudo journalctl -u digitalsignage-client -n 200 | grep -i discovery
```

---

### Problem: Dropdown bleibt leer

**Mögliche Ursachen:**
1. API Endpoint gibt Fehler zurück
2. JavaScript Fehler im Browser

**Lösung:**
```bash
# API direkt testen:
curl http://localhost:8081/api/discovered-servers

# Browser Console öffnen (F12)
# Nach JavaScript Errors suchen
```

---

### Problem: Client verbindet sich mit falscher IP

**Mögliche Ursachen:**
1. Alte Config in `/opt/digitalsignage-client/config.json`
2. IP-Priorisierung funktioniert nicht

**Lösung:**
```bash
# Config prüfen:
cat /opt/digitalsignage-client/config.json | python3 -m json.tool

# Expected:
# "server_host": "192.168.x.x"  (NICHT localhost!)
# "auto_discover": true

# Falls falsch:
sudo nano /opt/digitalsignage-client/config.json
# server_host auf "" setzen oder korrekte IP
sudo systemctl restart digitalsignage-client
```

---

### Problem: "Permission denied" beim Config-Speichern

**Mögliche Ursachen:**
- Config-Datei hat falsche Permissions

**Lösung:**
```bash
sudo chmod 666 /opt/digitalsignage-client/config.json
```

---

## Erfolgs-Kriterien

**✅ ALLE Tests erfolgreich wenn:**

1. ✅ 192.168.x.x IPs werden bevorzugt (Test 1)
2. ✅ Localhost wird gefiltert (Test 2)
3. ✅ Web-Interface zeigt Server an (Test 3)
4. ✅ User kann Server auswählen (Test 4)
5. ✅ API gibt korrekte Daten (Test 5)
6. ✅ Mehrere Interfaces funktionieren (Test 6)
7. ✅ Fallback funktioniert OHNE localhost (Test 7)

---

## Debugging Commands

```bash
# Logs in Echtzeit
sudo journalctl -u digitalsignage-client -f

# Letzte 200 Zeilen mit Discovery-Infos
sudo journalctl -u digitalsignage-client -n 200 | grep -i discovery

# Config anzeigen
cat /opt/digitalsignage-client/config.json | python3 -m json.tool

# Service Status
sudo systemctl status digitalsignage-client

# API Test
curl http://localhost:8081/api/discovered-servers | python3 -m json.tool

# Netzwerk-Interfaces
ip addr show

# Discovery manuell testen
cd /opt/digitalsignage-client
./venv/bin/python3 -c "
from discovery import discover_all_servers
servers = discover_all_servers(timeout=5.0)
for s in servers:
    print(f'{s.server_name}: {s.local_ips}')
"
```

---

## Nach erfolgreichem Test

**Bitte dokumentieren:**

1. **Welche Tests waren erfolgreich?**
   - [ ] Test 1: IP-Priorisierung
   - [ ] Test 2: Localhost-Filterung
   - [ ] Test 3: Web-Interface Anzeige
   - [ ] Test 4: Manuelle Auswahl
   - [ ] Test 5: API Endpoint
   - [ ] Test 6: Mehrere Interfaces
   - [ ] Test 7: Fallback

2. **Welche IP wurde vom Client gewählt?**
   - Server IPs: _______________
   - Client wählte: _______________

3. **Probleme aufgetreten?**
   - Beschreibung: _______________
   - Lösung: _______________

4. **Screenshots vom Web-Interface?**
   - Settings-Seite mit Dropdown
   - Discovery-Status

---

## Weitere Verbesserungen (Optional)

Falls Zeit bleibt:

1. **Config Default ändern:**
   - `server_host: str = ""` statt `"localhost"`
   - Verhindert localhost-Verbindung bei Discovery-Fehler

2. **Client speichert discovered_servers:**
   - Cache für schnellere UI-Anzeige
   - Kein Re-Discovery bei jedem API Call

3. **UI Verbesserungen:**
   - Auto-Refresh alle 30 Sekunden
   - Anzeige ob Server erreichbar ist
   - Ping-Test zu ausgewähltem Server

---

**Viel Erfolg beim Testen! 🚀**
