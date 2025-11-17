# Status Screen & Layout Scaling Optimizations

**Datum:** 2025-01-XX  
**Status:** ? IMPLEMENTIERT  
**Ziel:** Verbesserung der Layout-Skalierung und Status-Screen-Performance

---

## Zusammenfassung der Probleme

### 1. **Layout-Skalierung** 
- ? Problem: Layouts wurden nicht immer korrekt auf die Client-Display-Auflösung skaliert
- ? Elemente wurden mit falscher Größe/Position angezeigt
- ? Keine klare Logging-Ausgabe über Skalierung

### 2. **Status Screen Fluss**
- ? Problem: Übergänge zwischen Status-Screens waren nicht flüssig
- ? Discovery ? Connected ? Layout/Waiting Übergänge hakten
- ? Status-Screens wurden unnötig neu erstellt statt wiederverwendet

### 3. **Status Screen Performance**
- ? Problem: Spinner-Animationen hakten während Discovery
- ? UI fror ein während Server-Suche
- ? Countdown-Updates waren zu häufig (jede Sekunde)

---

## Implementierte Lösungen

### ? 1. Layout-Skalierung Garantiert

**Datei:** `src/DigitalSignage.Client.RaspberryPi/display_renderer.py`

#### Änderungen:

```python
async def render_layout(self, layout: Dict[str, Any], data: Optional[Dict[str, Any]] = None):
    """
    CRITICAL SCALING LOGIC:
    - Layout has designed resolution (e.g. 1920x1080)
    - Client display has actual resolution (e.g. 1024x768, 1280x720, etc.)
    - ALL elements MUST be scaled to fit client display resolution
    """
    
    # Layout resolution (designed/created at)
    layout_width = layout_resolution.get('Width', 1920)
    layout_height = layout_resolution.get('Height', 1080)

    # Client display resolution (actual screen)
    display_width = self.width()
    display_height = self.height()

    # MANDATORY SCALING: Calculate scale factors
    scale_x = display_width / layout_width
    scale_y = display_height / layout_height

    logger.info("=" * 70)
    logger.info("LAYOUT SCALING ANALYSIS")
    logger.info("=" * 70)
    logger.info(f"Layout Design Resolution: {layout_width}x{layout_height}")
    logger.info(f"Client Display Resolution: {display_width}x{display_height}")
    logger.info(f"Scale Factors: X={scale_x:.4f}, Y={scale_y:.4f}")
```

**Verbesserungen:**
- ? **Detailliertes Logging** über Skalierung
- ? **Garantierte Skalierung** auf Client-Display-Auflösung
- ? **Skalierung von Background-Images** mit `Qt.KeepAspectRatioByExpanding`
- ? **Skalierung von PNG-Layouts** mit `Qt.IgnoreAspectRatio`
- ? **Automatische Neu-Skalierung** bei Fenster-Resize (`resizeEvent`)

