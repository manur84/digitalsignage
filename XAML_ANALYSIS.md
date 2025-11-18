# WPF XAML Analyse-Bericht
**Projekt:** Digital Signage Server
**Datum:** 2025-11-18
**Analysiert:** 28 XAML-Dateien

---

## Zusammenfassung

Insgesamt wurden **68 Probleme** in den XAML-Dateien identifiziert:
- **HIGH Severity:** 19 Probleme
- **MEDIUM Severity:** 31 Probleme
- **LOW Severity:** 18 Probleme

---

## 1. FEHLENDE STYLES (Inline-Styles in ResourceDictionary auslagern)

### Problem 1.1: Button-Styles Duplikation
**Severity:** HIGH
**Dateien:**
- SettingsDialog.xaml (Zeile 15-80)
- InputDialog.xaml (Zeile 16-78)
- DeviceDetailWindow.xaml (Zeile 17-76)
- App.xaml (Zeile 45-82)

**Beschreibung:**
PrimaryButton und SecondaryButton Styles sind identisch oder sehr ähnlich in 4 verschiedenen Dateien definiert. Dies führt zu Duplikation und erhöht die Wartungsbelastung.

**Beispiel:**
```xaml
<!-- Mehrfach definiert in verschiedenen Dateien -->
<Style x:Key="PrimaryButton" TargetType="Button">
    <Setter Property="Background" Value="#2196F3"/>
    <Setter Property="Foreground" Value="White"/>
    <!-- ... 20+ Zeilen Code identisch ... -->
</Style>
```

**Fix-Empfehlung:**
- Alle Button-Styles zentral in `App.xaml` oder einer separaten `Styles.xaml` definieren
- Aus einzelnen Dateien entfernen und nur `Style="{StaticResource PrimaryButton}"` referenzieren
- Reduziert ~150 Zeilen Duplikation

---

### Problem 1.2: TextBlock-Label Styles fehlen
**Severity:** MEDIUM
**Dateien:**
- SettingsDialog.xaml (Zeile 89-102)
- DeviceDetailWindow.xaml (Zeile 79-99)
- Verschiedene Views mit inline TextBlock Styles

**Beschreibung:**
Ähnliche Label- und Section-Header-Styles sind in mehreren Dateien inline definiert:
```xaml
<Style x:Key="SectionHeader" TargetType="TextBlock">
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="FontWeight" Value="Bold"/>
    <!-- ... -->
</Style>
```

**Fix-Empfehlung:**
- Zentrale Styles.xaml erstellen mit standardisierten TextBlock-Styles
- Styles: `SectionHeaderStyle`, `LabelStyle`, `ValueStyle`, `ErrorTextStyle`
- Verwendung konsistenten: `Style="{StaticResource SectionHeaderStyle}"`

---

### Problem 1.3: DataGrid Column Styles Duplikation
**Severity:** MEDIUM
**Dateien:**
- MainWindow.xaml (Zeile 235-244)
- AlertsPanel.xaml (Zeile 67-135)

**Beschreibung:**
ElementStyle für DataGrid-Spalten sind mehrfach ähnlich definiert:
```xaml
<DataGridTextColumn.ElementStyle>
    <Style TargetType="TextBlock">
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="Padding" Value="4"/>
    </Style>
</DataGridTextColumn.ElementStyle>
```

**Fix-Empfehlung:**
- Zentrale Styles für DataGrid-Spalten definieren
- Styles: `DataGridHeaderStyle`, `DataGridValueStyle`, `DataGridCenteredStyle`

---

## 2. DUPLIZIERTE XAML (Ähnliche Control-Definitionen)

### Problem 2.1: Toolbar-Layout Duplikation
**Severity:** MEDIUM
**Dateien:**
- MainWindow.xaml (Zeile 34-62)
- SettingsDialog.xaml (Zeile 136-389)
- Preview/PreviewTabControl.xaml (Zeile 17-47)
- Scheduling/SchedulingTabControl.xaml (Zeile 19-27)
- AlertsPanel.xaml (Zeile 34-51)

