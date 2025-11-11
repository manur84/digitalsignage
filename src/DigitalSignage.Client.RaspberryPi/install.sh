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

# Get the actual user (not root)
ACTUAL_USER="${SUDO_USER:-pi}"
USER_HOME="/home/$ACTUAL_USER"

echo "Installing for user: $ACTUAL_USER"
echo ""

# Update package lists
echo "[1/8] Updating package lists..."
apt-get update

# Install system dependencies
echo "[2/8] Installing system dependencies..."
apt-get install -y \
    python3 \
    python3-pip \
    python3-pyqt5 \
    python3-psutil \
    sqlite3 \
    libsqlite3-dev \
    x11-xserver-utils \
    unclutter \
    xdotool

# Install Python dependencies
echo "[3/8] Installing Python dependencies..."
pip3 install --upgrade pip
pip3 install \
    python-socketio \
    qrcode[pil] \
    Pillow \
    requests

# Create installation directory
echo "[4/8] Creating installation directory..."
INSTALL_DIR="/opt/digitalsignage-client"
mkdir -p "$INSTALL_DIR"

# Copy client files
echo "[5/8] Copying client files..."
cp client.py "$INSTALL_DIR/"
cp config.py "$INSTALL_DIR/"
cp device_manager.py "$INSTALL_DIR/"
cp display_renderer.py "$INSTALL_DIR/"
cp cache_manager.py "$INSTALL_DIR/"

# Set ownership
chown -R "$ACTUAL_USER:$ACTUAL_USER" "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/client.py"

# Create config directory
echo "[6/8] Creating config directory..."
CONFIG_DIR="$USER_HOME/.digitalsignage"
mkdir -p "$CONFIG_DIR/cache"
mkdir -p "$CONFIG_DIR/logs"
chown -R "$ACTUAL_USER:$ACTUAL_USER" "$CONFIG_DIR"

# Install systemd service
echo "[7/8] Installing systemd service..."
if [ -f "digitalsignage-client.service" ]; then
    # Update service file with actual user
    sed "s/User=pi/User=$ACTUAL_USER/g" digitalsignage-client.service | \
    sed "s|/home/pi|$USER_HOME|g" > /tmp/digitalsignage-client.service
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
ExecStart=/usr/bin/python3 $INSTALL_DIR/client.py
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
echo "[8/8] Configuring autostart..."
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
echo "Installation Complete!"
echo "========================================="
echo ""
echo "Configuration:"
echo "  - Installation directory: $INSTALL_DIR"
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
echo "The service will automatically start on boot."
echo "To disable autostart: sudo systemctl disable digitalsignage-client"
echo ""
