# Network Interface Binding - Configuration Guide

## Overview

The Digital Signage Server can bind to network interfaces in three modes:

1. **Wildcard Binding (Default)** - Listens on ALL network interfaces
2. **Localhost Only** - Listens only on localhost (127.0.0.1)
3. **Specific IP Binding** - Listens only on a specific network interface

## Quick Start

### Standard Installation (Recommended)

For most users, the default wildcard binding is sufficient:

1. Run `setup-urlacl.bat` as Administrator (one-time setup)
2. Start the server normally (no admin rights needed)
3. Server will be accessible on ALL network interfaces

**No additional configuration required!**

---

## Binding Modes Explained

### 1. Wildcard Binding (http://+:8080/)

**Default behavior when URL ACL is configured**

- Listens on ALL network interfaces (Ethernet, WiFi, etc.)
- Clients can connect from any network
- Best for most scenarios

**Configuration:**
```json
{
  "WebSocketSettings": {
    "Port": 8080
    // No PreferredNetworkInterface specified
  }
}
```

**Setup:**
```bash
# Run once as Administrator:
setup-urlacl.bat
```

---

### 2. Localhost Only (http://127.0.0.1:8080/)

**Fallback when URL ACL is NOT configured**

- Only accessible from the same computer
- Clients on the network CANNOT connect
- Used automatically when no permissions

**When this happens:**
- No URL ACL configured
- Not running as administrator
- You'll see a warning in the logs

**To fix:**
```bash
# Run as Administrator:
setup-urlacl.bat
```

---

### 3. Specific IP Binding (http://192.168.1.100:8080/)

**Optional - Advanced users only**

- Listens ONLY on one specific network interface
- Useful for multi-homed servers (multiple network cards)
- Requires special URL ACL configuration

**When to use:**
- Server has multiple network interfaces
- You want to bind only to Ethernet, not WiFi
- Security requirement to limit access

**Configuration:**

1. **Edit appsettings.json:**
```json
{
  "WebSocketSettings": {
    "Port": 8080,
    "PreferredNetworkInterface": "192.168.1.100"
    // OR: "Ethernet 2"
    // OR: "Wi-Fi"
  }
}
```

2. **Configure URL ACL:**

Option A - Interactive Script:
```bash
# Run as Administrator:
setup-urlacl-specific-ip.bat
# Script will ask for IP address
```

Option B - Manual Command:
```bash
# PowerShell as Administrator:
netsh http add urlacl url=http://192.168.1.100:8080/ user=Everyone
netsh http add urlacl url=http://192.168.1.100:8080/ws/ user=Everyone
```

3. **Restart the server**

---

## Intelligent Fallback Logic

The server automatically handles missing permissions:

```
1. Check appsettings.json for PreferredNetworkInterface
   |
   v
2. If configured: Get IP address of that interface
   |
   v
3. Check if URL ACL exists for specific IP
   |
   +--YES--> Use specific IP binding (http://192.168.1.100:8080/)
   |
   +--NO---> Check if running as Administrator
             |
             +--YES--> Use specific IP binding
             |
             +--NO---> FALL BACK to wildcard or localhost
                       |
                       v
                       Check if wildcard URL ACL exists
                       |
                       +--YES--> Use wildcard (http://+:8080/)
                       |
                       +--NO---> Use localhost (http://127.0.0.1:8080/)
```

**Key Point:** Server ALWAYS starts, even without proper URL ACL!

---

## Finding Your Network Interfaces

### Option 1 - Windows Command Line
```bash
ipconfig
```

Look for:
- **Interface Name:** "Ethernet 2", "Wi-Fi", "vEthernet (WSL)"
- **IPv4 Address:** 192.168.1.100

### Option 2 - Server Logs
The server logs all available interfaces at startup:

```
[INFO] Found 3 operational network interfaces
[INFO] No preferred interface specified, using first available: Ethernet 2 (192.168.1.100)
```

### Option 3 - PowerShell
```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.InterfaceAlias -notlike "*Loopback*"}
```

---

## Troubleshooting

### Server shows: "PREFERRED IP CONFIGURED BUT NO PERMISSIONS"

**Cause:** PreferredNetworkInterface is set, but no URL ACL for that IP

**Solution 1 - Configure URL ACL (Recommended):**
```bash
# Run as Administrator:
setup-urlacl-specific-ip.bat
```

**Solution 2 - Remove Specific Binding:**
```json
{
  "WebSocketSettings": {
    "Port": 8080
    // Remove or comment out PreferredNetworkInterface
  }
}
```

**Solution 3 - Run as Administrator:**
Start the server with admin rights (not recommended for production)

---

### Server shows: "URL ACL NOT CONFIGURED - Running in localhost-only mode"

**Cause:** No URL ACL configured at all

**Solution:**
```bash
# Run ONCE as Administrator:
setup-urlacl.bat
```

Then restart server normally (no admin needed)

---

### Clients cannot connect to server

**Check 1 - Firewall:**
```bash
# Allow port 8080 through Windows Firewall:
netsh advfirewall firewall add rule name="Digital Signage Server" dir=in action=allow protocol=TCP localport=8080
```

**Check 2 - Binding Mode:**
Look at server logs:
```
[INFO] Using wildcard binding (all network interfaces)  ← GOOD
[INFO] WebSocket server started on port 8080 using HTTP/WS (all interfaces)
```

**Check 3 - Server IP Address:**
```bash
# On server:
ipconfig
# Note the IPv4 address

# On client:
ping <server-ip-address>
# Should respond
```

---

### Wrong network interface selected

**Problem:** Server binds to wrong interface (e.g., WSL instead of Ethernet)

