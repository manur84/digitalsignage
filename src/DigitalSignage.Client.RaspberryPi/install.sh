#!/bin/bash

# Digital Signage Client Installation Script for Raspberry Pi
# Usage: sudo ./install.sh

set -e

echo "========================================="
echo "Digital Signage Client Installer"
echo "========================================="
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: Please run as root (use sudo)"
    exit 1
fi

# Detect the current user (the one who ran sudo)
ACTUAL_USER="${SUDO_USER:-$USER}"

# Validate user exists
if [ -z "$ACTUAL_USER" ] || [ "$ACTUAL_USER" = "root" ]; then
    echo "ERROR: Could not detect non-root user"
    echo "Please run this script with sudo as a regular user:"
    echo "  sudo ./install.sh"
    exit 1
fi

if ! id "$ACTUAL_USER" &>/dev/null; then
    echo "ERROR: User '$ACTUAL_USER' does not exist"
    exit 1
fi

USER_HOME=$(eval echo "~$ACTUAL_USER")

echo "Installing for user: $ACTUAL_USER"
echo "User home directory: $USER_HOME"
echo ""

# Check for existing installation and prompt for confirmation
if [ -d "/opt/digitalsignage-client" ] || systemctl list-unit-files | grep -q "digitalsignage-client.service"; then
    echo ""
    echo "WARNING: Existing installation detected"
    echo "This will:"
    echo "  - Stop and disable the current service"
    echo "  - Backup config.py to /tmp (if exists)"
    echo "  - Remove the old installation completely"
    echo "  - Install fresh version"
    echo "  - Enable and start the service automatically"
    echo ""
    read -p "Continue with installation? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Installation cancelled."
        exit 0
    fi
fi

# Clean up old installation
echo ""
echo "========================================="
echo "Cleaning Up Old Installation"
echo "========================================="
echo ""

# Stop service if running
if systemctl is-active --quiet digitalsignage-client 2>/dev/null; then
    echo "Stopping existing service..."
    systemctl stop digitalsignage-client
    echo "✓ Service stopped"
else
    echo "✓ No running service found"
fi

# Disable service if enabled
if systemctl is-enabled --quiet digitalsignage-client 2>/dev/null; then
    echo "Disabling existing service..."
    systemctl disable digitalsignage-client
    echo "✓ Service disabled"
else
    echo "✓ Service not enabled"
fi

# Backup old config if exists
if [ -f "/opt/digitalsignage-client/config.py" ]; then
    BACKUP_FILE="/tmp/digitalsignage-config-backup-$(date +%s).py"
    cp /opt/digitalsignage-client/config.py "$BACKUP_FILE"
    echo "✓ Old config backed up to: $BACKUP_FILE"
    echo "  (You can restore this later if needed)"
fi

# Remove old installation directory
if [ -d "/opt/digitalsignage-client" ]; then
    echo "Removing old installation directory..."
    rm -rf /opt/digitalsignage-client
    echo "✓ Old installation removed"
else
    echo "✓ No old installation found (fresh install)"
fi

echo ""
echo "========================================="
echo "Installing Digital Signage Client"
echo "========================================="
echo ""

# Update package lists
echo "[1/10] Updating package lists..."
apt-get update

# Install system dependencies
echo "[2/10] Installing system dependencies..."
apt-get install -y \
    python3 \
    python3-pip \
    python3-venv \
    python3-pyqt5 \
    python3-pyqt5.qtsvg \
    python3-pyqt5.qtmultimedia \
    python3-psutil \
    sqlite3 \
    libsqlite3-dev \
    x11-xserver-utils \
    unclutter \
    xdotool \
    libqt5multimedia5-plugins \
    xvfb \
    x11vnc

