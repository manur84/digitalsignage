# Auto-Discovery Issue - Root Cause Analysis

## Executive Summary

**Status:** Discovery is ENABLED by default but NOT working on deployed clients
**Root Cause:** Configuration mismatch - code has discovery enabled, but deployed clients use config.json which may have different defaults
**Impact:** Clients cannot auto-discover servers and must be manually configured

---

## Detailed Analysis

### 1. Discovery Code Status ✅

The discovery implementation is **complete and properly integrated**:

**File: `src/DigitalSignage.Client.RaspberryPi/discovery.py`**
- ✅ Complete UDP broadcast discovery implementation
- ✅ Optional mDNS/Zeroconf support (graceful fallback if not installed)
- ✅ Proper network interface detection (eth0 support)
- ✅ Comprehensive error handling and logging
- ✅ Standalone test mode available

**File: `src/DigitalSignage.Client.RaspberryPi/client.py`**
- ✅ Discovery properly imported (line 553)
- ✅ Discovery logic implemented in `start()` method (lines 544-592)
- ✅ Extensive diagnostic logging for discovery process
- ✅ Proper error handling with fallback to manual config
- ✅ Configuration saved after successful discovery

### 2. Default Configuration ✅

**File: `src/DigitalSignage.Client.RaspberryPi/config.py`**

```python
@dataclass
class Config:
    auto_discover: bool = True  # ✅ ENABLED BY DEFAULT (line 24)
    discovery_timeout: float = 5.0  # ✅ Reasonable timeout
```

**DEFAULT BEHAVIOR:**
- Auto-discovery is **ENABLED** by default
- Discovery timeout is **5 seconds**
- Discovery runs **BEFORE** attempting manual connection
- Discovery uses **both UDP broadcast and mDNS** (if available)

### 3. The Problem ❌

**Client logs show:**
```
Configuration loaded - Server: localhost:8080
```

**Expected if discovery was working:**
```
AUTO-DISCOVERY CONFIGURATION CHECK
auto_discover = True
AUTO-DISCOVERY ENABLED - Searching for servers...
✓ SERVER FOUND: ws://192.168.0.145:8080/ws
```

**This indicates ONE of the following:**

#### Issue A: Config File Override
The deployed client has a `config.json` file at `/opt/digitalsignage-client/config.json` that overrides defaults:

```json
{
  "client_id": "...",
  "server_host": "localhost",
  "server_port": 8080,
  "auto_discover": false  // ← OVERRIDING DEFAULT
}
```

**Evidence:**
- `config.py` loads from `/opt/digitalsignage-client/config.json` (line 41)
- If file exists, it loads ALL values from JSON, overriding dataclass defaults
- JSON may not have `auto_discover` key, which would default to `False` (JSON deserialization issue)

#### Issue B: Old Client Version Running
The client on the Raspberry Pi may be running an **old version** of the code:

- Discovery features were added recently
- Client may not have been updated with latest code
- `install.sh` does include a git pull step (lines 168-194), but it may have failed

#### Issue C: discovery.py Not Deployed
The `install.sh` script may not have copied `discovery.py`:

**Checking install.sh (lines 272-286):**
```bash
cp discovery.py "$INSTALL_DIR/"  # ✅ LINE 275 - It IS copied
```

However, if the client was installed **before** `discovery.py` was added to the repository, it won't be present.

### 4. Installation Analysis

**File: `src/DigitalSignage.Client.RaspberryPi/install.sh`**

The install script **DOES include discovery.py** (line 275):
```bash
cp discovery.py "$INSTALL_DIR/"
```

But there are potential issues:

1. **Old installations:** If client was installed before discovery.py was committed, it won't be present
2. **Update mechanism:** Install script tries to `git pull` (lines 174-187), but:
   - May fail if there are local modifications
   - May fail if git credentials aren't configured
   - Errors are only warnings, installation continues with old code

3. **No forced update:** Script doesn't verify file presence or force re-copy of updated files

---

## Root Cause Determination

Based on the evidence, the most likely root cause is:

**PRIMARY CAUSE: Config File Override**
- `config.json` created by old installation doesn't have `auto_discover` key
- When Python loads JSON without this key, it may not get the dataclass default
- Client connects to `localhost:8080` instead of auto-discovering

**SECONDARY CAUSE: Old Client Code**
- Client may be running old version without discovery features
- `git pull` in install.sh may have failed silently
- No verification that discovery.py exists after installation

---

## Verification Steps

### Step 1: Check if discovery.py exists on client
```bash
ls -l /opt/digitalsignage-client/discovery.py
```

**Expected:** File should exist
**If missing:** Client needs to be updated

### Step 2: Check config.json contents
```bash
cat /opt/digitalsignage-client/config.json
```

**Look for:**
```json
{
  "auto_discover": true,  // ← Should be present and true
  "discovery_timeout": 5.0
}
```

**If missing:** Config needs to be regenerated or manually edited

### Step 3: Check client.py version
```bash
grep -n "AUTO-DISCOVERY CONFIGURATION CHECK" /opt/digitalsignage-client/client.py
```

**Expected:** Line 536-541 should exist with this text
**If missing:** Client code is old and needs update

### Step 4: Check service logs for discovery attempts
```bash
sudo journalctl -u digitalsignage-client -n 100 | grep -i discovery
```

**Expected:** Should see discovery log messages
**If absent:** Discovery code isn't running

---

## Solutions

### Solution 1: Update Existing Installation (Recommended)

Run these commands on the Raspberry Pi:

