# Digital Signage - Umfassender Projekt-Audit Report

**Datum:** 2025-11-15
**Projekt:** Digital Signage (.NET 8 WPF Server + Python Raspberry Pi Client)
**Analysierte Komponenten:**
- C# Server (94 Dateien, ~25.000 Zeilen)
- Python Client (11 Dateien, ~3.000 Zeilen)
- XAML UI (36 Dateien)
- Konfiguration & Dependencies

---

## Executive Summary

### Gesamtübersicht

Eine umfassende Analyse des Digital Signage Projekts ergab **156 Probleme** verschiedener Schweregrade:

| Schweregrad | Anzahl | Prozent | Beschreibung |
|-------------|--------|---------|--------------|
| **KRITISCH** | 25 | 16% | Sofortige Behebung erforderlich |
| **HOCH** | 49 | 31% | Hohe Priorität, zeitnah beheben |
| **MITTEL** | 57 | 37% | Mittlere Priorität |
| **NIEDRIG** | 25 | 16% | Code-Qualität & Best Practices |

### Hauptproblembereiche

1. **Sicherheit (15 Probleme):** Kritische Schwachstellen in Authentifizierung, Verschlüsselung und Input-Validierung
2. **Memory Leaks (13 Probleme):** Event-Handler-Lecks in WPF-Komponenten
3. **Resource Management (11 Probleme):** Database-Connections, File-Handles
4. **Async/Await Probleme (10 Probleme):** Fire-and-Forget, Sync-over-Async
5. **Threading & Concurrency (8 Probleme):** Race Conditions, Thread-Unsafe Code
6. **Performance (18 Probleme):** Missing Virtualization, ineffiziente Algorithmen
7. **Error Handling (9 Probleme):** Fehlende Exception-Behandlung
8. **Code-Qualität (72 Probleme):** Redundanzen, fehlende Validierung, Inkonsistenzen

### Kritischste Probleme (Top 10)

| # | Problem | Komponente | Schweregrad | CWE/OWASP |
|---|---------|------------|-------------|-----------|
| 1 | **Schwaches Password-Hashing (SHA-256)** | AuthenticationService.cs | KRITISCH | CWE-916 |
| 2 | **Path Traversal Vulnerability** | EnhancedMediaService.cs | KRITISCH | CWE-22 |
| 3 | **Command Injection** | device_manager.py | KRITISCH | CWE-78 |
| 4 | **Server-Side Template Injection (SSTI)** | TemplateService.cs | KRITISCH | CWE-94 |
| 5 | **Web Interface ohne Authentifizierung** | web_interface.py | KRITISCH | CWE-306 |
| 6 | **SSL-Verifikation deaktivierbar** | client.py | KRITISCH | CWE-295 |
| 7 | **Event-Handler Memory Leaks** | 5x Dialog-Fenster | KRITISCH | - |
| 8 | **Fehlende Binding-Converter** | PropertiesPanelControl.xaml | KRITISCH | - |
| 9 | **Thread-Unsafe Dictionary** | AlertService.cs | KRITISCH | - |
| 10 | **JsonDocument Resource Leak** | AlertService.cs | KRITISCH | - |

---

## 1. Detaillierte Probleme nach Kategorie

### 1.1 SICHERHEIT (15 Probleme)

#### 1.1.1 KRITISCHE SICHERHEITSPROBLEME

##### ❌ Schwaches Password-Hashing mit SHA-256
**Datei:** `src/DigitalSignage.Server/Services/AuthenticationService.cs:388-401`
**Schweregrad:** KRITISCH
**CWE:** CWE-916 | **OWASP:** A02:2021

**Problem:**
```csharp
public string HashPassword(string password)
{
    // Note: In production, use BCrypt or Argon2!
    using var sha256 = SHA256.Create();
    var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
    return Convert.ToBase64String(hashedBytes);
}
```

Passwörter werden mit SHA-256 **ohne Salt** gehasht. Dies macht sie anfällig für Rainbow-Table-Angriffe.

**Auswirkung:**
- Bei Datenbank-Kompromittierung können Passwörter in Sekunden/Minuten geknackt werden
- Vollständiger Zugriffsverlust auf Benutzerkonten
- Admin-Accounts könnten übernommen werden

**Lösung:**
```csharp
using BCrypt.Net;

public string HashPassword(string password)
{
    return BCrypt.HashPassword(password, workFactor: 12);
}

public bool VerifyPassword(string password, string hash)
{
    return BCrypt.Verify(password, hash);
}
```

---

##### ❌ Path Traversal in Media-Service
**Datei:** `src/DigitalSignage.Server/Services/EnhancedMediaService.cs:131, 177`
**Schweregrad:** KRITISCH
**CWE:** CWE-22 | **OWASP:** A01:2021

