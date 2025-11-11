# API-Dokumentation

## WebSocket-API

### Verbindung

**Endpoint**: `ws://<server>:8080/ws/`

**Protokoll**: WebSocket über HTTP/HTTPS

### Authentifizierung

Authentifizierung erfolgt nach der Verbindung über die REGISTER-Nachricht.

## Nachrichtentypen

### 1. REGISTER

**Richtung**: Client → Server

**Beschreibung**: Registriert einen neuen Client beim Server

**Format**:
```json
{
  "Id": "message-uuid",
  "Type": "REGISTER",
  "Timestamp": "2024-01-01T12:00:00Z",
  "SenderId": "client-id",
  "ClientId": "unique-client-id",
  "MacAddress": "aa:bb:cc:dd:ee:ff",
  "IpAddress": "192.168.1.100",
  "DeviceInfo": {
    "Model": "Raspberry Pi 4 Model B",
    "OsVersion": "Linux 5.15.0",
    "ClientVersion": "1.0.0",
    "CpuTemperature": 45.5,
    "CpuUsage": 12.3,
    "MemoryTotal": 4294967296,
    "MemoryUsed": 1073741824,
    "DiskTotal": 32000000000,
    "DiskUsed": 8000000000,
    "ScreenWidth": 1920,
    "ScreenHeight": 1080,
    "NetworkLatency": 5,
    "Uptime": 3600
  }
}
```

**Antwort**: Server bestätigt Registrierung und sendet ggf. initiales Layout

### 2. HEARTBEAT

**Richtung**: Client → Server (alle 30 Sekunden)

**Beschreibung**: Lebenszeichen des Clients

**Format**:
```json
{
  "Id": "message-uuid",
  "Type": "HEARTBEAT",
  "Timestamp": "2024-01-01T12:00:00Z",
  "SenderId": "client-id",
  "ClientId": "unique-client-id",
  "Status": "Online",
  "DeviceInfo": {
    "CpuTemperature": 45.5,
    "CpuUsage": 12.3,
    "MemoryUsed": 1073741824,
    "Uptime": 3600
  }
}
```

### 3. DISPLAY_UPDATE

**Richtung**: Server → Client

**Beschreibung**: Aktualisiert das angezeigte Layout

**Format**:
```json
{
  "Id": "message-uuid",
  "Type": "DISPLAY_UPDATE",
  "Timestamp": "2024-01-01T12:00:00Z",
  "SenderId": "server",
  "Layout": {
    "Id": "layout-uuid",
    "Name": "Room Display",
    "Version": "1.0",
    "Resolution": {
      "Width": 1920,
      "Height": 1080
    },
    "BackgroundColor": "#FFFFFF",
    "Elements": [
      {
        "Id": "element-1",
        "Type": "text",
        "Name": "Room Name",
        "Position": { "X": 100, "Y": 100 },
        "Size": { "Width": 400, "Height": 60 },
        "ZIndex": 1,
        "Rotation": 0,
        "Opacity": 1.0,
        "Visible": true,
        "DataBinding": "{{room.name}}",
        "Properties": {
          "Content": "{{room.name}}",
          "FontFamily": "Arial",
          "FontSize": 32,
          "Color": "#000000",
          "TextAlign": "center"
        }
      }
    ],
    "DataSources": [
      {
        "Id": "datasource-1",
        "Name": "Room Data",
        "Type": "SQL",
        "Query": "SELECT * FROM rooms WHERE id = @roomId",
        "RefreshInterval": 60
      }
    ]
  },
  "Data": {
    "room": {
      "name": "Conference Room A",
      "status": "Available",
      "capacity": 12
    }
  },
  "ForceRefresh": false
}
```

### 4. STATUS_REPORT

**Richtung**: Client → Server

**Beschreibung**: Detaillierter Status-Bericht vom Client

**Format**:
```json
{
  "Id": "message-uuid",
  "Type": "STATUS_REPORT",
  "Timestamp": "2024-01-01T12:00:00Z",
  "SenderId": "client-id",
  "ClientId": "unique-client-id",
  "Status": "Online",
  "DeviceInfo": {
    "Model": "Raspberry Pi 4 Model B",
    "OsVersion": "Linux 5.15.0",
    "ClientVersion": "1.0.0",
    "CpuTemperature": 45.5,
    "CpuUsage": 12.3,
    "MemoryTotal": 4294967296,
    "MemoryUsed": 1073741824,
    "DiskTotal": 32000000000,
    "DiskUsed": 8000000000,
    "ScreenWidth": 1920,
    "ScreenHeight": 1080,
    "NetworkLatency": 5,
    "Uptime": 3600
  },
  "CurrentLayoutId": "layout-uuid",
  "ErrorMessage": null
}
```

### 5. COMMAND

**Richtung**: Server → Client

**Beschreibung**: Sendet einen Steuerbefehl an den Client

**Format**:
```json
{
  "Id": "message-uuid",
  "Type": "COMMAND",
  "Timestamp": "2024-01-01T12:00:00Z",
  "SenderId": "server",
  "Command": "RESTART",
  "Parameters": {
    "delay": 5
  }
}
```

**Verfügbare Commands**:

| Command | Parameter | Beschreibung |
|---------|-----------|--------------|
| RESTART | delay (optional) | System neu starten |
| RESTART_APP | - | Client-App neu starten |
| SCREENSHOT | - | Screenshot erstellen |
| SCREEN_ON | - | Bildschirm einschalten |
| SCREEN_OFF | - | Bildschirm ausschalten |
| SET_VOLUME | volume (0-100) | Lautstärke einstellen |
| GET_LOGS | lines (optional) | Log-Dateien abrufen |
| CLEAR_CACHE | - | Cache leeren |

