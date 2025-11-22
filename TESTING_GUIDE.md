# Testing Guide: iOS Mobile App für Digital Signage System

## Voraussetzungen

### Hardware
- **macOS Computer** (für iOS-Entwicklung erforderlich)
- **Optional**: iPhone oder iPad (für Tests auf echter Hardware)
- **Optional**: Raspberry Pi mit Client installiert (für vollständige Tests)

### Software auf macOS
1. **macOS**: Version 13.0 (Ventura) oder höher
2. **Xcode**: Version 15.0 oder höher
3. **.NET 8 SDK**:
   ```bash
   # Download von https://dotnet.microsoft.com/download/dotnet/8.0
   # Oder via Homebrew:
   brew install dotnet
   ```

4. **.NET MAUI Workloads**:
   ```bash
   dotnet workload install maui
   dotnet workload install ios
   ```

5. **Git**: Zum Klonen des Repositories

---

## Schritt 1: Repository klonen (auf macOS)

```bash
# Repository klonen
git clone https://github.com/manur84/digitalsignage.git
cd digitalsignage
```

---

## Schritt 2: Windows Server vorbereiten

### 2.1 Server auf Windows PC starten

```bash
# Auf Windows PC (wo der Server normalerweise läuft)
cd C:\path\to\digitalsignage
git pull  # Neueste Änderungen holen

# Server builden und starten
dotnet build DigitalSignage.sln
dotnet run --project src/DigitalSignage.Server/DigitalSignage.Server.csproj
```

**Wichtig**: Notiere dir die Server-IP-Adresse:
- Im Server-Log steht: `mDNS service started: DigitalSignage-HOSTNAME on port 8080`
- Oder finde die IP mit `ipconfig` (Windows) / `ifconfig` (Linux/macOS)
- Beispiel: `192.168.0.100:8080`

### 2.2 Firewall-Einstellungen prüfen

Der Server benötigt folgende offene Ports:
- **Port 8080-8083** (oder 8888/9000): WebSocket-Server
- **Port 5353 UDP**: mDNS/Bonjour für Auto-Discovery

**Windows Firewall erlauben:**
```powershell
# PowerShell als Administrator
New-NetFirewallRule -DisplayName "Digital Signage WebSocket" `
  -Direction Inbound -Protocol TCP -LocalPort 8080-8083,8888,9000 -Action Allow

New-NetFirewallRule -DisplayName "Digital Signage mDNS" `
  -Direction Inbound -Protocol UDP -LocalPort 5353 -Action Allow
```

### 2.3 Server-Logs überwachen

Der Server sollte zeigen:
```
[INFO] WebSocketCommunicationService started on port 8080
[INFO] mDNS service started: DigitalSignage-DESKTOP-ABC on port 8080
```

---

## Schritt 3: Mobile App builden (auf macOS)

### 3.1 Projekt öffnen

```bash
cd src/DigitalSignage.App.Mobile
```

### 3.2 Dependencies wiederherstellen

```bash
dotnet restore
```

### 3.3 Erste Build (kann 5-10 Minuten dauern)

```bash
# iOS Debug Build
dotnet build -f net8.0-ios
```

**Mögliche Fehler:**

**Fehler: "No valid iOS code signing keys found"**
- Lösung: Xcode öffnen → Preferences → Accounts → Apple ID hinzufügen
- Oder: Für Simulator nicht erforderlich

**Fehler: "Workload not installed"**
```bash
dotnet workload restore
```

### 3.4 App im iOS Simulator starten

```bash
# Simulator starten und App deployen
dotnet build -t:Run -f net8.0-ios
```

**Oder in Visual Studio for Mac / Rider:**
1. Solution `DigitalSignage.sln` öffnen
2. Projekt `DigitalSignage.App.Mobile` als Startup-Projekt setzen
3. Target: `iPhone 15 Pro Simulator` (oder anderes Gerät)
4. Framework: `net8.0-ios`
5. Debug → Run (F5)

---

## Schritt 4: App testen - Funktionale Tests

### 4.1 Test: Auto-Discovery (mDNS)

**Voraussetzung**: Server läuft, macOS und Windows PC im **gleichen Netzwerk**

1. **App startet** → Login Page erscheint
2. **Tap "Scan for Servers"**
3. **Erwartetes Verhalten**:
   - Scan-Animation läuft (~5 Sekunden)
   - Server erscheint in der Liste:
     ```
     DigitalSignage-DESKTOP-ABC
     wss://192.168.0.100:8080
     0 clients connected
     ```

