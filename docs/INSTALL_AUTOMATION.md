# Install.sh Automatic Display Detection and Configuration

## Overview

The enhanced `install.sh` script now automatically detects and configures your Raspberry Pi for the correct display mode, eliminating the need to run separate configuration scripts and providing a seamless installation experience.

## What's New

### Automatic Hardware Detection

The script now intelligently detects:

1. **HDMI Display Presence**: Uses three detection methods:
   - `tvservice` (Raspberry Pi specific)
   - `/sys/class/drm/*/status` (DRM subsystem)
   - `xrandr` (X11 display detection)

2. **X11 State**: Checks if X11 is already running on display `:0`

3. **System Configuration**: Detects if auto-login and desktop environment are already configured

### Intelligent Recommendations

Based on the detection results, the script recommends the appropriate mode:

#### Scenario 1: HDMI Connected, X11 Not Running
```
RECOMMENDATION: PRODUCTION MODE
Reason: HDMI display detected, but X11 not configured

This will:
  ✓ Enable auto-login to desktop
  ✓ Start X11 automatically on HDMI display
  ✓ Disable screen blanking
  ✓ Hide mouse cursor
  ✓ Show digital signage on your display
  ⚠ Requires reboot
```

#### Scenario 2: HDMI Connected, X11 Already Running
```
RECOMMENDATION: PRODUCTION MODE (Already Configured)
Reason: X11 already running on display

This will:
  ✓ Verify auto-login settings
  ✓ Ensure power management is disabled
  ✓ Install the client service
  ℹ May not require reboot
```

#### Scenario 3: No HDMI Display
```
RECOMMENDATION: DEVELOPMENT MODE (Headless)
Reason: No HDMI display detected or headless environment

This will:
  ✓ Use Xvfb (virtual display)
  ✓ Allow testing without physical display
  ✓ No reboot required
```

### Smart Configuration

The production mode configuration now checks each setting before applying:

1. **Auto-login Check**: Only enables if not already configured
2. **LightDM Check**: Only modifies if configuration needed
3. **X11 Startup Check**: Only creates `.xinitrc` if missing or incomplete
4. **Reboot Decision**: Only prompts for reboot if changes were made

### Enhanced User Experience

The script provides clear feedback at each step:

```bash
[1/5] Checking auto-login...
  ✓ Auto-login already enabled

[2/5] Configuring display manager...
  ✓ LightDM already configured

[3/5] Configuring X11 startup...
  ✓ X11 already configured

[4/5] Updating service configuration...
  ✓ Service configured for display auto-detection

[5/5] Verifying configuration...
  ✓ System already configured for production mode
  ✓ X11 is running - no reboot needed
```

## Usage

### Fresh Installation with HDMI Display

```bash
cd /opt/digitalsignage-client
sudo ./install.sh
```

The script will:
1. Detect your HDMI display
2. Recommend Production Mode
3. Configure auto-login and X11
4. Prompt for reboot

### Re-running on Already Configured System

```bash
sudo ./install.sh
```

The script will:
1. Detect existing configuration
2. Skip already-configured settings
3. Report "No reboot required"

### Headless Installation

```bash
sudo ./install.sh
```

The script will:
1. Detect no HDMI display
2. Recommend Development Mode
3. Configure Xvfb virtual display
4. Start service immediately (no reboot)

## Technical Details

### Display Detection Functions

#### `detect_display_mode()`
```bash
# Checks if X11 is accessible on :0
# Returns: 0 if X11 running, 1 otherwise
# Sets: DETECTED_MODE (desktop|console|other)
```

#### `check_hdmi_display()`
```bash
# Uses multiple methods to detect physical display
# Returns: 0 if display found, 1 otherwise
# Methods:
#   1. tvservice -s (Raspberry Pi)
#   2. /sys/class/drm/*/status (DRM)
#   3. xrandr (X11)
```

### Configuration Tracking

The `NEEDS_REBOOT` flag tracks whether system changes require a reboot:

```bash
NEEDS_REBOOT=false

# Set to true when:
- Auto-login configuration changes
- LightDM configuration changes
- X11 not running (but should be)

# Remains false when:
- Settings already configured
- X11 already running
- Development mode selected
```

### Idempotency

The script is fully idempotent - it can be run multiple times safely:

- ✓ Detects existing configurations
- ✓ Skips unnecessary changes
- ✓ Only modifies what needs modification
- ✓ Creates backups before changing configs
- ✓ Never breaks an already-working system

## Benefits