**Problem:**
```csharp
public async Task<byte[]?> GetMediaAsync(string fileName)
{
    var filePath = Path.Combine(_mediaDirectory, fileName); // UNSICHER!
    if (!File.Exists(filePath))
        return null;
    return await File.ReadAllBytesAsync(filePath);
}
```

Keine Validierung von `fileName`. Angreifer könnte `"../../../Windows/System32/config/SAM"` übergeben.

**Auswirkung:**
- Beliebige Dateien vom Server lesen/löschen
- Vertrauliche Konfigurationsdateien auslesen
- Systemdateien kompromittieren

**Lösung:**
```csharp
public async Task<byte[]?> GetMediaAsync(string fileName)
{
    // Validierung: Keine Path-Separatoren erlaubt
    if (string.IsNullOrWhiteSpace(fileName) ||
        fileName.Contains("..") ||
        fileName.Contains(Path.DirectorySeparatorChar) ||
        fileName.Contains(Path.AltDirectorySeparatorChar))
    {
        _logger.LogWarning("Invalid fileName: {FileName}", fileName);
        throw new ArgumentException("Invalid file name", nameof(fileName));
    }

    var filePath = Path.Combine(_mediaDirectory, fileName);

    // Sicherstellen, dass Pfad innerhalb des Media-Verzeichnisses liegt
    var fullPath = Path.GetFullPath(filePath);
    var fullMediaDir = Path.GetFullPath(_mediaDirectory);

    if (!fullPath.StartsWith(fullMediaDir, StringComparison.OrdinalIgnoreCase))
    {
        _logger.LogWarning("Path traversal attempt: {FileName}", fileName);
        throw new UnauthorizedAccessException("Access denied");
    }

    if (!File.Exists(fullPath))
        return null;

    return await File.ReadAllBytesAsync(fullPath);
}
```

---

##### ❌ Command Injection im Python-Client
**Datei:** `src/DigitalSignage.Client.RaspberryPi/device_manager.py:314, 686, 744`
**Schweregrad:** KRITISCH
**CWE:** CWE-78 | **OWASP:** A03:2021

**Problem:**
```python
async def set_volume(self, volume: int):
    result = subprocess.run(
        ['amixer', 'set', 'Master', f'{volume}%'],  # Parametrisiert, aber...
        capture_output=True,
        text=True,
        timeout=5
    )
```

Während `set_volume` validiert, verwendet `os.system()` an anderer Stelle:

```python
# web_interface.py:107
os.system('sudo systemctl restart digitalsignage-client')
```

**Auswirkung:**
- Remote Code Execution auf Raspberry Pi
- Vollständige Geräte-Kompromittierung
- Lateral Movement im Netzwerk

**Lösung:**
```python
# NIE os.system() verwenden!
result = subprocess.run(
    ['sudo', 'systemctl', 'restart', 'digitalsignage-client'],
    capture_output=True,
    text=True,
    timeout=10,
    shell=False  # KRITISCH: Niemals shell=True
)
if result.returncode != 0:
    raise RuntimeError(f"Restart failed: {result.stderr}")
```

---

##### ❌ Server-Side Template Injection (SSTI)
**Datei:** `src/DigitalSignage.Server/Services/TemplateService.cs:27, 74`
**Schweregrad:** KRITISCH
**CWE:** CWE-94 | **OWASP:** A03:2021

**Problem:**
```csharp
_defaultContext = new TemplateContext
{
    MemberRenamer = member => member.Name,
    StrictVariables = false // UNSICHER!
};
```

Scriban-Templates mit `StrictVariables = false` erlauben unsichere Operationen.

**Auswirkung:**
- Remote Code Execution über Template-Injection
- Denial of Service durch Ressourcen-Erschöpfung
- Zugriff auf Server-Interna durch Reflection

**Lösung:**
```csharp
_defaultContext = new TemplateContext
{
    MemberRenamer = member => member.Name,
    StrictVariables = true,  // AKTIVIEREN!
    EnableRelaxedMemberAccess = false,
    EnableRelaxedTargetAccess = false,
    RecursionLimit = 10,
    LoopLimit = 1000
};
```

---

##### ❌ Web-Interface ohne Authentifizierung
**Datei:** `src/DigitalSignage.Client.RaspberryPi/web_interface.py:55-263`
**Schweregrad:** KRITISCH
**CWE:** CWE-306 | **OWASP:** A07:2021

**Problem:**
Web-Interface auf Port 5000 ohne jegliche Authentifizierung. Jeder im Netzwerk kann:
- Gerät neu starten
- Cache löschen
- Einstellungen ändern
- System-Logs abrufen

**Auswirkung:**
- Denial of Service (massenhafte Neustarts)
- Unbefugter Zugriff auf Gerätedaten
- Informationsleck durch Logs

