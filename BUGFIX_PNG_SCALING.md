# PNG Layout Scaling Fix

**Datum:** 2025-01-XX  
**Bug:** PNG-Layouts werden zu klein angezeigt  
**Status:** ? BEHOBEN

---

## Problem

### Symptome:
```
2025-11-17 22:20:09,779 ERROR Failed to render PNG layout: wrapped C/C++ object of type QLabel has been deleted
2025-11-17 22:20:09,780 INFO Rendering 0 elements with scaling factors X=0.3333, Y=0.4444
2025-11-17 22:20:09,781 INFO ? Layout 'mainlogo' rendered: 0 elements created, 1 failed
2025-11-17 22:20:09,782 INFO All elements scaled to client display resolution 640x480
```

### Ursachen:

1. **"wrapped C/C++ object has been deleted" Error**
   - `self._png_label` wurde durch `deleteLater()` gelöscht
   - Referenz existierte noch, aber C++ Objekt war weg
   - Beim zweiten `render_layout()` Aufruf ? Crash

2. **PNG zu klein angezeigt**
   - Ursprüngliche Skalierung: `Qt.IgnoreAspectRatio`
   - Streckt/Quetscht das Bild auf exakte Display-Größe
   - Verzerrt Aspect Ratio ? Bild sieht falsch aus
   - Bei 640x480 Display: Bild wurde gestaucht

3. **Fehlende Referenz-Verwaltung**
   - `self._png_label` wurde nicht auf `None` gesetzt nach `deleteLater()`
   - Alte Referenzen blieben im Speicher
   - Nächster Render-Versuch ? Zugriff auf gelöschtes Objekt

---

## Lösung

### 1. Bessere Referenz-Verwaltung

**Datei:** `src/DigitalSignage.Client.RaspberryPi/display_renderer.py`

**Vorher:**
```python
# 2. Delete all tracked elements
for element in self.elements:
    element.hide()
    element.deleteLater()
self.elements.clear()

# Problem: self._png_label ist noch referenziert, aber C++ Objekt ist weg!
```

**Nachher:**
```python
# 2. Delete all tracked elements INCLUDING PNG label
for element in self.elements:
    element.hide()
    element.deleteLater()
self.elements.clear()

# CRITICAL FIX: Reset PNG label references after cleanup
self._png_label = None
self._png_pixmap = None
self._rendering_png_only = False
```

**Effekt:**
- ? Referenzen werden korrekt gecleart
- ? Kein "wrapped C/C++ object deleted" Error mehr
- ? Neues Label wird beim nächsten Render erstellt

---

### 2. Korrekte PNG-Skalierung

**Vorher:**
```python
scaled = self._png_pixmap.scaled(
    self.width(), 
    self.height(), 
    Qt.IgnoreAspectRatio,  # ? Streckt Bild, verzerrt Aspect Ratio
    Qt.SmoothTransformation
)
self._png_label.setScaledContents(True)  # ? Streckt nochmal
```

**Nachher:**
```python
# CRITICAL FIX: Always create NEW label for PNG rendering
self._png_label = QLabel(parent=self)
self._png_label.setGeometry(0, 0, self.width(), self.height())

# Use KeepAspectRatioByExpanding to maintain aspect ratio
scaled = self._png_pixmap.scaled(
    self.width(), 
    self.height(), 
    Qt.KeepAspectRatioByExpanding,  # ? Behält Aspect Ratio, füllt Screen
    Qt.SmoothTransformation
)

self._png_label.setPixmap(scaled)
self._png_label.setAlignment(Qt.AlignCenter)  # ? Zentriert Bild
self._png_label.setScaledContents(False)  # ? Kein weiteres Stretchen

logger.info(f"? PNG layout rendered: "
          f"original={self._png_pixmap.width()}x{self._png_pixmap.height()}, "
          f"scaled={scaled.width()}x{scaled.height()}, "
          f"display={self.width()}x{self.height()}")
```

**Effekt:**
- ? Aspect Ratio wird beibehalten
- ? Bild füllt den gesamten Screen
- ? Überschüssige Bereiche werden abgeschnitten (nicht gestreckt)
- ? Detailliertes Logging über Skalierung

---

### 3. Verbessertes Resize-Handling

**Datei:** `src/DigitalSignage.Client.RaspberryPi/display_renderer.py`

**Vorher:**
```python
def resizeEvent(self, event):
    if self._png_label and self._png_pixmap:
        scaled = self._png_pixmap.scaled(
            self.width(), self.height(),
            Qt.IgnoreAspectRatio,  # ? Verzerrt bei Resize
            Qt.SmoothTransformation
        )
        self._png_label.setPixmap(scaled)
```

**Nachher:**
```python
def resizeEvent(self, event):
    if hasattr(self, "_png_label") and self._png_label and self._png_pixmap:
        try:
            # Use KeepAspectRatioByExpanding
            scaled = self._png_pixmap.scaled(
                self.width(), 
                self.height(), 
                Qt.KeepAspectRatioByExpanding,  # ? Behält Aspect Ratio
                Qt.SmoothTransformation
            )
            self._png_label.setGeometry(0, 0, self.width(), self.height())
            self._png_label.setPixmap(scaled)
            self._png_label.setAlignment(Qt.AlignCenter)
            
            logger.info(f"? PNG layout rescaled: "
                      f"original={self._png_pixmap.width()}x{self._png_pixmap.height()}, "
                      f"scaled={scaled.width()}x{scaled.height()}, "
                      f"display={self.width()}x{self.height()}")
        except Exception as e:
            logger.error(f"Failed to rescale PNG layout: {e}")
            # Reset references on error
            self._png_label = None
            self._png_pixmap = None
```

