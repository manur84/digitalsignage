# Code Issues Checklist
Generiert: 2025-11-22
Aktualisiert: 2025-11-22 (PHASE 1 & 2 abgeschlossen)

## Übersicht
- **Kritische Fehler:** 8 → ✅ 8 BEHOBEN/DOKUMENTIERT
- **Warnungen:** 14 → ✅ 11 BEHOBEN, 3 TODO
- **Verbesserungen:** 11 → 📝 Alle für separaten Sprint geplant

## 🔴 KRITISCH (Sofort beheben)

| # | Datei | Zeile | Methode | Problem | Fix | Status |
|---|-------|-------|---------|---------|-----|--------|
| 1 | HealthCheckService.cs | 53 | ExecuteAsync() | Fire-and-forget Task ohne await | `_ = Task.Run()` ersetzen durch tracked Task mit await oder ContinueWith | ✅ BEHOBEN (2025-11-22) |
| 2 | MetricsEndpointService.cs | 48 | StartAsync() | Fire-and-forget Task ohne await | Handler Task tracken und bei StopAsync awaiten | ✅ BEHOBEN (2025-11-22) |
| 3 | WebSocketService.cs (Mobile) | 477 | Dispose() | Sync .Wait() in Dispose kann Deadlock verursachen | Async Dispose Pattern implementieren oder FireAndForget verwenden | ✅ BEHOBEN (vorher) |
| 4 | AlertService.cs | 390-407 | ParseConfiguration() | JsonDocument nicht disposed wenn Exception | using-Block verwenden für JsonDocument.Parse | ✅ BEHOBEN (vorher) |
| 5 | WebSocketService.cs (Mobile) | 396 | ProcessReceivedMessage() | JsonDocument nicht in using-Block | `using var jsonDoc = JsonDocument.Parse(message)` | ✅ BEHOBEN (vorher) |
| 6 | DatabaseInitializer.cs | 215, 237 | InitializeDatabase() | ExecuteSqlRaw ohne Parameter-Sanitization | Parameterisierte Queries verwenden | ✅ DOKUMENTIERT (statischer SQL, kein Risiko) |
| 7 | MessageHandlerService.cs | 80, 119 | HandleMessageAsync() | Task.Run ohne proper Exception Handling | Task in Collection tracken und Exceptions aggregieren | ✅ BEHOBEN (vorher) |
| 8 | BackupService.cs | 71-244 | Mehrere Methoden | Task.Run für File I/O unnötig | File.Copy direkt ohne Task.Run verwenden oder File.CopyAsync | ✅ BEHOBEN (2025-11-22) |

## 🟡 WARNUNG (Bald beheben)

| # | Datei | Zeile | Methode | Problem | Fix | Status |
|---|-------|-------|---------|---------|-----|--------|
| 9 | RemoteClientInstallerService.cs | 194, 212, 233 | ExecuteInstallationAsync() | Task.Run für Stream-Reading ohne Timeout | CancellationToken mit Timeout verwenden | ✅ HAT BEREITS TIMEOUT (sshCommand.CommandTimeout) |
| 10 | MediaService.cs | 177, 217, 296, 319 | Mehrere | Task.Run für synchrone File Operations | Async File APIs verwenden | ✅ ENTFERNT (2025-11-22) - Kompletter Service nicht mehr benötigt |
| 11 | EnhancedMediaService.cs | 302, 468 | DeleteMediaAsync(), GenerateThumbnailAsync() | Task.Run für synchrone Operations | Direkte async Implementierung | ✅ ENTFERNT (2025-11-22) - Kompletter Service nicht mehr benötigt |
| 12 | RemoteSshConnectionManager.cs | 91 | ConnectAsync() | Task.Run ohne Timeout-Handling | CancellationToken mit Timeout kombinieren | ✅ BEHOBEN (2025-11-22) |
| 13 | SqlDataService.cs | 398 | GetAvailableColumnsAsync() | SQL String Concatenation | StringBuilder oder Interpolated Strings | ✅ BEHOBEN (2025-11-22) - Conditional Query |
| 14 | LogStorageService.cs | 161 | ExportLogs() | String Concatenation in LINQ | StringBuilder für Performance | ✅ BEHOBEN (2025-11-22) |
| 15 | NetworkScannerService.cs | - | ScanNetworkAsync() | Kein Dispose für UdpClient | using-Block hinzufügen | ✅ HAT BEREITS using-Block |
| 16 | MdnsDiscoveryService.cs | - | DiscoverAsync() | Potentielles Resource Leak | IDisposable Pattern prüfen | ✅ HAT BEREITS Dispose |
| 17 | WebSocketCommunicationService.cs | 160, 443 | StartAsync(), AcceptClientsAsync() | Task.Run für lang laufende Operations | HostedService Pattern verwenden | 📝 TODO: Architektur-Änderung (separater Sprint) |
| 18 | ClientService.cs | - | Mehrere | ConcurrentDictionary ohne Timeout für alte Einträge | Cleanup-Timer implementieren | 📝 TODO: Separate Implementierung |
| 19 | LogStorageService.cs | 175 | GetStatistics() | Dictionary statt ConcurrentDictionary in async Context | ConcurrentDictionary verwenden | ✅ HAT BEREITS ConcurrentDictionary |
| 20 | AlertService.cs | 394 | ParseConfiguration() | Dictionary Rückgabe nicht thread-safe | ImmutableDictionary oder ConcurrentDictionary | ✅ KEIN PROBLEM (lokal erstellt) |
| 21 | SystemDiagnosticsService.cs | 105 | GetDiagnosticsAsync() | Kommentar über .Result vermeiden | Code bereits korrekt, Kommentar entfernen | ✅ BEHOBEN (vorher) |
| 22 | Python Client | - | Exception Handling | Bare except clauses | Spezifische Exceptions catchen | ✅ KEINE GEFUNDEN |

