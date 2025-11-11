# System-Architektur

## Übersicht

Das Digital Signage System folgt einer Client-Server-Architektur mit Echtzeit-Kommunikation über WebSockets.

## Komponenten

### 1. Windows Server/Manager

#### Technologie-Stack
- **Framework**: WPF (Windows Presentation Foundation) mit .NET 8
- **Pattern**: MVVM (Model-View-ViewModel)
- **DI**: Microsoft.Extensions.DependencyInjection
- **UI-Framework**: CommunityToolkit.MVVM
- **Logging**: Serilog
- **Datenbank**: Entity Framework Core + Dapper
- **Kommunikation**: WebSocket (System.Net.WebSockets)

#### Schichten

```
┌─────────────────────────────────────────┐
│           Presentation Layer            │
│  ┌────────────────────────────────────┐ │
│  │  Views (XAML)                      │ │
│  │  ├─ MainWindow                     │ │
│  │  ├─ DesignerView                   │ │
│  │  ├─ DeviceManagementView           │ │
│  │  └─ DataSourceView                 │ │
│  └────────────────────────────────────┘ │
│  ┌────────────────────────────────────┐ │
│  │  ViewModels                        │ │
│  │  ├─ MainViewModel                  │ │
│  │  ├─ DesignerViewModel              │ │
│  │  ├─ DeviceManagementViewModel      │ │
│  │  └─ DataSourceViewModel            │ │
│  └────────────────────────────────────┘ │
└─────────────────────────────────────────┘
                    │
┌─────────────────────────────────────────┐
│            Business Layer               │
│  ┌────────────────────────────────────┐ │
│  │  Services                          │ │
│  │  ├─ LayoutService                  │ │
│  │  ├─ ClientService                  │ │
│  │  ├─ CommunicationService           │ │
│  │  ├─ DataService                    │ │
│  │  └─ MediaService                   │ │
│  └────────────────────────────────────┘ │
└─────────────────────────────────────────┘
                    │
┌─────────────────────────────────────────┐
│              Data Layer                 │
│  ┌────────────────────────────────────┐ │
│  │  Repositories                      │ │
│  │  ├─ LayoutRepository               │ │
│  │  ├─ ClientRepository               │ │
│  │  └─ DataSourceRepository           │ │
│  └────────────────────────────────────┘ │
│  ┌────────────────────────────────────┐ │
│  │  DbContext / Dapper                │ │
│  └────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

### 2. Core Library

Enthält gemeinsam genutzte Modelle und Interfaces:
- **Models**: Domain-Entitäten (DisplayLayout, RaspberryPiClient, etc.)
- **Interfaces**: Service-Contracts
- **DTOs**: Datenübertragungsobjekte

### 3. Data Layer

- **SQL Service**: Verbindung zu SQL Server
- **Entity Framework**: ORM für komplexe Abfragen
- **Dapper**: Für Performance-kritische Queries
- **Repository Pattern**: Abstraktion der Datenzugriffe

### 4. Raspberry Pi Client

#### Technologie-Stack
- **Sprache**: Python 3.9+
- **Display**: PyQt5
- **WebSocket**: python-socketio
- **System**: psutil für Hardware-Monitoring
- **Process Management**: systemd

#### Komponenten

```
┌─────────────────────────────────────────┐
│         DigitalSignageClient            │
│  ┌────────────────────────────────────┐ │
│  │  WebSocket Client                  │ │
│  │  ├─ Connection Manager             │ │
│  │  ├─ Message Handler                │ │
│  │  └─ Reconnection Logic             │ │
│  └────────────────────────────────────┘ │
│  ┌────────────────────────────────────┐ │
│  │  Display Renderer                  │ │
│  │  ├─ Layout Parser                  │ │
│  │  ├─ Element Renderer               │ │
│  │  └─ Data Binding                   │ │
│  └────────────────────────────────────┘ │
│  ┌────────────────────────────────────┐ │
│  │  Device Manager                    │ │
│  │  ├─ Hardware Info                  │ │
│  │  ├─ System Commands                │ │
│  │  └─ Status Monitoring              │ │
│  └────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

## Kommunikationsprotokoll

### WebSocket-Verbindung

```
Client                          Server
  │                               │
  ├──── Connect (WS) ────────────>│
  │<─── Connected ────────────────┤
  │                               │
  ├──── REGISTER ────────────────>│
  │<─── ACK ──────────────────────┤
  │                               │
  ├──── HEARTBEAT (30s) ─────────>│
  │<─── ACK ──────────────────────┤
  │                               │
  │<─── DISPLAY_UPDATE ───────────┤
  ├──── ACK ─────────────────────>│
  │                               │
  │<─── COMMAND ──────────────────┤
  ├──── STATUS_REPORT ───────────>│
  │                               │
```

