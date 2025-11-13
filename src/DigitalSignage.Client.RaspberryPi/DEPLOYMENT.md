# Raspberry Pi Client Deployment Guide

## Quick Update (Automated)

The easiest way to update the client on a Raspberry Pi:

```bash
cd /opt/digitalsignage-client
./update.sh
```

This script will:
1. Stop the service
2. Pull latest changes from git
3. Update the systemd service file
4. Restart the service
5. Show the service status

## Manual Deployment Steps

If you prefer to update manually or need more control:

### 1. Stop the Service

```bash
sudo systemctl stop digitalsignage-client
```

### 2. Pull Latest Changes

```bash
cd /opt/digitalsignage-client
git pull origin claude/digital-signage-management-system-011CV1bUPLZ3uM2W8Dj7Wdcn
```

### 3. Update Systemd Service (if changed)

```bash
sudo cp digitalsignage-client.service /etc/systemd/system/
sudo systemctl daemon-reload
```

### 4. Restart Service

```bash
sudo systemctl start digitalsignage-client
```

### 5. Check Status

```bash
sudo systemctl status digitalsignage-client
```

### 6. View Logs

```bash
sudo journalctl -u digitalsignage-client -f
```

## Recent Changes

### Device Command Fixes (Latest)

- **System Restart:** Now uses `systemctl reboot` instead of `sudo reboot`
- **Screen Control:** Enhanced SCREEN_ON/SCREEN_OFF commands
  - Primary method: `xset dpms force on/off` (works on most systems)
  - Fallback: `vcgencmd display_power 1/0` (Raspberry Pi HDMI)
  - Comprehensive error handling and logging
- **Systemd Service:** Disabled `NoNewPrivileges` to allow system commands
  - This allows the service to execute `systemctl reboot` and other privileged commands
  - Still maintains security with `PrivateTmp=true`

### Commands Now Working

All device management commands from the server are now fully functional:
- ✅ RESTART (system reboot)
- ✅ RESTART_APP (application restart)
- ✅ SCREEN_ON (turn display on)
- ✅ SCREEN_OFF (turn display off)
- ✅ SET_VOLUME (adjust audio volume)
- ✅ SCREENSHOT (capture and send screenshot)
- ✅ CLEAR_CACHE (clear local cache)

## Troubleshooting

### Service Won't Start

Check the logs for errors:
```bash
sudo journalctl -u digitalsignage-client -n 50
```

Common issues:
- X11 display not available (check `DISPLAY` environment variable)
- PyQt5 not installed (run diagnostic: `python3 client.py --test`)
- Virtual environment missing (reinstall: `./install.sh`)

### Git Pull Fails

If you have local changes that conflict:
```bash
# Backup your config
cp config.json config.json.backup

# Discard local changes
git reset --hard HEAD

# Pull latest
git pull

# Restore your config
cp config.json.backup config.json
```

### Commands Not Working

If device commands (RESTART, SCREEN_ON, etc.) don't work:

1. Check the systemd service has `NoNewPrivileges` disabled
2. Verify the user is in the `sudo` group: `groups`
3. Check logs for permission errors: `sudo journalctl -u digitalsignage-client -f`

### Display Issues

For HDMI/display problems:
```bash
# Run diagnostic
sudo /opt/digitalsignage-client/diagnose.sh

# Check X11 is running
ps aux | grep X

# Check DISPLAY variable
echo $DISPLAY
```

## Configuration

The client configuration is stored in `/opt/digitalsignage-client/config.json`.

To update client configuration from the server, use the Device Management UI:
- Change server settings
- The client will automatically reconnect with new settings

## Service Management

```bash
# Start service
sudo systemctl start digitalsignage-client

# Stop service
sudo systemctl stop digitalsignage-client

# Restart service
sudo systemctl restart digitalsignage-client

# Enable auto-start on boot
sudo systemctl enable digitalsignage-client

# Disable auto-start
sudo systemctl disable digitalsignage-client

# View status
sudo systemctl status digitalsignage-client

# View logs (follow mode)
sudo journalctl -u digitalsignage-client -f

# View last 100 lines of logs
sudo journalctl -u digitalsignage-client -n 100
```

## Installation (First Time)

For a fresh installation on a new Raspberry Pi:

```bash
# Clone repository
cd /opt
sudo git clone https://github.com/manur84/digitalsignage.git digitalsignage-client
cd digitalsignage-client/src/DigitalSignage.Client.RaspberryPi

# Run installation script
sudo ./install.sh

# The script will:
# - Install system dependencies (PyQt5, X11 tools, etc.)
# - Create Python virtual environment
# - Install Python packages
# - Set up systemd service
# - Enable auto-start on boot
```

## Security Notes

The systemd service has been configured with specific security settings:

- **NoNewPrivileges=disabled:** Allows the client to execute system commands like `systemctl reboot`
- **PrivateTmp=true:** Isolates /tmp directory for security
- **User/Group:** Runs as the installation user (not root)

This configuration balances security with functionality, allowing remote device management while maintaining system isolation.

## Support

For issues or questions:
1. Check the logs: `sudo journalctl -u digitalsignage-client -f`
2. Run diagnostic mode: `python3 client.py --test`
3. Review the main documentation in `/var/www/html/digitalsignage/CLAUDE.md`