```bash
# Stop the service
sudo systemctl stop digitalsignage-client

# Update from repository
cd /path/to/repository/src/DigitalSignage.Client.RaspberryPi
git pull

# Copy updated files
sudo cp discovery.py /opt/digitalsignage-client/
sudo cp client.py /opt/digitalsignage-client/
sudo cp config.py /opt/digitalsignage-client/

# Fix config.json to enable discovery
sudo python3 -c "
import json
config_path = '/opt/digitalsignage-client/config.json'
with open(config_path, 'r') as f:
    config = json.load(f)
config['auto_discover'] = True
config['discovery_timeout'] = 5.0
with open(config_path, 'w') as f:
    json.dump(config, f, indent=2)
print('✓ Config updated')
"

# Restart service
sudo systemctl restart digitalsignage-client

# Watch logs
sudo journalctl -u digitalsignage-client -f
```

### Solution 2: Reinstall Client

```bash
cd /path/to/repository/src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh
```

This will:
1. Pull latest code from git
2. Copy all files including discovery.py
3. Create fresh config.json with correct defaults

### Solution 3: Manual Config Fix

Edit the config file directly:

```bash
sudo nano /opt/digitalsignage-client/config.json
```

Add these lines:
```json
{
  "client_id": "existing-id-here",
  "server_host": "localhost",
  "server_port": 8080,
  "auto_discover": true,       // ← ADD THIS
  "discovery_timeout": 5.0,    // ← ADD THIS
  ...
}
```

Then restart:
```bash
sudo systemctl restart digitalsignage-client
```

---

## Testing

### Test 1: Test Discovery from Windows
Run the test script on the Windows server machine:

```bash
cd /var/www/html/digitalsignage
python test-client-discovery.py
```

**Expected output:**
```
SUCCESS: Found 1 server(s)

Server 1:
  Name: DESKTOP-PV93T8F
  IPs: 192.168.0.145
  Port: 8080
  Protocol: WS
  WebSocket URLs:
    ws://192.168.0.145:8080/ws
```

### Test 2: Test Discovery on Client
SSH to Raspberry Pi and run:

```bash
cd /opt/digitalsignage-client
sudo -u pi python3 -c "
import sys
sys.path.insert(0, '/opt/digitalsignage-client')
from discovery import discover_server
url = discover_server(timeout=5.0)
print(f'Discovered: {url}' if url else 'No server found')
"
```

**Expected output:**
```
Discovered: ws://192.168.0.145:8080/ws
```

### Test 3: Check Client Startup Logs
```bash
sudo journalctl -u digitalsignage-client -n 200 | grep -A 10 "AUTO-DISCOVERY"
```

**Expected output:**
```
AUTO-DISCOVERY CONFIGURATION CHECK
config.auto_discover = True
config.auto_discover type = <class 'bool'>
discovery_timeout = 5.0
AUTO-DISCOVERY ENABLED - Searching for servers...
✓ SERVER FOUND: ws://192.168.0.145:8080/ws
```

---

## Prevention

### Fix 1: Update Config.load() to handle missing keys

**File: `config.py`**

```python
@classmethod
def load(cls, config_path: str = "/opt/digitalsignage-client/config.json") -> 'Config':
    """Load configuration from file"""
    config_file = Path(config_path)

    if config_file.exists():
        with open(config_file, 'r') as f:
            data = json.load(f)

            # Ensure all fields have defaults
            defaults = {
                'auto_discover': True,
                'discovery_timeout': 5.0,
                'remote_logging_enabled': True,
                'remote_logging_level': 'INFO',
                'remote_logging_batch_size': 50,
                'remote_logging_batch_interval': 5.0
            }

            # Merge defaults with loaded data
            for key, value in defaults.items():
                if key not in data:
                    data[key] = value

            return cls(**data)
    else:
        # Create default configuration
        config = cls(client_id=str(uuid.uuid4()))
        config.save(config_path)
        return config
```

### Fix 2: Add verification to install.sh

**File: `install.sh`**

After copying files (around line 286), add:

```bash
# Verify critical files
echo "Verifying installation files..."
MISSING_FILES=()

for file in client.py config.py discovery.py device_manager.py display_renderer.py cache_manager.py watchdog_monitor.py; do
    if [ ! -f "$INSTALL_DIR/$file" ]; then
        MISSING_FILES+=("$file")
    fi
done

if [ ${#MISSING_FILES[@]} -gt 0 ]; then
    echo "ERROR: Critical files missing: ${MISSING_FILES[*]}"
    exit 1
fi

echo "✓ All required files present"
```

---

## Summary

| Component | Status | Issue |
|-----------|--------|-------|
| discovery.py | ✅ Complete | Implementation is correct |
| client.py | ✅ Integrated | Discovery logic properly implemented |
| config.py | ⚠️ Partial | Defaults correct, but JSON loading doesn't preserve defaults |
| install.sh | ⚠️ Partial | Copies discovery.py, but doesn't verify |
| Deployed Client | ❌ Not Working | Old config.json or old code version |

**Action Required:**
1. Update config.json on deployed clients to include `auto_discover: true`
2. OR reinstall client with latest code
3. Update config.py to merge defaults when loading from JSON (prevention)

---

## Files in This Analysis

- **`discovery.py`** - Complete UDP/mDNS discovery implementation (20KB, 560 lines)
- **`client.py`** - Main client with discovery integration (33KB, 849 lines)
- **`config.py`** - Configuration with auto_discover=True default (5KB, 123 lines)
- **`install.sh`** - Installation script that copies all files (29KB, 849 lines)
- **`test-client-discovery.py`** - NEW test script for Windows testing

**All code is present and correct. The issue is deployment/configuration, not implementation.**
