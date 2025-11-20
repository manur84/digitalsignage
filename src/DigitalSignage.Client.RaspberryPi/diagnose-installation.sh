#!/bin/bash
#
# Digital Signage Client - Installation Diagnostics
# This script checks for common installation issues and provides solutions
#

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo "=========================================="
echo "  Digital Signage Client - Diagnostics"
echo "=========================================="
echo ""

# Check if running as root
if [ "$EUID" -eq 0 ]; then
    echo -e "${YELLOW}Warning: Running as root. Some tests may behave differently.${NC}"
    ACTUAL_USER="${SUDO_USER:-pi}"
else
    ACTUAL_USER="$USER"
fi

echo "User: $ACTUAL_USER"
echo "Home: $HOME"
echo ""

# Function to check status
check_ok() {
    echo -e "${GREEN}✓${NC} $1"
}

check_fail() {
    echo -e "${RED}✗${NC} $1"
}

check_warn() {
    echo -e "${YELLOW}⚠${NC} $1"
}

check_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

# 1. Check installation directory
echo "1. Installation Directory"
echo "-------------------------"
INSTALL_DIR="/opt/digitalsignage-client"
if [ -d "$INSTALL_DIR" ]; then
    check_ok "Installation directory exists: $INSTALL_DIR"

    # Check important files
    REQUIRED_FILES=("client.py" "display_renderer.py" "config.py" "start-with-display.sh")
    for file in "${REQUIRED_FILES[@]}"; do
        if [ -f "$INSTALL_DIR/$file" ]; then
            check_ok "  $file exists"
        else
            check_fail "  $file missing!"
        fi
    done
else
    check_fail "Installation directory not found: $INSTALL_DIR"
fi
echo ""

# 2. Check Python environment
echo "2. Python Environment"
echo "--------------------"
if [ -d "$INSTALL_DIR/venv" ]; then
    check_ok "Virtual environment exists"

    if [ -f "$INSTALL_DIR/venv/bin/python3" ]; then
        check_ok "Python executable found"
        PYTHON_VERSION=$("$INSTALL_DIR/venv/bin/python3" --version 2>&1)
        check_info "  Version: $PYTHON_VERSION"

        # Test PyQt5
        if "$INSTALL_DIR/venv/bin/python3" -c "import PyQt5" 2>/dev/null; then
            check_ok "  PyQt5 module available"
        else
            check_fail "  PyQt5 module not available"
        fi

        # Test socketio
        if "$INSTALL_DIR/venv/bin/python3" -c "import socketio" 2>/dev/null; then
            check_ok "  python-socketio module available"
        else
            check_fail "  python-socketio module not available"
        fi
    else
        check_fail "Python executable not found in venv"
    fi
else
    check_fail "Virtual environment not found"
fi
echo ""

# 3. Check X11/Display
echo "3. Display Configuration"
echo "-----------------------"

# Check if X11 is running
if pgrep -x "Xorg" > /dev/null; then
    check_ok "X11 server (Xorg) is running"

    # Find which display it's on
    XORG_DISPLAY=$(ps aux | grep '[X]org' | awk '{for(i=1;i<=NF;i++) if($i ~ /^:[0-9]+$/) print $i}' | head -1)
    if [ -n "$XORG_DISPLAY" ]; then
        check_info "  Running on display: $XORG_DISPLAY"
    fi
else
    check_warn "X11 server (Xorg) not running"
    check_info "  This is normal for headless systems"
fi

# Check DISPLAY variable
if [ -n "$DISPLAY" ]; then
    check_info "DISPLAY variable set to: $DISPLAY"

    # Test if we can access it
    if DISPLAY="$DISPLAY" xset q &>/dev/null 2>&1; then
        check_ok "  Can access display $DISPLAY"
    else
        check_warn "  Cannot access display $DISPLAY"
    fi
else
    check_warn "DISPLAY variable not set"
fi

# Check .Xauthority
XAUTH_FILE="$HOME/.Xauthority"
if [ "$EUID" -eq 0 ] && [ -n "$SUDO_USER" ]; then
    XAUTH_FILE="/home/$SUDO_USER/.Xauthority"
fi

if [ -f "$XAUTH_FILE" ]; then
    check_ok ".Xauthority file exists: $XAUTH_FILE"

    # Check permissions
    XAUTH_PERMS=$(stat -c '%a' "$XAUTH_FILE" 2>/dev/null)
    if [ "$XAUTH_PERMS" = "600" ]; then
        check_ok "  Permissions correct: 600"
    else
        check_warn "  Permissions: $XAUTH_PERMS (should be 600)"
    fi

    # Check ownership
    XAUTH_OWNER=$(stat -c '%U' "$XAUTH_FILE" 2>/dev/null)
    if [ "$XAUTH_OWNER" = "$ACTUAL_USER" ]; then
        check_ok "  Owner correct: $ACTUAL_USER"
    else
        check_warn "  Owner: $XAUTH_OWNER (should be $ACTUAL_USER)"
    fi
else
    check_warn ".Xauthority file not found"
fi

# Check xhost permissions
if command -v xhost &>/dev/null; then
    XHOST_OUTPUT=$(DISPLAY=:0 xhost 2>&1)
    if echo "$XHOST_OUTPUT" | grep -q "access control disabled"; then
        check_ok "X11 access control disabled (open access)"
    elif echo "$XHOST_OUTPUT" | grep -q "LOCAL:"; then
        check_ok "X11 local connections allowed"
    elif echo "$XHOST_OUTPUT" | grep -q "unable to open display"; then
        check_warn "Cannot check xhost (X11 not accessible)"
    else
        check_warn "X11 access may be restricted"
    fi
