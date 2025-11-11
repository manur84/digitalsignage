# Digital Signage Client - Troubleshooting Guide

This guide helps diagnose and fix service startup issues.

## Quick Fix

If your service is failing, run the quick-fix script:

```bash
cd /opt/digitalsignage-client
sudo ./quick-fix.sh
```

This will:
- Stop the service
- Fix permissions
- Verify Python packages
- Test imports
- Create a minimal service file
- Start the service
- Show logs

## Diagnostic Commands

### View Service Status
```bash
sudo systemctl status digitalsignage-client
```

### View Service Logs
```bash
# Live logs (follow mode)
sudo journalctl -u digitalsignage-client -f

# Last 100 lines
sudo journalctl -u digitalsignage-client -n 100 --no-pager

# Since last boot
sudo journalctl -u digitalsignage-client -b
```

### View Startup Log
The startup script creates a detailed log:

```bash
# Try main log location
sudo cat /var/log/digitalsignage-client-startup.log

# Or fallback location
sudo cat /tmp/digitalsignage-client-startup.log
```

### Manual Test
Test the client manually without the service:

```bash
# Test mode (runs pre-flight checks only)
sudo -u pi /opt/digitalsignage-client/start-with-display.sh --test

# Full run (replace 'pi' with your username)
sudo -u pi /opt/digitalsignage-client/start-with-display.sh
```

### Run Diagnostic Script
```bash
sudo /opt/digitalsignage-client/diagnose.sh
```

## Common Issues and Solutions

### Issue 1: Service fails with "control process exited with error code"

**Symptoms:**
- Service fails to start
- `systemctl status` shows "control process exited"
- No detailed error message

**Solutions:**

1. Check the startup log:
   ```bash
   sudo cat /var/log/digitalsignage-client-startup.log
   ```

2. Run the fix script:
   ```bash
   sudo /opt/digitalsignage-client/../fix-installation.sh
   ```

3. Run manual test to see exact error:
   ```bash
   sudo -u pi /opt/digitalsignage-client/start-with-display.sh --test
   ```

### Issue 2: PyQt5 import fails

**Symptoms:**
- Error: "ModuleNotFoundError: No module named 'PyQt5'"
- Startup log shows PyQt5 import failed

**Solutions:**

1. Install system PyQt5:
   ```bash
   sudo apt-get install python3-pyqt5 python3-pyqt5.qtsvg python3-pyqt5.qtmultimedia
   ```

2. Verify installation:
   ```bash
   python3 -c "import PyQt5; print('OK')"
   ```

3. Check venv has access:
   ```bash
   /opt/digitalsignage-client/venv/bin/python3 -c "import PyQt5; print('OK')"
   ```

4. If venv doesn't have access, recreate with --system-site-packages:
   ```bash
   cd /opt/digitalsignage-client
   sudo rm -rf venv
   sudo python3 -m venv --system-site-packages venv
   sudo venv/bin/pip install -r /path/to/requirements.txt
   sudo chown -R pi:pi venv
   ```

### Issue 3: Xvfb fails to start

**Symptoms:**
- Error: "Xvfb not installed" or "Xvfb failed to start"
- Headless environment without display

**Solutions:**

1. Install Xvfb:
   ```bash
   sudo apt-get install xvfb x11-xserver-utils x11-utils
   ```

2. Test Xvfb manually:
   ```bash
   Xvfb :99 -screen 0 1920x1080x24 &
   export DISPLAY=:99
   xset q
   ```

3. Kill test Xvfb:
   ```bash
   pkill Xvfb
   ```

### Issue 4: Permission denied errors

**Symptoms:**
- Error: "Permission denied" when accessing files
- Script fails to execute

**Solutions:**

1. Fix ownership:
   ```bash
   sudo chown -R pi:pi /opt/digitalsignage-client
   ```

2. Fix script permissions:
   ```bash
   sudo chmod +x /opt/digitalsignage-client/start-with-display.sh
   sudo chmod +x /opt/digitalsignage-client/client.py
   ```

3. Fix line endings (if edited on Windows):
   ```bash
   sudo sed -i 's/\r$//' /opt/digitalsignage-client/start-with-display.sh
   ```

### Issue 5: Config.py syntax errors

**Symptoms:**
- Error: "config.py import failed"
- SyntaxError in logs

**Solutions:**

1. Check config.py syntax:
   ```bash
   /opt/digitalsignage-client/venv/bin/python3 -m py_compile /opt/digitalsignage-client/config.py
   ```

2. Edit config.py:
   ```bash
   sudo nano /opt/digitalsignage-client/config.py
   ```

3. Common issues:
   - Missing quotes around strings
   - Incorrect indentation (must use spaces, not tabs)
   - Missing commas in lists

### Issue 6: python-socketio version error

**Symptoms:**
- Error: "AttributeError: module 'socketio' has no attribute '__version__'"
- Socket connection fails

**Solutions:**

1. Reinstall correct version:
   ```bash
   sudo /opt/digitalsignage-client/venv/bin/pip uninstall python-socketio -y
   sudo /opt/digitalsignage-client/venv/bin/pip install python-socketio[client]==5.10.0
   ```

