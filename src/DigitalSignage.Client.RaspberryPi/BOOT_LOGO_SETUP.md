# Boot and Shutdown Logo Setup for Digital Signage Client

This guide explains the boot and shutdown logo system for the Digital Signage client on Raspberry Pi.

## Overview

The Digital Signage client displays a branded logo during boot and shutdown sequences for a professional appearance. This uses multiple technologies:

- **Plymouth** - Modern boot splash system (primary)
- **Boot splash** - Direct /boot/splash.png for fallback
- **Shutdown logo** - Custom systemd service shows logo during shutdown
- **Quiet boot** - Kernel parameters suppress boot messages

## Components

### 1. boot_logo_manager.py
Main manager for boot logo configuration. Handles:
- Detection of /boot directory
- Logo discovery and validation
- Plymouth installation and configuration
- cmdline.txt kernel parameter updates
- config.txt splash screen setup
- Intelligent image scaling and centering

**Usage:**
```bash
# Setup complete boot logo system
sudo python3 /opt/digitalsignage-client/boot_logo_manager.py

# Setup with specific logo
sudo python3 /opt/digitalsignage-client/boot_logo_manager.py --logo /path/to/logo.png

# Boot splash only (skip Plymouth)
sudo python3 /opt/digitalsignage-client/boot_logo_manager.py --boot-only

# Debug mode
sudo python3 /opt/digitalsignage-client/boot_logo_manager.py --debug
```

### 2. shutdown_logo_display.py
Displays logo during system shutdown. Features:
- Signal handlers for graceful shutdown
- Multiple display methods:
  - Plymouth (if active)
  - fbi (framebuffer image viewer)
  - ImageMagick display command
- Logging to /var/log/digitalsignage-shutdown.log
- 30-second timeout

**Called by systemd service during shutdown sequence.**

### 3. digitalsignage-client-shutdown.service
Systemd service unit that:
- Runs during shutdown phase
- Shows shutdown logo before system halts
- Supports both desktop and headless environments
- Gracefully cleans up on timeout

**Location:** `/etc/systemd/system/digitalsignage-client-shutdown.service`

### 4. setup-splash-screen.sh
Legacy splash screen setup script using Plymouth. Now integrated into boot_logo_manager.py but available as standalone tool.

## Installation and Setup

### Automatic Setup (via install.sh)

The complete boot logo system is automatically configured during installation:

```bash
cd ~/digitalsignage/src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh
```

The installer will:
1. Copy digisign-logo.png to /opt/digitalsignage-client/
2. Install boot_logo_manager.py and shutdown_logo_display.py
3. Install systemd services
4. Configure Plymouth splash screen
5. Update /boot/cmdline.txt with quiet boot parameters
6. Setup shutdown logo service

### Manual Setup

If you need to reconfigure boot/shutdown logos:

```bash
# Setup complete boot logo system
sudo /opt/digitalsignage-client/boot_logo_manager.py

# Or with specific logo
sudo /opt/digitalsignage-client/boot_logo_manager.py --logo /path/to/custom-logo.png
```

## Boot Sequence

1. **BIOS/Bootloader** - Hardware initialization
2. **Kernel loads** - Kernel boots with quiet parameters
3. **Plymouth starts** - Boot splash screen displays
4. **digisign-logo.png** - Centered and scaled on screen
5. **Filesystem mounts** - No boot messages shown
6. **systemd services start** - X11 and Digital Signage client
7. **Logo displayed** - Until X11 is ready

### Kernel Parameters

These parameters are added to `/boot/cmdline.txt`:

- `quiet` - Suppress kernel messages
- `splash` - Show splash screen
- `logo.nologo` - Hide kernel tux logo
- `loglevel=0` - Suppress all kernel logging
- `vt.global_cursor_default=0` - Hide text cursor
- `plymouth.ignore-serial-consoles` - Don't show on serial console

### /boot/config.txt

These settings are added to `/boot/config.txt`:

```ini
disable_splash=1    # Disable default rainbow splash screen
```

## Shutdown Sequence

1. **Shutdown initiated** - User power-off or systemd shutdown
2. **Normal services stop** - Graphical/network services stop
3. **digitalsignage-client-shutdown service runs** - Before final shutdown
4. **Shutdown logo displayed** - Via Plymouth or framebuffer
5. **System halts** - With branded logo visible

## Troubleshooting

### Boot logo not appearing

**Check Plymouth installation:**
```bash
sudo apt-get install plymouth plymouth-themes pix-plym-splash
```

**Verify splash configuration:**
```bash
sudo cat /boot/config.txt | grep splash
sudo cat /boot/cmdline.txt | grep splash
```

**Check Plymouth theme:**
```bash
plymouth-set-default-theme --list
sudo plymouth-set-default-theme -R pix
```

**Verify logo file:**
```bash
ls -lh /usr/share/plymouth/themes/pix/splash.png
file /usr/share/plymouth/themes/pix/splash.png
```

**View boot logs:**
```bash
sudo journalctl -b -0  # Current boot
sudo journalctl -b -1  # Previous boot
```

### Shutdown logo not appearing