else
    check_warn "xhost command not found"
fi

# Check Xvfb
if command -v Xvfb &>/dev/null; then
    check_ok "Xvfb installed (fallback display available)"
else
    check_warn "Xvfb not installed (no fallback for headless mode)"
    check_info "  Install with: sudo apt-get install xvfb"
fi
echo ""

# 4. Check systemd service
echo "4. Systemd Service"
echo "-----------------"
SERVICE_NAME="digitalsignage-client"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

if [ -f "$SERVICE_FILE" ]; then
    check_ok "Service file exists: $SERVICE_FILE"

    # Check if enabled
    if systemctl is-enabled "$SERVICE_NAME" &>/dev/null 2>&1; then
        check_ok "Service is enabled (auto-start on boot)"
    else
        check_warn "Service not enabled"
        check_info "  Enable with: sudo systemctl enable $SERVICE_NAME"
    fi

    # Check if active
    if systemctl is-active "$SERVICE_NAME" &>/dev/null 2>&1; then
        check_ok "Service is running"
    else
        check_warn "Service not running"
        check_info "  Start with: sudo systemctl start $SERVICE_NAME"
    fi
else
    check_fail "Service file not found"
    check_info "  Run install.sh to create service"
fi
echo ""

# 5. Check configuration
echo "5. Configuration"
echo "---------------"
CONFIG_FILE="$INSTALL_DIR/config.json"
if [ -f "$CONFIG_FILE" ]; then
    check_ok "config.json exists"

    # Check if it's valid JSON
    if python3 -m json.tool "$CONFIG_FILE" > /dev/null 2>&1; then
        check_ok "  Valid JSON format"

        # Extract key settings
        if command -v jq &>/dev/null; then
            SERVER_HOST=$(jq -r '.server_host' "$CONFIG_FILE" 2>/dev/null)
            CLIENT_ID=$(jq -r '.client_id' "$CONFIG_FILE" 2>/dev/null)
            check_info "  Server: $SERVER_HOST"
            check_info "  Client ID: $CLIENT_ID"
        else
            check_info "  Install jq for detailed config analysis: sudo apt-get install jq"
        fi
    else
        check_fail "  Invalid JSON format!"
    fi
else
    check_fail "config.json not found"
fi
echo ""

# 6. Test Qt Application
echo "6. Qt Application Test"
echo "---------------------"
echo "Testing Qt initialization..."

# Create test script
cat > /tmp/qt_diagnostic.py <<'EOF'
import sys
import os

print(f"Python: {sys.version}")
print(f"DISPLAY: {os.environ.get('DISPLAY', 'not set')}")
print(f"XAUTHORITY: {os.environ.get('XAUTHORITY', 'not set')}")

try:
    from PyQt5.QtWidgets import QApplication
    app = QApplication(['test'])
    screen = app.primaryScreen()
    if screen:
        size = screen.size()
        print(f"✓ Qt initialized successfully (Screen: {size.width()}x{size.height()})")
    else:
        print("✓ Qt initialized but no screen detected")
    sys.exit(0)
except Exception as e:
    print(f"✗ Qt initialization failed: {e}")
    sys.exit(1)
EOF

# Run test
if [ -f "$INSTALL_DIR/venv/bin/python3" ]; then
    export DISPLAY="${DISPLAY:-:0}"
    export XAUTHORITY="${XAUTHORITY:-$HOME/.Xauthority}"

    if timeout 5 "$INSTALL_DIR/venv/bin/python3" /tmp/qt_diagnostic.py 2>&1; then
        check_ok "Qt application test passed"
    else
        check_fail "Qt application test failed"
        check_info "  This may be normal if X11 is not running"
        check_info "  The service will use Xvfb as fallback"
    fi
else
    check_warn "Cannot test Qt (Python venv not found)"
fi

rm -f /tmp/qt_diagnostic.py
echo ""

# 7. Check logs
echo "7. Recent Logs"
echo "-------------"
if [ -f "/var/log/digitalsignage-client-startup.log" ]; then
    check_info "Startup log exists"
    echo "  Last 5 lines:"
    tail -5 /var/log/digitalsignage-client-startup.log | sed 's/^/    /'
else
    check_info "No startup log found"
fi

echo ""
echo "To view service logs: sudo journalctl -u $SERVICE_NAME -n 20"
echo ""

# Summary
echo "=========================================="
echo "  Summary"
echo "=========================================="

echo ""
echo "Common issues and solutions:"
echo ""
echo "1. Qt test fails with X11 error:"
echo "   - This is normal for headless systems"
echo "   - Service will automatically use Xvfb"
echo ""
echo "2. Service not starting:"
echo "   - Check logs: sudo journalctl -u $SERVICE_NAME -f"
echo "   - Try manual start: sudo $INSTALL_DIR/start-with-display.sh"
echo ""
echo "3. X11 authorization issues:"
echo "   - Enable local connections: xhost +local:"
echo "   - Fix .Xauthority: touch ~/.Xauthority && chmod 600 ~/.Xauthority"
echo ""
echo "4. Missing PyQt5:"
echo "   - Reinstall: sudo apt-get install --reinstall python3-pyqt5"
echo ""

exit 0