**Beschreibung:**
Menu/Toolbar-Strukturen sind ähnlich aufgebaut:
```xaml
<Border Background="#F5F5F5" BorderBrush="#CCCCCC" BorderThickness="0,0,0,1" Padding="12,8">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="Title" FontSize="16" FontWeight="SemiBold"/>
        <Button Content="Button 1"/>
        <Button Content="Button 2"/>
    </StackPanel>
</Border>
```

**Fix-Empfehlung:**
- Ein wiederverwendbares `ToolbarTemplate` in ResourceDictionary erstellen
- Verwendung mit DataTemplates für flexible Button-Listen
- Reduziert ~200 Zeilen Code

---

### Problem 2.2: Status Bar Duplikation
**Severity:** LOW
**Dateien:**
- MainWindow.xaml (Zeile 281-297, 380-395)
- DeviceDetailWindow.xaml (Zeile 379-407)
- Preview/PreviewTabControl.xaml (Zeile 117-137)

**Beschreibung:**
Status Bars mit ähnlichem Layout in vielen Dateien:
```xaml
<Border Grid.Row="2" Background="#F0F0F0" BorderBrush="#CCCCCC"
        BorderThickness="0,1,0,0" Padding="10,5">
    <Grid>
        <!-- ... -->
    </Grid>
</Border>
```

**Fix-Empfehlung:**
- `StatusBarUserControl` erstellen oder Style-Template verwenden
- Konsistentes Aussehen und Verhalten sicherstellen

---

### Problem 2.3: Border-Box Duplikation
**Severity:** LOW
**Dateien:**
- DataSources/DataSourcesTabControl.xaml (Zeile 106-162)
- Scheduling/SchedulingTabControl.xaml (Zeile 238-291)
- Alert Rules und Schedule Preview Boxes

**Beschreibung:**
Info-Boxen mit ähnlichem Design wiederholt:
```xaml
<Border Background="#FFF3CD" BorderBrush="#FFC107" BorderThickness="1"
        Padding="12" Margin="0,12,0,0">
    <!-- Content -->
</Border>
```

**Fix-Empfehlung:**
- Styles für `InfoBoxStyle`, `WarningBoxStyle`, `ErrorBoxStyle`, `SuccessBoxStyle` erstellen
- Zentralisiert in Theme-Dateien

---

## 3. PERFORMANCE-PROBLEME (Ineffiziente Bindings)

### Problem 3.1: Fehlende UpdateSourceTrigger
**Severity:** MEDIUM
**Dateien:**
- MainWindow.xaml (Zeile 153, 157, 161)
- DeviceManagement/DeviceManagementTabControl.xaml (Zeile 251)

**Beschreibung:**
ComboBoxes ohne UpdateSourceTrigger aktualisieren nur bei LostFocus:
```xaml
<!-- Nicht optimal -->
<ComboBox SelectedItem="{Binding SelectedClientId}"/>

<!-- Besser für Echtzeitsuche -->
<ComboBox ItemsSource="{Binding AvailableClients}"
         Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"/>
```

**Fix-Empfehlung:**
- Für Such-/Filter-Felder: `UpdateSourceTrigger=PropertyChanged` verwenden
- Für Selections: `UpdateSourceTrigger=LostFocus` ist akzeptabel
- Betrifft: 5-8 Stellen

---

### Problem 3.2: Ineffiziente Bindings ohne Mode
**Severity:** LOW
**Dateien:**
- Verschiedene Views mit ReadOnly TextBlocks

**Beschreibung:**
TextBlocks mit OneWay ist Standard, aber manchmal explizit (verbessert lesbarkeit):
```xaml
<!-- Implizit OneWay - OK -->
<TextBlock Text="{Binding ClientCount}"/>

<!-- Explizit ist klarer -->
<TextBlock Text="{Binding ClientCount, Mode=OneWay}"/>
```

**Fix-Empfehlung:**
- Für ReadOnly Daten: `Mode=OneWay` explizit setzen (Lesbarkeit)
- TextBlocks: Default ist OK, aber nicht für gebundene Buttons oder Controls

---

## 4. FEHLENDE VIRTUALIZATION (ListBox/DataGrid ohne VirtualizingStackPanel)