**Check shutdown service:**
```bash
sudo systemctl status digitalsignage-client-shutdown.service
sudo journalctl -u digitalsignage-client-shutdown.service -n 50
```

**Check shutdown log:**
```bash
sudo tail -f /var/log/digitalsignage-shutdown.log
```

**Verify logo file accessibility:**
```bash
sudo ls -l /opt/digitalsignage-client/digisign-logo.png
```

**Test shutdown logo manually:**
```bash
sudo /opt/digitalsignage-client/shutdown_logo_display.py
```

### Image not centered or scaled properly

**Reconfigure Plymouth script:**
```bash
sudo /opt/digitalsignage-client/boot_logo_manager.py
```

This regenerates the Plymouth script with intelligent scaling.

## Manual Logo Replacement

To use a different logo image:

1. **Copy new logo to client directory:**
```bash
sudo cp /path/to/new-logo.png /opt/digitalsignage-client/digisign-logo.png
```

2. **Reconfigure boot logo:**
```bash
sudo /opt/digitalsignage-client/boot_logo_manager.py --logo /opt/digitalsignage-client/digisign-logo.png
```

3. **Reboot to see changes:**
```bash
sudo reboot
```

## Logo Requirements

- **Format:** PNG (supported by Plymouth, fbi, and ImageMagick)
- **Size:** 1920x1080 recommended (will be scaled intelligently)
- **Aspect ratio:** Any (will maintain aspect ratio while filling screen)
- **Color space:** RGB or RGBA
- **File size:** < 5 MB recommended

## Advanced Configuration

### Disable boot splash

If you want to revert to standard boot:

```bash
# Remove quiet boot parameters
sudo sed -i 's/ quiet/ /g; s/ splash/ /g; s/ logo.nologo/ /g' /boot/cmdline.txt

# Remove disable_splash from config.txt
sudo sed -i '/^disable_splash=/d' /boot/config.txt

# Set default Plymouth theme
sudo plymouth-set-default-theme -R default
```

### Custom Plymouth Script

To customize logo positioning/scaling, edit:
```bash
/usr/share/plymouth/themes/pix/pix.script
```

Then rebuild:
```bash
sudo update-initramfs -u
```

### Environment Variables

When testing boot_logo_manager.py:

```bash
# Debug mode
export DEBUG=1
sudo python3 boot_logo_manager.py

# Custom boot directory (for testing)
export BOOT_DIR=/mnt/boot
sudo python3 boot_logo_manager.py
```

## Security Considerations

- Boot logo files are world-readable
- Shutdown service runs as root
- Kernel parameters are visible in /proc/cmdline
- No sensitive information should be in logo image

## Performance Impact

- **Boot time:** +1-2 seconds (Plymouth initialization)
- **Shutdown time:** +1-2 seconds (logo display)
- **RAM:** < 10 MB (Plymouth + logo image)
- **CPU:** Minimal (image scaling only during boot/shutdown)

## Files and Locations

```
/opt/digitalsignage-client/
├── boot_logo_manager.py          # Boot logo manager script
├── shutdown_logo_display.py       # Shutdown logo display script
├── digisign-logo.png             # Branded logo image
├── setup-splash-screen.sh        # Legacy splash setup script
└── digitalsignage-client-shutdown.service  # Shutdown service template

/etc/systemd/system/
└── digitalsignage-client-shutdown.service  # Installed shutdown service

/boot/
├── splash.png                    # Boot splash image
├── cmdline.txt                   # Kernel parameters (updated)
└── config.txt                    # Raspberry Pi config (updated)

/usr/share/plymouth/themes/pix/
├── splash.png                    # Plymouth theme logo
└── pix.script                    # Plymouth script (updated)

/var/log/
└── digitalsignage-shutdown.log   # Shutdown process log
```

## Logs and Debugging

### Boot Logo Logs

Plymouth logs:
```bash
# View Plymouth logs
sudo journalctl -u systemd-ask-password-console
sudo dmesg | grep -i plymouth
```

config_txt_manager.py logs:
```bash
# Check if setup completed
sudo cat ~/.digitalsignage/logs/client.log | grep -i boot
```

### Shutdown Logo Logs

```bash
# View shutdown service logs
sudo journalctl -u digitalsignage-client-shutdown.service -n 100

# View shutdown log file
sudo tail -50 /var/log/digitalsignage-shutdown.log

# Real-time shutdown log
sudo tail -f /var/log/digitalsignage-shutdown.log
```

## Related Files

- **DEPLOYMENT.md** - Raspberry Pi deployment guide
- **INSTALLATION.md** - Installation instructions
- **TROUBLESHOOTING.md** - General troubleshooting
- **install.sh** - Automated installer (calls boot_logo_manager)

## Support

For issues or questions:

1. Check logs (see above)
2. Run diagnose script: `sudo /opt/digitalsignage-client/diagnose.sh`
3. Test boot logo manually: `sudo /opt/digitalsignage-client/boot_logo_manager.py --debug`
4. Review TROUBLESHOOTING.md for additional help

## Version History

- **1.0** - Initial boot/shutdown logo implementation
  - Plymouth integration
  - Intelligent image scaling
  - Shutdown service
  - Quiet boot configuration