echo ""
echo "[3/10] Verifying PyQt5 installation..."
if python3 -c "import PyQt5" 2>/dev/null; then
    PYQT5_VERSION=$(python3 -c "from PyQt5.QtCore import PYQT_VERSION_STR; print(PYQT_VERSION_STR)" 2>/dev/null)
    echo "✓ PyQt5 $PYQT5_VERSION installed successfully"
    echo "✓ PyQt5 modules available for use in virtual environment"
else
    echo "✗ ERROR: PyQt5 installation failed"
    echo "Please check apt-get output above for errors"
    exit 1
fi

# Create installation directory
echo "[4/10] Creating installation directory..."
INSTALL_DIR="/opt/digitalsignage-client"
mkdir -p "$INSTALL_DIR"

# Create virtual environment
echo "[5/10] Creating Python virtual environment..."
VENV_DIR="$INSTALL_DIR/venv"
if [ -d "$VENV_DIR" ]; then
    echo "Virtual environment already exists, removing old one..."
    rm -rf "$VENV_DIR"
fi
# Use --system-site-packages to allow access to system-installed PyQt5 from apt
# This is necessary because PyQt5 is installed via apt (python3-pyqt5) rather than pip
python3 -m venv --system-site-packages "$VENV_DIR"

# Install Python dependencies in virtual environment
echo "[6/10] Installing Python dependencies in virtual environment..."
"$VENV_DIR/bin/pip" install --upgrade pip
if [ -f "requirements.txt" ]; then
    "$VENV_DIR/bin/pip" install -r requirements.txt
else
    echo "Warning: requirements.txt not found, installing basic dependencies..."
    "$VENV_DIR/bin/pip" install \
        python-socketio[client]==5.10.0 \
        aiohttp==3.9.1 \
        requests==2.31.0 \
        psutil==5.9.6 \
        pillow==10.1.0 \
        qrcode==7.4.2
    echo "Note: PyQt5 installed via apt (python3-pyqt5) in step 2"
fi

# Copy client files
echo "[7/10] Copying client files..."
cp client.py "$INSTALL_DIR/"
cp config.py "$INSTALL_DIR/"
cp device_manager.py "$INSTALL_DIR/"
cp display_renderer.py "$INSTALL_DIR/"
cp cache_manager.py "$INSTALL_DIR/"
cp watchdog_monitor.py "$INSTALL_DIR/"
cp remote_log_handler.py "$INSTALL_DIR/" 2>/dev/null || echo "Note: remote_log_handler.py not found (optional)"
cp diagnose.sh "$INSTALL_DIR/" 2>/dev/null || echo "Note: diagnose.sh not found (optional)"
cp start-with-display.sh "$INSTALL_DIR/" 2>/dev/null || echo "Note: start-with-display.sh not found (optional)"
cp enable-autologin-x11.sh "$INSTALL_DIR/" 2>/dev/null || echo "Note: enable-autologin-x11.sh not found (optional)"

# Set ownership
chown -R "$ACTUAL_USER:$ACTUAL_USER" "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/client.py"
chmod +x "$INSTALL_DIR/diagnose.sh" 2>/dev/null || true
chmod +x "$INSTALL_DIR/start-with-display.sh" 2>/dev/null || true
chmod +x "$INSTALL_DIR/enable-autologin-x11.sh" 2>/dev/null || true

# Create config directory
echo "[8/10] Creating config directory..."
CONFIG_DIR="$USER_HOME/.digitalsignage"
mkdir -p "$CONFIG_DIR/cache"
mkdir -p "$CONFIG_DIR/logs"
chown -R "$ACTUAL_USER:$ACTUAL_USER" "$CONFIG_DIR"

# Install systemd service
echo "[9/10] Installing systemd service..."
if [ -f "digitalsignage-client.service" ]; then
    # Update service file with actual user and venv path
    sed "s/INSTALL_USER/$ACTUAL_USER/g" digitalsignage-client.service | \
    sed "s|/usr/bin/python3|$VENV_DIR/bin/python3|g" > /tmp/digitalsignage-client.service
    cp /tmp/digitalsignage-client.service /etc/systemd/system/
    rm /tmp/digitalsignage-client.service