### 6. SCREENSHOT

**Richtung**: Server → Client (Request), Client → Server (Response)

**Request**:
```json
{
  "Id": "message-uuid",
  "Type": "COMMAND",
  "Command": "SCREENSHOT"
}
```

**Response**:
```json
{
  "Id": "message-uuid",
  "Type": "SCREENSHOT",
  "Timestamp": "2024-01-01T12:00:00Z",
  "SenderId": "client-id",
  "ClientId": "unique-client-id",
  "ImageData": "base64-encoded-png-data",
  "Format": "png"
}
```

### 7. LOG

**Richtung**: Client → Server

**Beschreibung**: Log-Nachricht vom Client

**Format**:
```json
{
  "Id": "message-uuid",
  "Type": "LOG",
  "Timestamp": "2024-01-01T12:00:00Z",
  "SenderId": "client-id",
  "ClientId": "unique-client-id",
  "Level": "Error",
  "Message": "Failed to load image: file not found",
  "Exception": "FileNotFoundException: /path/to/image.png"
}
```

**Log Levels**: Debug, Info, Warning, Error, Critical

## Layout-Schema

### DisplayLayout

```typescript
interface DisplayLayout {
  id: string;
  name: string;
  version: string;
  created: string; // ISO 8601
  modified: string; // ISO 8601
  resolution: Resolution;
  backgroundImage?: string;
  backgroundColor?: string;
  elements: DisplayElement[];
  dataSources: DataSource[];
  metadata: Record<string, any>;
}
```

### Resolution

```typescript
interface Resolution {
  width: number;
  height: number;
  orientation: "landscape" | "portrait";
}
```

### DisplayElement

```typescript
interface DisplayElement {
  id: string;
  type: "text" | "image" | "shape" | "qrcode" | "table" | "datetime";
  name: string;
  position: Position;
  size: Size;
  zIndex: number;
  rotation: number; // 0-360
  opacity: number; // 0-1
  visible: boolean;
  dataBinding?: string; // {{variable.path}}
  properties: Record<string, any>;
  animation?: Animation;
}
```

### Position & Size

```typescript
interface Position {
  x: number;
  y: number;
  unit: "px" | "%";
}

interface Size {
  width: number;
  height: number;
  unit: "px" | "%";
}
```

### Animation

```typescript
interface Animation {
  type: "fade" | "slide" | "none";
  duration: number; // milliseconds
  easing: string; // CSS easing function
}
```

### DataSource

```typescript
interface DataSource {
  id: string;
  name: string;
  type: "SQL" | "REST" | "StaticData" | "StoredProcedure";
  connectionString: string;
  query: string;
  parameters: Record<string, any>;
  refreshInterval: number; // seconds
  lastRefresh?: string; // ISO 8601
  enabled: boolean;
  metadata: Record<string, any>;
}
```

## Element-Properties

### Text Element

```typescript
interface TextProperties {
  content: string;
  fontFamily: string;
  fontSize: number;
  fontWeight: "normal" | "bold";
  fontStyle: "normal" | "italic";
  color: string; // hex color
  textAlign: "left" | "center" | "right";
  verticalAlign: "top" | "middle" | "bottom";
  wordWrap: boolean;
}
```

### Image Element

```typescript
interface ImageProperties {
  source: string; // URL or base64
  fit: "contain" | "cover" | "fill" | "none";
  altText?: string;
}
```

### Shape Element

```typescript
interface ShapeProperties {
  shapeType: "rectangle" | "circle" | "line";
  fillColor: string;
  strokeColor: string;
  strokeWidth: number;
  cornerRadius: number;
}
```

### QR Code Element

```typescript
interface QRCodeProperties {
  data: string;
  errorCorrection: "L" | "M" | "Q" | "H";
  foregroundColor: string;
  backgroundColor: string;
}
```

### Table Element

```typescript
interface TableProperties {
  columns: string[];
  headerBackground: string;
  rowBackground: string;
  alternateRowBackground: string;
  borderColor: string;
  borderWidth: number;
}
```

## Variablen-Syntax

Variablen können in Text- und QR-Code-Elementen verwendet werden:

### Einfache Variable
```
{{variableName}}
```

### Verschachtelter Pfad
```
{{object.property.subProperty}}
```

### Mit Formatierung

**Datum**:
```
{{date|dd.MM.yyyy}}
{{time|HH:mm:ss}}
```

**Zahlen**:
```
{{number|#,##0.00}}
{{currency|C2}}
```

**Text**:
```
{{text|UPPER}}
{{text|LOWER}}
{{text|CAPITALIZE}}
```

### Fallback-Wert
```
{{variable|default:"Kein Wert"}}
```

### Berechnungen
```
{{value1 + value2}}
{{price * quantity}}
```

## Fehler-Codes

| Code | Nachricht | Beschreibung |
|------|-----------|--------------|
| 1000 | Connection Error | WebSocket-Verbindungsfehler |
| 1001 | Authentication Failed | Authentifizierung fehlgeschlagen |
| 2000 | Layout Not Found | Layout existiert nicht |
| 2001 | Invalid Layout | Layout-Format ungültig |
| 3000 | Data Source Error | Fehler bei Datenquelle |
| 3001 | SQL Error | SQL-Abfrage fehlgeschlagen |
| 4000 | Command Failed | Befehl konnte nicht ausgeführt werden |
| 5000 | Internal Server Error | Interner Serverfehler |

## Rate Limiting

- Heartbeat: Max. 1 pro 10 Sekunden
- Status Report: Max. 1 pro Minute
- Commands: Max. 10 pro Minute pro Client
- Screenshots: Max. 1 pro Minute pro Client