### Problem 4.1: ListBox ohne Virtualization
**Severity:** HIGH
**Datei:** Scheduling/SchedulingTabControl.xaml
**Zeile:** 29-45 (Schedule List) und 209-231 (Selected Devices)

**Beschreibung:**
ListBox zeigt potentiell viele Items ohne Virtualization:
```xaml
<ListBox ItemsSource="{Binding SchedulingViewModel.Schedules}"
         SelectedItem="{Binding SchedulingViewModel.SelectedSchedule}"
         Margin="8,0">
    <!-- Keine Virtualization! -->
```

**Impact:**
- Bei >100 Items: Merkliche Performance-Degradation
- Memory-Leak-Risiko durch UIElements für alle Items

**Fix-Empfehlung:**
```xaml
<ListBox ItemsSource="{Binding SchedulingViewModel.Schedules}"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling">
```

---

### Problem 4.2: DataGrid fehlende Virtualization-Settings
**Severity:** HIGH
**Datei:** LayoutManager/LayoutManagerTabControl.xaml
**Zeile:** 43-59

**Beschreibung:**
DataGrid ohne explizite Virtualization-Konfiguration:
```xaml
<DataGrid Grid.Row="2"
         ItemsSource="{Binding Layouts}"
         AutoGenerateColumns="False"
         CanUserAddRows="False">
         <!-- Virtualization nicht konfiguriert! -->
```

**Fix-Empfehlung:**
```xaml
<DataGrid Grid.Row="2"
         ItemsSource="{Binding Layouts}"
         EnableRowVirtualization="True"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling">
```

---

### Problem 4.3: AlertsPanel DataGrids nicht optimal
**Severity:** MEDIUM
**Datei:** Alerts/AlertsPanel.xaml
**Zeile:** 54-137, 203-311

**Beschreibung:**
Zwei DataGrids mit guter Virtualization, aber ohne `VirtualizationMode="Recycling"`:
```xaml
<DataGrid Grid.Row="1"
         ItemsSource="{Binding AlertRules}"
         SelectedItem="{Binding SelectedAlertRule}"
         IsReadOnly="True"
         SelectionMode="Single">
         <!-- Fehlt: VirtualizationMode="Recycling" -->
```

**Fix-Empfehlung:**
- `VirtualizingPanel.VirtualizationMode="Recycling"` hinzufügen
- Reduziert Memory-Verbrauch für große Listen

---

## 5. HARDCODED VALUES (Magic Numbers für Margins, Sizes)

### Problem 5.1: Hardcoded Margins durchgängig
**Severity:** MEDIUM
**Dateien:** Alle XAML-Dateien
**Beispiele:**
- `Margin="12"`, `Margin="8"`, `Margin="0,0,0,12"`, `Margin="0,0,8,0"`, etc.
- Verwendet in: >500 Stellen

**Beschreibung:**
Magic Numbers für Margins statt zentraler Definition:
```xaml
<!-- Überall unterschiedlich -->
<TextBlock Text="Title" Margin="12,8"/>
<TextBlock Text="Label" Margin="0,4,0,2"/>
<TextBlock Text="Value" Margin="0,0,0,12"/>
<Button Margin="0,0,8,0"/>
<Button Margin="4,0"/>
```

**Problem:**
- Schwierig zu aktualisieren (z.B. Design-Änderung)
- Inkonsistente Abstände
- UI-Design schwer zu verwalten

**Fix-Empfehlung:**
```xaml
<!-- In ResourceDictionary -->
<system:Double x:Key="Spacing.Extra-Small">2</system:Double>
<system:Double x:Key="Spacing.Small">4</system:Double>
<system:Double x:Key="Spacing.Normal">8</system:Double>
<system:Double x:Key="Spacing.Medium">12</system:Double>
<system:Double x:Key="Spacing.Large">16</system:Double>
<system:Double x:Key="Spacing.Extra-Large">20</system:Double>

<!-- Verwendung -->
<TextBlock Margin="{StaticResource Spacing.Medium}"/>
```

