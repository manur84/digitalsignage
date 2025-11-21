# Auto-Discovery Flow Analyse

## Problem-Beschreibung

1. **Keine IP-Auswahl im Web-Interface**: User kann nicht zwischen entdeckten Server-IPs wählen
2. **Falsche IP-Priorisierung**: Client bevorzugt nicht 192.168.x.x gegenüber 10.x.x.x
3. **Localhost-Verbindung**: Client könnte versuchen sich mit localhost zu verbinden

---

## Auto-Discovery Ablauf (Aktueller Zustand)

### 1. Config Initialization (`config.py`)

```python
server_host: str = "localhost"  # DEFAULT VALUE!
auto_discover: bool = True
discovery_timeout: float = 5.0
```

**PROBLEM**: Default `server_host = "localhost"` ist gefährlich!
- Wenn Auto-Discovery fehlschlägt → Fallback auf localhost
- Localhost sollte NIE als Default verwendet werden

---

### 2. Client Startup (`client.py` Zeile 1056-1192)

**Workflow:**

```
START
  │
  ├─→ Auto-Discovery aktiviert?
  │   │
  │   YES → Discovery Loop (10 Versuche à 5s)
  │   │     │
  │   │     ├─→ mDNS Discovery (discovery.py)
  │   │     ├─→ UDP Broadcast Discovery (discovery.py)
  │   │     │
  │   │     ├─→ Server gefunden?
  │   │     │   YES → Parse URL, update config, save
  │   │     │   NO  → Retry (max 10x)
  │   │     │
  │   │     └─→ Nach 10 Versuchen: FALLBACK zu config.server_host
  │   │
  │   NO  → Verwende config.server_host direkt
  │
  └─→ Verbindung aufbauen mit server_host
```

**CRITICAL FALLBACK (Zeile 1176-1181):**
```python
# Fallback: Disable auto_discover and try configured server...
logger.warning("FALLBACK: Disabling auto_discover and trying configured server...")
logger.info(f"Configured server: {self.config.server_host}:{self.config.server_port}")
self.config.auto_discover = False
# Verwendet dann: self.config.server_host
```

→ Wenn `server_host = "localhost"` → **Verbindung zu localhost!**

---

### 3. Discovery Module (`discovery.py`)

#### 3.1 IP-Filterung (Zeile 112-172)

```python
def filter_and_prioritize_ips(ips: List[str]) -> List[str]:
    """
    Priorität:
    1. Private IPs (192.168.x.x, 10.x.x.x, 172.16-31.x.x)
    2. Andere valid IPs

    Filtert:
    - Localhost/loopback (127.x.x.x, ::1)  ✓ GUT!
    - Unspecified (0.0.0.0, ::)           ✓ GUT!
    - Link-local (169.254.x.x)            ✓ GUT!
    """
```

**✓ IP-Filterung ist KORREKT!**
- Localhost wird herausgefiltert (Zeile 142-144)
- Private IPs werden bevorzugt (Zeile 153-154)

**❌ ABER: Keine Sub-Priorisierung innerhalb Private IPs!**
- `192.168.x.x` sollte höher sein als `10.x.x.x`
- Aktuell: Alle private IPs gleichwertig

---

#### 3.2 ServerInfo Dataclass (Zeile 175-201)

```python
@dataclass
class ServerInfo:
    local_ips: List[str]  # Liste von IPs

    def __post_init__(self):
        # Filtert und priorisiert IPs automatisch!
        self.local_ips = filter_and_prioritize_ips(self.local_ips)

    def get_primary_url(self) -> str:
        # Gibt ERSTE IP zurück
        urls = self.get_urls()
        return urls[0] if urls else ""
```

**✓ Automatische IP-Filterung bei Server-Discovery!**

**Problem**: Reihenfolge in `local_ips` nach `filter_and_prioritize_ips()`:
1. Alle private IPs (unsortiert zwischen 192.168/10.x/172.x)
2. Public IPs

→ Wenn Server sowohl 192.168.1.100 ALS AUCH 10.0.0.50 hat, ist Reihenfolge ZUFÄLLIG!

---

#### 3.3 Discovery Functions

**mDNS Discovery (Zeile 289-379):**
- Nutzt Zeroconf Library
- Findet Server via `_digitalsignage._tcp.local.`
- Parst Service Info → ServerInfo Objekt
- **IPs werden automatisch gefiltert via `__post_init__`!**