**Effekt:**
- ? Robustes Error-Handling
- ? Referenzen werden bei Fehler gecleart
- ? `hasattr()` Check verhindert Attribute-Errors
- ? Aspect Ratio bleibt auch bei Resize erhalten

---

## Qt Scaling-Modi Erklärung

### `Qt.IgnoreAspectRatio` (VORHER - FALSCH)
```
Original: 1920x1080 (16:9)
Display:  640x480   (4:3)

Result: 640x480 (gestreckt/gestaucht)
```
- ? Bild wird auf exakte Größe gezwungen
- ? Aspect Ratio wird NICHT beibehalten
- ? Verzerrung bei unterschiedlichen Ratios

### `Qt.KeepAspectRatio` (ALTERNATIV - OK)
```
Original: 1920x1080 (16:9)
Display:  640x480   (4:3)

Result: 640x360 (zentriert, schwarze Balken oben/unten)
```
- ? Aspect Ratio wird beibehalten
- ?? Schwarze Balken (letterboxing)
- ?? Nutzt nicht den ganzen Screen

### `Qt.KeepAspectRatioByExpanding` (NACHHER - PERFEKT)
```
Original: 1920x1080 (16:9)
Display:  640x480   (4:3)

Result: 853x480 ? cropped to 640x480 (zentriert, Seiten abgeschnitten)
```
- ? Aspect Ratio wird beibehalten
- ? Füllt den gesamten Screen
- ? Überschuss wird abgeschnitten (nicht gestreckt)
- ? Kein Letterboxing

---

## Log-Beispiele

### Vorher (FEHLER):
```
2025-11-17 22:20:09,779 ERROR Failed to render PNG layout: wrapped C/C++ object of type QLabel has been deleted
2025-11-17 22:20:09,781 INFO ? Layout 'mainlogo' rendered: 0 elements created, 1 failed
```

### Nachher (ERFOLG):
```
======================================================================
LAYOUT SCALING ANALYSIS
======================================================================
Layout Design Resolution: 1920x1080
Client Display Resolution: 640x480
Scale Factors: X=0.3333, Y=0.4444
? SCALING REQUIRED: Layout will be scaled to fit display!
======================================================================
? PNG layout rendered: original=1920x1080, scaled=853x480, display=640x480
? Layout 'mainlogo' rendered: 1 elements created, 0 failed
   All elements scaled to client display resolution 640x480
```

**Erklärung:**
- Original PNG: 1920x1080 (16:9)
- Display: 640x480 (4:3)
- Scaled: 853x480 (behält 16:9 Ratio)
- Display zeigt: Zentriert mit Seiten abgeschnitten

---

## Testing

### Test 1: PNG Layout auf 640x480 Display
```python
# Layout: 1920x1080 PNG
# Display: 640x480

Erwartetes Ergebnis:
? PNG wird auf 853x480 skaliert (16:9 Ratio beibehalten)
? Zentriert angezeigt, Seiten werden abgeschnitten
? Kein "wrapped C/C++ object deleted" Error
? Logs zeigen Skalierungs-Details
```

### Test 2: Mehrfaches Render (Cache)
```python
# 1. Render Layout (initial)
# 2. Render Layout (aus Cache)

Erwartetes Ergebnis:
? Beide Render-Vorgänge erfolgreich
? Keine "wrapped object deleted" Errors
? Referenzen werden korrekt gecleart
? PNG ist bei beiden Malen korrekt skaliert
```

### Test 3: Window Resize
```python
# 1. Render Layout bei 640x480
# 2. Resize Window auf 1024x768

Erwartetes Ergebnis:
? PNG wird automatisch neu skaliert
? Aspect Ratio bleibt erhalten
? Logs zeigen neue Skalierung
? Kein Memory Leak
```

---

## Bekannte Einschränkungen

### 1. Überschuss wird abgeschnitten
- Bei unterschiedlichen Aspect Ratios (Layout vs. Display)
- Wird der Überschuss abgeschnitten (cropped)
- **Lösung:** Layout im Designer in Ziel-Aspect-Ratio erstellen

### 2. Keine Letterboxing-Option
- Aktuell: `KeepAspectRatioByExpanding` (crop)
- Alternative: `KeepAspectRatio` (schwarze Balken)
- **Zukunft:** Config-Option hinzufügen

### 3. Performance bei sehr großen PNGs
- Skalierung von 4K+ PNGs auf kleine Displays
- Kann bei Raspberry Pi 3 langsam sein
- **Lösung:** PNG vorher serverseitig skalieren

---

## Zusammenfassung

### Behobene Probleme:
1. ? "wrapped C/C++ object deleted" Error
2. ? PNG zu klein/verzerrt angezeigt
3. ? Fehlende Referenz-Verwaltung
4. ? Schlechte Skalierungs-Qualität

### Verbesserungen:
1. ? Robustes Label-Management
2. ? Korrekte Aspect-Ratio-Beibehaltung
3. ? Detailliertes Skalierungs-Logging
4. ? Besseres Error-Handling

### Performance:
- Vorher: Crash beim zweiten Render
- Nachher: Flüssig, keine Crashes, korrekte Skalierung

---

**Erstellt:** 2025-01-XX  
**Autor:** GitHub Copilot  
**Bug-ID:** PNG-SCALING-001  
