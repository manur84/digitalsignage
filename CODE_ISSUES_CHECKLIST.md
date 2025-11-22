# Code Issues Checklist
Generiert: 2025-11-22

## Übersicht
- **Kritische Fehler:** 8
- **Warnungen:** 14
- **Verbesserungen:** 11

## 🔴 KRITISCH (Sofort beheben)

| # | Datei | Zeile | Methode | Problem | Fix |
|---|-------|-------|---------|---------|-----|
| 1 | HealthCheckService.cs | 53 | ExecuteAsync() | Fire-and-forget Task ohne await | `_ = Task.Run()` ersetzen durch tracked Task mit await oder ContinueWith |
| 2 | MetricsEndpointService.cs | 48 | StartAsync() | Fire-and-forget Task ohne await | Handler Task tracken und bei StopAsync awaiten |
| 3 | WebSocketService.cs (Mobile) | 477 | Dispose() | Sync .Wait() in Dispose kann Deadlock verursachen | Async Dispose Pattern implementieren oder FireAndForget verwenden |
| 4 | AlertService.cs | 390-407 | ParseConfiguration() | JsonDocument nicht disposed wenn Exception | using-Block verwenden für JsonDocument.Parse |
| 5 | WebSocketService.cs (Mobile) | 396 | ProcessReceivedMessage() | JsonDocument nicht in using-Block | `using var jsonDoc = JsonDocument.Parse(message)` |
| 6 | DatabaseInitializer.cs | 215, 237 | InitializeDatabase() | ExecuteSqlRaw ohne Parameter-Sanitization | Parameterisierte Queries verwenden |
| 7 | MessageHandlerService.cs | 80, 119 | HandleMessageAsync() | Task.Run ohne proper Exception Handling | Task in Collection tracken und Exceptions aggregieren |
| 8 | BackupService.cs | 71-244 | Mehrere Methoden | Task.Run für File I/O unnötig | File.Copy direkt ohne Task.Run verwenden oder File.CopyAsync |

## 🟡 WARNUNG (Bald beheben)

| # | Datei | Zeile | Methode | Problem | Fix |
|---|-------|-------|---------|---------|-----|
| 9 | RemoteClientInstallerService.cs | 194, 212, 233 | ExecuteInstallationAsync() | Task.Run für Stream-Reading ohne Timeout | CancellationToken mit Timeout verwenden |
| 10 | MediaService.cs | 177, 217, 296, 319 | Mehrere | Task.Run für synchrone File Operations | Async File APIs verwenden |
| 11 | EnhancedMediaService.cs | 302, 468 | DeleteMediaAsync(), GenerateThumbnailAsync() | Task.Run für synchrone Operations | Direkte async Implementierung |
| 12 | RemoteSshConnectionManager.cs | 91 | ConnectAsync() | Task.Run ohne Timeout-Handling | CancellationToken mit Timeout kombinieren |
| 13 | SqlDataService.cs | 398 | GetAvailableColumnsAsync() | SQL String Concatenation | StringBuilder oder Interpolated Strings |
| 14 | LogStorageService.cs | 161 | ExportLogs() | String Concatenation in LINQ | StringBuilder für Performance |
| 15 | NetworkScannerService.cs | - | ScanNetworkAsync() | Kein Dispose für UdpClient | using-Block hinzufügen |
| 16 | MdnsDiscoveryService.cs | - | DiscoverAsync() | Potentielles Resource Leak | IDisposable Pattern prüfen |
| 17 | WebSocketCommunicationService.cs | 160, 443 | StartAsync(), AcceptClientsAsync() | Task.Run für lang laufende Operations | HostedService Pattern verwenden |
| 18 | ClientService.cs | - | Mehrere | ConcurrentDictionary ohne Timeout für alte Einträge | Cleanup-Timer implementieren |
| 19 | LogStorageService.cs | 175 | GetStatistics() | Dictionary statt ConcurrentDictionary in async Context | ConcurrentDictionary verwenden |
| 20 | AlertService.cs | 394 | ParseConfiguration() | Dictionary Rückgabe nicht thread-safe | ImmutableDictionary oder ConcurrentDictionary |
| 21 | SystemDiagnosticsService.cs | 105 | GetDiagnosticsAsync() | Kommentar über .Result vermeiden | Code bereits korrekt, Kommentar entfernen |
| 22 | Python Client | - | Exception Handling | Bare except clauses | Spezifische Exceptions catchen |

## 🔵 VERBESSERUNG (Bei Gelegenheit)

| # | Datei | Zeile | Methode | Problem | Fix |
|---|-------|-------|---------|---------|-----|
| 23 | WebSocketCommunicationService.cs | - | Gesamt | 2652 Zeilen in einer Datei | Service in kleinere Services aufteilen |
| 24 | MessageHandlers | - | Alle | Keine Unit Tests | Tests für kritische Handler schreiben |
| 25 | Services allgemein | - | - | 25+ Services ohne klare Boundaries | Service Layer Refactoring |
| 26 | ViewModels | - | - | 15+ ViewModels mit viel Business Logic | Logic in Services verschieben |
| 27 | Error Handling | - | Global | Inkonsistente Exception Behandlung | Global Exception Handler |
| 28 | Logging | - | - | Mix aus Console.WriteLine und Logger | Nur ILogger verwenden |
| 29 | Configuration | - | - | Hardcoded Ports und Timeouts | Alle in appsettings.json |
| 30 | Python client.py | - | - | Monolithische Datei | In Module aufteilen |
| 31 | SSL/TLS | - | - | Self-signed Certificate ohne Validation | Certificate Pinning implementieren |
| 32 | Database | - | - | SQLite ohne Connection Pooling | Connection Pool konfigurieren |
| 33 | Memory | - | - | Keine Memory Leak Detection | Memory Profiling einrichten |

## Zusammenfassung & Prioritäten

### Quick Wins (< 30 min)
- [x] Issue #3: WebSocketService.Wait() in Dispose ersetzen
- [x] Issue #4,5: JsonDocument in using-Blocks
- [x] Issue #21: Veralteten Kommentar entfernen

### Diese Woche
- [ ] Issue #1,2,7: Fire-and-forget Tasks tracken
- [ ] Issue #6: SQL Injection Gefahr beheben
- [ ] Issue #8: Unnötige Task.Run entfernen
- [ ] Issue #9-13: Task.Run mit Timeouts versehen

### Nächster Sprint
- [ ] Issue #23: WebSocketCommunicationService aufteilen
- [ ] Issue #24: Unit Tests für MessageHandlers
- [ ] Issue #25-26: Service Layer Refactoring

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