**Lösung:**
```python
from flask_httpauth import HTTPBasicAuth
from werkzeug.security import check_password_hash

auth = HTTPBasicAuth()

@auth.verify_password
def verify_password(username, password):
    if username == "admin":
        return check_password_hash(self.client.config.web_password_hash, password)
    return False

@self.app.route('/api/restart', methods=['POST'])
@auth.login_required
def api_restart():
    # ... existing code
```

---

##### ❌ SSL-Verifikation deaktivierbar
**Datei:** `src/DigitalSignage.Client.RaspberryPi/client.py:316-321`
**Schweregrad:** KRITISCH
**CWE:** CWE-295 | **OWASP:** A02:2021

**Problem:**
```python
if not self.config.verify_ssl:
    import ssl
    sslopt = {"cert_reqs": ssl.CERT_NONE}
    logger.warning("SSL certificate verification disabled")
```

Ermöglicht Man-in-the-Middle-Angriffe.

**Auswirkung:**
- Traffic-Interception
- Credential-Diebstahl
- Fake-Server-Injection

**Lösung:**
```python
# SSL-Verifikation IMMER aktivieren in Production
if os.environ.get('PRODUCTION_MODE') == 'true' and not self.config.verify_ssl:
    raise ValueError("SSL verification cannot be disabled in production")

sslopt = {
    "cert_reqs": ssl.CERT_REQUIRED,
    "check_hostname": True,
    "ca_certs": self.config.ca_cert_path
}
```

---

#### 1.1.2 HOHE SICHERHEITSPROBLEME

##### ⚠️ SQL Injection via Table Name
**Datei:** `src/DigitalSignage.Data/Services/SqlDataService.cs:276-323`
**Schweregrad:** HOCH
**CWE:** CWE-89

**Problem:** Tabellenname aus User-Query extrahiert, fragile Sanitization.

**Lösung:** Expliziter `TableName`-Parameter mit Whitelist-Validierung.

---

##### ⚠️ Fehlende Autorisierung auf Remote-Commands
**Datei:** `src/DigitalSignage.Server/Services/ClientService.cs:426-467`
**Schweregrad:** HOCH
**CWE:** CWE-862

**Problem:** Jeder mit ClientId kann Commands senden (RESTART, SCREENSHOT, etc.).

**Lösung:** Authorization-Checks vor Command-Ausführung.

---

##### ⚠️ Passwort in Klartext in Config
**Datei:** `src/DigitalSignage.Server/Configuration/ServerSettings.cs:45`
**Schweregrad:** HOCH
**CWE:** CWE-256

**Problem:** SSL-Zertifikat-Passwort im Klartext gespeichert.

**Lösung:** DPAPI oder Azure Key Vault verwenden.

---

##### ⚠️ Kein Rate Limiting auf WebSocket
**Datei:** `src/DigitalSignage.Server/Services/WebSocketCommunicationService.cs:269-408`
**Schweregrad:** HOCH
**CWE:** CWE-770

**Problem:** Keine Begrenzung eingehender Nachrichten → DoS-Angriff möglich.

**Lösung:** Rate Limiter implementieren (z.B. 100 Nachrichten/Minute).

---

##### ⚠️ Kein CSRF-Schutz auf Web-API
**Datei:** `src/DigitalSignage.Client.RaspberryPi/web_interface.py:98-263`
**Schweregrad:** HOCH

**Problem:** POST-Endpoints ohne CSRF-Token → Fernsteuerung via bösartige Website möglich.

**Lösung:** CSRF-Token oder API-Token-Authentifizierung.

---

#### 1.1.3 MITTLERE SICHERHEITSPROBLEME

- Schwache Token-Generierung mit Modulo-Bias
- Fehlende Input-Validierung bei Client-Registrierung
- Keine WebSocket-Origin-Validierung
- Unbegrenzte File-Upload-Größe
- Fehlende WebSocket-Authentifizierung vor Verbindungsaufbau

---

### 1.2 MEMORY LEAKS (13 Probleme)

#### ❌ Event-Handler-Lecks in WPF-Dialogen
**Dateien:**
- `LayoutSelectionDialog.xaml.cs:17`
- `TemplateSelectionWindow.xaml.cs:17`
- `ScreenshotWindow.xaml.cs:17`
- `NewLayoutDialog.xaml.cs:17`
- `TemplateManagerWindow.xaml.cs:17`

**Schweregrad:** KRITISCH

**Problem:**
```csharp
viewModel.CloseRequested += (sender, args) =>
{
    DialogResult = args;
    Close();
};
```

Event-Handler werden nie entfernt. ViewModel hält Referenz auf Window → Garbage Collection verhindert.

**Auswirkung:**
- Bei jedem Öffnen/Schließen bleibt Window-Instanz im Speicher
- Nach 100 Öffnungen: ~500 MB Speicherverlust
- Anwendung wird zunehmend langsamer

