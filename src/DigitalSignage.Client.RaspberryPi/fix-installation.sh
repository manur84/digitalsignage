#!/bin/bash
#
# Digital Signage Client - Installation Fix Script
# Run this if install.sh succeeds but the service still fails to start
#
# Usage: sudo ./fix-installation.sh
#

set -e

echo "=========================================="
echo "Digital Signage Client - Installation Fix"
echo "=========================================="
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: Please run as root (use sudo)"
    exit 1
fi

# Detect the user
ACTUAL_USER="${SUDO_USER:-$USER}"
if [ -z "$ACTUAL_USER" ] || [ "$ACTUAL_USER" = "root" ]; then
    echo "ERROR: Could not detect non-root user"
    exit 1
fi

USER_HOME=$(eval echo "~$ACTUAL_USER")
INSTALL_DIR="/opt/digitalsignage-client"
VENV_DIR="$INSTALL_DIR/venv"

echo "User: $ACTUAL_USER"
echo "Home: $USER_HOME"
echo "Install Dir: $INSTALL_DIR"
echo ""

# Check 1: Installation directory exists
echo "[1/15] Checking installation directory..."
if [ -d "$INSTALL_DIR" ]; then
    echo "  ✓ Installation directory exists"
else
    echo "  ✗ Installation directory not found"
    echo "  Run: sudo ./install.sh"
    exit 1
fi

# Check 2: Virtual environment exists
echo "[2/15] Checking virtual environment..."
if [ -d "$VENV_DIR" ]; then
    echo "  ✓ Virtual environment exists"
else
    echo "  ✗ Virtual environment not found"
    exit 1
fi

# Check 3: Python executable
echo "[3/15] Checking Python executable..."
if [ -f "$VENV_DIR/bin/python3" ]; then
    PYTHON_VERSION=$($VENV_DIR/bin/python3 --version)
    echo "  ✓ Python executable found: $PYTHON_VERSION"
else
    echo "  ✗ Python executable not found"
    exit 1
fi

# Check 4: Required files
echo "[4/15] Checking required files..."
MISSING_FILES=()

for file in client.py config.py device_manager.py display_renderer.py cache_manager.py watchdog_monitor.py start-with-display.sh; do
    if [ ! -f "$INSTALL_DIR/$file" ]; then
        MISSING_FILES+=("$file")
    fi
done