### For End Users

1. **Simpler Installation**: One script does everything
2. **Clear Recommendations**: Script tells you what it detected and recommends
3. **No Unnecessary Reboots**: Only reboots if actually needed
4. **Better Feedback**: Clear progress indicators and status messages

### For Developers

1. **Easier Testing**: Can run on both headless and desktop systems
2. **Reproducible**: Same script works in all scenarios
3. **Maintainable**: All logic in one place
4. **Documented**: Clear detection and configuration logic

### For System Administrators

1. **Automated Deployment**: Works in provisioning scripts
2. **Safe Re-runs**: Can be run multiple times safely
3. **Audit Trail**: Clear output shows what was changed
4. **Backup Safety**: Original configs backed up before changes

## Comparison: Before vs After

### Before

```bash
# Manual process:
sudo ./install.sh          # Install basics
# ... wait for installation ...
# Choose production mode
# Reboot
# Wait for reboot
# Run separate script:
sudo ./configure-display.sh
# Reboot again
```

### After

```bash
# Automated process:
sudo ./install.sh          # Install and configure everything
# Script detects display automatically
# Script configures everything needed
# Script only prompts for reboot if needed
# Done!
```

## Migration from Old Process

If you previously used `configure-display.sh`:

1. The functionality is now built into `install.sh`
2. You can still run `configure-display.sh` separately if needed
3. Running `install.sh` again will detect your existing configuration
4. No need to reconfigure - just use the new script for fresh installs

## Troubleshooting

### Script Recommends Production Mode But I Want Development Mode

You can override the recommendation:
```
Select deployment mode:
  1) PRODUCTION MODE
  2) DEVELOPMENT MODE

Enter choice [1/2] (default: 1): 2
```

### Script Says "No HDMI Display Detected" But I Have One Connected

Check your display:
```bash
# Raspberry Pi specific:
tvservice -s

# DRM subsystem:
cat /sys/class/drm/*/status

# X11 (if running):
DISPLAY=:0 xrandr
```

If display is connected but not detected, you may need to:
1. Check HDMI cable connection
2. Check display power
3. Try a different HDMI port (use HDMI 0 on Pi 4)

### Script Says "X11 Already Running" But Display is Black

This can happen if X11 started but no desktop environment loaded. The script will configure `.xinitrc` to ensure proper X11 startup after reboot.

### Need to Force Reconfiguration

If you want to force a complete reconfiguration:
```bash
# Remove X11 config:
rm ~/.xinitrc

# Reset boot behavior:
sudo raspi-config nonint do_boot_behaviour B1  # Console

# Re-run installation:
sudo ./install.sh
```

## Related Documentation

- [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - Detailed troubleshooting guide
- [DEPLOYMENT.md](DEPLOYMENT.md) - Production deployment guide
- [SSL_SETUP.md](SSL_SETUP.md) - SSL/TLS configuration

## Implementation Notes

### Why Three Detection Methods?

Different Raspberry Pi models and OS versions expose display information differently:

1. **tvservice**: Works on older Raspberry Pi OS (legacy GPU driver)
2. **DRM**: Works on newer systems with KMS driver
3. **xrandr**: Works when X11 is running (any configuration)

Using all three ensures reliable detection across all configurations.

### Why Check Existing Configuration?

Avoiding unnecessary changes:
- Reduces risk of breaking working systems
- Avoids unnecessary reboots
- Faster re-installation for updates
- Better user experience

### Why sudo -u in Detection Functions?

Display detection must run as the actual user (not root) because:
- X11 authority files are user-specific
- DISPLAY environment belongs to user session
- Ensures accurate detection of user's X11 state

## Future Enhancements

Potential improvements for future versions:

1. **Display Resolution Detection**: Auto-configure optimal resolution
2. **Multiple Display Support**: Handle dual HDMI on Pi 4
3. **Composite/DSI Detection**: Support for non-HDMI displays
4. **Remote Display**: Handle VNC/remote X11 scenarios
5. **Wayland Support**: Detect and configure Wayland compositors

## Changelog

### Version 2.0 (2025-11-11)
- Added automatic HDMI display detection
- Added automatic X11 state detection
- Added intelligent mode recommendation
- Added smart reboot logic (only when needed)
- Added idempotent configuration checks
- Added comprehensive status reporting
- Integrated configure-display.sh functionality

### Version 1.0 (Previous)
- Basic installation with manual mode selection
- Required separate configure-display.sh script
- Always prompted for reboot in production mode