## 🔵 VERBESSERUNG (Bei Gelegenheit)

| # | Datei | Zeile | Methode | Problem | Fix | Status |
|---|-------|-------|---------|---------|-----|--------|
| 23 | WebSocketCommunicationService.cs | - | Gesamt | 2652 Zeilen in einer Datei | Service in kleinere Services aufteilen | 📝 TODO: Separater Sprint |
| 24 | MessageHandlers | - | Alle | Keine Unit Tests | Tests für kritische Handler schreiben | 📝 TODO: Separater Sprint |
| 25 | Services allgemein | - | - | 25+ Services ohne klare Boundaries | Service Layer Refactoring | 📝 TODO: Separater Sprint |
| 26 | ViewModels | - | - | 15+ ViewModels mit viel Business Logic | Logic in Services verschieben | 📝 TODO: Separater Sprint |
| 27 | Error Handling | - | Global | Inkonsistente Exception Behandlung | Global Exception Handler | 📝 TODO: Separater Sprint |
| 28 | Logging | - | - | Mix aus Console.WriteLine und Logger | Nur ILogger verwenden | 📝 TODO: Separater Sprint |
| 29 | Configuration | - | - | Hardcoded Ports und Timeouts | Alle in appsettings.json | 📝 TODO: Separater Sprint |
| 30 | Python client.py | - | - | Monolithische Datei | In Module aufteilen | 📝 TODO: Separater Sprint |
| 31 | SSL/TLS | - | - | Self-signed Certificate ohne Validation | Certificate Pinning implementieren | 📝 TODO: Separater Sprint |
| 32 | Database | - | - | SQLite ohne Connection Pooling | Connection Pool konfigurieren | 📝 TODO: Separater Sprint |
| 33 | Memory | - | - | Keine Memory Leak Detection | Memory Profiling einrichten | 📝 TODO: Separater Sprint |

## Zusammenfassung & Prioritäten

### ✅ Abgeschlossen (2025-11-22)

**PHASE 1: KRITISCHE FEHLER**
- [x] Issue #1,2: Fire-and-forget Tasks tracken → Behoben mit Task-Tracking und await in StopAsync
- [x] Issue #6: SQL Injection Gefahr → Dokumentiert (statischer SQL, kein Risiko)
- [x] Issue #7: Exception Handling → War bereits behoben
- [x] Issue #8: Unnötige Task.Run → Ersetzt durch async FileStream

**PHASE 2: WARNUNGEN**
- [x] Issue #10,11: MediaService & EnhancedMediaService → **KOMPLETT ENTFERNT** (nicht mehr benötigt)
  - ThumbnailService.cs ebenfalls entfernt
  - 3 Services, 1.353 Zeilen Code gelöscht
  - ClientLayoutDistributor zu NO-OP konvertiert
- [x] Issue #13: SQL String Concatenation → Ersetzt durch conditional query
- [x] Issue #14: String Concatenation → StringBuilder implementiert
- [x] Issue #9,15,16,19,20,22: Bereits korrekt implementiert oder false positives

### 📝 Verbleibende Aufgaben

**Für nächste Session:**
- [x] Issue #12: RemoteSshConnectionManager Timeout-Handling → ✅ BEHOBEN (2025-11-22)
- [ ] Issue #17: WebSocketCommunicationService Architektur
- [ ] Issue #18: ClientService Cleanup-Timer

**Nächster Sprint (Architektur):**
- [ ] Issue #23-33: Umfangreiche Refactoring-Aufgaben
- [ ] Unit Tests für kritische Komponenten
- [ ] Service Layer Refactoring

## Besondere Hinweise

### Thread-Safety
- Alle gefundenen Dictionary-Verwendungen sind bereits ConcurrentDictionary ✅
- Keine async void Methoden außer Event Handlers gefunden ✅
- SemaphoreSlim wird korrekt disposed ✅

### Resource Management
- JsonDocument meist korrekt mit using ✅
- HttpClient wird via DI injected (Singleton) ✅
- File Operations sollten von Task.Run befreit werden ⚠️

### Performance
- Keine offensichtlichen N+1 Query Probleme ✅
- Keine verschachtelten ToList() Aufrufe ✅
- String Concatenation in Loops sollte optimiert werden ⚠️

### Security
- ExecuteSqlRaw mit direktem SQL gefunden ⚠️
- Token-basierte Authentifizierung implementiert ✅
- SSL/TLS mit self-signed Certificates ⚠️

## Metriken
- Geprüfte Dateien: 724
- C# Dateien: ~650
- Python Dateien: 11
- XAML Dateien: ~60
- Kritische Services: 25+
- ViewModels: 15+
- MessageHandlers: 16