else
    echo "Warning: digitalsignage-client.service not found, creating basic service..."
    cat > /etc/systemd/system/digitalsignage-client.service <<EOF
[Unit]
Description=Digital Signage Client
After=network-online.target graphical.target
Wants=network-online.target

[Service]
Type=simple
User=$ACTUAL_USER
Group=$ACTUAL_USER
WorkingDirectory=$INSTALL_DIR
Environment="DISPLAY=:0"
Environment="XAUTHORITY=$USER_HOME/.Xauthority"
ExecStartPre=/bin/sleep 10
ExecStart=$VENV_DIR/bin/python3 $INSTALL_DIR/client.py
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=graphical.target
EOF
fi

systemctl daemon-reload

# Configure autostart
echo "[10/10] Configuring autostart..."
AUTOSTART_DIR="$USER_HOME/.config/autostart"
mkdir -p "$AUTOSTART_DIR"

# Hide mouse cursor
cat > "$AUTOSTART_DIR/unclutter.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Unclutter
Exec=unclutter -idle 0.1 -root
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
EOF

# Disable screen blanking
cat > "$AUTOSTART_DIR/disable-screensaver.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Disable Screensaver
Exec=sh -c 'xset s off && xset -dpms && xset s noblank'
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
EOF

chown -R "$ACTUAL_USER:$ACTUAL_USER" "$AUTOSTART_DIR"

echo ""
echo "========================================="
echo "Verifying Installation"
echo "========================================="
echo ""
echo "Checking PyQt5 accessibility from virtual environment..."
if "$VENV_DIR/bin/python3" -c "import PyQt5; from PyQt5.QtWidgets import QApplication; print('PyQt5 OK')" 2>/dev/null; then
    echo "✓ PyQt5 is accessible from virtual environment"
else
    echo "✗ WARNING: PyQt5 not accessible from virtual environment"
    echo "  This may cause client startup issues"
fi

echo ""
echo "========================================="
echo "Starting Digital Signage Client Service"
echo "========================================="
echo ""

# Enable service to start on boot
echo "Enabling service to start on boot..."
systemctl enable digitalsignage-client
echo "✓ Service enabled"

# Start the service
echo ""
echo "Starting service..."
systemctl start digitalsignage-client
echo "✓ Service started"

# Wait a moment and check status
echo ""
echo "Waiting for service to initialize..."
sleep 3