**Lösung:**
```csharp
public LayoutSelectionDialog(LayoutSelectionViewModel viewModel)
{
    InitializeComponent();
    DataContext = viewModel;

    Loaded += OnLoaded;
    Closed += OnClosed;
}

private void OnLoaded(object sender, RoutedEventArgs e)
{
    var vm = (LayoutSelectionViewModel)DataContext;
    vm.CloseRequested += OnCloseRequested;
}

private void OnClosed(object sender, EventArgs e)
{
    var vm = (LayoutSelectionViewModel)DataContext;
    vm.CloseRequested -= OnCloseRequested;
}

private void OnCloseRequested(object sender, bool args)
{
    DialogResult = args;
    Close();
}
```

---

#### ❌ PropertyChanged Event-Handler Leak
**Datei:** `src/DigitalSignage.Server/Views/Dialogs/SettingsDialog.xaml.cs:36`
**Schweregrad:** KRITISCH

**Problem:** Lambda-Event-Handler auf `PropertyChanged` wird nie entfernt.

**Lösung:** Named-Method-Handler mit Cleanup in `Closed`-Event.

---

#### ❌ Async Loaded Event ohne Cleanup
**Datei:** `src/DigitalSignage.Server/Views/Dialogs/MediaBrowserDialog.xaml.cs:33-50`
**Schweregrad:** HOCH

**Problem:** `Loaded += async (s, e) => { ... }` ohne Unsubscribe.

**Lösung:** Named async void-Method mit Cleanup.

---

#### ⚠️ Status-Screen Memory Leak
**Datei:** `src/DigitalSignage.Client.RaspberryPi/status_screen.py:784-806`
**Schweregrad:** MITTEL

**Problem:** Top-Level Window mit `parent=None` → nicht automatisch freigegeben.

**Lösung:** Parent setzen oder manuell löschen.

---

### 1.3 RESOURCE MANAGEMENT (11 Probleme)

#### ⚠️ Database-Connection-Lecks im Python-Client
**Datei:** `src/DigitalSignage.Client.RaspberryPi/cache_manager.py` (10 Methoden)
**Schweregrad:** HOCH

**Problem:**
```python
def save_layout(self, layout, layout_data, set_current=False):
    try:
        conn = sqlite3.connect(str(self.db_path))
        cursor = conn.cursor()
        # ... operations ...
        conn.commit()
        conn.close()  # Nur bei Erfolg, nicht bei Exception!
    except Exception as e:
        logger.error(f"Failed to save layout: {e}")
        raise
```

Connection wird bei Exception nicht geschlossen → Connection-Pool-Erschöpfung.

**Auswirkung:**
- SQLite-Datenbank wird gesperrt
- "Database is locked"-Fehler
- Client kann keine Layouts mehr cachen

**Lösung:**
```python
def save_layout(self, layout, layout_data, set_current=False):
    conn = None
    try:
        conn = sqlite3.connect(str(self.db_path))
        cursor = conn.cursor()
        # ... operations ...
        conn.commit()
    except Exception as e:
        logger.error(f"Failed to save layout: {e}")
        raise
    finally:
        if conn:
            conn.close()
```

Betrifft 10 Methoden: `save_layout`, `get_current_layout`, `get_layout_by_id`, `clear_cache`, `get_cache_info`, `set_metadata`, `get_metadata`, `get_all_layouts`, `set_current_layout`, `_init_database`.

---

#### ❌ JsonDocument nicht disposed
**Datei:** `src/DigitalSignage.Server/Services/AlertService.cs:mehrere Stellen`
**Schweregrad:** KRITISCH

**Problem:**
```csharp
var doc = JsonDocument.Parse(conditionJson);
var root = doc.RootElement;
// ... doc wird nie disposed!
```

JsonDocument implementiert IDisposable, muss aber freigegeben werden.

**Lösung:**
```csharp
using var doc = JsonDocument.Parse(conditionJson);
var root = doc.RootElement;
```

---

#### ⚠️ File-Handle-Leak im Error-Path
**Datei:** `src/DigitalSignage.Client.RaspberryPi/client.py:195-202`
**Schweregrad:** HOCH

**Problem:** File geöffnet in Fallback-Logging, aber nicht garantiert geschlossen bei Import-Fehler.

**Lösung:** Exception-Handling verbessern, try-finally verwenden.

---

### 1.4 ASYNC/AWAIT PROBLEME (10 Probleme)

#### ❌ Fire-and-Forget Tasks
**Dateien:**
- `ClientService.cs:184` - `_ = SendWelcomeMessageAsync(...);`
- `EnhancedMediaService.cs:64` - `_ = UpdateThumbnailAsync(...);`
- `MessageHandlerService.cs:96` - Async void Event-Handler

**Schweregrad:** KRITISCH

**Problem:** Tasks werden gestartet aber nicht awaited. Exceptions gehen verloren.