**Solution - Explicit Configuration:**
```json
{
  "WebSocketSettings": {
    "PreferredNetworkInterface": "Ethernet 2"
    // OR: "192.168.1.100"
  }
}
```

**Then configure URL ACL:**
```bash
setup-urlacl-specific-ip.bat
```

---

## Verifying Configuration

### Check Current URL ACL Configuration
```bash
netsh http show urlacl
```

**Look for:**
```
Reserved URL            : http://+:8080/
    User: \Everyone       ← Wildcard binding configured
```

OR:
```
Reserved URL            : http://192.168.1.100:8080/
    User: \Everyone       ← Specific IP binding configured
```

### Remove URL ACL
```bash
# Remove wildcard:
netsh http delete urlacl url=http://+:8080/

# Remove specific IP:
netsh http delete urlacl url=http://192.168.1.100:8080/
```

---

## Security Considerations

### Wildcard Binding (http://+:8080/)
- ✅ Easy to configure
- ⚠️ Accessible from all network interfaces
- ⚠️ Make sure firewall is configured

### Specific IP Binding (http://192.168.1.100:8080/)
- ✅ Only accessible from one interface
- ✅ Better security for multi-homed servers
- ⚠️ Requires additional configuration
- ⚠️ Must update URL ACL if IP changes (DHCP!)

### Recommendations:
1. **Home/Office:** Use wildcard binding with firewall
2. **Production:** Use specific IP binding on internal network interface
3. **Development:** Localhost only is fine

---

## Examples

### Example 1 - Standard Setup (Wildcard)
```json
// appsettings.json
{
  "WebSocketSettings": {
    "Port": 8080
  }
}
```

```bash
# One-time setup as Administrator:
cd src\DigitalSignage.Server
setup-urlacl.bat
```

**Result:** Server accessible on all interfaces

---

### Example 2 - Specific Ethernet Interface
```json
// appsettings.json
{
  "WebSocketSettings": {
    "Port": 8080,
    "PreferredNetworkInterface": "Ethernet 2"
  }
}
```

```bash
# Configure URL ACL for Ethernet 2 IP:
setup-urlacl-specific-ip.bat
# Enter IP address shown by script (e.g., 192.168.1.100)
```

**Result:** Server ONLY accessible via Ethernet 2

---

### Example 3 - Specific IP Address
```json
// appsettings.json
{
  "WebSocketSettings": {
    "Port": 8080,
    "PreferredNetworkInterface": "192.168.1.100"
  }
}
```

```bash
# Configure URL ACL:
setup-urlacl-specific-ip.bat
# Enter: 192.168.1.100
```

**Result:** Server ONLY accessible via 192.168.1.100

---

## Migration Guide

### From Wildcard to Specific IP

1. **Note current URL ACL:**
```bash
netsh http show urlacl | findstr :8080
```

2. **Add specific IP configuration:**
```json
{
  "WebSocketSettings": {
    "PreferredNetworkInterface": "192.168.1.100"
  }
}
```

3. **Configure URL ACL:**
```bash
setup-urlacl-specific-ip.bat
```

4. **Restart server**

5. **Test connectivity**

6. **Optional - Remove wildcard ACL:**
```bash
netsh http delete urlacl url=http://+:8080/
netsh http delete urlacl url=http://+:8080/ws/
```

---

### From Specific IP back to Wildcard

1. **Remove PreferredNetworkInterface:**
```json
{
  "WebSocketSettings": {
    "Port": 8080
    // PreferredNetworkInterface removed
  }
}
```

2. **Restart server**

Server will automatically fall back to wildcard binding (if URL ACL configured)

**Optional - Clean up specific IP ACL:**
```bash
netsh http delete urlacl url=http://192.168.1.100:8080/
netsh http delete urlacl url=http://192.168.1.100:8080/ws/
```

---

## Technical Details

### URL ACL (Access Control List)

Windows HTTP.sys requires permission to listen on HTTP URLs.

**Without URL ACL:**
- ❌ Cannot bind to wildcard (+) or specific IP
- ✅ Can bind to localhost (127.0.0.1)
- Requires running as Administrator

**With URL ACL:**
- ✅ Can bind without administrator rights
- ✅ Can bind to wildcard or specific IP
- One-time configuration

**URL ACL Format:**
```
netsh http add urlacl url=<URL> user=<USER>
```

Where:
- `<URL>` = `http://+:8080/` (wildcard) or `http://192.168.1.100:8080/` (specific)
- `<USER>` = `Everyone` or SDDL string `D:(A;;GX;;;S-1-1-0)`

### SDDL String
`D:(A;;GX;;;S-1-1-0)` means:
- `D:` = DACL (Discretionary Access Control List)
- `(A;;GX;;;S-1-1-0)` = Allow Execute/Generic-Execute to Everyone
- `S-1-1-0` = SID for "Everyone" (works on all Windows language versions)

---

## Support

For issues or questions:

1. **Check server logs** - Most issues are logged with clear messages
2. **Run diagnostics:**
```bash
netsh http show urlacl
ipconfig
netstat -ano | findstr :8080
```

3. **Create GitHub issue** with:
   - Server logs
   - `netsh http show urlacl` output
   - `ipconfig` output
   - appsettings.json (without secrets)

---

## Summary

**For 95% of users:**
```bash
# Run once as Administrator:
setup-urlacl.bat
```

**For advanced multi-interface setups:**
```bash
# 1. Edit appsettings.json - add PreferredNetworkInterface
# 2. Run as Administrator:
setup-urlacl-specific-ip.bat
```

**Key principle:** Server always starts, falls back intelligently when permissions missing!
