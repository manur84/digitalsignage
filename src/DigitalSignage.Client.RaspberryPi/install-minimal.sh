#!/bin/bash
#
# Digital Signage Client - Minimal Installation Script
# Simplified installation for maximum reliability
#
# Usage: sudo ./install-minimal.sh
#

set -e

echo ""
echo "========================================================================="
echo "  DIGITAL SIGNAGE CLIENT - MINIMAL INSTALLATION"
echo "========================================================================="
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root"
    echo "Usage: sudo ./install-minimal.sh"
    exit 1
fi

# Detect the actual user (not root)
if [ -n "$SUDO_USER" ]; then
    ACTUAL_USER="$SUDO_USER"
else
    ACTUAL_USER="pro"
fi

echo "Installation will run as user: $ACTUAL_USER"
echo ""

read -p "Continue? (y/n) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Installation cancelled"
    exit 1
fi

# Installation directory
INSTALL_DIR="/opt/digitalsignage-client"

echo ""
echo "[1/9] Updating package lists..."
apt-get update -qq

echo ""
echo "[2/9] Installing system dependencies..."
apt-get install -y \
    python3 \
    python3-venv \
    python3-pip \
    python3-pyqt5 \
    python3-pyqt5.qtwebengine \
    sqlite3 \
    x11-xserver-utils

echo ""
echo "[3/9] Creating installation directory..."
mkdir -p "$INSTALL_DIR"

echo ""
echo "[4/9] Copying client files..."
# Copy all Python files
cp -v *.py "$INSTALL_DIR/" 2>/dev/null || true

# Copy requirements.txt if exists
if [ -f "requirements.txt" ]; then
    cp -v requirements.txt "$INSTALL_DIR/"
fi

echo ""
echo "[5/9] Creating Python virtual environment..."
cd "$INSTALL_DIR"
python3 -m venv --system-site-packages venv

echo ""
echo "[6/9] Installing Python dependencies..."
venv/bin/pip install --upgrade pip
venv/bin/pip install \
    python-socketio[client] \
    aiohttp \
    psutil

echo ""
echo "[7/9] Creating configuration directory..."
mkdir -p /etc/digitalsignage

# Create default config if it doesn't exist
if [ ! -f "/etc/digitalsignage/config.json" ]; then
    echo "Creating default configuration..."
    cat > /etc/digitalsignage/config.json <<EOF
{
  "client_id": "$(uuidgen 2>/dev/null || cat /proc/sys/kernel/random/uuid)",
  "server_host": "192.168.1.100",
  "server_port": 8080,
  "registration_token": "",
  "use_ssl": false,
  "verify_ssl": true,
  "fullscreen": true,
  "log_level": "INFO",
  "cache_dir": "/home/$ACTUAL_USER/.digitalsignage/cache",
  "data_dir": "/home/$ACTUAL_USER/.digitalsignage/data",
  "auto_discover": false,
  "discovery_timeout": 5.0,
  "remote_logging_enabled": true,
  "remote_logging_level": "INFO",
  "remote_logging_batch_size": 50,
  "remote_logging_batch_interval": 5.0
}
EOF
    echo ""
    echo "IMPORTANT: Edit /etc/digitalsignage/config.json to set your server address!"
    echo ""
fi

echo ""
echo "[8/9] Testing Python environment..."
echo "Testing imports..."

TEST_OUTPUT=$(venv/bin/python3 -c "
import sys
print('Python version:', sys.version)

try:
    import PyQt5
    from PyQt5.QtCore import PYQT_VERSION_STR
    print('PyQt5 version:', PYQT_VERSION_STR)
except ImportError as e:
    print('PyQt5 import FAILED:', e)
    sys.exit(1)

try:
    import socketio
    print('socketio: OK')
except ImportError as e:
    print('socketio import FAILED:', e)
    sys.exit(1)

print('All imports successful!')
" 2>&1) || {
    echo "ERROR: Python import test failed"
    echo "$TEST_OUTPUT"
    echo ""
    echo "Installation incomplete - please fix errors above"
    exit 1
}

echo "$TEST_OUTPUT"

echo ""
echo "[9/9] Creating systemd service..."

# Create service file
cat > /etc/systemd/system/digitalsignage-client.service <<EOF
[Unit]
Description=Digital Signage Client
After=network.target
Wants=network.target

[Service]
Type=simple
User=$ACTUAL_USER
WorkingDirectory=$INSTALL_DIR
Environment="DISPLAY=:0"
Environment="XAUTHORITY=/home/$ACTUAL_USER/.Xauthority"
Environment="PYTHONUNBUFFERED=1"
ExecStart=$INSTALL_DIR/venv/bin/python3 $INSTALL_DIR/client.py
Restart=on-failure
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

# Fix permissions
echo ""
echo "Fixing permissions..."
chown -R "$ACTUAL_USER:$ACTUAL_USER" "$INSTALL_DIR"
chmod +x "$INSTALL_DIR"/*.py 2>/dev/null || true

# Create cache directories
mkdir -p "/home/$ACTUAL_USER/.digitalsignage/cache"
mkdir -p "/home/$ACTUAL_USER/.digitalsignage/data"
chown -R "$ACTUAL_USER:$ACTUAL_USER" "/home/$ACTUAL_USER/.digitalsignage"

# Reload systemd
systemctl daemon-reload

echo ""
echo "========================================================================="
echo "  INSTALLATION COMPLETE"
echo "========================================================================="
echo ""
echo "Installation directory: $INSTALL_DIR"
echo "Configuration file:     /etc/digitalsignage/config.json"
echo "Service user:           $ACTUAL_USER"
echo ""
echo "Next steps:"
echo ""
echo "  1. Edit configuration:"
echo "     sudo nano /etc/digitalsignage/config.json"
echo ""
echo "  2. Set your server address in the config file"
echo ""
echo "  3. Enable and start the service:"
echo "     sudo systemctl enable digitalsignage-client"
echo "     sudo systemctl start digitalsignage-client"
echo ""
echo "  4. Check service status:"
echo "     sudo systemctl status digitalsignage-client"
echo ""
echo "  5. View logs:"
echo "     sudo journalctl -u digitalsignage-client -f"
echo ""
echo "  6. Run diagnostics if needed:"
echo "     cd $INSTALL_DIR"
echo "     venv/bin/python3 client.py --test"
echo ""
echo "========================================================================="
echo ""