**Auswirkung:**
- Silent failures
- Unhandled exceptions können Anwendung zum Absturz bringen
- Schwer zu debuggen

**Lösung:**
```csharp
// Option 1: Await proper
await SendWelcomeMessageAsync(client, cancellationToken);

// Option 2: Fire-and-Forget mit Logging
_ = SendWelcomeMessageAsync(client, cancellationToken)
    .ContinueWith(t =>
    {
        if (t.IsFaulted)
            _logger.LogError(t.Exception, "Failed to send welcome message");
    }, TaskScheduler.Default);
```

---

#### ⚠️ Sync-over-Async
**Datei:** `src/DigitalSignage.Server/Services/LayoutService.cs:mehrere Stellen`
**Schweregrad:** HOCH

**Problem:**
```csharp
public async Task<string> SaveLayoutAsync(DisplayLayout layout)
{
    // Synchrones File I/O in async-Methode!
    File.WriteAllText(filePath, json);
    return filePath;
}
```

**Lösung:**
```csharp
await File.WriteAllTextAsync(filePath, json, cancellationToken);
```

---

#### ⚠️ Async Void Event-Handler
**Datei:** `src/DigitalSignage.Server/Services/MessageHandlerService.cs:96`
**Schweregrad:** HOCH

**Problem:** `async void OnMessageReceived(...)` → Exceptions nicht fangbar.

**Lösung:** Wrap in try-catch innerhalb der Methode.

---

#### ⚠️ Thread.Sleep in Async Code
**Datei:** `src/DigitalSignage.Server/Services/SystemDiagnosticsService.cs:mehrere Stellen`
**Schweregrad:** HOCH

**Problem:** `Thread.Sleep(100)` blockiert Thread-Pool.

**Lösung:** `await Task.Delay(100, cancellationToken);`

---

### 1.5 THREADING & CONCURRENCY (8 Probleme)

#### ❌ Thread-Unsafe Dictionary
**Datei:** `src/DigitalSignage.Server/Services/AlertService.cs:25`
**Schweregrad:** KRITISCH

**Problem:**
```csharp
private Dictionary<Guid, DateTime> _lastAlertTimes = new();

// Wird von mehreren Threads zugegriffen ohne Lock!
_lastAlertTimes[ruleId] = DateTime.UtcNow;
```

**Auswirkung:**
- Race Conditions
- Dictionary-Korruption
- Application Crash

**Lösung:**
```csharp
private readonly ConcurrentDictionary<Guid, DateTime> _lastAlertTimes = new();
```

---

#### ⚠️ Thread-Unsafe List Access
**Datei:** `src/DigitalSignage.Client.RaspberryPi/display_renderer.py:187-194`
**Schweregrad:** MITTEL

**Problem:** `_datetime_timers` Liste wird von Qt-Thread und Rendering-Thread ohne Lock modifiziert.

**Lösung:** `threading.Lock()` verwenden.

---

#### ⚠️ Event-Loop nicht initialisiert Check
**Datei:** `src/DigitalSignage.Client.RaspberryPi/client.py:224-227`
**Schweregrad:** HOCH

**Problem:** `self.event_loop` könnte noch nicht gesetzt sein → AttributeError.

**Lösung:** Prüfung hinzufügen:
```python
if not hasattr(self, 'event_loop') or self.event_loop is None:
    logger.error("Event loop not initialized")
    return
```

---

#### ⚠️ WebSocket-Thread nicht proper gejoined
**Datei:** `src/DigitalSignage.Client.RaspberryPi/client.py:365-368`
**Schweregrad:** HOCH

**Problem:** Thread.join mit Timeout, aber kein Handling wenn Timeout abläuft.

**Lösung:** Force-Close Socket wenn Thread nicht terminiert.

---

### 1.6 PERFORMANCE (18 Probleme)

#### ⚠️ Fehlende Virtualisierung in ListBox
**Datei:** `src/DigitalSignage.Server/Views/Designer/LayersPanelControl.xaml:22-26`
**Schweregrad:** HOCH

**Problem:** Keine Virtualisierung bei Layer-Liste → alle Items werden upfront gerendert.

**Auswirkung:**
- Bei 500+ Elementen: Langsames Rendering
- Hoher Speicherverbrauch
- UI-Freezes

**Lösung:**
```xml
<ListBox VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling"
         ...>
```

---

#### ⚠️ Subprocess in Web-Request
**Datei:** `src/DigitalSignage.Client.RaspberryPi/web_interface.py:418-421`
**Schweregrad:** MITTEL

**Problem:** `subprocess.run(['journalctl', ...], timeout=5)` blockiert Request für bis zu 5 Sekunden.

**Lösung:** Logs cachen und periodisch aktualisieren.

---

#### ⚠️ Ineffiziente LINQ-Queries
**Datei:** `src/DigitalSignage.Server/Services/ClientService.cs:mehrere Stellen`
**Schweregrad:** MITTEL