**UDP Broadcast Discovery (Zeile 382-515):**
- Sendet "DIGITALSIGNAGE_DISCOVER" Broadcast
- Empfängt JSON Response mit `LocalIPs` Array
- Erstellt ServerInfo Objekt
- **IPs werden automatisch gefiltert via `__post_init__`!**

**discover_server() Convenience Function (Zeile 518-558):**
```python
def discover_server(timeout: float = 5.0, prefer_mdns: bool = True) -> Optional[str]:
    # Try mDNS first
    if prefer_mdns and MDNS_AVAILABLE:
        servers = mdns_discovery.discover_servers(timeout=timeout)
        if servers:
            return servers[0].get_primary_url()  # ← Erste URL!

    # Fallback to UDP
    servers = udp_discovery.discover_servers(timeout=timeout)
    if servers:
        return servers[0].get_primary_url()  # ← Erste URL!

    return None
```

**Return Value:** Nur der ERSTE entdeckte Server, nur die ERSTE IP!
- Client bekommt keine Liste aller Server
- Client bekommt keine Liste aller IPs pro Server
- Keine Wahlmöglichkeit!

---

### 4. Client URL Parsing (Zeile 1123-1140)

```python
# Parse the discovered URL to update config
match = re.match(r'(wss?)://([^:]+):(\d+)/(.+)', discovered_url)
if match:
    protocol, host, port, endpoint = match.groups()
    self.config.server_host = host  # ← Setzt server_host!
    self.config.server_port = int(port)
    self.config.endpoint_path = endpoint
    self.config.use_ssl = (protocol == 'wss')

    # Save discovered configuration
    self.config.save()
```

**✓ Config wird aktualisiert und gespeichert!**

---

### 5. Web Interface (`web_interface.py`)

#### 5.1 GET /api/settings (Zeile 128-158)

```python
return jsonify({
    'server_host': self.client.config.server_host,
    'server_port': self.client.config.server_port,
    'auto_discover': self.client.config.auto_discover,
    # ...
})
```

**❌ FEHLT: Liste der entdeckten Server!**

#### 5.2 POST /api/settings (Zeile 160-295)

```python
if 'server_host' in data and data['server_host']:
    self.client.config.server_host = str(data['server_host'])
    updated_fields.append('server_host')
```

**✓ User kann server_host setzen!**

**❌ FEHLT: API Endpoint für discovered servers!**

#### 5.3 HTML Dashboard (dashboard.html Zeile 988)

```html
<input type="text" id="serverHost" value="${data.server_host || ''}"
       placeholder="Server host" style="...">
```

**❌ NUR Text-Input, kein Dropdown!**

---

## Identifizierte Probleme

### Problem 1: Keine Sub-Priorisierung innerhalb Private IPs

**Datei:** `discovery.py`, Funktion `filter_and_prioritize_ips()`

**Aktuell:**
```python
# Categorize by priority
if ip.is_private:
    private_ips.append(ip_str)  # Alle gleichwertig!
```

**Gewünscht:**
```python
Priority:
1. 192.168.x.x (höchste Priorität)
2. 10.x.x.x
3. 172.16-31.x.x
4. Andere private IPs
```

---

### Problem 2: Client speichert nur eine Server-URL

**Datei:** `client.py`, Discovery Loop

**Aktuell:**
```python
discovered_url = discover_server(self.config.discovery_timeout)
# Gibt NUR eine URL zurück!
```

**Client hat keine Liste aller entdeckten Server/IPs!**

---

### Problem 3: Kein API Endpoint für entdeckte Server

**Datei:** `web_interface.py`

**Fehlt:** GET `/api/discovered-servers`

Sollte zurückgeben:
```json
{
  "servers": [
    {
      "server_name": "Desktop-PC",
      "ips": ["192.168.0.100", "10.0.0.50"],
      "port": 8080,
      "protocol": "wss"
    }
  ]
}
```

---

### Problem 4: Web-Interface hat nur Text-Input

**Datei:** `templates/dashboard.html`

**Aktuell:** `<input type="text" id="serverHost">`

**Gewünscht:**
- Dropdown mit entdeckten Servern
- Text-Input für manuelle Eingabe
- Anzeige der aktuell verbundenen IP

---

### Problem 5: Default server_host = "localhost"

**Datei:** `config.py`, Zeile 15

```python
server_host: str = "localhost"  # ← GEFÄHRLICH!
```

**Besser:**
```python
server_host: str = ""  # Leer, zwingt zu Auto-Discovery oder manueller Eingabe
```

