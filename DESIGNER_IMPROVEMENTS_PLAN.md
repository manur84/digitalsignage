# Designer Improvement Plan - Moderne & Professionelle Features

## 🎯 ZIEL
Den Designer auf das Niveau von Canva, Figma, Adobe XD bringen

## 📋 FEATURE-LISTE (Priorisiert)

### 🔴 PHASE 1: Smart Guides & Alignment (High Impact - 8-12h)
1. ✅ **Smart Guides (Alignment Guides)**
   - Automatische Hilfslinien beim Verschieben
   - Snap-to-Guide (Magnet-Effekt)
   - Abstand-Anzeigen zwischen Elementen
   - Zentrale Ausrichtungshilfen (Canvas-Mitte)
   - Visuelle Guide-Lines (gestrichelt, rot/magenta)

2. ✅ **Snap-to-Element**
   - An anderen Elementen einrasten
   - An Canvas-Rändern einrasten
   - Snap-Threshold konfigurierbar (5-10px)

3. ✅ **Alignment Commands**
   - Align Left (alle selektierten Elemente)
   - Align Right
   - Align Top
   - Align Bottom
   - Center Horizontal
   - Center Vertical
   - Center Both (H+V)

4. ✅ **Distribution Commands**
   - Distribute Horizontal (gleichmäßige Abstände)
   - Distribute Vertical

5. ✅ **Alignment Toolbar**
   - Icon-Buttons für alle Alignment-Funktionen
   - Unterhalb der Tool Palette
   - Tooltips mit Shortcuts

### 🟡 PHASE 2: Element Grouping (8-10h)
1. ✅ **Group/Ungroup Commands**
   - Group Selected Elements (Ctrl+G)
   - Ungroup (Ctrl+Shift+G)
   - Group-Hierarchie (verschachtelt möglich)

2. ✅ **Group Management**
   - Gruppen im Layer Panel mit Indent
   - Expand/Collapse Groups
   - Group Transform (alle Elemente zusammen bewegen/skalieren)
   - Group Properties (gemeinsame Eigenschaften)

3. ✅ **Group Rendering**
   - GroupContainer Control
   - Selection Border um gesamte Gruppe
   - Enter Group Mode (Doppelklick)

### 🟡 PHASE 3: Advanced Properties (6-8h)
1. ✅ **Rectangle Enhancements**
   - Border Radius Slider (0-50%)
   - Gradient Fill (Linear, Radial)
   - Shadow (Offset X/Y, Blur, Color)

2. ✅ **Text Enhancements**
   - Line Height Slider
   - Letter Spacing
   - Text Decoration (Underline, Strike-through)
   - Text Shadow

3. ✅ **Common Properties**
   - Rotation Slider (bereits vorhanden, UI verbessern)
   - Opacity Slider (bereits vorhanden, UI verbessern)
   - Blend Mode (Normal, Multiply, Screen, etc.)
   - Lock Aspect Ratio Toggle

### 🟢 PHASE 4: Copy/Paste & Keyboard (4-6h)
1. ✅ **Clipboard Operations**
   - Copy (Ctrl+C)
   - Paste (Ctrl+V)
   - Cut (Ctrl+X)
   - Duplicate (Ctrl+D) - bereits vorhanden

2. ✅ **Keyboard Shortcuts**
   - Delete (Del) - bereits vorhanden
   - Select All (Ctrl+A) - bereits vorhanden
   - Undo (Ctrl+Z) - Command vorhanden, Binding fehlt
   - Redo (Ctrl+Y) - Command vorhanden, Binding fehlt
   - Save (Ctrl+S)
   - Arrow Keys (Move Element 1px)
   - Shift+Arrow (Move Element 10px)

3. ✅ **Keyboard Shortcuts Display**
   - Shortcuts im Context Menu anzeigen
   - Help Dialog mit allen Shortcuts
   - Tooltips mit Shortcuts

### 🟢 PHASE 5: UI Enhancements (8-12h)
1. ✅ **Context Menu Improvements**
   - Arrange Submenu (Bring to Front, Send to Back, etc.)
   - Align Submenu
   - Transform Submenu (Flip Horizontal, Flip Vertical, Rotate 90°)
   - Icons neben Menu-Items

2. ✅ **Rulers**
   - Horizontal Ruler (oben)
   - Vertical Ruler (links)
   - Unit: Pixels
   - Guides durch Klick-und-Ziehen erstellen