**Fehler-Diagnose wenn kein Server gefunden wird:**

```bash
# Auf macOS: DNS-SD Tool verwenden
dns-sd -B _digitalsignage._tcp

# Sollte zeigen:
# Browsing for _digitalsignage._tcp
# Timestamp A/R Flags if Domain Service Type Instance Name
# 12:34:56.789 Add 3 0 local. _digitalsignage._tcp. DigitalSignage-DESKTOP-ABC
```

**Falls nichts gefunden wird:**
- Prüfe: Beide Geräte im **gleichen WLAN**?
- Prüfe: Windows Firewall Port 5353 UDP offen?
- Prüfe: Server-Log zeigt "mDNS service started"?
- Alternative: Manuelle Eingabe verwenden (siehe 4.2)

### 4.2 Test: Manuelle Server-Verbindung

**Fallback wenn Auto-Discovery nicht funktioniert:**

1. **Tap "Or enter server manually"**
2. **Eingeben**: `192.168.0.100:8080` (deine Server-IP)
3. **Optional**: Registration Token eingeben (wenn Server einen erwartet)
4. **Tap "Connect"**

**Erwartetes Verhalten**:
- Loading Indicator erscheint
- WebSocket-Verbindung wird aufgebaut
- Nach ~2-3 Sekunden: Navigation zu "Devices" Page

### 4.3 Test: App-Registrierung & Admin-Freigabe

**Wenn App sich zum ersten Mal verbindet:**

1. **App sendet Registrierung** zum Server
2. **Server erstellt Pending-Registrierung**

**Auf Windows Server:**
1. **Öffne "Mobile Apps" Tab** in der Server-App
2. **Siehst du**:
   - Orange Badge mit "1" (Pending Count)
   - Neue Zeile in der Tabelle:
     ```
     Device Name: iPhone 15 Pro
     Platform: iOS
     Status: Pending (Orange)
     ```

3. **Registrierung genehmigen**:
   - Wähle die Zeile aus
   - Permissions auswählen:
     - ☑ View (Device List, Screenshots)
     - ☑ Control (Restart, Commands)
     - ☑ Manage (Assign Layouts)
   - **Klick "Approve"**

**Auf iOS App:**
- App erhält Token
- Status ändert sich zu "Connected"
- Navigation zu "Devices" Page

### 4.4 Test: Device List anzeigen

**Voraussetzung**: Mindestens 1 Raspberry Pi Client läuft und mit Server verbunden

**Erwartetes Verhalten**:
- **Header** zeigt:
  ```
  Online Devices: 1 / 1
  ```
- **Device List** zeigt Geräte:
  ```
  🍓 pi-livingroom
  IP: 192.168.0.178
  Status: Online (Grün)
  Resolution: 1920x1080
  Last Seen: 2 minutes ago
  ```

**Pull-to-Refresh testen**:
- Ziehe Liste nach unten
- Loading Indicator erscheint
- Liste aktualisiert sich

### 4.5 Test: Device Detail Page

1. **Tap auf ein Device** in der Liste
2. **Device Detail Page öffnet sich**

**Erwartete Anzeige**:

**Header:**
```
pi-livingroom
IP: 192.168.0.178
Status: Online
Resolution: 1920x1080
```

**Hardware Metrics:**
```
CPU:     [████████░░] 75%
Memory:  [██████░░░░] 60%
Disk:    [████░░░░░░] 45%
Temperature: 58.3°C (Grün)
```

**Remote Controls:**
- 🔄 Restart Device (Rot)
- 🔊 Volume Up / 🔉 Volume Down
- 💡 Screen On / 🌙 Screen Off

### 4.6 Test: Remote Commands senden

**Test: Volume Up**
1. **Tap "🔊 Volume Up"**
2. **Erwartetes Verhalten**:
   - Loading Indicator kurz sichtbar
   - Success-Message: "VolumeUp command sent successfully"
   - **Auf Raspberry Pi**: Lautstärke erhöht sich

**Test: Screenshot**
1. **Scroll nach unten** zu "Screenshot" Section
2. **Tap "📸 Take Screenshot"**
3. **Erwartetes Verhalten**:
   - Loading Spinner erscheint (~2-5 Sekunden)
   - Screenshot wird angezeigt
   - Timestamp: "Captured: 14:32:15"
   - Bild zeigt aktuellen Pi-Bildschirm