**Oder:**
```python
server_host: str = "0.0.0.0"  # Ungültige IP, verhindert Verbindung zu localhost
```

---

## Warum wird localhost verwendet?

### Trace:

1. **Config Default:** `server_host = "localhost"` (config.py Zeile 15)

2. **Auto-Discovery Fallback:** Wenn Discovery fehlschlägt:
   ```python
   # client.py Zeile 1177-1178
   logger.warning("FALLBACK: Disabling auto_discover and trying configured server...")
   logger.info(f"Configured server: {self.config.server_host}:{self.config.server_port}")
   # → Verwendet self.config.server_host = "localhost"!
   ```

3. **WebSocket Connect:**
   ```python
   # client.py beim Connect
   url = f"{protocol}://{self.config.server_host}:{self.config.server_port}/{self.config.endpoint_path}"
   # → ws://localhost:8080/ws/
   ```

**Root Cause:**
- Config Default `server_host = "localhost"`
- Auto-Discovery Fallback nutzt diesen Default
- → Client verbindet sich mit localhost wenn Discovery fehlschlägt

**Wann tritt das auf?**
- Server ist offline während Discovery (10 Versuche à 5s = 50s)
- Netzwerk-Problem während Discovery
- Server ist im anderen Subnet (10.x vs 192.168.x)
- Firewall blockt Discovery-Ports (UDP 5555, mDNS 5353)

---

## Lösungsansatz

### Fix 1: Sub-Priorisierung in `filter_and_prioritize_ips()`

```python
def filter_and_prioritize_ips(ips: List[str]) -> List[str]:
    import ipaddress

    def ip_priority(ip_str: str) -> int:
        """
        Return priority (lower = better):
        0 = 192.168.x.x (höchste Priorität)
        1 = 10.x.x.x
        2 = 172.16-31.x.x
        3 = andere private IPs
        4 = public IPs
        """
        try:
            ip = ipaddress.ip_address(ip_str)

            if ip.is_private:
                # Check specific subnets
                if ip_str.startswith('192.168.'):
                    return 0  # Höchste Priorität
                elif ip_str.startswith('10.'):
                    return 1
                elif ip_str.startswith('172.'):
                    # Check if 172.16-31.x.x
                    octets = ip_str.split('.')
                    if len(octets) >= 2:
                        second_octet = int(octets[1])
                        if 16 <= second_octet <= 31:
                            return 2
                return 3  # Other private
            else:
                return 4  # Public IP
        except:
            return 999  # Invalid

    # Filter invalid IPs (loopback, link-local, etc.)
    valid_ips = []
    for ip_str in ips:
        try:
            ip = ipaddress.ip_address(ip_str)
            if not (ip.is_loopback or ip.is_unspecified or ip.is_link_local):
                valid_ips.append(ip_str)
        except:
            continue

    # Sort by priority
    return sorted(valid_ips, key=ip_priority)
```

---

### Fix 2: Client speichert alle entdeckten Server

```python
# client.py - Neue Methode
class DigitalSignageClient:
    def __init__(self):
        self.discovered_servers = []  # Cache für entdeckte Server

    async def discover_all_servers(self):
        """Discover all servers and cache results"""
        from discovery import discover_all_servers

        servers = discover_all_servers(
            timeout=self.config.discovery_timeout,
            use_mdns=True,
            use_udp=True
        )

        self.discovered_servers = servers
        logger.info(f"Discovered {len(servers)} server(s)")

        # Return best server URL (first IP of first server)
        if servers:
            best_server = servers[0]
            best_url = best_server.get_primary_url()
            return best_url
        return None
```

---

### Fix 3: Web Interface `/api/discovered-servers` Endpoint

```python
# web_interface.py
@self.app.route('/api/discovered-servers')
def api_get_discovered_servers():
    """Get list of discovered servers"""
    try:
        # Trigger discovery
        from discovery import discover_all_servers

        servers = discover_all_servers(timeout=3.0)

        result = []
        for server in servers:
            result.append({
                'server_name': server.server_name,
                'ips': server.local_ips,  # Already filtered and prioritized!
                'port': server.port,
                'protocol': server.protocol,
                'ssl_enabled': server.ssl_enabled,
                'endpoint_path': server.endpoint_path,
                'urls': server.get_urls()
            })

        return jsonify({
            'success': True,
            'servers': result,
            'count': len(result)
        })
    except Exception as e:
        logger.error(f"Error discovering servers: {e}")
        return jsonify({
            'success': False,
            'error': str(e),
            'servers': []
        })
```