if systemctl is-active --quiet digitalsignage-client; then
    echo "✓ Service is running successfully!"

    # Deployment Mode Selection
    echo ""
    echo "=========================================="
    echo "Deployment Mode Selection"
    echo "=========================================="
    echo ""
    echo "Select deployment mode:"
    echo ""
    echo "  1) PRODUCTION MODE (Recommended)"
    echo "     - HDMI display connected"
    echo "     - Auto-login enabled"
    echo "     - X11 starts automatically on boot"
    echo "     - Screen blanking disabled"
    echo "     - Mouse cursor hidden"
    echo "     - Client starts automatically"
    echo "     → Requires REBOOT after installation"
    echo ""
    echo "  2) DEVELOPMENT MODE"
    echo "     - Headless (no display required)"
    echo "     - Uses Xvfb virtual display"
    echo "     - Service runs but no auto-login"
    echo "     - Good for testing"
    echo "     → No reboot required"
    echo ""
    read -p "Enter choice [1/2] (default: 1): " DEPLOYMENT_MODE
    DEPLOYMENT_MODE=${DEPLOYMENT_MODE:-1}

    if [ "$DEPLOYMENT_MODE" = "1" ]; then
        echo ""
        echo "=========================================="
        echo "Configuring PRODUCTION MODE"
        echo "=========================================="
        echo ""

        # Enable auto-login
        echo "[1/4] Enabling auto-login for user $ACTUAL_USER..."
        if command -v raspi-config &>/dev/null; then
            # B4 = Desktop Autologin (boot to desktop, automatically logged in)
            raspi-config nonint do_boot_behaviour B4 2>/dev/null
            if [ $? -eq 0 ]; then
                echo "  ✓ Auto-login enabled"
            else
                echo "  ⚠ raspi-config command completed with warnings (may still be successful)"
            fi
        else
            echo "  ⚠ raspi-config not found, skipping auto-login setup"
            echo "  Manual: Run 'sudo raspi-config' → System Options → Boot/Auto Login"
        fi

        # Create .xinitrc for X11 configuration
        echo ""
        echo "[2/4] Configuring X11 startup for user $ACTUAL_USER..."
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
exec tail -f /dev/null
EOF

        chown "$ACTUAL_USER:$ACTUAL_USER" "$USER_HOME/.xinitrc"
        chmod +x "$USER_HOME/.xinitrc"
        echo "  ✓ X11 configuration created (~/.xinitrc)"

        # Configure LightDM for auto-login (if installed)
        echo ""
        echo "[3/4] Configuring display manager..."
        if [ -f /etc/lightdm/lightdm.conf ]; then
            # Backup original config if not already backed up
            if [ ! -f /etc/lightdm/lightdm.conf.backup ]; then
                cp /etc/lightdm/lightdm.conf /etc/lightdm/lightdm.conf.backup
                echo "  ✓ LightDM config backed up"
            fi

            # Set auto-login (handle both commented and uncommented lines)
            sed -i "s/^#autologin-user=.*/autologin-user=$ACTUAL_USER/" /etc/lightdm/lightdm.conf
            sed -i "s/^autologin-user=.*/autologin-user=$ACTUAL_USER/" /etc/lightdm/lightdm.conf
            echo "  ✓ LightDM configured for auto-login"
        else
            echo "  ⚠ LightDM not found, using raspi-config settings only"
            echo "  This is normal for Raspberry Pi OS Lite"
        fi

        # Create autostart entry (already done earlier, but ensure it's there)
        echo ""
        echo "[4/4] Verifying autostart entries..."
        if [ -f "$AUTOSTART_DIR/unclutter.desktop" ] && [ -f "$AUTOSTART_DIR/disable-screensaver.desktop" ]; then
            echo "  ✓ Autostart entries already configured"
        else
            echo "  ⚠ Autostart entries missing (should have been created earlier)"
        fi

        echo ""
        echo "=========================================="
        echo "✓ PRODUCTION MODE configured successfully!"
        echo "=========================================="
        echo ""
        echo "Configuration applied:"
        echo "  1. Auto-login to desktop enabled"
        echo "  2. X11 will start automatically on boot"
        echo "  3. Screen blanking and screensaver disabled"
        echo "  4. Mouse cursor will auto-hide"
        echo "  5. Digital Signage client will start automatically"
        echo ""
        echo "IMPORTANT: A REBOOT IS REQUIRED"
        echo ""
        echo "After reboot:"
        echo "  - System will auto-login as $ACTUAL_USER"
        echo "  - X11 will start automatically"
        echo "  - Digital Signage client will start automatically"
        echo "  - Display will show the signage content"
        echo ""
        echo "Requirements:"
        echo "  - Physical display (HDMI) must be connected"
        echo "  - Server must be configured in $INSTALL_DIR/config.py"
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
            echo "Please reboot manually when ready:"
            echo "  sudo reboot"
            echo ""
        fi
    else
        echo ""
        echo "=========================================="
        echo "✓ DEVELOPMENT MODE selected"
        echo "=========================================="
        echo ""
        echo "The service is configured for headless operation:"
        echo "  - Uses Xvfb virtual display (via start-with-display.sh)"
        echo "  - No auto-login configured"
        echo "  - No reboot required"
        echo ""
        echo "To enable production mode later, run:"
        echo "  sudo $INSTALL_DIR/enable-autologin-x11.sh"
        echo ""
    fi

    # Show final configuration summary
    echo ""
    echo "========================================="
    echo "Installation Complete!"
    echo "========================================="
    echo ""
    echo "Installation Paths:"
    echo "  - Installation directory: $INSTALL_DIR"
    echo "  - Virtual environment: $VENV_DIR"
    echo "  - Config directory: $CONFIG_DIR"
    echo "  - Service file: /etc/systemd/system/digitalsignage-client.service"
    echo ""
    echo "Service Status: RUNNING"
    echo ""
    echo "Next Steps:"
    echo "  1. Edit configuration: sudo nano $INSTALL_DIR/config.py"
    echo "  2. Set server_host and server_port"
    echo "  3. Set registration_token"
    echo "  4. Restart service: sudo systemctl restart digitalsignage-client"
    if [ "$DEPLOYMENT_MODE" = "1" ]; then
        echo "  5. Reboot system: sudo reboot"
    fi
    echo ""
    echo "Useful Commands:"
    echo "  View status:    sudo systemctl status digitalsignage-client"
    echo "  View logs:      sudo journalctl -u digitalsignage-client -f"
    echo "  Run diagnostic: sudo $INSTALL_DIR/diagnose.sh"
    echo "  Test client:    sudo -u $ACTUAL_USER $VENV_DIR/bin/python3 $INSTALL_DIR/client.py --test"
    echo "  Restart:        sudo systemctl restart digitalsignage-client"
    echo "  Stop:           sudo systemctl stop digitalsignage-client"
    echo ""
    echo "Note: Python packages are installed in a virtual environment at $VENV_DIR"
    echo "This avoids conflicts with system Python packages (Python 3.11+ requirement)."
    echo "The venv uses --system-site-packages to access system packages:"
    echo "  - PyQt5 (python3-pyqt5, python3-pyqt5.qtsvg, python3-pyqt5.qtmultimedia)"
    echo "  - psutil (python3-psutil)"
    echo ""
