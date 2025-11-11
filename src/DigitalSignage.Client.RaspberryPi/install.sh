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

# Update package lists
echo "[1/9] Updating package lists..."
apt-get update

# Install system dependencies
echo "[2/9] Installing system dependencies..."
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
    libqt5multimedia5-plugins

echo ""
echo "[3/9] Verifying PyQt5 installation..."
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

# Set ownership
chown -R "$ACTUAL_USER:$ACTUAL_USER" "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/client.py"

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
systemctl enable digitalsignage-client.service

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
echo "Installation Complete!"
echo "========================================="
echo ""
echo "Configuration:"
echo "  - Installation directory: $INSTALL_DIR"
echo "  - Virtual environment: $VENV_DIR"
echo "  - Config directory: $CONFIG_DIR"
echo "  - Service file: /etc/systemd/system/digitalsignage-client.service"
echo ""
echo "Next steps:"
echo "  1. Edit configuration: sudo nano $INSTALL_DIR/config.py"
echo "  2. Set server host and port"
echo "  3. Start service: sudo systemctl start digitalsignage-client"
echo "  4. Check status: sudo systemctl status digitalsignage-client"
echo "  5. View logs: sudo journalctl -u digitalsignage-client -f"
echo ""
echo "Note: Python packages are installed in a virtual environment at $VENV_DIR"
echo "This avoids conflicts with system Python packages (Python 3.11+ requirement)."
echo "The venv uses --system-site-packages to access system packages:"
echo "  - PyQt5 (python3-pyqt5, python3-pyqt5.qtsvg, python3-pyqt5.qtmultimedia)"
echo "  - psutil (python3-psutil)"
echo ""
echo "The service will automatically start on boot."
echo "To disable autostart: sudo systemctl disable digitalsignage-client"
echo ""