### Nachrichtenformat

Alle Nachrichten folgen einem einheitlichen JSON-Format:

```json
{
  "Id": "uuid",
  "Type": "MESSAGE_TYPE",
  "Timestamp": "2024-01-01T12:00:00Z",
  "SenderId": "client-or-server-id",
  "Payload": { ... }
}
```

### Nachrichtentypen

| Typ | Richtung | Beschreibung |
|-----|----------|--------------|
| REGISTER | C→S | Client-Registrierung |
| HEARTBEAT | C→S | Lebenszeichen |
| DISPLAY_UPDATE | S→C | Layout-Update |
| STATUS_REPORT | C→S | Status-Bericht |
| COMMAND | S→C | Steuerbefehl |
| SCREENSHOT | S→C, C→S | Screenshot-Request/Response |
| LOG | C→S | Log-Nachricht |

## Datenfluss

### Layout-Erstellung und -Verteilung

```
1. Designer erstellt Layout in UI
   └─> Layout-JSON wird generiert
       └─> In Datenbank gespeichert
           └─> Layout-ID wird generiert

2. Admin weist Layout einem Client zu
   └─> AssignLayoutCommand
       └─> Server lädt Layout aus DB
           └─> DISPLAY_UPDATE wird an Client gesendet
               └─> Client rendert Layout
```

### Dynamische Daten-Updates

```
1. SQL-Datenquelle konfiguriert
   └─> Query mit Refresh-Intervall

2. Server führt Query periodisch aus
   └─> Daten werden gecacht
       └─> Bei Änderung: DISPLAY_UPDATE an Clients
           └─> Nur geänderte Daten übertragen (Delta)

3. Client empfängt Update
   └─> Variablen in Layout werden ersetzt
       └─> UI wird aktualisiert
```

## Skalierbarkeit

### Horizontale Skalierung

Der Server kann horizontal skaliert werden durch:
- Load Balancer vor mehreren Server-Instanzen
- Redis für geteilten Session-State
- Zentrale Datenbank für alle Instanzen

### Vertikale Optimierung

- Connection Pooling für SQL
- WebSocket-Kompression
- Caching von häufig abgerufenen Daten
- Lazy Loading von Medien-Dateien

## Fehlerbehandlung

### Client-Seite

```python
try:
    await sio.connect(server_url)
except Exception as e:
    logger.error(f"Connection failed: {e}")
    # Exponential backoff retry
    await asyncio.sleep(retry_delay)
    retry_delay = min(retry_delay * 2, 60)
```

### Offline-Modus

Wenn die Verbindung zum Server verloren geht:
1. Client zeigt letztes bekanntes Layout
2. Gecachte Daten werden verwendet
3. Automatische Wiederverbindung im Hintergrund
4. Nach Reconnect: Status-Synchronisation

## Sicherheit

### Transport-Verschlüsselung

- TLS 1.2+ für WebSocket-Verbindungen
- Zertifikat-basierte Authentifizierung optional

### Authentifizierung

```
1. Client sendet API-Key bei REGISTER
2. Server validiert Key gegen Datenbank
3. Session-Token wird generiert
4. Alle weiteren Requests mit Token
```

### Autorisierung

- Rollbasiertes Zugriffsmodell (RBAC)
- Granulare Berechtigungen pro Client
- Audit-Log für alle Aktionen

## Monitoring und Logging

### Server-Seite

- Serilog für strukturiertes Logging
- Log-Levels: Debug, Info, Warning, Error, Critical
- Rotation nach Datum und Größe
- Zentrales Log-Aggregation möglich (z.B. ELK-Stack)

### Client-Seite

- Python logging Framework
- systemd journal Integration
- Remote-Logging an Server optional
- Lokale Log-Rotation

## Performance-Optimierung

### Server

- Async/Await für I/O-Operations
- Thread-Pool für CPU-intensive Tasks
- Object Pooling für häufig genutzte Objekte
- Garbage Collection Tuning

### Client

- Hardware-Beschleunigung für Rendering
- Lazy Loading von Bildern
- Virtualisierung für große Listen
- Memory Profiling für Leak-Detection

## Disaster Recovery

### Backup-Strategie

- Automatische Datenbank-Backups (täglich)
- Layout-Export als JSON
- Konfiguration in Version Control
- Client-Konfiguration auf SD-Karte

### Wiederherstellung

1. Datenbank aus Backup wiederherstellen
2. Server-Software neu installieren
3. Clients registrieren sich automatisch neu
4. Layouts aus Export wiederherstellen
