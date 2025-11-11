# Digital Signage Client - Quick Start Guide

## Service Not Starting? Run This NOW!

If your service is failing, you have two options:

### Option A: Quick Fix (Without Git Pull)

Run this command directly on your Raspberry Pi:

```bash
curl -sSL https://raw.githubusercontent.com/manur84/digitalsignage/main/src/DigitalSignage.Client.RaspberryPi/quick-fix.sh | sudo bash
```

This will:
- Fix permissions
- Install missing packages
- Create a minimal service file
- Start the service
- Show you the logs

### Option B: After Git Pull

If you've already pulled the latest code:

```bash
cd /path/to/digitalsignage/src/DigitalSignage.Client.RaspberryPi
sudo ./quick-fix.sh
```

## Fresh Installation

For a clean installation:

```bash
cd /path/to/digitalsignage/src/DigitalSignage.Client.RaspberryPi
sudo ./install-minimal.sh
```

This creates a bulletproof setup with:
- System PyQt5 packages
- Virtual environment with --system-site-packages
- Minimal service file (no complex scripts)
- Auto-detection of X11 display
- Comprehensive error handling

## What Changed?

1. **Auto-Detection of DISPLAY**: The client now automatically finds the X11 display
2. **Simplified Service**: No more complex startup scripts
3. **Better Error Messages**: Clear instructions when something fails
4. **Immediate Diagnostics**: See exactly what's wrong

## After Installation

### Configure Your Server

Edit the configuration file:

```bash
sudo nano /etc/digitalsignage/config.json
```

Update these fields:
- `server_host`: Your server's IP address
- `server_port`: Usually 8080
- `registration_token`: If required by your server

### Start the Service

```bash
sudo systemctl enable digitalsignage-client
sudo systemctl start digitalsignage-client
```

### Check Status

```bash
# View service status
sudo systemctl status digitalsignage-client

# View live logs
sudo journalctl -u digitalsignage-client -f

# View last 50 lines
sudo journalctl -u digitalsignage-client -n 50
```

### Run Diagnostics

```bash
cd /opt/digitalsignage-client
venv/bin/python3 client.py --test
```

This will test:
- Environment variables (DISPLAY, etc.)
- X11 connection
- Configuration file
- Python imports
- Directory permissions

## Common Issues

### DISPLAY Not Set

The client now auto-detects DISPLAY. If it fails:

```bash
# Check if X11 is running
ps aux | grep X

# Start X11 if needed
startx

# Or use virtual display
sudo apt-get install xvfb
Xvfb :99 -screen 0 1920x1080x24 &
```

### PyQt5 Not Found

```bash
sudo apt-get update
sudo apt-get install python3-pyqt5 python3-pyqt5.qtwebengine
```

### Can't Connect to Server

```bash
# Test network
ping <your-server-ip>

# Edit configuration
sudo nano /etc/digitalsignage/config.json

# Restart service
sudo systemctl restart digitalsignage-client
```

## File Locations

- Installation: `/opt/digitalsignage-client/`
- Configuration: `/etc/digitalsignage/config.json`
- Service: `/etc/systemd/system/digitalsignage-client.service`
- Logs: `journalctl -u digitalsignage-client`

## Need More Help?

See `TROUBLESHOOTING.md` for detailed solutions to common problems.

## Scripts Summary

- `quick-fix.sh` - Fix service issues and restart (run this first!)
- `install-minimal.sh` - Clean installation with minimal configuration
- `client.py --test` - Run comprehensive diagnostics