else
    echo "✗ WARNING: Service failed to start"
    echo ""
    echo "========================================="
    echo "Installation Complete (with warnings)"
    echo "========================================="
    echo ""
    echo "The installation completed but the service failed to start."
    echo ""
    echo "Showing last 50 lines of logs:"
    echo "------------------------------------------------------------------------"
    journalctl -u digitalsignage-client -n 50 --no-pager || echo "No logs available"
    echo "------------------------------------------------------------------------"
    echo ""
    echo ""
    echo "========================================="
    echo "Running Diagnostic Script"
    echo "========================================="
    echo ""
    if [ -f "$INSTALL_DIR/diagnose.sh" ]; then
        bash "$INSTALL_DIR/diagnose.sh"
    else
        echo "Diagnostic script not found"
        echo ""
        echo "Common issues:"
        echo "  - Missing dependencies (check PyQt5 import above)"
        echo "  - Configuration errors in config.py"
        echo "  - Display server (X11) not available"
        echo "  - Server host not reachable (expected on fresh install)"
        echo ""
        echo "Troubleshooting steps:"
        echo "  1. Run test mode: sudo -u $ACTUAL_USER $VENV_DIR/bin/python3 $INSTALL_DIR/client.py --test"
        echo "  2. Check environment: echo \$DISPLAY (should be :0)"
        echo "  3. Verify X11 running: ps aux | grep X"
        echo "  4. Check PyQt5: $VENV_DIR/bin/python3 -c 'import PyQt5'"
    fi
    echo ""
    echo ""
    echo "After fixing issues, restart the service:"
    echo "  sudo systemctl restart digitalsignage-client"
    echo ""
    echo "Configuration:"
    echo "  - Installation directory: $INSTALL_DIR"
    echo "  - Virtual environment: $VENV_DIR"
    echo "  - Config directory: $CONFIG_DIR"
    echo ""
    exit 1
fi
