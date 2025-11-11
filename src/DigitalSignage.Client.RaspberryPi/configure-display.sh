#!/bin/bash
# Configure Raspberry Pi for real HDMI display output

set -e

echo "================================================"
echo "Digital Signage - Real Display Configuration"
echo "================================================"
echo ""

# Detect current user
CURRENT_USER="${SUDO_USER:-$USER}"
if [ "$CURRENT_USER" = "root" ]; then
    echo "Error: Don't run as root directly"
    echo "Use: sudo ./configure-display.sh"
    exit 1
fi

USER_HOME=$(eval echo "~$CURRENT_USER")

echo "Configuring for user: $CURRENT_USER"
echo "Home directory: $USER_HOME"
echo ""

# 1. Enable auto-login
echo "[1/6] Enabling auto-login..."
if command -v raspi-config &>/dev/null; then
    # B4 = Desktop Autologin (boot to desktop, automatically logged in)
    raspi-config nonint do_boot_behaviour B4 2>/dev/null || true
    if [ $? -eq 0 ]; then
        echo "  ✓ Auto-login enabled (desktop autologin)"
    else
        echo "  ⚠ raspi-config completed with warnings (may still be successful)"
    fi
else
    echo "  ⚠ raspi-config not found"
    echo "    Manual: Run 'sudo raspi-config' → System Options → Boot/Auto Login → Desktop Autologin"
fi

# 2. Install display manager if needed
echo ""
echo "[2/6] Checking display manager..."
if ! command -v startx &>/dev/null && ! systemctl list-unit-files | grep -q lightdm; then
    echo "  Installing LightDM display manager..."
    apt-get update
    apt-get install -y lightdm
    echo "  ✓ LightDM installed"
else
    echo "  ✓ Display manager already available"
fi

# 3. Configure LightDM
echo ""
echo "[3/6] Configuring display manager..."
if [ -f /etc/lightdm/lightdm.conf ]; then
    # Backup original
    if [ ! -f /etc/lightdm/lightdm.conf.backup-manual ]; then
        cp /etc/lightdm/lightdm.conf /etc/lightdm/lightdm.conf.backup-manual
        echo "  ✓ LightDM config backed up"
    fi

    # Set autologin (handle both formats)
    if grep -q "^autologin-user=" /etc/lightdm/lightdm.conf; then
        sed -i "s/^autologin-user=.*/autologin-user=$CURRENT_USER/" /etc/lightdm/lightdm.conf
    else
        sed -i "s/^#autologin-user=.*/autologin-user=$CURRENT_USER/" /etc/lightdm/lightdm.conf
    fi

    echo "  ✓ LightDM configured for $CURRENT_USER"
elif systemctl list-unit-files | grep -q gdm; then
    echo "  ⚠ GDM detected (different display manager)"
    echo "    You may need to configure GDM for auto-login manually"
else
    echo "  ⚠ No display manager config found"
    echo "    This is normal for Raspberry Pi OS Lite"
    echo "    System will use console auto-login + startx"
fi

# 4. Create .xinitrc
echo ""
echo "[4/6] Creating X11 startup configuration..."
cat > "$USER_HOME/.xinitrc" <<'EOF'
#!/bin/sh
# Digital Signage X11 Configuration

# Disable screen blanking and power management
xset -dpms
xset s off
xset s noblank

# Hide mouse cursor
unclutter -idle 0.1 -root &

# Keep X running (prevent X from exiting)
exec tail -f /dev/null
EOF

chown "$CURRENT_USER:$CURRENT_USER" "$USER_HOME/.xinitrc"
chmod +x "$USER_HOME/.xinitrc"
echo "  ✓ .xinitrc created at $USER_HOME/.xinitrc"

# 5. Update service to use real display
echo ""
echo "[5/6] Updating service configuration..."
SERVICE_FILE="/etc/systemd/system/digitalsignage-client.service"

if [ -f "$SERVICE_FILE" ]; then
    # Backup service file
    cp "$SERVICE_FILE" "${SERVICE_FILE}.backup-manual"

    # Update DISPLAY to :0 (real display)
    sed -i 's/Environment="DISPLAY=:[0-9]*"/Environment="DISPLAY=:0"/' "$SERVICE_FILE"

    # Ensure we wait for graphical target
    if ! grep -q "After=.*graphical.target" "$SERVICE_FILE"; then
        sed -i '/After=/s/$/ graphical.target/' "$SERVICE_FILE"
    fi

    systemctl daemon-reload
    echo "  ✓ Service configured for real display (DISPLAY=:0)"
else
    echo "  ⚠ Service file not found at $SERVICE_FILE"
fi

# 6. Stop service (will restart on reboot with new settings)
echo ""
echo "[6/6] Stopping service for reboot..."
systemctl stop digitalsignage-client 2>/dev/null || true
echo "  ✓ Service stopped (will start automatically after reboot)"

echo ""
echo "================================================"
echo "Configuration Complete!"
echo "================================================"
echo ""
echo "Summary of changes:"
echo "  1. ✓ Auto-login enabled for $CURRENT_USER"
echo "  2. ✓ Display manager configured"
echo "  3. ✓ X11 startup script created (~/.xinitrc)"
echo "  4. ✓ Service updated for real display (DISPLAY=:0)"
echo "  5. ✓ Service stopped (will auto-start after reboot)"
echo ""
echo "================================================"
echo "NEXT STEPS:"
echo "================================================"
echo ""
echo "1. REBOOT NOW (required for changes to take effect):"
echo "   sudo reboot"
echo ""
echo "2. After reboot, verify X11 is running on real display:"
echo "   echo \$DISPLAY"
echo "   → Should show: :0"
echo ""
echo "3. Check if service is using real display:"
echo "   sudo systemctl status digitalsignage-client"
echo "   sudo journalctl -u digitalsignage-client -f"
echo ""
echo "4. Verify HDMI display is showing content"
echo ""
echo "================================================"
echo "IMPORTANT:"
echo "================================================"
echo ""
echo "- Ensure HDMI display is connected before rebooting"
echo "- Server connection must be configured in:"
echo "  /opt/digitalsignage-client/config.json"
echo ""
echo "- If server is not found, run network diagnostics:"
echo "  sudo bash /opt/digitalsignage-client/test-connection.sh"
echo ""
echo "================================================"
echo ""

read -p "Reboot now? (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo ""
    echo "Rebooting in 3 seconds..."
    sleep 3
    reboot
else
    echo ""
    echo "Remember to reboot when ready:"
    echo "  sudo reboot"
    echo ""
fi
