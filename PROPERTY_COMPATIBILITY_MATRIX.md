# Property Compatibility Matrix - Server (C#) ↔ Client (Python)

**Generated:** 2025-11-15
**Purpose:** Comprehensive mapping of property names between Server and Client to prevent rendering failures

---

## 🚨 CRITICAL FINDINGS

### Root Cause Analysis: Shape Elements Not Rendering

**Problem:** Rectangles and circles created in Designer are NOT displaying on Raspberry Pi clients.

**Investigation Steps Taken:**

1. **Property Name Mismatch Detected:**
   - Server sends: `BorderColor`, `BorderThickness`
   - Client expected: `StrokeColor`, `StrokeWidth` (primary) with fallbacks
   - Original client code used `or` operator which may not work correctly with None values

2. **Debug Logging Added:**
   - Comprehensive logging in `create_shape_element()` method
   - Logging in `ShapeWidget.paintEvent()` to track rendering calls
   - Property dictionary inspection to see exact values

3. **Property Retrieval Fixed:**
   - Changed from: `properties.get('StrokeColor') or properties.get('BorderColor', '#000000')`
   - Changed to: Explicit None checks with proper fallback logic

---

## Property Name Mapping Table

### Shape Elements (rectangle, circle, ellipse)

| Server Property (C#) | Client Property 1 (Python) | Client Property 2 (Fallback) | Default Value | Type | Notes |
|---------------------|----------------------------|------------------------------|---------------|------|-------|
| `FillColor` | `FillColor` | - | `#CCCCCC` | string (hex color) | ✅ MATCHES |
| `BorderColor` | `BorderColor` | `StrokeColor` | `#000000` | string (hex color) | **Server sends BorderColor** |
| `BorderThickness` | `BorderThickness` | `StrokeWidth` | `1` | int | **Server sends BorderThickness** |
| `CornerRadius` | `CornerRadius` | `BorderRadius` | `0` | float | Rectangle only |

**Server Code (DesignerViewModel.cs:467-481):**
```csharp
// Rectangle
Properties = new Dictionary<string, object>
{
    ["FillColor"] = "#ADD8E6",        // ✅ Sent as FillColor
    ["BorderColor"] = "#00008B",      // ⚠️ Sent as BorderColor (NOT StrokeColor!)
    ["BorderThickness"] = 2           // ⚠️ Sent as BorderThickness (NOT StrokeWidth!)
}

// Circle
Properties = new Dictionary<string, object>
{
    ["FillColor"] = "#FFD700",        // ✅ Sent as FillColor
    ["BorderColor"] = "#FF8C00",      // ⚠️ Sent as BorderColor (NOT StrokeColor!)
    ["BorderThickness"] = 2           // ⚠️ Sent as BorderThickness (NOT StrokeWidth!)
}
```

**Client Code (display_renderer.py:656-776) - FIXED VERSION:**
```python
# OLD (BROKEN) - Used 'or' operator which doesn't handle None correctly
stroke_color = properties.get('StrokeColor') or properties.get('BorderColor', '#000000')

# NEW (FIXED) - Explicit None checks
stroke_color = properties.get('BorderColor')  # Check server's property name FIRST!
if stroke_color is None:
    stroke_color = properties.get('StrokeColor')  # Fallback to alternative name
    if stroke_color is None:
        stroke_color = '#000000'  # Default value
```

---

### Text Elements

| Server Property (C#) | Client Property (Python) | Default Value | Type | Notes |
|---------------------|--------------------------|---------------|------|-------|
| `Content` | `Content` | `""` | string | ✅ MATCHES |
| `FontFamily` | `FontFamily` | `Arial` | string | ✅ MATCHES |
| `FontSize` | `FontSize` | `24.0` | double | ✅ MATCHES (must be float!) |
| `FontWeight` | `FontWeight` | `Normal` | string | ✅ MATCHES (Normal/Bold) |
| `FontStyle` | `FontStyle` | `Normal` | string | ✅ MATCHES (Normal/Italic) |
| `Color` | `Color` | `#000000` | string (hex color) | ✅ MATCHES |
| `TextAlign` | `TextAlign` | `Left` | string | ✅ MATCHES (Left/Center/Right) |
| `VerticalAlign` | `VerticalAlign` | `Top` | string | ✅ MATCHES (Top/Middle/Bottom) |
| `WordWrap` | `WordWrap` | `True` | boolean | ✅ MATCHES |
| `TextDecoration_Underline` | `TextDecoration_Underline` | `False` | boolean | ✅ MATCHES |
| `TextDecoration_Strikethrough` | `TextDecoration_Strikethrough` | `False` | boolean | ✅ MATCHES |
| `BackgroundColor` | `BackgroundColor` | `Transparent` | string (hex color) | ✅ MATCHES |

---

### Image Elements

| Server Property (C#) | Client Property (Python) | Default Value | Type | Notes |
|---------------------|--------------------------|---------------|------|-------|
| `Source` | `Source` | `""` | string (file path) | ✅ MATCHES (fallback) |
| `MediaData` | `MediaData` | - | string (Base64) | ✅ MATCHES (priority #1) |
| `Stretch` | `Stretch` OR `Fit` | `Uniform` | string | Client uses `Fit` property |

**Priority:** Client checks `MediaData` FIRST (Base64 from server), then falls back to `Source` (file path).

---

### QR Code Elements

| Server Property (C#) | Client Property 1 (Python) | Client Property 2 (Fallback) | Default Value | Type | Notes |
|---------------------|----------------------------|------------------------------|---------------|------|-------|
| `Content` | `Data` | `Content` | `""` | string | Server uses both names |
| `Data` | `Data` | - | `""` | string | Legacy property name |
| `ErrorCorrection` | `ErrorCorrectionLevel` | `ErrorCorrection` | `M` | string | L/M/Q/H |
| `ForegroundColor` | `ForegroundColor` | - | `#000000` | string (hex color) | ✅ MATCHES |
| `BackgroundColor` | `BackgroundColor` | - | `#FFFFFF` | string (hex color) | ✅ MATCHES |

---

### DateTime Elements

| Server Property (C#) | Client Property 1 (Python) | Client Property 2 (Fallback) | Default Value | Type | Notes |
|---------------------|----------------------------|------------------------------|---------------|------|-------|
| `Format` | `Format` | - | `dddd, dd MMMM yyyy HH:mm:ss` | string | C# format → Python strftime |
| `FontFamily` | `FontFamily` | - | `Arial` | string | ✅ MATCHES |
| `FontSize` | `FontSize` | - | `24.0` | double | ✅ MATCHES |
| `Color` | `Color` | - | `#000000` | string (hex color) | ✅ MATCHES |
| `UpdateInterval` | `UpdateInterval` | - | `1000` | int (milliseconds) | ✅ MATCHES |
| `TextAlign` | `TextAlignment` | `TextAlign` | `Center` | string | Server uses TextAlignment |

---

### Table Elements

| Server Property (C#) | Client Property (Python) | Default Value | Type | Notes |
|---------------------|--------------------------|---------------|------|-------|
| `Rows` | `Rows` | `3` | int | ✅ MATCHES |
| `Columns` | `Columns` | `3` | int | ✅ MATCHES |
| `ShowHeaderRow` | `ShowHeaderRow` | `True` | boolean | ✅ MATCHES |
| `ShowHeaderColumn` | `ShowHeaderColumn` | `False` | boolean | ✅ MATCHES |
| `BorderColor` | `BorderColor` | `#000000` | string (hex color) | ✅ MATCHES |
| `BorderThickness` | `BorderThickness` | `1` | int | ✅ MATCHES |
| `BackgroundColor` | `BackgroundColor` | `#FFFFFF` | string (hex color) | ✅ MATCHES |
| `AlternateRowColor` | `AlternateRowColor` | `#F5F5F5` | string (hex color) | ✅ MATCHES |
| `HeaderBackgroundColor` | `HeaderBackgroundColor` | `#CCCCCC` | string (hex color) | ✅ MATCHES |
| `TextColor` | `TextColor` | `#000000` | string (hex color) | ✅ MATCHES |
| `FontFamily` | `FontFamily` | `Arial` | string | ✅ MATCHES |
| `FontSize` | `FontSize` | `14.0` | double | ✅ MATCHES |
| `CellPadding` | `CellPadding` | `5` | int | ✅ MATCHES |
| `CellData` | `CellData` | `[]` | string (JSON) | ✅ MATCHES |

---

### DataGrid Elements (SQL Data Sources)

| Server Property (C#) | Client Property (Python) | Default Value | Type | Notes |
|---------------------|--------------------------|---------------|------|-------|
| `DataSourceId` | `DataSourceId` | `Guid.Empty` | string (GUID) | ✅ MATCHES |
| `RowsPerPage` | `RowsPerPage` | `10` | int | ✅ MATCHES |
| `ShowHeader` | `ShowHeader` | `True` | boolean | ✅ MATCHES |
| `AutoScroll` | `AutoScroll` | `False` | boolean | ✅ MATCHES |
| `ScrollInterval` | `ScrollInterval` | `5` | int (seconds) | ✅ MATCHES |
| `HeaderBackgroundColor` | `HeaderBackgroundColor` | `#2196F3` | string (hex color) | ✅ MATCHES |
| `HeaderTextColor` | `HeaderTextColor` | `#FFFFFF` | string (hex color) | ✅ MATCHES |
| `RowBackgroundColor` | `RowBackgroundColor` | `#FFFFFF` | string (hex color) | ✅ MATCHES |
| `AlternateRowColor` | `AlternateRowColor` | `#F5F5F5` | string (hex color) | ✅ MATCHES |

---

### Common Properties (All Element Types)

| Server Property (C#) | Client Property (Python) | Default Value | Type | Notes |
|---------------------|--------------------------|---------------|------|-------|
| `Opacity` | `Opacity` | `1.0` | double (0.0-1.0) | ✅ MATCHES |
| `Rotation` | `Rotation` | `0.0` | double (degrees) | ✅ MATCHES (not fully supported in client) |
| `Visible` | `Visible` | `True` | boolean | ✅ MATCHES (element-level) |
| `IsVisible` | `IsVisible` | `True` | boolean | Property-level visibility |
| `IsLocked` | `IsLocked` | `False` | boolean | Designer-only (not sent to client) |
| `EnableShadow` | `EnableShadow` | `False` | boolean | ✅ MATCHES |
| `HasShadow` | `HasShadow` | `False` | boolean | Alternative property name |
| `ShadowBlur` | `ShadowBlur` | `5.0` | double | ✅ MATCHES |
| `ShadowBlurRadius` | `ShadowBlurRadius` | `5.0` | double | Alternative property name |
| `ShadowColor` | `ShadowColor` | `#000000` | string (hex color) | ✅ MATCHES |
| `ShadowOffsetX` | `ShadowOffsetX` | `5` | double | ✅ MATCHES |
| `ShadowOffsetY` | `ShadowOffsetY` | `5` | double | ✅ MATCHES |

---

## 🔧 Deployment & Testing Instructions

### Step 1: Deploy to Raspberry Pi

```bash
# SSH to Raspberry Pi
sshpass -p 'mr412393' ssh pro@192.168.0.178

# Navigate to repository (in home directory, NOT /opt!)
cd ~/digitalsignage

# Pull latest changes
git pull

# Update client installation
cd src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh  # Auto-detects UPDATE mode

# Restart service
sudo systemctl restart digitalsignage-client
```

### Step 2: Collect Logs

```bash
# Real-time logs (Ctrl+C to exit)
sudo journalctl -u digitalsignage-client -f

# Last 200 lines (no pager)
sudo journalctl -u digitalsignage-client -n 200 --no-pager
```

### Step 3: Look for Debug Messages

Search for these patterns in logs:
- `=== CREATE SHAPE ELEMENT DEBUG ===` - Shape creation started
- `Properties dict:` - See ALL properties sent from server
- `BorderColor from properties:` - Verify BorderColor is present
- `BorderThickness from properties:` - Verify BorderThickness is present
- `=== ShapeWidget.paintEvent CALLED ===` - Verify painting is happening
- `Drawing rectangle` / `Drawing circle` - Verify actual drawing commands

### Step 4: Test Scenarios

1. **Create Rectangle in Designer:**
   - Add Rectangle element
   - Set FillColor = Blue (#0000FF)
   - Set BorderColor = Red (#FF0000)
   - Set BorderThickness = 5
   - Save layout
   - Send to Raspberry Pi device
   - **Expected:** Logs show all properties, widget created, paintEvent called, blue rectangle with red border visible on HDMI monitor

2. **Create Circle in Designer:**
   - Add Circle element
   - Set FillColor = Yellow (#FFFF00)
   - Set BorderColor = Black (#000000)
   - Set BorderThickness = 3
   - Save layout
   - Send to Raspberry Pi device
   - **Expected:** Logs show all properties, widget created, paintEvent called, yellow circle with black border visible on HDMI monitor

---

## 🔍 Known Issues & Solutions

### Issue 1: Property Name Mismatch (FIXED)

**Symptoms:** Shapes not rendering, default colors used
**Cause:** Client checking for `StrokeColor`/`StrokeWidth` but server sends `BorderColor`/`BorderThickness`
**Solution:** Client now checks `BorderColor` FIRST, then fallback to `StrokeColor`

### Issue 2: Python `or` Operator with None (FIXED)

**Symptoms:** Fallback logic not working
**Cause:** `properties.get('X') or properties.get('Y')` doesn't work correctly when X key exists but value is None
**Solution:** Explicit `if prop is None` checks

### Issue 3: Widget Not Visible (POTENTIALLY FIXED)

**Symptoms:** Widget created but not displayed
**Cause:** Missing `show()` call or widget hidden by default
**Solution:** Explicit `widget.setVisible(True)`, `widget.setEnabled(True)`, `widget.show()` calls added

---

## 📊 Verification Checklist

After deploying:
- [ ] Logs show `=== CREATE SHAPE ELEMENT DEBUG ===` for rectangles
- [ ] Logs show `=== CREATE SHAPE ELEMENT DEBUG ===` for circles
- [ ] Logs show `BorderColor from properties: #XXXXXX` (actual color)
- [ ] Logs show `BorderThickness from properties: N` (actual number)
- [ ] Logs show `FillColor from properties: #XXXXXX` (actual color)
- [ ] Logs show `=== SHAPE CREATED SUCCESSFULLY ===`
- [ ] Logs show `=== ShapeWidget.paintEvent CALLED ===`
- [ ] Logs show `Drawing rectangle` or `Drawing circle`
- [ ] HDMI monitor shows rectangle with correct colors
- [ ] HDMI monitor shows circle with correct colors

---

## 🚀 Next Steps After Testing

1. **If logs show properties correctly but shapes still don't render:**
   - Check Qt rendering pipeline
   - Verify parent widget hierarchy
   - Check Z-index/layer ordering
   - Verify widget isn't being destroyed immediately

2. **If logs show missing properties:**
   - Check JSON serialization on server
   - Verify layout save/load preserves properties
   - Check WebSocket message structure

3. **If paintEvent is not being called:**
   - Check widget visibility flags
   - Verify widget is added to parent
   - Check if widget size is zero
   - Verify update() is called after property changes

---

**END OF DOCUMENT**