**Test: Restart (mit Bestätigung)**
1. **Tap "🔄 Restart Device"**
2. **Confirmation Dialog** erscheint:
   ```
   Restart Device
   Are you sure you want to restart this device?
   [Cancel] [OK]
   ```
3. **Tap "OK"**
4. **Erwartetes Verhalten**:
   - Command wird gesendet
   - **Auf Raspberry Pi**: Neustart beginnt
   - **In App**: Device Status ändert sich zu "Offline" nach ~30 Sekunden
   - Nach ~2 Minuten: Status wieder "Online"

---

## Schritt 5: Test auf echtem iPhone (Optional)

### 5.1 Apple Developer Account einrichten

**Kostenlose Option (für lokale Tests):**
1. Xcode öffnen
2. Preferences → Accounts
3. Apple ID hinzufügen (kostenloser Account)
4. "Manage Certificates" → Klick "+"
5. "iOS Development" wählen

### 5.2 iPhone vorbereiten

1. **iPhone mit USB-Kabel verbinden**
2. **iPhone**: Settings → General → VPN & Device Management → Trust Computer
3. **iPhone**: Settings → Privacy & Security → Developer Mode → Enable

### 5.3 App deployen

```bash
# Mit verbundenem iPhone
dotnet build -t:Run -f net8.0-ios -p:RuntimeIdentifier=ios-arm64
```

**In Xcode/Rider:**
- Target: Dein iPhone auswählen (statt Simulator)
- Run

### 5.4 Testen auf echtem Gerät

**Vorteile:**
- Face ID/Touch ID funktioniert (Biometric Auth)
- Echte Netzwerkbedingungen
- Performance-Tests

**Tests:**
- Alle Tests von Schritt 4 wiederholen
- Zusätzlich: Face ID/Touch ID (wenn implementiert)
- Zusätzlich: Push Notifications (wenn implementiert)

---

## Troubleshooting

### Problem: "App startet nicht im Simulator"

**Lösung 1**: Simulator neu starten
```bash
xcrun simctl shutdown all
xcrun simctl boot "iPhone 15 Pro"
```

**Lösung 2**: Clean und Rebuild
```bash
dotnet clean
dotnet build -f net8.0-ios
```

### Problem: "Cannot connect to server"

**Diagnose-Schritte:**

1. **Server läuft?**
   ```bash
   # Auf Windows
   netstat -ano | findstr 8080
   # Sollte zeigen: LISTENING auf Port 8080
   ```

2. **Ping-Test**:
   ```bash
   # Auf macOS
   ping 192.168.0.100
   ```

3. **WebSocket-Test** mit Browser:
   ```
   http://192.168.0.100:8080
   # Sollte "WebSocket endpoint" anzeigen oder 404
   ```

4. **Firewall prüfen** (siehe Schritt 2.2)

5. **Logs prüfen**:
   - **Server**: `logs/log-YYYYMMDD.txt`
   - **App**: Xcode → Window → Devices and Simulators → View Device Logs

### Problem: "Auto-Discovery findet keinen Server"

**Debugging auf macOS:**

```bash
# mDNS Dienste scannen
dns-sd -B _digitalsignage._tcp

# Sollte Server anzeigen innerhalb 5 Sekunden
```

**Wenn Server nicht erscheint:**
1. Prüfe: Port 5353 UDP offen? (Windows Firewall)
2. Prüfe: Bonjour Service läuft auf Windows?
3. Alternative: Manuelle Eingabe verwenden

### Problem: "Permission Denied" bei Commands

**App hat keine Berechtigungen:**
1. **Server**: Öffne "Mobile Apps" Tab
2. **Finde deine App-Registrierung**
3. **Status**: Sollte "Approved" sein
4. **Permissions**: Mindestens "View" sollte gecheckt sein
5. **Für Commands**: "Control" Permission erforderlich
6. **Für Layout Assignment**: "Manage" Permission erforderlich

### Problem: "Screenshot funktioniert nicht"

**Mögliche Ursachen:**

1. **Raspberry Pi Client läuft nicht**:
   ```bash
   # SSH zum Pi
   ssh pro@192.168.0.178
   sudo systemctl status digitalsignage-client
   ```