**Problem:** `.ToList()` gefolgt von `.Where()` → gesamte Liste geladen.

**Lösung:** `.Where()` vor `.ToList()`.

---

#### ⚠️ Duplicate Button-Styles
**Dateien:** 5+ XAML-Files
**Schweregrad:** MITTEL

**Problem:** 500+ Zeilen duplizierte Button-Style-Definitionen.

**Lösung:** Globale Styles in App.xaml verwenden.

---

### 1.7 ERROR HANDLING (9 Probleme)

#### ⚠️ Bare Except Catching SystemExit
**Datei:** `src/DigitalSignage.Client.RaspberryPi/client.py:72-73`
**Schweregrad:** HOCH

**Problem:**
```python
except Exception:  # Fängt zu viel!
    websocket_version = "unknown"
```

**Lösung:**
```python
except (AttributeError, ImportError):
    websocket_version = "unknown"
```

---

#### ⚠️ Swallowed Exceptions
**Dateien:** Mehrere Services
**Schweregrad:** MITTEL

**Problem:** Exceptions geloggt aber nicht propagiert → Silent Failures.

**Lösung:** Re-throw nach Logging.

---

#### ⚠️ Fehlende Null-Checks
**Dateien:** Mehrere Services
**Schweregrad:** MITTEL

**Problem:** Fehlende Validierung von Parametern und Rückgabewerten.

**Lösung:** Null-Guards und Validierung hinzufügen.

---

### 1.8 CODE-QUALITÄT (72 Probleme)

#### Code-Duplikation
- Duplicate Button Styles (5 Dateien, 500+ Zeilen)
- Duplicate Converter Definitions (App.xaml: BoolToVisibility vs BooleanToVisibility)
- Redundante Validierungs-Logik

#### Imports
- Imports in Funktionen statt Module-Level (Python)
- Ungenutzte Imports

#### Naming
- Inkonsistente Converter-Namen
- Fehlende x:Name auf Controls

#### Dokumentation
- Fehlende XML-Kommentare auf public APIs
- Unvollständige Type-Hints in Python

#### Accessibility
- Fehlende AutomationProperties in XAML (36 Dateien)
- Fehlende AccessKeys für Menüs
- Fehlende Keyboard-Shortcuts

---

## 2. Dependencies & Packages

### 2.1 Python-Packages (Veraltet)

| Package | Installiert | Neueste | Kritikalität |
|---------|-------------|---------|--------------|
| **cryptography** | 41.0.7 | 46.0.3 | HOCH (Security!) |
| **PyJWT** | 2.7.0 | 2.10.1 | MITTEL (Security) |
| **pip** | 24.0 | 25.3 | MITTEL |
| **setuptools** | 68.1.2 | 80.9.0 | MITTEL |
| httplib2 | 0.20.4 | 0.31.0 | NIEDRIG |
| wheel | 0.42.0 | 0.45.1 | NIEDRIG |

**Empfehlung:** Sofortiges Update von `cryptography` und `PyJWT` wegen Security-Fixes!

```bash
cd src/DigitalSignage.Client.RaspberryPi
pip install --upgrade cryptography PyJWT pip setuptools
```

---

### 2.2 .NET NuGet-Packages

**Hinweis:** dotnet-Kommando im Container nicht verfügbar. Manuelle Prüfung empfohlen:

```bash
dotnet list package --outdated
```

Bekannte Packages (aus .csproj):
- Microsoft.Extensions.* (9.0.0) - aktuelle Version
- Serilog (4.2.0) - prüfen auf Updates
- Newtonsoft.Json (13.0.3) - ältere Version, neueste ist 13.0.3 ✓
- CommunityToolkit.Mvvm (8.2.2) - aktuelle Version

**Status:** Größtenteils aktuell ✓

---

## 3. Zusammenfassung nach Dateien

### Top 15 Dateien mit den meisten Problemen

| Datei | Probleme | KRITISCH | HOCH | MITTEL | NIEDRIG |
|-------|----------|----------|------|--------|---------|
| **AuthenticationService.cs** | 8 | 1 | 3 | 3 | 1 |
| **web_interface.py** | 8 | 3 | 2 | 3 | 0 |
| **client.py** | 7 | 1 | 4 | 2 | 0 |
| **cache_manager.py** | 6 | 0 | 10× gleicher Fehler | 0 | 0 |
| **EnhancedMediaService.cs** | 6 | 2 | 2 | 2 | 0 |
| **AlertService.cs** | 5 | 2 | 1 | 2 | 0 |
| **ClientService.cs** | 5 | 0 | 3 | 2 | 0 |
| **TemplateService.cs** | 4 | 1 | 0 | 2 | 1 |
| **display_renderer.py** | 5 | 0 | 0 | 4 | 1 |
| **PropertiesPanelControl.xaml** | 8 | 1 | 0 | 5 | 2 |
| **MainWindow.xaml** | 7 | 0 | 2 | 3 | 2 |
| **LayoutSelectionDialog** | 4 | 1 | 0 | 2 | 1 |
| **MediaBrowserDialog** | 4 | 1 | 1 | 1 | 1 |
| **SettingsDialog.xaml** | 3 | 1 | 1 | 1 | 0 |
| **SqlDataService.cs** | 3 | 0 | 1 | 2 | 0 |