3. ✅ **Grid Configuration Dialog**
   - Grid Size Input (aktuell fest)
   - Grid Color Picker
   - Show/Hide Grid Toggle
   - Snap to Grid Toggle

4. ✅ **Better Color Picker**
   - Erweitern des vorhandenen ColorPicker
   - Recent Colors
   - Color Palette (Material Design, Tailwind)
   - Eyedropper Tool

5. ✅ **Font Picker Dialog**
   - Font Family Auswahl mit Preview
   - Font Size Slider
   - Font Weight Buttons (Thin, Light, Regular, Medium, Bold, Black)
   - Font Style Toggles (Italic, Underline)

6. ✅ **Image Selection from Media Library**
   - Media Browser Dialog
   - Thumbnail View
   - Filter by Type
   - Double-Click to Select

### 🟢 PHASE 6: Advanced Features (12-16h)
1. ⭐ **Element Templates/Presets**
   - Vordefinierte Button-Styles
   - Vordefinierte Card-Layouts
   - Vordefinierte Header-Styles
   - Drag-and-Drop aus Template Panel

2. ⭐ **Layers Enhancements**
   - Layer Thumbnails
   - Layer Search/Filter
   - Layer Opacity in Panel
   - Layer Lock Icon
   - Layer Hide Icon

3. ⭐ **Transform Tools**
   - Free Transform Mode (8 Handles)
   - Rotate Handle
   - Skew Handles
   - Scale from Center (Alt+Drag)
   - Maintain Aspect Ratio (Shift+Drag)

4. ⭐ **Path Editor** (für Shapes)
   - Bezier Curves
   - Edit Points
   - Add/Remove Points
   - Convert to Curve

## 🚀 IMPLEMENTATION PLAN

### Week 1: Phase 1 (Smart Guides & Alignment)
**Day 1-2:** Smart Guides Control
- AlignmentGuidesAdorner.cs
- Guide Drawing Logic
- Snap-to-Guide Logic

**Day 3-4:** Alignment Commands
- AlignmentService.cs
- Alignment Commands in DesignerViewModel
- Alignment Toolbar UI

**Day 5:** Testing & Polish

### Week 2: Phase 2 (Element Grouping)
**Day 1-2:** Group/Ungroup Logic
- GroupElement Model
- Group Commands
- Group Hierarchy

**Day 3-4:** Group Rendering & UI
- GroupContainer Control
- Layer Panel Updates
- Group Transform

**Day 5:** Testing & Polish

### Week 3: Phase 3 & 4 (Properties & Keyboard)
**Day 1-2:** Advanced Properties
- Border Radius, Shadow, Gradient
- Properties Panel UI Updates

**Day 3-4:** Copy/Paste & Keyboard
- Clipboard Operations
- Keyboard Bindings
- Shortcuts Help Dialog

**Day 5:** Testing & Polish

### Week 4: Phase 5 & 6 (UI Enhancements)
**Day 1-2:** Context Menu, Rulers, Grid Dialog
**Day 3-4:** Color Picker, Font Picker, Media Browser
**Day 5:** Element Templates, Final Testing

## 📊 EXPECTED OUTCOME

Nach der Implementierung wird der Designer folgendes bieten:

✅ **Professional Alignment**
- Automatische Guides wie in Figma
- Präzise Ausrichtung mit einem Klick
- Gleichmäßige Verteilung

✅ **Efficient Workflow**
- Alle wichtigen Keyboard Shortcuts
- Copy/Paste zwischen Layouts
- Gruppe von Elementen

✅ **Advanced Styling**
- Border Radius, Shadows, Gradients
- Font Auswahl mit Preview
- Color Picker mit Palettes

✅ **Intuitive UI**
- Rulers für Präzision
- Thumbnails im Layer Panel
- Template Library

✅ **Professional Features**
- Free Transform
- Path Editor (Zukunft)
- Component System (Zukunft)

## ⏱️ TOTAL TIME ESTIMATE
- Phase 1: 8-12h
- Phase 2: 8-10h
- Phase 3: 6-8h
- Phase 4: 4-6h
- Phase 5: 8-12h
- Phase 6: 12-16h

**TOTAL: 46-64 hours (1.5-2 Wochen bei 40h/Woche)**

---

**Status:** Planning Complete
**Ready to Start:** Phase 1 - Smart Guides & Alignment
**Target:** Professional Designer wie Canva/Figma

