#!/bin/bash
#
# Digital Signage Client - Quick Fix Script
# Run this on the Raspberry Pi to fix service startup issues
#
# Usage: sudo ./quick-fix.sh
#

set -e

echo ""
echo "========================================================================="
echo "  DIGITAL SIGNAGE CLIENT - QUICK FIX"
echo "========================================================================="
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: This script must be run as root"
    echo "Usage: sudo ./quick-fix.sh"
    exit 1
fi

# Detect the actual user (not root)
if [ -n "$SUDO_USER" ]; then
    ACTUAL_USER="$SUDO_USER"
else
    ACTUAL_USER="pro"
fi

echo "Target user: $ACTUAL_USER"
echo ""

# Stop service if running
echo "[1/7] Stopping service..."
systemctl stop digitalsignage-client 2>/dev/null || true
echo "    Service stopped"
echo ""

# Check if installation directory exists
if [ ! -d "/opt/digitalsignage-client" ]; then
    echo "ERROR: /opt/digitalsignage-client does not exist"
    echo "Please run the installation script first"
    exit 1
fi

# Fix permissions
echo "[2/7] Fixing permissions..."
chown -R "$ACTUAL_USER:$ACTUAL_USER" /opt/digitalsignage-client 2>/dev/null || true
chmod +x /opt/digitalsignage-client/*.py 2>/dev/null || true
chmod +x /opt/digitalsignage-client/*.sh 2>/dev/null || true
echo "    Permissions fixed"
echo ""

# Check Python virtual environment
echo "[3/7] Checking Python virtual environment..."
if [ ! -f "/opt/digitalsignage-client/venv/bin/python3" ]; then
    echo "    Virtual environment missing - creating..."

    # Install python3-venv if needed
    if ! dpkg -l | grep -q python3-venv; then
        echo "    Installing python3-venv..."
        apt-get update -qq
        apt-get install -y python3-venv
    fi

    # Create venv with system packages
    cd /opt/digitalsignage-client
    python3 -m venv --system-site-packages venv
    chown -R "$ACTUAL_USER:$ACTUAL_USER" venv
    echo "    Virtual environment created"
else
    echo "    Virtual environment exists"
fi
echo ""

# Install/Check system packages
echo "[4/7] Checking system packages..."
PACKAGES_TO_INSTALL=""

if ! dpkg -l | grep -q python3-pyqt5; then
    PACKAGES_TO_INSTALL="$PACKAGES_TO_INSTALL python3-pyqt5"
fi

if ! dpkg -l | grep -q python3-pyqt5.qtwebengine; then
    PACKAGES_TO_INSTALL="$PACKAGES_TO_INSTALL python3-pyqt5.qtwebengine"
fi

if [ -n "$PACKAGES_TO_INSTALL" ]; then
    echo "    Installing required packages:$PACKAGES_TO_INSTALL"
    apt-get update -qq
    apt-get install -y $PACKAGES_TO_INSTALL
    echo "    Packages installed"
else
    echo "    All required packages installed"
fi
echo ""

# Test Python imports
echo "[5/7] Testing Python imports..."
cd /opt/digitalsignage-client

TEST_RESULT=$(/opt/digitalsignage-client/venv/bin/python3 -c "
import sys
try:
    import PyQt5
    print('PyQt5: OK')
except ImportError as e:
    print(f'PyQt5: FAILED - {e}')
    sys.exit(1)

try:
    import socketio
    print('socketio: OK')
except ImportError as e:
    print(f'socketio: FAILED - {e}')
    sys.exit(1)
" 2>&1) || {
    echo "    Import test FAILED:"
    echo "$TEST_RESULT"
    echo ""
    echo "    Installing missing Python packages..."
    /opt/digitalsignage-client/venv/bin/pip install python-socketio[client] aiohttp psutil
    echo "    Packages installed - retrying import test..."
    /opt/digitalsignage-client/venv/bin/python3 -c "import PyQt5; import socketio; print('All imports OK')"
}

echo "    Python imports successful"
echo ""

# Create minimal service file
echo "[6/7] Creating service file..."
cat > /etc/systemd/system/digitalsignage-client.service <<EOF
[Unit]
Description=Digital Signage Client
After=network.target
Wants=network.target

[Service]
Type=simple
User=$ACTUAL_USER
WorkingDirectory=/opt/digitalsignage-client
Environment="DISPLAY=:0"
Environment="XAUTHORITY=/home/$ACTUAL_USER/.Xauthority"
Environment="PYTHONUNBUFFERED=1"
ExecStart=/opt/digitalsignage-client/venv/bin/python3 /opt/digitalsignage-client/client.py
Restart=on-failure
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

echo "    Service file created"
echo ""

# Reload systemd
echo "[7/7] Starting service..."
systemctl daemon-reload
systemctl enable digitalsignage-client
systemctl start digitalsignage-client

echo "    Service started"
echo ""

# Wait for service to settle
sleep 3

# Show status
echo "========================================================================="
echo "  SERVICE STATUS"
echo "========================================================================="
systemctl status digitalsignage-client --no-pager --lines=0 || true
echo ""

echo "========================================================================="
echo "  RECENT LOGS (last 30 lines)"
echo "========================================================================="
journalctl -u digitalsignage-client -n 30 --no-pager
echo ""

echo "========================================================================="
echo "  QUICK FIX COMPLETE"
echo "========================================================================="
echo ""
echo "Commands:"
echo "  View live logs:    sudo journalctl -u digitalsignage-client -f"
echo "  Check status:      sudo systemctl status digitalsignage-client"
echo "  Restart service:   sudo systemctl restart digitalsignage-client"
echo "  Stop service:      sudo systemctl stop digitalsignage-client"
echo ""
echo "If the service is still failing, check the logs above for error messages."
echo ""
