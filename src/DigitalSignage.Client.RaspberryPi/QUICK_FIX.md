# Quick Fix Guide - Digital Signage Client

## Problem 1: Client running with Xvfb instead of real HDMI display

### Symptoms
- Client service is running
- Logs show "Display renderer created"
- But HDMI display shows nothing
- `ps aux | grep X` shows "Xvfb :99" instead of "X :0"

### Solution (5 minutes)

```bash
# Step 1: Navigate to client directory
cd ~/digitalsignage
git pull  # Get latest fixes
cd src/DigitalSignage.Client.RaspberryPi

# Step 2: Run display configuration script
sudo bash configure-display.sh

# Step 3: Follow prompts and reboot when asked
# (Press 'y' when asked to reboot)

# After reboot, verify:
echo $DISPLAY
# Should show: :0 (not :99)

sudo systemctl status digitalsignage-client
# Should show: active (running)
```

---

## Problem 2: Server cannot find/discover client

### Symptoms
- Server shows no clients
- Client logs show connection errors
- Network seems OK but no connection

### Solution (10 minutes)

```bash
# Step 1: Run network diagnostics
cd ~/digitalsignage/src/DigitalSignage.Client.RaspberryPi
sudo bash test-connection.sh

# This will show you exactly what's wrong:
# - Network configuration
# - Server reachability
# - Port connectivity
# - Configuration issues
```

### Common Fixes

**Fix 1: Wrong server IP**
```bash
# Edit config file
sudo nano /opt/digitalsignage-client/config.json

# Change server_host to your Windows PC IP:
{
  "server_host": "192.168.1.100",  # <-- PUT YOUR WINDOWS IP HERE
  "server_port": 8080,
  "registration_token": "YOUR_TOKEN",
  ...
}

# Save: Ctrl+O, Enter, Ctrl+X

# Restart client
sudo systemctl restart digitalsignage-client
```

**Fix 2: Firewall blocking port 8080 on Windows**

On Windows Server (PowerShell as Administrator):
```powershell
New-NetFirewallRule -DisplayName "Digital Signage Server" `
  -Direction Inbound -LocalPort 8080 -Protocol TCP -Action Allow
```

Or manually:
1. Windows Firewall → Advanced Settings
2. Inbound Rules → New Rule
3. Port → TCP → 8080
4. Allow the connection
5. Apply to all profiles

**Fix 3: Different networks**
```bash
# Check client IP
ip addr show | grep "inet " | grep -v "127.0.0.1"

# Should be same subnet as server
# Example: Client 192.168.1.50, Server 192.168.1.100 = OK
# Example: Client 192.168.0.50, Server 192.168.1.100 = PROBLEM
```

If different subnets:
- Connect both to same network (same WiFi/router)
- Or configure network routing (advanced)

---

## Quick Status Check

```bash
# One command to check everything:
cd ~/digitalsignage/src/DigitalSignage.Client.RaspberryPi

echo "=== DISPLAY CHECK ==="
echo "Current DISPLAY: $DISPLAY"
ps aux | grep -E "X|Xvfb" | grep -v grep

echo ""
echo "=== SERVICE CHECK ==="
sudo systemctl status digitalsignage-client | head -15

echo ""
echo "=== CONFIG CHECK ==="
cat /opt/digitalsignage-client/config.json | grep -E "server_host|server_port"

echo ""
echo "=== NETWORK CHECK ==="
ip addr show | grep "inet " | grep -v "127.0.0.1"
```

---

## When Everything is Working

You should see:
- Display is :0 (not :99)
- Service status: active (running)
- Server can see client in Devices tab
- HDMI display shows content

---

## Need More Help?

1. **View full diagnostics:**
   ```bash
   sudo bash test-connection.sh
   ```

2. **View recent logs:**
   ```bash
   sudo journalctl -u digitalsignage-client -n 50
   ```

3. **View live logs:**
   ```bash
   sudo journalctl -u digitalsignage-client -f
   ```

4. **Check comprehensive troubleshooting:**
   - See README.md "Troubleshooting" section
   - See TROUBLESHOOTING.md (if exists)

---

## File Locations

| Item | Location |
|------|----------|
| Config file | `/opt/digitalsignage-client/config.json` |
| Client code | `/opt/digitalsignage-client/` |
| Service file | `/etc/systemd/system/digitalsignage-client.service` |
| Scripts | `~/digitalsignage/src/DigitalSignage.Client.RaspberryPi/` |
| Logs | `sudo journalctl -u digitalsignage-client` |

---

## Quick Commands Reference

```bash
# Start/stop/restart service
sudo systemctl start digitalsignage-client
sudo systemctl stop digitalsignage-client
sudo systemctl restart digitalsignage-client

# View service status
sudo systemctl status digitalsignage-client

# View logs (last 50 lines)
sudo journalctl -u digitalsignage-client -n 50

# View logs (live/follow)
sudo journalctl -u digitalsignage-client -f

# Edit configuration
sudo nano /opt/digitalsignage-client/config.json

# Test network connection to server
sudo bash test-connection.sh

# Configure real display (requires reboot)
sudo bash configure-display.sh

# Manual test (diagnostic)
cd /opt/digitalsignage-client
sudo -u $USER ./venv/bin/python3 client.py --test
```