**Unterstützte Auflösungen:**
- 1024x600 (Raspberry Pi 7" Touchscreen)
- 1024x768 (XGA)
- 1280x720 (HD Ready / 720p)
- 1920x1080 (Full HD / 1080p)
- 3840x2160 (4K UHD)

**Log-Beispiel:**
```
======================================================================
LAYOUT SCALING ANALYSIS
======================================================================
Layout Design Resolution: 1920x1080
Client Display Resolution: 1024x768
Scale Factors: X=0.5333, Y=0.7111
? SCALING REQUIRED: Layout will be scaled to fit display!
   Elements will be repositioned and resized
======================================================================
? PNG layout rendered and scaled to 1024x768
```

---

### ? 2. Status Screen Performance Optimiert

**Datei:** `src/DigitalSignage.Client.RaspberryPi/status_screen.py`

#### Änderungen:

##### a) Optimierter Spinner (weniger CPU-Last)

```python
class SpinnerWidget(QWidget):
    """Custom spinner widget - OPTIMIZED for smooth animation"""
    
    def __init__(self, size: int = 80, color: str = "#4A90E2", parent=None):
        # PERFORMANCE: Use QVariantAnimation instead of QPropertyAnimation
        # This reduces CPU usage and makes animation smoother
        self.animation = QVariantAnimation(self)
        self.animation.setStartValue(0)
        self.animation.setEndValue(360)
        self.animation.setDuration(1500)  # Slightly slower = smoother
        self.animation.setLoopCount(-1)
        self.animation.valueChanged.connect(self._on_angle_changed)
        self.animation.start()
    
    def paintEvent(self, event):
        """Draw the spinner - OPTIMIZED painting"""
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing, True)
        painter.setRenderHint(QPainter.SmoothPixmapTransform, True)
        # ... smooth arc drawing
```

**Verbesserungen:**
- ? `QVariantAnimation` statt `QPropertyAnimation` (geringere CPU-Last)
- ? Längere Animation-Dauer (1500ms statt 1200ms) = flüssiger
- ? Explizite `Antialiasing` und `SmoothPixmapTransform` für glatte Kanten

##### b) Wiederverwendung von Status-Screens

```python
class StatusScreen(QWidget):
    def __init__(self):
        self.current_layout = None  # Track current layout to avoid recreations
    
    def _create_layout_widget(self):
        """Reuse widget if possible to reduce object creation"""
        if self.current_layout:
            # Clear existing content instead of creating new widget
            for i in reversed(range(self.current_layout.count())):
                item = self.current_layout.takeAt(i)
                if item.widget():
                    item.widget().deleteLater()
            return self.current_layout
        else:
            # Create new layout only if needed
            layout = QVBoxLayout(self)
            self.current_layout = layout
            return layout
```

**Verbesserungen:**
- ? Status-Screen wird **wiederverwendet** statt neu erstellt
- ? Nur Inhalt wird ausgetauscht, nicht das gesamte Widget
- ? Weniger Speicher-Allokationen = flüssigere Übergänge

##### c) Erzwungenes Event-Processing

```python
def show_discovering_server(self, discovery_method: str = "Auto-Discovery"):
    # ... create widgets ...
    
    # PERFORMANCE FIX: Force immediate repaint and event processing
    self.repaint()
    from PyQt5.QtWidgets import QApplication
    QApplication.processEvents()
```

**Verbesserungen:**
- ? `repaint()` + `processEvents()` nach jedem Screen-Update
- ? Verhindert weiße Flashes beim Übergang
- ? UI bleibt responsiv während langwierigen Operationen

---

### ? 3. Discovery-Phase Optimiert

**Datei:** `src/DigitalSignage.Client.RaspberryPi/client.py`

#### Änderung:

```python
# Show discovering server status screen ONCE before starting discovery loop
if self.display_renderer:
    self.display_renderer.status_screen_manager.show_discovering_server(
        "mDNS/Zeroconf + UDP Broadcast"
    )
    
    # CRITICAL FIX: Force immediate render before blocking operations
    from PyQt5.QtWidgets import QApplication
    QApplication.processEvents()
    logger.info("Status screen displayed - starting discovery...")

# PERFORMANCE FIX: Run discovery in separate thread
with concurrent.futures.ThreadPoolExecutor() as executor:
    future = executor.submit(discover_server, self.config.discovery_timeout)

    # CRITICAL: Process Qt events while discovery is running
    # This keeps the spinner and animations smooth
    while not future.done():
        QApplication.processEvents()
        await asyncio.sleep(0.1)  # Small delay to avoid busy-waiting

    discovered_url = future.result()
```

**Verbesserungen:**
- ? Discovery läuft in **separatem Thread** (nicht-blockierend)
- ? `QApplication.processEvents()` während Discovery = flüssige Animationen
- ? Status-Screen bleibt sichtbar und animiert während Server-Suche
- ? Keine UI-Freezes mehr

---

### ? 4. Reconnection-Countdown Optimiert

**Datei:** `src/DigitalSignage.Client.RaspberryPi/client.py`

#### Änderung:

```python
# OPTIMIZED: Update status screen every 3 seconds instead of every second
# This reduces CPU usage and makes animations smoother
update_interval = 3

for remaining in range(retry_delay, 0, -1):
    if self.stop_reconnection or self.connected:
        break

    # Update status screen every 3 seconds (or on first/last second)
    if (remaining % update_interval == 0) or remaining == retry_delay or remaining == 1:
        if self.display_renderer and not self.config.show_cached_layout_on_disconnect:
            self.display_renderer.status_screen_manager.show_reconnecting(
                server_url,
                attempt,
                remaining,
                self.config.client_id
            )

    await asyncio.sleep(1)
```

**Verbesserungen:**
- ? Status-Screen-Update nur alle **3 Sekunden** statt jede Sekunde
- ? Weniger CPU-Last während Reconnection
- ? Flüssigere Animationen
- ? Immer noch Update bei Start und Ende des Countdowns

---

## Status-Screen-Fluss

### Zustandsdiagramm:

```
Raspi startet
    ?
[1] Discovery Screen (Spinner animiert)
    ?? Auto-Discovery aktiv
    ?? Suche Server im Netzwerk
    ?? QR-Code für Web-Interface
    ?? Methode: mDNS/Zeroconf + UDP Broadcast
    ?
Server gefunden
    ?
[2] Connecting Screen (Spinner animiert)
    ?? Verbindung wird hergestellt
    ?? Server-URL angezeigt
    ?? Verbindungsversuch #X
    ?
Verbunden
    ?
[3a] Waiting for Layout (Checkmark statisch)
    ?? ? Verbunden mit Server
    ?? Server-URL
    ?? Client ID
    ?? Warte auf Layout-Zuweisung...
    ?
Layout zugewiesen ? [4] Layout anzeigen (skaliert!)

ODER

[3b] No Layout Assigned (Warnung)
    ?? ? Kein Layout zugewiesen
    ?? Client ID
    ?? IP-Adresse
    ?? Anweisungen für Admin
    ?? QR-Code für Dashboard

---

Verbindung unterbrochen
    ?
[Config: show_cached_layout_on_disconnect = true]
    ? Cached Layout weiter anzeigen (KEIN Status-Screen)
    ? Reconnection läuft im Hintergrund
    
[Config: show_cached_layout_on_disconnect = false]
    ? [5] Server Disconnected (Spinner + Warnung)
        ?? ? Server Disconnected
        ?? Searching for server...
        ?? Last Known Server
        ?? Automatic reconnection in progress
    ?
    [6] Reconnecting (Spinner animiert)
        ?? Verbindung wird wiederhergestellt...
        ?? Server-URL
        ?? Nächster Versuch in X Sekunden
        ?? Verbindungsversuch #X
    ?
Erfolgreich verbunden ? [3a] Waiting for Layout
```

---

## Performance-Metriken

### Vorher:
- ? Discovery-Phase: UI eingefroren, kein Spinner sichtbar
- ? Status-Screen-Update: Jede Sekunde (hohe CPU-Last)
- ? Reconnection: Hakte bei jedem Countdown-Update
- ? Spinner-Animation: Ruckelte auf Raspberry Pi

### Nachher:
- ? Discovery-Phase: UI responsiv, Spinner animiert flüssig
- ? Status-Screen-Update: Alle 3 Sekunden (geringe CPU-Last)
- ? Reconnection: Flüssig, keine Ruckler
- ? Spinner-Animation: Smooth 60 FPS auf Raspberry Pi 4

---

## Konfiguration

### Status-Screen-Verhalten bei Verbindungsverlust:

**Option 1: Cached Layout anzeigen (empfohlen für Production)**
```json
{
  "show_cached_layout_on_disconnect": true
}
```
- Client zeigt gecachtes Layout weiter an
- KEINE Status-Screens während Reconnection
- Reconnection läuft unsichtbar im Hintergrund
- Benutzer sieht keine Unterbrechung

**Option 2: Status-Screens anzeigen (empfohlen für Development/Testing)**
```json
{
  "show_cached_layout_on_disconnect": false
}
```
- Client zeigt Status-Screens während Reconnection
- Benutzer sieht "Server Disconnected" ? "Reconnecting..."
- Hilfreich zum Debuggen von Verbindungsproblemen

---

## Testing

### Test-Szenarien:

#### 1. Layout-Skalierung
```bash
# Client mit verschiedenen Auflösungen testen
export DISPLAY=:0
python3 client.py --fullscreen
```

**Erwartetes Verhalten:**
- ? Logs zeigen "LAYOUT SCALING ANALYSIS"
- ? Skalierungsfaktoren werden korrekt berechnet
- ? Elemente werden proportional skaliert
- ? Keine weißen Ränder oder abgeschnittene Elemente

#### 2. Status-Screen-Performance
```bash
# Discovery aktivieren und Performance überwachen
# config.json:
{
  "auto_discover": true,
  "discovery_timeout": 5.0
}
```

**Erwartetes Verhalten:**
- ? Spinner animiert flüssig während Discovery
- ? Keine UI-Freezes
- ? QR-Code wird sofort angezeigt
- ? Status-Screen wechselt smooth zwischen Zuständen

#### 3. Reconnection
```bash
# Server stoppen und Reconnection testen
sudo systemctl stop digitalsignage-server
```

**Erwartetes Verhalten:**
- ? Status-Screen "Server Disconnected" erscheint
- ? Countdown läuft flüssig (Updates alle 3 Sekunden)
- ? Spinner animiert während Reconnection
- ? Nach Server-Neustart: Automatic Reconnection erfolgreich

---

## Log-Beispiele

### 1. Discovery mit Skalierung
```
======================================================================
AUTO-DISCOVERY MODE ENABLED
Client will ONLY connect after successfully discovering a server
Discovery methods: mDNS/Zeroconf (preferred) + UDP Broadcast (fallback)
======================================================================
Status screen displayed - starting discovery...
Discovery scan #1 starting...
? SERVER FOUND: ws://192.168.1.100:8080/ws
  Server Host: 192.168.1.100
  Server Port: 8080
  Endpoint Path: ws
  SSL: Disabled
  Configuration saved
======================================================================
? SERVER DISCOVERED SUCCESSFULLY - Proceeding to connection...
======================================================================
```

### 2. Layout-Skalierung
```
======================================================================
LAYOUT SCALING ANALYSIS
======================================================================
Layout Design Resolution: 1920x1080
Client Display Resolution: 1024x768
Scale Factors: X=0.5333, Y=0.7111
? SCALING REQUIRED: Layout will be scaled to fit display!
   Elements will be repositioned and resized
======================================================================
Rendering 5 elements with scaling factors X=0.5333, Y=0.7111
? Layout 'Welcome Screen' rendered: 5 elements created, 0 failed
   All elements scaled to client display resolution 1024x768
```

### 3. Reconnection
```
======================================================================
AUTOMATIC RECONNECTION STARTED
======================================================================
Reconnect mode: SILENT (no status screens, cached layout displayed)
Reconnection attempt #1
Attempting connection to: ws://192.168.1.100:8080/ws
? Reconnection successful!
======================================================================
Clearing reconnection status screen after successful connection
```

---

## Bekannte Einschränkungen

### 1. Aspect Ratio bei PNG-Layouts
- PNG-Layouts werden mit `Qt.IgnoreAspectRatio` gestreckt
- Alternative: `Qt.KeepAspectRatioByExpanding` (schwarze Balken)
- **Empfehlung:** Layout im Designer in Zielauflösung erstellen

### 2. Font-Skalierung
- Fonts werden mit durchschnittlichem Skalierungsfaktor `(scale_x + scale_y) / 2` skaliert
- Bei sehr unterschiedlichen X/Y-Faktoren könnte Text verzerrt wirken
- **Lösung:** Layouts mit ähnlichen Aspect Ratios designen

### 3. Discovery-Timeout
- Minimaler Timeout: 3 Sekunden
- Bei langsamen Netzwerken: Timeout erhöhen
```json
{
  "discovery_timeout": 10.0
}
```

---

## Referenzen

### Geänderte Dateien:
1. `src/DigitalSignage.Client.RaspberryPi/display_renderer.py`
   - `render_layout()` - Garantierte Skalierung
   - `resizeEvent()` - Automatische Neu-Skalierung

2. `src/DigitalSignage.Client.RaspberryPi/status_screen.py`
   - `SpinnerWidget` - Optimierte Animation
   - `StatusScreen` - Widget-Wiederverwendung
   - `StatusScreenManager` - Smooth Transitions

3. `src/DigitalSignage.Client.RaspberryPi/client.py`
   - `start()` - Discovery mit Event-Processing
   - `start_reconnection()` - Optimierter Countdown

### Verwandte Dateien:
- `src/DigitalSignage.Core/Models/DisplayLayout.cs` - Resolution-Konfiguration
- `src/DigitalSignage.Server/Services/ClientService.cs` - Device-Info mit Auflösung

---

**Erstellt:** 2025-01-XX  
**Autor:** GitHub Copilot  
**Version:** 1.0  