2. Verify installation:
   ```bash
   /opt/digitalsignage-client/venv/bin/python3 -c "import socketio; print(socketio.__version__)"
   ```

### Issue 7: Display not accessible

**Symptoms:**
- Error: "cannot connect to X server"
- QApplication fails to create

**Solutions:**

1. Check if X11 is running:
   ```bash
   ps aux | grep X
   echo $DISPLAY
   ```

2. If on physical display, set DISPLAY:
   ```bash
   export DISPLAY=:0
   xset q
   ```

3. Grant X11 access (if needed):
   ```bash
   xhost +local:
   ```

4. For headless, ensure Xvfb works (see Issue 3)

### Issue 8: Service starts but immediately crashes

**Symptoms:**
- Service starts but exits within seconds
- No clear error in logs

**Solutions:**

1. Run client directly to see error:
   ```bash
   cd /opt/digitalsignage-client
   sudo -u pi venv/bin/python3 client.py
   ```

2. Check for:
   - Network connectivity issues
   - Invalid server configuration
   - Missing dependencies

3. Enable debug logging:
   Edit `client.py` and set logging level to DEBUG:
   ```python
   logging.basicConfig(level=logging.DEBUG)
   ```

### Issue 9: Watchdog timeout

**Symptoms:**
- Error: "start operation timed out"
- Service fails after 60 seconds

**Solutions:**

1. The watchdog expects systemd notifications. Check if `watchdog_monitor.py` is working:
   ```bash
   /opt/digitalsignage-client/venv/bin/python3 -c "from watchdog_monitor import WatchdogMonitor; print('OK')"
   ```

2. If you don't need watchdog, use the simple service:
   ```bash
   sudo cp /opt/digitalsignage-client/../digitalsignage-client-simple.service /etc/systemd/system/digitalsignage-client.service
   sudo sed -i "s/INSTALL_USER/pi/g" /etc/systemd/system/digitalsignage-client.service
   sudo systemctl daemon-reload
   sudo systemctl restart digitalsignage-client
   ```

### Issue 10: Virtual environment issues

**Symptoms:**
- Error: "Python venv not found"
- Missing packages despite installation

**Solutions:**

1. Recreate virtual environment:
   ```bash
   cd /opt/digitalsignage-client
   sudo rm -rf venv
   sudo python3 -m venv --system-site-packages venv
   ```

2. Install dependencies:
   ```bash
   sudo venv/bin/pip install --upgrade pip
   sudo venv/bin/pip install \
       python-socketio[client]==5.10.0 \
       aiohttp==3.9.1 \
       requests==2.31.0 \
       psutil==5.9.6 \
       pillow==10.1.0 \
       qrcode==7.4.2
   ```

3. Fix ownership:
   ```bash
   sudo chown -R pi:pi venv
   ```

## Advanced Troubleshooting

### Enable Verbose Logging

1. Edit the service file:
   ```bash
   sudo nano /etc/systemd/system/digitalsignage-client.service
   ```

2. Add to the `[Service]` section:
   ```ini
   Environment="QT_LOGGING_RULES=*=true"
   Environment="QT_DEBUG_PLUGINS=1"
   ```

3. Reload and restart:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl restart digitalsignage-client
   ```

### Check System Resources

```bash
# Memory usage
free -h

# Disk space
df -h

# CPU load
uptime

# Process list
ps aux | grep python
```

### Verify Network Connectivity

```bash
# Ping server
ping -c 4 <server-ip>

# Test port
telnet <server-ip> <port>
# or
nc -zv <server-ip> <port>
```

### Manual Service Reset

If the service is completely stuck:

```bash
# Stop service
sudo systemctl stop digitalsignage-client

# Kill any remaining processes
sudo pkill -f digitalsignage
sudo pkill -f "Xvfb :99"

# Clear service state
sudo systemctl reset-failed digitalsignage-client

# Start fresh
sudo systemctl start digitalsignage-client
```

## Getting Help

If none of these solutions work:

1. Gather diagnostic information:
   ```bash
   sudo /opt/digitalsignage-client/diagnose.sh > /tmp/diagnostic.txt
   sudo journalctl -u digitalsignage-client -n 200 >> /tmp/diagnostic.txt
   sudo cat /var/log/digitalsignage-client-startup.log >> /tmp/diagnostic.txt
   ```

2. Share `/tmp/diagnostic.txt` when asking for help

3. Include:
   - Raspberry Pi model and OS version
   - Python version: `python3 --version`
   - Installation method (fresh install vs upgrade)
   - Any customizations made

## Prevention

To avoid issues in the future:

1. Keep system updated:
   ```bash
   sudo apt-get update && sudo apt-get upgrade
   ```

2. Test after configuration changes:
   ```bash
   sudo -u pi /opt/digitalsignage-client/start-with-display.sh --test
   ```

3. Monitor service status:
   ```bash
   sudo systemctl status digitalsignage-client
   ```

4. Review logs periodically:
   ```bash
   sudo journalctl -u digitalsignage-client -n 50
   ```
