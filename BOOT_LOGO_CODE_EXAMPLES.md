# Boot and Shutdown Logo - Code Examples

This document provides practical code examples for using the new boot and shutdown logo system.

## 1. Boot Logo Manager Usage

### Basic Setup (Auto-discover Logo)

```python
#!/usr/bin/env python3
from boot_logo_manager import BootLogoManager

# Create manager (auto-discovers logo)
manager = BootLogoManager()

# Check root
if not manager.require_root():
    sys.exit(1)

# Setup complete system
if manager.setup_all():
    print("Boot logo system configured successfully")
else:
    print("Failed to configure boot logo system")
```

### Setup with Specific Logo

```python
from boot_logo_manager import BootLogoManager

manager = BootLogoManager("/path/to/custom-logo.png")

if manager.validate_logo():
    if manager.setup_all():
        print("Custom logo installed")
```

### Individual Components

```python
from boot_logo_manager import BootLogoManager

manager = BootLogoManager()

# Setup just the boot splash (without Plymouth)
if manager.setup_boot_splash():
    print("Boot splash configured")

# Configure only kernel parameters
if manager.configure_cmdline():
    print("Kernel parameters updated")

# Setup only Plymouth
if manager.install_plymouth() and manager.configure_plymouth_theme():
    print("Plymouth installed and configured")
```

### Debug Mode

```python
import logging
from boot_logo_manager import BootLogoManager

# Enable debug logging
logging.basicConfig(level=logging.DEBUG)

manager = BootLogoManager()
manager.setup_all()  # Verbose output
```

## 2. Shutdown Logo Display Usage

### Display Logo (Manual)

```python
#!/usr/bin/env python3
from shutdown_logo_display import ShutdownLogoDisplay

# Create display manager
display = ShutdownLogoDisplay()

# Configure logging
display.configure_logging("/var/log/test-shutdown.log")

# Setup signal handlers
display.setup_signal_handlers()

# Show logo
if display.show_logo():
    print("Logo displayed successfully")
    display.wait_for_shutdown()
else:
    print("Failed to display logo")
```

### With Custom Timeout

```python
from shutdown_logo_display import ShutdownLogoDisplay

display = ShutdownLogoDisplay()
display.SHUTDOWN_TIMEOUT = 60  # 60 seconds instead of default 30

display.run()
```

### Test Shutdown Display

```python
#!/usr/bin/env python3
import sys
from shutdown_logo_display import ShutdownLogoDisplay

# Test shutdown logo display
display = ShutdownLogoDisplay()
exit_code = display.run()
sys.exit(exit_code)
```

## 3. Systemd Integration

### Check Shutdown Service Status

```bash
# View service status
sudo systemctl status digitalsignage-client-shutdown.service

# View recent logs
sudo journalctl -u digitalsignage-client-shutdown.service -n 50

# Enable service
sudo systemctl enable digitalsignage-client-shutdown.service

# Test service
sudo systemctl start digitalsignage-client-shutdown.service

# Disable service
sudo systemctl disable digitalsignage-client-shutdown.service
```

### Manual Service Testing

```bash
# Run shutdown service manually
sudo /opt/digitalsignage-client/venv/bin/python3 \
    /opt/digitalsignage-client/shutdown_logo_display.py

# With timeout
timeout 35 sudo /opt/digitalsignage-client/venv/bin/python3 \
    /opt/digitalsignage-client/shutdown_logo_display.py
```

## 4. Setup Script Usage

### Auto-discover and Install

```bash
# Basic setup
sudo ./setup-boot-shutdown-logos.sh

# Output:
# === Digital Signage Boot and Shutdown Logo Setup ===
# [*] Auto-discovering logo...
# [✓] Found logo: /opt/digitalsignage-client/digisign-logo.png
# [✓] Logo validated: ...
# [*] Running boot logo manager...
# ... setup progress ...
# [✓] All configurations completed successfully!
```

### With Custom Logo

```bash
# Use specific logo
sudo ./setup-boot-shutdown-logos.sh --logo /path/to/custom-logo.png

# Boot logo only (no shutdown service)
sudo ./setup-boot-shutdown-logos.sh --no-shutdown

# Help
sudo ./setup-boot-shutdown-logos.sh --help
```