---

### Fix 4: Web Interface HTML Dropdown

```html
<!-- Settings Tab -->
<div class="form-group">
    <label>Server Connection</label>

    <!-- Discovered Servers Dropdown -->
    <label for="discovered-servers">Discovered Servers:</label>
    <select id="discovered-servers" class="form-control" onchange="selectDiscoveredServer()">
        <option value="">-- Select Server --</option>
    </select>
    <button class="btn btn-secondary" onclick="refreshDiscoveredServers()">
        🔍 Scan for Servers
    </button>

    <!-- Manual Server IP Input -->
    <label for="serverHost" style="margin-top: 15px;">Manual Server IP:</label>
    <input type="text" id="serverHost" placeholder="192.168.1.100"
           class="form-control" value="${data.server_host}">
    <small>Leave empty for auto-discovery. Manual entry overrides.</small>

    <!-- Current Connection -->
    <div class="alert alert-info" style="margin-top: 10px;">
        Currently connected to: <strong id="current-server">${data.server_host || 'Not connected'}</strong>
    </div>
</div>

<script>
async function refreshDiscoveredServers() {
    const select = document.getElementById('discovered-servers');
    const btn = event.target;

    btn.disabled = true;
    btn.textContent = '🔄 Scanning...';

    try {
        const response = await fetch('/api/discovered-servers');
        const data = await response.json();

        // Clear dropdown
        select.innerHTML = '<option value="">-- Select Server --</option>';

        // Add discovered servers
        if (data.success && data.servers.length > 0) {
            data.servers.forEach(server => {
                // Add option for each IP
                server.ips.forEach((ip, index) => {
                    const option = document.createElement('option');
                    option.value = ip;
                    option.text = `${server.server_name} - ${ip}:${server.port} (${server.protocol.toUpperCase()})`;
                    if (index === 0) {
                        option.text += ' ⭐'; // Mark primary IP
                    }
                    select.appendChild(option);
                });
            });
            alert(`Found ${data.servers.length} server(s)`);
        } else {
            alert('No servers discovered. Check network and server status.');
        }
    } catch (error) {
        alert('Discovery failed: ' + error.message);
    } finally {
        btn.disabled = false;
        btn.textContent = '🔍 Scan for Servers';
    }
}

function selectDiscoveredServer() {
    const select = document.getElementById('discovered-servers');
    const input = document.getElementById('serverHost');
    if (select.value) {
        input.value = select.value;
    }
}

// Auto-refresh on page load
window.addEventListener('load', () => {
    setTimeout(refreshDiscoveredServers, 1000);
});
</script>
```

---

### Fix 5: Config Default ändern

```python
# config.py
@dataclass
class Config:
    server_host: str = ""  # LEER statt "localhost"!
    auto_discover: bool = True
```

**Fallback Logic anpassen:**
```python
# client.py
if not server_discovered:
    if self.config.server_host:
        logger.warning(f"FALLBACK: Using configured server {self.config.server_host}")
        # OK to use configured server
    else:
        logger.error("FALLBACK: No server configured and discovery failed!")
        # Show error screen, DO NOT connect to localhost!
```

---

## Zusammenfassung

**Hauptprobleme:**
1. ✅ IP-Filterung funktioniert (localhost wird gefiltert)
2. ❌ Keine Sub-Priorisierung (192.168 vs 10.x)
3. ❌ Client speichert keine Server-Liste
4. ❌ Web-Interface hat keine Server-Auswahl
5. ❌ Config Default `server_host = "localhost"` ist gefährlich

**Localhost wird verwendet weil:**
- Default Config `server_host = "localhost"`
- Auto-Discovery Fallback nutzt diesen Default

**Fixes:**
1. Erweitere `filter_and_prioritize_ips()` mit Sub-Priorisierung
2. Client speichert `discovered_servers` Liste
3. Füge `/api/discovered-servers` Endpoint hinzu
4. Erweitere Web-Interface mit Dropdown
5. Ändere Config Default zu `""` statt `"localhost"`

---

## Nächste Schritte

1. ✅ Dokumentation erstellt (diese Datei)
2. ⏳ Implementiere IP-Priorisierung
3. ⏳ Implementiere `/api/discovered-servers` Endpoint
4. ⏳ Erweitere Web-Interface HTML
5. ⏳ Teste auf Raspberry Pi
6. ⏳ Push zu GitHub