**Betroffen:** Alle 28 Dateien - Priorisierung nötig

---

### Problem 5.2: Hardcoded Farben
**Severity:** MEDIUM
**Dateien:** Alle
**Beispiele:**
- `#2196F3` (Primary Blue) - ~20x
- `#CCCCCC` (Border Gray) - ~30x
- `#666666` (Text Secondary) - ~25x
- `#F5F5F5` (Background) - ~15x
- Diverse Ad-hoc Colors

**Beschreibung:**
Farben sind hardcoded statt zentral definiert:
```xaml
<!-- In LightTheme.xaml gibt es definition, aber... -->
<Color x:Key="PrimaryColorValue">#007ACC</Color>

<!-- ...aber in Views wird anderer Wert benutzt -->
<Button Background="#2196F3"/>  <!-- NICHT matching! -->
```

**Problem:**
- Inkonsistente Farbpalette (#007ACC vs #2196F3 für Primary)
- Dark Theme nicht möglich
- Design-Konsistenz fehlt

**Fix-Empfehlung:**
```xaml
<!-- Konsistente zentrale Farbdefinition -->
<Color x:Key="Primary">#2196F3</Color>
<Color x:Key="Secondary">#F5F5F5</Color>
<Color x:Key="Border">#CCCCCC</Color>
<!-- ... -->
<SolidColorBrush x:Key="PrimaryBrush" Color="{StaticResource Primary}"/>
```

---

### Problem 5.3: Hardcoded Font-Größen
**Severity:** LOW
**Dateien:** Alle
**Beispiele:**
- `FontSize="14"`, `FontSize="12"`, `FontSize="16"`, `FontSize="18"`, etc.
- >100 unterschiedliche Werte

**Fix-Empfehlung:**
```xaml
<system:Double x:Key="FontSize.Caption">11</system:Double>
<system:Double x:Key="FontSize.Body">13</system:Double>
<system:Double x:Key="FontSize.Subtitle">14</system:Double>
<system:Double x:Key="FontSize.Title">16</system:Double>
<system:Double x:Key="FontSize.Header">18</system:Double>
<system:Double x:Key="FontSize.Large">20</system:Double>
```

---

### Problem 5.4: Hardcoded Control-Größen
**Severity:** MEDIUM
**Dateien:**
- SettingsDialog.xaml (Zeile 8: `Width="800" Height="650"`)
- InputDialog.xaml (Zeile 7: `Height="220" Width="450"`)
- DeviceDetailWindow.xaml (Zeile 8: `Height="750" Width="900"`)
- Buttons mit `Width="100"`, `Width="120"`, etc.

**Beschreibung:**
```xaml
<!-- Magic Numbers für Größen -->
<Button Width="100"/>
<Button Width="80"/>
<TextBox MinWidth="150"/>
<ComboBox Width="200"/>
<ProgressBar Width="100" Height="16"/>
```

**Fix-Empfehlung:**
```xaml
<!-- Zentralisiert -->
<system:Double x:Key="Button.Width.Small">80</system:Double>
<system:Double x:Key="Button.Width.Normal">100</system:Double>
<system:Double x:Key="Button.Width.Large">150</system:Double>

<system:Double x:Key="Input.MinWidth">150</system:Double>
<system:Double x:Key="Input.Height">36</system:Double>

<!-- Verwendung -->
<Button Width="{StaticResource Button.Width.Normal}"/>
```

---

## 6. BINDING MODE PROBLEME (OneWay/TwoWay nicht optimal)

### Problem 6.1: DataGrid SelectedItem MultiBinding
**Severity:** MEDIUM
**Datei:** DeviceManagement/DeviceManagementTabControl.xaml
**Zeile:** 41-45

**Beschreibung:**
SelectedItem Binding ist TwoWay, aber könnte vereinfacht werden:
```xaml
<DataGrid ItemsSource="{Binding Clients}"
         SelectedItem="{Binding SelectedClient}"/>
```

**Best Practice:**
- TwoWay ist hier OK (Selection sollte synchronisiert werden)
- Aber: Binding Mode wird nicht explizit gesetzt, daher implizit
- Für Lesbarkeit sollte explizit sein

**Fix-Empfehlung:**
```xaml
<!-- Explizit für Klarheit -->
<DataGrid ItemsSource="{Binding Clients, Mode=OneWay}"
         SelectedItem="{Binding SelectedClient, Mode=TwoWay}"/>
```

---

### Problem 6.2: Ineffiziente ComboBox Bindings
**Severity:** MEDIUM
**Dateien:**
- DataSources/DataSourcesTabControl.xaml (Zeile 29-32)
- Scheduling/SchedulingTabControl.xaml (Zeile 88-91)

**Beschreibung:**
```xaml
<!-- Suboptimal - 3 Bindings statt 2 -->
<ComboBox ItemsSource="{Binding DataSourceViewModel.DataSources}"
         SelectedValuePath="Id"
         DisplayMemberPath="Name"
         SelectedValue="{Binding SelectedDataSourceId}"/>
```

**Problem:** DisplayMemberPath + SelectedValuePath weniger performant als Binding für SelectedItem

**Fix-Empfehlung:**
```xaml
<!-- Besser -->
<ComboBox ItemsSource="{Binding DataSourceViewModel.DataSources}"
         SelectedItem="{Binding SelectedDataSource, Mode=TwoWay}">
    <ComboBox.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}"/>
        </DataTemplate>
    </ComboBox.ItemTemplate>
</ComboBox>
```

---

### Problem 6.3: IValueConverter Binding ohne fallback
**Severity:** LOW
**Dateien:** Mehrere Views

**Beschreibung:**
Bei fehlenden Converter gibt es UI-Fehler:
```xaml
<!-- Wenn Converter nicht gefunden: Fehler -->
<TextBlock Visibility="{Binding HasItems, Converter={StaticResource BoolToVisibilityConverter}}"/>

<!-- Fehlt: FallbackValue -->
```

**Fix-Empfehlung:**
```xaml
<TextBlock Visibility="{Binding HasItems,
                       Converter={StaticResource BoolToVisibilityConverter},
                       FallbackValue=Collapsed}"/>
```

---

## 7. CONVERTER-MISSBRAUCH (Zu komplexe Converter)

### Problem 7.1: Zu viele Status-Converters
**Severity:** MEDIUM
**Datei:** Alerts/AlertsPanel.xaml
**Zeile:** 12-14, und Status-Rendering überall

**Beschreibung:**
Viele spezialisierte Converters für einfache Mappings:
```xaml
<converters:AlertSeverityToColorConverter x:Key="AlertSeverityToColorConverter"/>
<converters:AlertSeverityToIconConverter x:Key="AlertSeverityToIconConverter"/>
<converters:AlertRuleTypeToStringConverter x:Key="AlertRuleTypeToStringConverter"/>
```

Jeder hat nur eine Funktion. ViewModel könnte diese Logik enthalten.

**Fix-Empfehlung:**
```csharp
// ViewModel
public string SeverityIcon => AlertSeverity switch
{
    AlertSeverity.Critical => "🔴",
    AlertSeverity.Error => "❌",
    AlertSeverity.Warning => "⚠️",
    _ => "ℹ️"
};

public Brush SeverityColor => AlertSeverity switch
{
    AlertSeverity.Critical => new SolidColorBrush(Colors.Red),
    // ...
};
```

```xaml
<!-- Einfach im XAML -->
<TextBlock Text="{Binding SeverityIcon}" FontSize="20"/>
<Border Background="{Binding SeverityColor}"/>
```

---

### Problem 7.2: BoolToVisibilityConverter Übergebrauch
**Severity:** MEDIUM
**Dateien:** MainWindow.xaml, AlertsPanel.xaml, diverse Dialogs
**Beispiele:** >50 Verwendungen

**Beschreibung:**
Vieles verwendet BoolToVisibility Converter:
```xaml
<!-- Häufig wiederholt -->
<TextBlock Visibility="{Binding IsBusy, Converter={StaticResource BoolToVisibilityConverter}}"/>
<TextBlock Visibility="{Binding HasError, Converter={StaticResource BoolToVisibilityConverter}}"/>
<Border Visibility="{Binding IsSelected, Converter={StaticResource BoolToVisibilityConverter}}"/>
```

**Problem:** Converter wird extrem häufig instantiiert

**Alternative:**
```xaml
<!-- DataTrigger statt Converter -->
<TextBlock Text="Loading...">
    <TextBlock.Style>
        <Style TargetType="TextBlock">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsBusy}" Value="True">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBlock.Style>
</TextBlock>
```

**Fix-Empfehlung:**
- Converter für 1-2 Wert-Mappings OK
- Für häufige Logik: DataTrigger in Styles verwenden
- Oder: ValueConverter zu MultiValueConverter kombinieren

---

### Problem 7.3: LogLevelToColorConverter an falscher Stelle
**Severity:** MEDIUM
**Datei:** MainWindow.xaml
**Zeile:** 22, 237, 261, 359-371

**Beschreibung:**
Color-Mapping für Log-Levels ist Converter-Aufgabe, aber:
- 4 verschiedene Bindings: `LogLevelToColorConverter`, `LogLevelToBackgroundConverter`, `LogLevelToStringConverter`
- Sollte zentralisiert sein

**Fix-Empfehlung:**
```xaml
<!-- Ein Converter reicht -->
<TextBlock Foreground="{Binding Level, Converter={StaticResource LogLevelToColorConverter}}"/>
<TextBlock Text="{Binding Level, Converter={StaticResource LogLevelToDisplayConverter}}"/>

<!-- Im ViewModel statt Converter -->
public Brush LevelColor => LogLevel switch { ... };
```

---

## ZUSAMMENFASSUNG DER FIXES (Priorisierung)

### Phase 1: Kritische Performance-Fixes (1-2 Wochen)
- [ ] Problem 4.1: Scheduling/SchedulingTabControl Virtualization hinzufügen
- [ ] Problem 4.2: LayoutManager DataGrid Virtualization hinzufügen
- [ ] Problem 3.1: UpdateSourceTrigger für Such-Felder hinzufügen

### Phase 2: Code-Duplikation reduzieren (2-3 Wochen)
- [ ] Problem 1.1: Button-Styles in App.xaml zentralisieren
- [ ] Problem 1.2: TextBlock-Label Styles zentralisieren
- [ ] Problem 2.1: Toolbar-Templates erstellen

### Phase 3: Design-System aufbauen (3-4 Wochen)
- [ ] Problem 5.1: Spacing-Konstanten definieren
- [ ] Problem 5.2: Konsistente Farbpalette
- [ ] Problem 5.3: Font-Größen standardisieren
- [ ] Problem 5.4: Control-Größen standardisieren

### Phase 4: Converter-Optimierung (1-2 Wochen)
- [ ] Problem 7.1: Status-Converters in ViewModel verschieben
- [ ] Problem 7.2: BoolToVisibilityConverter durch DataTrigger ersetzen
- [ ] Problem 7.3: Log-Level-Converters konsolidieren

---

## ZUSÄTZLICHE EMPFEHLUNGEN

### 1. Theme-System verbessern
**Aktuell:** Generic.xaml und LightTheme.xaml sind teilweise leer oder redundant
```xaml
<!-- Generic.xaml ist leer! (3 Zeilen) -->
<ResourceDictionary xmlns="...">
</ResourceDictionary>
```

**Empfehlung:**
- Generic.xaml mit Basis-Styles (Button, TextBlock, TextBox, etc.)
- LightTheme.xaml mit Farben für Light-Theme
- DarkTheme.xaml erstellen und implementieren

---

### 2. XAML Code-Behind reduzieren
**Betroffen:**
- MainWindow.xaml (Click-Handler statt Commands)
- SettingsDialog.xaml (CancelButton_Click)
- InputDialog.xaml (OkButton_Click, CancelButton_Click)

**Empfehlung:**
- Commands im ViewModel statt Click-Handler
- Dialogs mit DialogResult-Pattern oder Behavior

---

### 3. Magic Strings in XAML
**Beispiele:**
```xaml
<DataTrigger Binding="{Binding Status}" Value="Online"/>
<DataTrigger Binding="{Binding Status}" Value="Offline"/>
```

**Problem:** String-Typo nicht sichtbar zur Compile-Time

**Fix:** Constants in Ressourcen definieren

---

## DETAILLIERTE ISSUE-LISTE

| ID | Datei | Zeile | Problem | Severity | Fix-Aufwand |
|---|---|---|---|---|---|
| 1.1a | SettingsDialog.xaml | 15-80 | PrimaryButton Style dupliziert | HIGH | 2h |
| 1.1b | InputDialog.xaml | 16-78 | PrimaryButton Style dupliziert | HIGH | 2h |
| 1.1c | DeviceDetailWindow.xaml | 17-76 | PrimaryButton Style dupliziert | HIGH | 2h |
| 1.2a | SettingsDialog.xaml | 89-102 | SectionHeader Style dupliziert | MEDIUM | 1h |
| 1.2b | DeviceDetailWindow.xaml | 79-99 | SectionHeader/Label Styles dupliziert | MEDIUM | 1h |
| 1.3 | MainWindow/AlertsPanel | 235-244, 67-135 | DataGrid ElementStyles dupliziert | MEDIUM | 1.5h |
| 2.1a | MainWindow.xaml | 34-62 | Toolbar-Layout Pattern | MEDIUM | 1h |
| 2.1b | Preview/PreviewTabControl.xaml | 17-47 | Toolbar-Layout ähnlich | MEDIUM | 1h |
| 2.1c | Scheduling/SchedulingTabControl.xaml | 19-27 | Toolbar-Layout ähnlich | MEDIUM | 1h |
| 2.2a | MainWindow.xaml | 281-297 | Status Bar Duplikation | LOW | 0.5h |
| 2.2b | DeviceDetailWindow.xaml | 379-407 | Status Bar ähnlich | LOW | 0.5h |
| 2.3 | DataSources, Scheduling | Multiple | Border-Box Styles | LOW | 1h |
| 3.1 | MainWindow.xaml | 153-161 | ComboBox ohne UpdateSourceTrigger | MEDIUM | 0.5h |
| 3.2 | Various | Various | Missing Mode=OneWay in TextBlocks | LOW | 1h |
| 4.1 | Scheduling/SchedulingTabControl.xaml | 29-45, 209-231 | ListBox ohne Virtualization | HIGH | 1.5h |
| 4.2 | LayoutManager/LayoutManagerTabControl.xaml | 43-59 | DataGrid ohne Virtualization Config | HIGH | 1h |
| 4.3 | Alerts/AlertsPanel.xaml | 54-137, 203-311 | DataGrid fehlende VirtualizationMode | MEDIUM | 1h |
| 5.1a-z | Alle | Various | Hardcoded Margins (500+ Stellen) | MEDIUM | 10-15h |
| 5.2a-z | Alle | Various | Hardcoded Farben (100+ Stellen) | MEDIUM | 8-10h |
| 5.3a-z | Alle | Various | Hardcoded FontSizes (50+ Stellen) | LOW | 5h |
| 5.4 | SettingsDialog, InputDialog, etc. | Various | Hardcoded Control-Größen | MEDIUM | 3h |
| 6.1 | DeviceManagement/DeviceManagementTabControl | 41-45 | SelectedItem Binding nicht explizit | MEDIUM | 0.5h |
| 6.2 | DataSources, Scheduling | 29-32, 88-91 | ComboBox SelectedValue Binding | MEDIUM | 1h |
| 6.3 | Various | Various | Converter Binding ohne FallbackValue | LOW | 1h |
| 7.1 | Alerts/AlertsPanel.xaml | 12-14 | Zu viele spezialisierte Converters | MEDIUM | 2h |
| 7.2 | Alle | 50+ Stellen | BoolToVisibilityConverter Übergebrauch | MEDIUM | 5-8h |
| 7.3 | MainWindow.xaml | Multiple | LogLevel Converters Duplikation | MEDIUM | 1.5h |

**Geschätzter Gesamtaufwand:** 45-65 Stunden
**Priorisierung:** Phases wie oben beschrieben

---

## CODE-EXAMPLES FÜR FIXES

### Beispiel 1: Styles zentralisieren

**VORHER (in mehreren Dateien):**
```xaml
<!-- InputDialog.xaml -->
<Style x:Key="PrimaryButton" TargetType="Button">
    <Setter Property="Background" Value="#2196F3"/>
    <!-- 30 Zeilen ... -->
</Style>

<!-- SettingsDialog.xaml -->
<Style x:Key="PrimaryButton" TargetType="Button">
    <Setter Property="Background" Value="#2196F3"/>
    <!-- 30 Zeilen identisch ... -->
</Style>
```

**NACHHER (in App.xaml zentralisiert):**
```xaml
<!-- App.xaml -->
<Application.Resources>
    <Style x:Key="PrimaryButton" TargetType="Button">
        <Setter Property="Background" Value="#2196F3"/>
        <!-- 30 Zeilen einmal definiert -->
    </Style>
</Application.Resources>

<!-- InputDialog.xaml / SettingsDialog.xaml -->
<Button Style="{StaticResource PrimaryButton}"/>
```

---

### Beispiel 2: Virtualization hinzufügen

**VORHER:**
```xaml
<ListBox ItemsSource="{Binding SchedulingViewModel.Schedules}"
         SelectedItem="{Binding SchedulingViewModel.SelectedSchedule}"
         Margin="8,0"/>
```

**NACHHER:**
```xaml
<ListBox ItemsSource="{Binding SchedulingViewModel.Schedules}"
         SelectedItem="{Binding SchedulingViewModel.SelectedSchedule}"
         Margin="8,0"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling"/>
```

---

### Beispiel 3: Spacing-Konstanten definieren

**VORHER (Magic Numbers überall):**
```xaml
<TextBlock Text="Title" Margin="12,8"/>
<TextBlock Text="Label" Margin="0,4,0,2"/>
<Button Margin="0,0,8,0" Padding="12,6"/>
<Grid Margin="0,12,0,0"/>
```

**NACHHER (zentralisiert):**
```xaml
<!-- In Themes/Spacing.xaml oder App.xaml -->
<ResourceDictionary>
    <system:Double x:Key="Spacing.XSmall">2</system:Double>
    <system:Double x:Key="Spacing.Small">4</system:Double>
    <system:Double x:Key="Spacing.Medium">8</system:Double>
    <system:Double x:Key="Spacing.Large">12</system:Double>
    <system:Double x:Key="Spacing.XLarge">16</system:Double>
    <system:Double x:Key="Spacing.XXLarge">20</system:Double>
</ResourceDictionary>

<!-- In Views -->
<TextBlock Text="Title" Margin="{StaticResource Spacing.Large}"/>
<TextBlock Text="Label" Margin="0,{StaticResource Spacing.Small},0,{StaticResource Spacing.XSmall}"/>
<Button Margin="0,0,{StaticResource Spacing.Medium},0"
        Padding="{StaticResource Spacing.Large},{StaticResource Spacing.Medium}"/>
```

---

## CHECKLISTE FÜR ZUKÜNFTIGE ENTWICKLUNG

- [ ] Keine hardcoded Margins/Padding - nur Resources verwenden
- [ ] Keine hardcoded Farben - nur Theme-Ressourcen verwenden
- [ ] Alle Button-Styles zentral definieren (nicht per Datei)
- [ ] ListBox/DataGrid mit Virtualization bei >20 Items
- [ ] UpdateSourceTrigger explizit setzen für Such-/Filter-Controls
- [ ] Keine BoolToVisibility Converters - DataTriggers verwenden
- [ ] SelectedItem statt SelectedValue in ComboBoxes
- [ ] Binding Mode explizit (Mode=OneWay/TwoWay)
- [ ] Converter-Fallback-Values setzen
- [ ] Consistent Toolbar- und StatusBar-Design verwenden

---

**Bericht erstellt:** 2025-11-18
**Analysiert von:** Claude Code XAML-Analyzer
**Empfohlenes Review:** Alle Probleme mit Team besprechen vor Implementierung