## 5. Integration with install.sh

### Automatic Setup During Installation

```bash
cd ~/digitalsignage/src/DigitalSignage.Client.RaspberryPi

# Run main installer (handles everything)
sudo ./install.sh

# Progress output shows boot logo configuration:
# [7/12] Copying client files...
# [8/12] Creating config directory...
# [9/12] Configuring splash screen...
# [10/12] Installing systemd services...
#   ✓ Service file installed
#   ✓ Shutdown service file installed
#   ✓ Systemd daemon reloaded
# ...
# INSTALLATION COMPLETE!
```

## 6. Configuration File Modifications

### What install.sh/boot_logo_manager.py Modify

#### /boot/cmdline.txt (Before)
```
console=serial0,115200 console=tty1 root=PARTUUID=... rootfstype=ext4 elevator=deadline
```

#### /boot/cmdline.txt (After)
```
console=serial0,115200 console=tty1 root=PARTUUID=... rootfstype=ext4 elevator=deadline quiet splash logo.nologo loglevel=0 vt.global_cursor_default=0 plymouth.ignore-serial-consoles
```

#### /boot/config.txt (Modified)
```ini
[All existing settings...]

# --- digital-signage display block (auto-generated) ---
# [display configuration from config_txt_manager.py]
# --- end digital-signage display block ---

# Disable default rainbow splash
disable_splash=1
```

#### /usr/share/plymouth/themes/pix/pix.script (Created)
```scheme
screen_width = Window.GetWidth();
screen_height = Window.GetHeight();

theme_image = Image("splash.png");
image_width = theme_image.GetWidth();
image_height = theme_image.GetHeight();

# [Intelligent scaling and centering logic]

sprite = Sprite(resized_image);
sprite.SetPosition(image_x, image_y, -100);
```

## 7. Troubleshooting Commands

### Check Boot Logo

```bash
# Verify installation
ls -lh /boot/splash.png
ls -lh /usr/share/plymouth/themes/pix/splash.png

# Check kernel parameters
cat /boot/cmdline.txt | grep -E "(quiet|splash|logo.nologo)"

# Check Plymouth
plymouth-set-default-theme
lsmod | grep plymouth

# View boot logs
sudo journalctl -b -0 | grep -iE "(plymouth|splash|boot)"

# Check config
grep -E "(disable_splash|boot)" /boot/config.txt
```

### Check Shutdown Logo

```bash
# Check service installation
sudo systemctl status digitalsignage-client-shutdown.service
sudo systemctl is-enabled digitalsignage-client-shutdown.service

# View shutdown logs
sudo tail -50 /var/log/digitalsignage-shutdown.log
sudo journalctl -u digitalsignage-client-shutdown.service

# Test shutdown logo
sudo /opt/digitalsignage-client/shutdown_logo_display.py

# Check logo file
ls -lh /opt/digitalsignage-client/digisign-logo.png
file /opt/digitalsignage-client/digisign-logo.png
```

## 8. Manual Boot Logo Configuration

### Step-by-Step Manual Setup

```bash
#!/bin/bash

# 1. Copy logo to /boot
sudo cp /opt/digitalsignage-client/digisign-logo.png /boot/splash.png

# 2. Install Plymouth
sudo apt-get install -y plymouth plymouth-themes pix-plym-splash

# 3. Copy logo to Plymouth theme directory
sudo cp /opt/digitalsignage-client/digisign-logo.png \
    /usr/share/plymouth/themes/pix/splash.png

# 4. Update /boot/config.txt
echo "disable_splash=1" | sudo tee -a /boot/config.txt

# 5. Update /boot/cmdline.txt (all on one line)
sudo sed -i 's/$/ quiet splash logo.nologo loglevel=0 vt.global_cursor_default=0 plymouth.ignore-serial-consoles/' /boot/cmdline.txt

# 6. Set Plymouth theme
sudo plymouth-set-default-theme -R pix

# 7. Reboot to see changes
sudo reboot
```

## 9. Logo Image Processing

### Create Custom Logo