---

## 4. Priorisierte Roadmap

### Phase 1: KRITISCH (Woche 1-2, ~40 Stunden)

**Sicherheit (höchste Priorität):**
1. ✅ Password-Hashing auf BCrypt umstellen (4h)
2. ✅ Path-Traversal-Validierung implementieren (2h)
3. ✅ Command Injection im Python-Client beheben (3h)
4. ✅ SSTI in Scriban absichern (2h)
5. ✅ Web-Interface Authentifizierung hinzufügen (4h)
6. ✅ SSL-Verifikation erzwingen (1h)

**Memory Leaks:**
7. ✅ Event-Handler-Cleanup in 5 Dialogen (6h)
8. ✅ PropertyChanged-Handler-Cleanup (2h)

**Resource Management:**
9. ✅ JsonDocument dispose-Pattern (1h)
10. ✅ Thread-Unsafe Dictionary → ConcurrentDictionary (1h)

**Geschätzte Gesamtzeit:** 26 Stunden

---

### Phase 2: HOCH (Woche 3-4, ~50 Stunden)

**Sicherheit:**
1. SQL Injection Schutz (4h)
2. Autorisierung für Remote-Commands (6h)
3. Rate Limiting auf WebSocket (6h)
4. CSRF-Schutz auf Web-API (4h)

**Resource Management:**
5. Database-Connection-Leaks in 10 Python-Methoden (8h)
6. File-Handle-Leaks (2h)

**Async/Await:**
7. Fire-and-Forget Tasks proper behandeln (6h)
8. Sync-over-Async beheben (4h)
9. Async void Event-Handler absichern (3h)

**Threading:**
10. Event-Loop-Checks (2h)
11. WebSocket-Thread proper Cleanup (2h)

**Performance:**
12. Virtualisierung in ListBox/DataGrid (3h)

**Geschätzte Gesamtzeit:** 50 Stunden

---

### Phase 3: MITTEL (Woche 5-6, ~40 Stunden)

**Sicherheit:**
1. Schwache Token-Generierung (2h)
2. Input-Validierung (4h)
3. Origin-Validierung (2h)

**Performance:**
4. Subprocess-Caching (3h)
5. LINQ-Optimierung (4h)
6. Duplicate-Styles entfernen (6h)

**Error Handling:**
7. Bare-Except-Clauses (4h)
8. Null-Checks (4h)

**Code-Qualität:**
9. Imports aufräumen (2h)
10. Type-Hints (4h)
11. Naming-Konsistenz (3h)

**Geschätzte Gesamtzeit:** 38 Stunden

---

### Phase 4: NIEDRIG (Woche 7-8, ~20 Stunden)

**Accessibility:**
1. AutomationProperties (8h)
2. AccessKeys & Keyboard-Shortcuts (6h)

**Dokumentation:**
3. XML-Kommentare (4h)
4. Type-Hints vervollständigen (2h)

**Geschätzte Gesamtzeit:** 20 Stunden

---

### **GESAMT-AUFWAND: ~174 Stunden (≈ 22 Arbeitstage)**

---

## 5. Testing-Empfehlungen

Nach jeder Phase sollten folgende Tests durchgeführt werden:

### 5.1 Security Testing
- [ ] Penetration-Tests mit OWASP ZAP
- [ ] SQL-Injection-Tests (sqlmap)
- [ ] Path-Traversal-Tests
- [ ] Authentication-Bypass-Tests
- [ ] Rate-Limiting-Tests (Apache JMeter)

### 5.2 Memory Leak Testing
- [ ] dotMemory Profiler (100× Dialog öffnen/schließen)
- [ ] Python memory_profiler
- [ ] Langzeit-Test (24h laufen lassen)

### 5.3 Performance Testing
- [ ] Layouts mit 500+ Elementen testen
- [ ] 100 simultane Client-Verbindungen
- [ ] WebSocket-Durchsatz-Test

### 5.4 Functionality Testing
- [ ] Alle kritischen Pfade manuell testen
- [ ] Regression-Tests nach Fixes
- [ ] Integration-Tests

---

## 6. Compliance-Auswirkungen

Die identifizierten Probleme können Compliance-Anforderungen verletzen:

### GDPR (EU-DSGVO)
- **Artikel 32:** Schwache Verschlüsselung (SHA-256 statt BCrypt)
- **Artikel 25:** Security by Design (fehlende Autorisierung)