if [ ${#MISSING_FILES[@]} -gt 0 ]; then
    echo "  ✗ Missing files: ${MISSING_FILES[*]}"
    exit 1
else
    echo "  ✓ All required files present"
fi

# Check 5: File permissions
echo "[5/15] Checking file permissions..."
PERMISSION_ISSUES=()

# Check ownership
OWNER=$(stat -c '%U' "$INSTALL_DIR")
if [ "$OWNER" != "$ACTUAL_USER" ]; then
    echo "  ⚠ Wrong ownership: $OWNER (should be $ACTUAL_USER)"
    echo "  Fixing ownership..."
    chown -R "$ACTUAL_USER:$ACTUAL_USER" "$INSTALL_DIR"
    echo "  ✓ Ownership fixed"
else
    echo "  ✓ Ownership correct"
fi

# Check executability
if [ ! -x "$INSTALL_DIR/start-with-display.sh" ]; then
    echo "  ⚠ start-with-display.sh not executable"
    echo "  Fixing permissions..."
    chmod +x "$INSTALL_DIR/start-with-display.sh"
    echo "  ✓ Permissions fixed"
else
    echo "  ✓ start-with-display.sh is executable"
fi

# Check 6: Line endings
echo "[6/15] Checking line endings..."
if file "$INSTALL_DIR/start-with-display.sh" | grep -q "CRLF"; then
    echo "  ⚠ Windows line endings detected"
    echo "  Converting to Unix format..."
    sed -i 's/\r$//' "$INSTALL_DIR/start-with-display.sh"
    echo "  ✓ Line endings fixed"
else
    echo "  ✓ Line endings are correct (Unix format)"
fi

# Check 7: System dependencies
echo "[7/15] Checking system dependencies..."
MISSING_DEPS=()

for cmd in python3 Xvfb xset xdpyinfo; do
    if ! command -v $cmd &>/dev/null; then
        MISSING_DEPS+=("$cmd")
    fi
done

if [ ${#MISSING_DEPS[@]} -gt 0 ]; then
    echo "  ✗ Missing system commands: ${MISSING_DEPS[*]}"
    echo "  Install with: sudo apt-get install python3 xvfb x11-xserver-utils x11-utils"
    exit 1
else
    echo "  ✓ All system dependencies installed"
fi

# Check 8: PyQt5 system package
echo "[8/15] Checking PyQt5 system package..."
if python3 -c "import PyQt5" 2>/dev/null; then
    PYQT5_VERSION=$(python3 -c "from PyQt5.QtCore import PYQT_VERSION_STR; print(PYQT_VERSION_STR)" 2>/dev/null)
    echo "  ✓ PyQt5 system package: $PYQT5_VERSION"
else
    echo "  ✗ PyQt5 not installed"
    echo "  Install with: sudo apt-get install python3-pyqt5 python3-pyqt5.qtsvg"
    exit 1
fi

# Check 9: PyQt5 in venv
echo "[9/15] Checking PyQt5 accessibility from venv..."
if $VENV_DIR/bin/python3 -c "import PyQt5" 2>/dev/null; then
    echo "  ✓ PyQt5 accessible from venv"
else
    echo "  ✗ PyQt5 not accessible from venv"
    echo "  The venv may not have --system-site-packages enabled"
    echo "  Recreating venv with --system-site-packages..."

    rm -rf "$VENV_DIR"
    python3 -m venv --system-site-packages "$VENV_DIR"

    echo "  Reinstalling pip packages..."
    $VENV_DIR/bin/pip install --upgrade pip
    if [ -f "$INSTALL_DIR/../requirements.txt" ]; then
        $VENV_DIR/bin/pip install -r "$INSTALL_DIR/../requirements.txt"
    else
        $VENV_DIR/bin/pip install \
            python-socketio[client]==5.10.0 \
            aiohttp==3.9.1 \
            requests==2.31.0 \
            psutil==5.9.6 \
            pillow==10.1.0 \
            qrcode==7.4.2
    fi

    chown -R "$ACTUAL_USER:$ACTUAL_USER" "$VENV_DIR"
    echo "  ✓ Virtual environment recreated"
fi

# Check 10: Python dependencies
echo "[10/15] Checking Python dependencies..."
MISSING_PYTHON_DEPS=()

for module in socketio aiohttp requests psutil PIL qrcode; do
    if ! $VENV_DIR/bin/python3 -c "import $module" 2>/dev/null; then
        MISSING_PYTHON_DEPS+=("$module")
    fi
done

if [ ${#MISSING_PYTHON_DEPS[@]} -gt 0 ]; then
    echo "  ✗ Missing Python modules: ${MISSING_PYTHON_DEPS[*]}"
    echo "  Reinstalling dependencies..."
    $VENV_DIR/bin/pip install \
        python-socketio[client]==5.10.0 \
        aiohttp==3.9.1 \
        requests==2.31.0 \
        psutil==5.9.6 \
        pillow==10.1.0 \
        qrcode==7.4.2
    echo "  ✓ Dependencies installed"
else
    echo "  ✓ All Python dependencies installed"
fi

# Check 11: config.py syntax
echo "[11/15] Checking config.py syntax..."
if $VENV_DIR/bin/python3 -m py_compile "$INSTALL_DIR/config.py" 2>/dev/null; then
    echo "  ✓ config.py has valid syntax"
else
    echo "  ✗ config.py has syntax errors"
    echo "  Check file: $INSTALL_DIR/config.py"
    exit 1
fi

# Check 12: Service file
echo "[12/15] Checking systemd service file..."
if [ -f "/etc/systemd/system/digitalsignage-client.service" ]; then
    echo "  ✓ Service file exists"

    # Check if it has correct user
    if grep -q "User=$ACTUAL_USER" "/etc/systemd/system/digitalsignage-client.service"; then
        echo "  ✓ Service user is correct"
    else
        echo "  ⚠ Service user may be incorrect"
        echo "  Updating service file..."

        sed -i "s/^User=.*/User=$ACTUAL_USER/" /etc/systemd/system/digitalsignage-client.service
        sed -i "s/^Group=.*/Group=$ACTUAL_USER/" /etc/systemd/system/digitalsignage-client.service

        systemctl daemon-reload
        echo "  ✓ Service file updated"
    fi
else
    echo "  ✗ Service file not found"
    echo "  Creating service file..."

    cat > /etc/systemd/system/digitalsignage-client.service <<EOF
[Unit]
Description=Digital Signage Client
After=network-online.target graphical.target
Wants=network-online.target

[Service]
Type=notify
NotifyAccess=main
User=$ACTUAL_USER
Group=$ACTUAL_USER
WorkingDirectory=$INSTALL_DIR
Environment="PYTHONUNBUFFERED=1"
Environment="QT_QPA_PLATFORM=xcb"
Environment="QT_LOGGING_RULES=*.debug=false"
ExecStartPre=/bin/sleep 10
ExecStart=$INSTALL_DIR/start-with-display.sh
ExecStopPost=/bin/bash -c 'pkill -f "Xvfb :99" || true'
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal
SyslogIdentifier=digitalsignage-client

# Watchdog
WatchdogSec=60
TimeoutStartSec=30

# Security
NoNewPrivileges=true
PrivateTmp=true

# Restart policy
StartLimitInterval=200
StartLimitBurst=5

[Install]
WantedBy=graphical.target
EOF

    systemctl daemon-reload
    echo "  ✓ Service file created"
fi

# Check 13: Display environment
echo "[13/15] Checking display environment..."
if [ -n "$DISPLAY" ]; then
    echo "  Current DISPLAY: $DISPLAY"
else
    echo "  DISPLAY not set (will use Xvfb)"
fi

if xset q &>/dev/null; then
    echo "  ✓ X11 display accessible"
else
    echo "  No X11 display (will use Xvfb - this is normal)"
fi

# Check 14: Test start-with-display.sh manually
echo "[14/15] Testing start-with-display.sh..."
echo "  Running manual test as user $ACTUAL_USER..."
echo ""

# Run as the actual user with timeout
if timeout 10 sudo -u "$ACTUAL_USER" "$INSTALL_DIR/start-with-display.sh" --test; then
    echo ""
    echo "  ✓ Manual test successful"
else
    TEST_EXIT_CODE=$?
    echo ""
    if [ $TEST_EXIT_CODE -eq 124 ]; then
        echo "  ⚠ Test timed out after 10 seconds"
        echo "  This may indicate a hanging process"
    else
        echo "  ✗ Manual test failed with exit code: $TEST_EXIT_CODE"
        echo "  Check the output above for errors"
    fi
    echo ""
    echo "  Check startup log:"
    echo "    cat /var/log/digitalsignage-client-startup.log"
    echo "  OR"
    echo "    cat /tmp/digitalsignage-client-startup.log"
    exit 1
fi

# Check 15: Service status
echo "[15/15] Checking service status..."
if systemctl is-enabled --quiet digitalsignage-client 2>/dev/null; then
    echo "  ✓ Service is enabled"
else
    echo "  ⚠ Service not enabled, enabling..."
    systemctl enable digitalsignage-client
    echo "  ✓ Service enabled"
fi

if systemctl is-active --quiet digitalsignage-client 2>/dev/null; then
    echo "  ✓ Service is running"
else
    echo "  Service not running"
fi

echo ""
echo "=========================================="
echo "Fix Script Complete"
echo "=========================================="
echo ""
echo "All checks passed! The installation appears to be correct."
echo ""
echo "Restarting service..."
systemctl stop digitalsignage-client 2>/dev/null || true
sleep 2
systemctl start digitalsignage-client

echo ""
echo "Waiting for service to initialize..."
sleep 5

if systemctl is-active --quiet digitalsignage-client; then
    echo "✓ Service started successfully!"
    echo ""
    echo "View logs with:"
    echo "  sudo journalctl -u digitalsignage-client -f"
    echo ""
    echo "View startup log with:"
    echo "  sudo cat /var/log/digitalsignage-client-startup.log"
    echo ""
else
    echo "✗ Service failed to start"
    echo ""
    echo "View detailed logs:"
    echo "  sudo journalctl -u digitalsignage-client -n 100 --no-pager"
    echo ""
    echo "View startup log:"
    echo "  sudo cat /var/log/digitalsignage-client-startup.log"
    echo "  OR"
    echo "  sudo cat /tmp/digitalsignage-client-startup.log"
    echo ""
    exit 1
fi