2. **Screenshot-Service fehlt**:
   - Prüfe Pi Client Logs: `sudo journalctl -u digitalsignage-client -f`
   - Sollte zeigen: "Screenshot captured"

3. **Timeout** (10 Sekunden):
   - Pi antwortet nicht rechtzeitig
   - Netzwerk zu langsam
   - Pi überlastet

---

## Performance-Tests

### Test 1: Verbindungsaufbau
- **Erwartung**: < 3 Sekunden vom Tap auf Server bis Navigation zu Devices
- **Messen**: Stopuhr oder Xcode Instruments

### Test 2: Device List Refresh
- **Erwartung**: < 2 Sekunden für Pull-to-Refresh
- **Test**: Liste mit 10+ Devices

### Test 3: Screenshot Capture
- **Erwartung**: 2-5 Sekunden
- **Abhängig von**: Netzwerk, Pi Performance

### Test 4: Memory Usage
- **Xcode**: Product → Profile → Leaks
- **Erwartung**: Keine Memory Leaks
- **Test**: 10x hin und her navigieren zwischen Pages

---

## Logs und Debugging

### Server-Logs

```bash
# Auf Windows
cd logs
cat log-YYYYMMDD.txt | grep "Mobile"

# Wichtige Log-Meldungen:
# [INFO] Mobile app registered: iPhone 15 Pro (iOS)
# [INFO] Mobile app connected: <token>
# [INFO] Forwarding command Restart to device <guid>
```

### App-Logs (iOS Simulator)

**In Xcode:**
1. Window → Devices and Simulators
2. Wähle deinen Simulator
3. "Open Console"
4. Filter: `DigitalSignage`

**In VS Code / Rider:**
- Output-Fenster zeigt `dotnet` Logs

### Network Traffic Debug

**Charles Proxy / Wireshark:**
- WebSocket-Verkehr mitschneiden
- Nachrichten zwischen App und Server sehen

---

## Checkliste: Vollständiger Test

- [ ] **Server startet erfolgreich**
- [ ] **mDNS Broadcasting aktiv** (Log-Eintrag sichtbar)
- [ ] **Firewall-Regeln** konfiguriert
- [ ] **Mobile App buildet ohne Fehler**
- [ ] **App startet im Simulator**
- [ ] **Auto-Discovery findet Server** (oder manuelle Eingabe funktioniert)
- [ ] **App-Registrierung** erscheint im Server "Mobile Apps" Tab
- [ ] **Admin genehmigt App** mit Permissions
- [ ] **Device List** zeigt verbundene Pi-Clients
- [ ] **Device Detail Page** öffnet bei Tap auf Device
- [ ] **Hardware Metrics** werden angezeigt
- [ ] **Volume Up/Down** Befehle funktionieren
- [ ] **Screen On/Off** Befehle funktionieren
- [ ] **Screenshot** wird erfolgreich angezeigt
- [ ] **Restart Command** mit Bestätigung funktioniert
- [ ] **Pull-to-Refresh** aktualisiert Device List
- [ ] **Navigation zurück** funktioniert
- [ ] **Keine Crashes** bei 10 Minuten Nutzung

---

## Bekannte Limitierungen

1. **macOS erforderlich**: iOS-Apps können nur auf macOS kompiliert werden
2. **Gleiches Netzwerk**: Auto-Discovery funktioniert nur im gleichen Subnet
3. **SSL-Zertifikat**: Self-Signed Certificate kann Warnungen verursachen (wird akzeptiert)
4. **Screenshot-Format**: Nur PNG unterstützt
5. **Timeout**: Screenshot-Requests haben 10 Sekunden Timeout

---

## Nächste Schritte

Nach erfolgreichem Test:

1. **Layout Assignment** implementieren (Phase 3.9)
2. **Biometric Auth** implementieren (Phase 3.10)
3. **Push Notifications** implementieren (Phase 3.11)
4. **TestFlight** Deployment für Beta-Tester
5. **App Store** Submission

---

## Support

**Bei Problemen:**
1. Prüfe Logs (Server + App)
2. Prüfe Firewall-Einstellungen
3. Prüfe Netzwerk-Verbindung
4. Erstelle Issue auf GitHub: https://github.com/manur84/digitalsignage/issues

**Log-Dateien mitschicken:**
- Server: `logs/log-YYYYMMDD.txt`
- App: Xcode Console Output
- System: `ipconfig` / `ifconfig` Output