```bash
# From JPEG
convert logo.jpg -resize 1920x1080 logo.png

# From SVG
convert logo.svg -resize 1920x1080 logo.png

# Create from text
convert -size 1920x1080 xc:black \
    -font Arial -pointsize 100 -fill white \
    -gravity center -annotate +0+0 "Digital Signage" \
    logo.png

# Verify format
file logo.png
identify logo.png  # ImageMagick tool
```

### Logo Validation Script

```python
#!/usr/bin/env python3
from pathlib import Path
import subprocess

def validate_logo(logo_path: str) -> bool:
    """Validate logo for boot use"""
    logo = Path(logo_path)

    # Check existence
    if not logo.exists():
        print(f"Error: {logo_path} not found")
        return False

    # Check format
    result = subprocess.run(
        ["file", str(logo)],
        capture_output=True,
        text=True
    )
    if "PNG" not in result.stdout:
        print(f"Error: Not a PNG file")
        return False

    # Check size
    size_mb = logo.stat().st_size / 1024 / 1024
    if size_mb > 10:
        print(f"Error: File too large ({size_mb}MB > 10MB)")
        return False

    # Get dimensions
    result = subprocess.run(
        ["identify", str(logo)],
        capture_output=True,
        text=True
    )
    print(f"Logo info: {result.stdout}")

    print("Logo is valid!")
    return True

if __name__ == "__main__":
    validate_logo("digisign-logo.png")
```

## 10. Logging and Debugging

### Enable Debug Logging

```python
import logging
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)

# Now run boot_logo_manager
from boot_logo_manager import BootLogoManager
manager = BootLogoManager()
manager.setup_all()  # Detailed output
```

### View All Boot/Shutdown Logs

```bash
# Combine boot and shutdown logs
echo "=== BOOT LOGS ==="
sudo journalctl -b | grep -iE "(boot|splash|plymouth)" | tail -20

echo ""
echo "=== SHUTDOWN LOGS ==="
sudo journalctl -b | grep digitalsignage-shutdown | tail -20

echo ""
echo "=== SHUTDOWN LOG FILE ==="
sudo tail -20 /var/log/digitalsignage-shutdown.log
```

### Debug Display Methods

```bash
# Test Plymouth display
sudo systemctl status plymouth

# Test fbi (framebuffer)
which fbi && fbi -h

# Test ImageMagick
display -version

# Check framebuffer
ls -l /dev/fb*
fbset -i
```

## 11. Performance Monitoring

### Boot Time Comparison

```bash
# Measure boot time
systemd-analyze

# Detailed service times
systemd-analyze blame

# Graph boot process
systemd-analyze plot > boot-process.svg
```

### Check Resource Usage

```bash
# During boot
ps aux | grep -E "(plymouth|digisignage)"

# Check memory
free -h

# Check disk space
df -h /boot
```

## 12. Complete Test Scenario

```bash
#!/bin/bash

echo "Testing Boot and Shutdown Logo System..."

# 1. Check boot logo files
echo "1. Checking boot logo installation..."
ls -lh /boot/splash.png /usr/share/plymouth/themes/pix/splash.png

# 2. Check kernel parameters
echo "2. Checking kernel parameters..."
cat /boot/cmdline.txt | grep -E "quiet|splash"

# 3. Check shutdown service
echo "3. Checking shutdown service..."
sudo systemctl status digitalsignage-client-shutdown.service

# 4. Test boot logo manager
echo "4. Testing boot logo manager..."
sudo python3 /opt/digitalsignage-client/boot_logo_manager.py --help

# 5. Test shutdown logo display
echo "5. Testing shutdown logo display..."
timeout 5 sudo /opt/digitalsignage-client/shutdown_logo_display.py || true

# 6. Check logs
echo "6. Recent logs..."
sudo tail -10 /var/log/digitalsignage-shutdown.log

echo "All tests completed!"
```

## Summary

These examples cover:
- Direct Python usage of BootLogoManager and ShutdownLogoDisplay
- Bash integration and systemd service management
- Configuration file modifications
- Troubleshooting and debugging techniques
- Logo image creation and validation
- Performance monitoring
- Complete test scenarios

For more detailed information, see BOOT_LOGO_SETUP.md and the inline code documentation.