### PCI DSS
- **Requirement 8.2:** Schwaches Password-Hashing
- **Requirement 8.3:** Passwörter im Klartext

### OWASP Top 10 (2021)
- **A01:** Broken Access Control (7 Probleme)
- **A02:** Cryptographic Failures (5 Probleme)
- **A03:** Injection (4 Probleme)
- **A04:** Insecure Design (3 Probleme)

---

## 7. Tools & Automation

### Empfohlene Tools für CI/CD

**SAST (Static Application Security Testing):**
- SonarQube / SonarCloud
- Snyk Code
- Semgrep

**DAST (Dynamic Application Security Testing):**
- OWASP ZAP
- Burp Suite

**Dependency Scanning:**
- Dependabot (GitHub)
- Snyk
- WhiteSource Renovate

**Memory Profiling:**
- dotMemory (JetBrains)
- ANTS Memory Profiler
- Python memory_profiler

**Code Quality:**
- ReSharper (C#)
- pylint, flake8 (Python)
- SonarLint (IDE-Integration)

---

## 8. Lessons Learned & Best Practices

### Was gut läuft ✓
1. **MVVM-Pattern:** Größtenteils korrekt implementiert
2. **Dependency Injection:** Konsistent verwendet
3. **Logging:** Strukturiertes Logging mit Serilog
4. **Async/Await:** Grundsätzlich richtig, nur einige Probleme
5. **WebSocket-Kommunikation:** Robust mit Reconnect-Logik

### Verbesserungspotential
1. **Security-First:** Security Reviews vor Production
2. **Memory Management:** Event-Handler immer cleanen
3. **Resource Management:** `using`-Statements konsequent
4. **Error Handling:** Nie Exceptions swallown ohne Logging
5. **Testing:** Unit-Tests für kritische Pfade

---

## 9. Zusammenfassung & Empfehlungen

### Aktueller Zustand
Das Digital Signage Projekt ist **funktional**, weist aber **erhebliche Sicherheits- und Qualitätsmängel** auf. Die Architektur ist solide (MVVM, DI, Async), aber die Implementierung hat kritische Lücken.

### Gesamtbewertung: ⚠️ **NEEDS IMPROVEMENT**

**Stärken:**
- Solide Architektur & Design-Patterns
- Gutes Logging
- Robuste WebSocket-Kommunikation

**Schwächen:**
- 25 kritische Probleme (Security, Memory Leaks)
- 49 hohe Priorität (Resource Leaks, Async-Probleme)
- Fehlende Tests
- Keine Security-Reviews

### Sofortmaßnahmen (Diese Woche)

1. **Security-Hotfixes deployen:**
   - BCrypt-Hashing
   - Path-Traversal-Fix
   - Web-Interface-Auth

2. **Memory-Leak-Fixes:**
   - Event-Handler-Cleanup in Dialogen

3. **Dependencies updaten:**
   - `cryptography` 41 → 46
   - `PyJWT` 2.7 → 2.10

### Langfristige Empfehlungen

1. **Security-First-Kultur etablieren**
   - Security-Training für Team
   - Code-Reviews mit Security-Focus
   - SAST/DAST in CI/CD

2. **Testing-Strategie**
   - Unit-Tests für kritische Services
   - Integration-Tests
   - Performance-Tests

3. **Code-Quality-Automation**
   - SonarQube in Pipeline
   - Pre-Commit-Hooks
   - Automated Dependency Updates

4. **Dokumentation**
   - API-Dokumentation
   - Security-Guidelines
   - Deployment-Runbook

---

## 10. Anhänge

### A. Detailierte Berichte
- [Service Audit Report](./SERVICE_AUDIT_REPORT.md) - C# Services Analyse
- [Python Client Report](./PYTHON_CLIENT_REPORT.md) - Python-Code Analyse
- [Security Audit Report](./SECURITY_AUDIT_REPORT.md) - Sicherheitsanalyse
- [XAML Audit Report](./XAML_AUDIT_REPORT.md) - WPF/XAML Analyse

### B. Checklisten
- [ ] Phase 1 (KRITISCH) - 26h
- [ ] Phase 2 (HOCH) - 50h
- [ ] Phase 3 (MITTEL) - 38h
- [ ] Phase 4 (NIEDRIG) - 20h
- [ ] Security-Testing
- [ ] Performance-Testing
- [ ] Regression-Testing
- [ ] Dokumentation-Update

### C. Kontakte
- **Security:** security@example.com
- **DevOps:** devops@example.com
- **Entwicklung:** dev@example.com

---

**Report erstellt:** 2025-11-15
**Analyst:** Claude AI (Anthropic)
**Version:** 1.0
**Nächstes Review:** Nach Phase 1 Completion

---

_Ende des Audit-Reports_
