#!/bin/bash
#
# DEPRECATED: This script is no longer needed!
# The install.sh script now handles all auto-login and X11 configuration automatically.
#
# Please use: sudo ./install.sh
#
# This script is kept for backward compatibility but should not be used for new installations.
#

echo "========================================================================"
echo "DEPRECATED SCRIPT"
echo "========================================================================"
echo ""
echo "WARNING: This script (enable-autologin-x11.sh) is deprecated!"
echo ""
echo "The install.sh script now handles all auto-login configuration automatically."
echo "Please run: sudo ./install.sh"
echo ""
echo "Do you want to continue anyway? (NOT recommended) [y/N]: "
read -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborted. Please run: sudo ./install.sh"
    exit 0
fi
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: Please run as root (use sudo)"
    exit 1
fi

# Detect the current user (the one who ran sudo)
ACTUAL_USER="${SUDO_USER:-$USER}"

if [ -z "$ACTUAL_USER" ] || [ "$ACTUAL_USER" = "root" ]; then
    echo "ERROR: Could not detect non-root user"
    echo "Please run this script with sudo as a regular user"
    exit 1
fi

USER_HOME=$(eval echo "~$ACTUAL_USER")

echo "Configuring automatic X11 start for user: $ACTUAL_USER"
echo ""

# Check if raspi-config is available (Raspberry Pi only)
if command -v raspi-config &> /dev/null; then
    echo "[1/4] Enabling auto-login to desktop..."
    # B4 = Desktop Autologin (boot to desktop, automatically logged in)
    raspi-config nonint do_boot_behaviour B4
    echo "    Auto-login enabled"
else
    echo "[1/4] raspi-config not found (not on Raspberry Pi?)"
    echo "    Skipping auto-login configuration"
fi

echo ""
echo "[2/4] Creating .xinitrc for X11 configuration..."

# Create .xinitrc for the user
cat > "$USER_HOME/.xinitrc" <<'EOF'
#!/bin/sh
# Digital Signage X11 Startup Configuration

# Disable power management features
xset -dpms     # Disable DPMS (Energy Star) features
xset s off     # Disable screen saver
xset s noblank # Don't blank the video device

# Hide mouse cursor after 0.1 seconds of inactivity
unclutter -idle 0.1 -root &

# Optional: Start a lightweight desktop environment
# Uncomment if you want a desktop (not needed for signage)
# exec startlxde

# Keep X11 running
exec xterm -geometry 80x24+0+0 -e /bin/bash
EOF

chown "$ACTUAL_USER:$ACTUAL_USER" "$USER_HOME/.xinitrc"
chmod +x "$USER_HOME/.xinitrc"
echo "    .xinitrc created"

echo ""
echo "[3/4] Configuring screen blanking prevention..."

# Create autostart directory if it doesn't exist
AUTOSTART_DIR="$USER_HOME/.config/autostart"
mkdir -p "$AUTOSTART_DIR"

# Disable screen blanking on startup
cat > "$AUTOSTART_DIR/disable-screensaver.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Disable Screensaver
Comment=Prevent screen from blanking for digital signage
Exec=sh -c 'xset s off && xset -dpms && xset s noblank'
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
StartupNotify=false
EOF

# Hide mouse cursor on startup
cat > "$AUTOSTART_DIR/unclutter.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Unclutter
Comment=Hide mouse cursor for digital signage
Exec=unclutter -idle 0.1 -root
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
StartupNotify=false
EOF

chown -R "$ACTUAL_USER:$ACTUAL_USER" "$AUTOSTART_DIR"
echo "    Autostart entries created"

echo ""
echo "[4/4] Verifying configuration..."

# Check if unclutter is installed
if command -v unclutter &> /dev/null; then
    echo "    unclutter: installed"
else
    echo "    WARNING: unclutter not installed"
    echo "    Install with: sudo apt-get install unclutter"
fi

# Check if X11 utilities are installed
if command -v xset &> /dev/null; then
    echo "    xset: installed"
else
    echo "    WARNING: xset not installed"
    echo "    Install with: sudo apt-get install x11-xserver-utils"
fi

echo ""
echo "========================================================================"
echo "Configuration Complete!"
echo "========================================================================"
echo ""
echo "Changes made:"
echo "  1. Auto-login to desktop enabled"
echo "  2. Screen blanking and screensaver disabled"
echo "  3. Mouse cursor will auto-hide"
echo "  4. X11 will start automatically on boot"
echo ""
echo "IMPORTANT:"
echo "  - Reboot required for changes to take effect"
echo "  - A physical display (HDMI) must be connected"
echo "  - The digitalsignage-client service will start automatically"
echo ""
echo "Reboot now? (y/N): "
read -n 1 -r
echo ""
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "Rebooting in 5 seconds..."
    sleep 5
    reboot
else
    echo "Reboot manually later with: sudo reboot"
fi
echo ""
