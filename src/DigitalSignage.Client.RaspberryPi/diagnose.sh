#!/bin/bash
#
# Digital Signage Client Diagnostic Script
# This script checks common issues that prevent the client from starting
#

echo ""
echo "========================================================================"
echo "Digital Signage Client - System Diagnostics"
echo "========================================================================"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Track overall status
ALL_OK=true

# Function to print status
print_status() {
    if [ "$1" = "OK" ]; then
        echo -e "${GREEN}[  OK  ]${NC} $2"
    elif [ "$1" = "WARN" ]; then
        echo -e "${YELLOW}[ WARN ]${NC} $2"
    else
        echo -e "${RED}[ FAIL ]${NC} $2"
        ALL_OK=false
    fi
}

echo "========================================================================"
echo "1. Display Configuration"
echo "========================================================================"
echo ""

# Check HDMI status (Raspberry Pi specific)
if command -v tvservice &> /dev/null; then
    echo "Checking HDMI output..."
    HDMI_STATUS=$(tvservice -s 2>/dev/null || echo "unknown")
    if echo "$HDMI_STATUS" | grep -q "HDMI"; then
        print_status "OK" "HDMI display connected"
        echo "    Status: $HDMI_STATUS"
    else
        print_status "WARN" "No HDMI display detected (running headless)"
        echo "    Status: $HDMI_STATUS"
        echo "    Note: Xvfb (virtual display) will be used automatically"
    fi
    echo ""
else
    print_status "WARN" "tvservice not available (not on Raspberry Pi?)"
    echo ""
fi

# Check if X11 is running
if ps aux | grep -v grep | grep -q "X.*:0"; then
    print_status "OK" "X11 server is running on :0"
    X11_PID=$(ps aux | grep -v grep | grep "X.*:0" | awk '{print $2}' | head -1)
    echo "    PID: $X11_PID"
else
    print_status "WARN" "X11 server not running on :0"
    echo "    Note: This is OK if running headless - Xvfb will be used"
fi

echo ""

# Check for Xvfb
if command -v Xvfb &> /dev/null; then
    print_status "OK" "Xvfb (virtual display) is installed"
    if ps aux | grep -v grep | grep -q "Xvfb :99"; then
        print_status "OK" "Xvfb is currently running on :99"
    fi
else
    print_status "WARN" "Xvfb not installed"
    echo "    Fix: sudo apt-get install xvfb"
    echo "    Note: Xvfb is needed for headless operation"
fi

echo ""

# Check DISPLAY variable
if [ -n "$DISPLAY" ]; then
    print_status "OK" "DISPLAY is set to: $DISPLAY"

    # Test if display is accessible
    if xset q &>/dev/null; then
        print_status "OK" "Display is accessible"
    else
        print_status "WARN" "DISPLAY set but not accessible"
        echo "    The display may not be running yet"
    fi
else
    print_status "WARN" "DISPLAY environment variable not set"
    echo "    Note: start-with-display.sh will set this automatically"
fi

echo ""

# Check XAUTHORITY
if [ -n "$XAUTHORITY" ]; then
    if [ -f "$XAUTHORITY" ]; then
        print_status "OK" "XAUTHORITY file exists: $XAUTHORITY"
    else
        print_status "WARN" "XAUTHORITY set but file missing: $XAUTHORITY"
    fi
else
    print_status "WARN" "XAUTHORITY not set"
    DEFAULT_XAUTH="$HOME/.Xauthority"
    if [ -f "$DEFAULT_XAUTH" ]; then
        echo "    Default location exists: $DEFAULT_XAUTH"
    else
        echo "    Note: Not needed for Xvfb (headless mode)"
    fi
fi

echo ""
echo ""
echo "========================================================================"
echo "2. System Packages"
echo "========================================================================"
echo ""

# Check Python version
if command -v python3 &> /dev/null; then
    PYTHON_VERSION=$(python3 --version)
    print_status "OK" "Python installed: $PYTHON_VERSION"
else
    print_status "FAIL" "Python 3 not installed"
    echo "    Fix: sudo apt-get install python3"
fi

echo ""

# Check PyQt5 system package
if dpkg -l | grep -q python3-pyqt5; then
    PYQT_VERSION=$(dpkg -l | grep python3-pyqt5 | head -1 | awk '{print $3}')
    print_status "OK" "PyQt5 system package installed (version $PYQT_VERSION)"

    # Try to import PyQt5
    if python3 -c "import PyQt5" 2>/dev/null; then
        print_status "OK" "PyQt5 can be imported by Python"
    else
        print_status "FAIL" "PyQt5 installed but cannot be imported"
    fi
else
    print_status "FAIL" "PyQt5 system package not installed"
    echo "    Fix: sudo apt-get install python3-pyqt5 python3-pyqt5.qtsvg"
fi

echo ""

# Check other dependencies
PACKAGES=(
    "python3-psutil:psutil"
    "sqlite3:SQLite3"
    "unclutter:Hide cursor"
    "x11-xserver-utils:X11 utilities"
)

for pkg_info in "${PACKAGES[@]}"; do
    IFS=':' read -r pkg desc <<< "$pkg_info"
    if dpkg -l | grep -q "^ii.*$pkg "; then
        print_status "OK" "$desc ($pkg)"
    else
        print_status "WARN" "$desc ($pkg) not installed"
    fi
done

echo ""
echo ""
echo "========================================================================"
echo "3. Installation Directory"
echo "========================================================================"
echo ""

INSTALL_DIR="/opt/digitalsignage-client"

if [ -d "$INSTALL_DIR" ]; then
    print_status "OK" "Installation directory exists: $INSTALL_DIR"

    # Check ownership
    OWNER=$(stat -c '%U:%G' "$INSTALL_DIR")
    echo "    Owner: $OWNER"

    # Check if writable
    if [ -w "$INSTALL_DIR" ]; then
        print_status "OK" "Directory is writable"
    else
        print_status "WARN" "Directory not writable by current user"
    fi
else
    print_status "FAIL" "Installation directory not found: $INSTALL_DIR"
    echo "    Fix: Run install.sh"
fi

echo ""

# Check for required files
FILES=("client.py" "config.py" "display_renderer.py" "device_manager.py" "cache_manager.py" "watchdog_monitor.py")
for file in "${FILES[@]}"; do
    if [ -f "$INSTALL_DIR/$file" ]; then
        print_status "OK" "Found: $file"
    else
        print_status "FAIL" "Missing: $file"
    fi
done

echo ""
echo ""
echo "========================================================================"
echo "4. Virtual Environment"
echo "========================================================================"
echo ""

VENV_DIR="$INSTALL_DIR/venv"

if [ -d "$VENV_DIR" ]; then
    print_status "OK" "Virtual environment exists: $VENV_DIR"

    # Check if venv Python works
    if [ -f "$VENV_DIR/bin/python3" ]; then
        print_status "OK" "Python executable found in venv"
        VENV_PYTHON_VERSION=$("$VENV_DIR/bin/python3" --version)
        echo "    Version: $VENV_PYTHON_VERSION"

        # Check if PyQt5 accessible from venv
        if "$VENV_DIR/bin/python3" -c "import PyQt5" 2>/dev/null; then
            print_status "OK" "PyQt5 accessible from virtual environment"
        else
            print_status "FAIL" "PyQt5 NOT accessible from virtual environment"
            echo "    Fix: Recreate venv with --system-site-packages"
            echo "    Command: python3 -m venv --system-site-packages $VENV_DIR"
        fi

        # Check socketio
        if "$VENV_DIR/bin/python3" -c "import socketio" 2>/dev/null; then
            print_status "OK" "socketio module installed in venv"
        else
            print_status "FAIL" "socketio module not found in venv"
            echo "    Fix: $VENV_DIR/bin/pip install 'python-socketio[client]'"
        fi
    else
        print_status "FAIL" "Python executable not found in venv"
    fi
else
    print_status "FAIL" "Virtual environment not found: $VENV_DIR"
    echo "    Fix: Run install.sh"
fi

echo ""
echo ""
echo "========================================================================"
echo "5. Configuration"
echo "========================================================================"
echo ""

CONFIG_FILE="/etc/digitalsignage/config.json"

if [ -f "$CONFIG_FILE" ]; then
    print_status "OK" "Configuration file exists: $CONFIG_FILE"

    # Check if valid JSON
    if python3 -c "import json; json.load(open('$CONFIG_FILE'))" 2>/dev/null; then
        print_status "OK" "Configuration is valid JSON"

        # Show key settings
        echo ""
        echo "Configuration:"
        python3 << EOF
import json
with open('$CONFIG_FILE') as f:
    config = json.load(f)
    print(f"    Server: {config.get('server_host', 'NOT SET')}:{config.get('server_port', 'NOT SET')}")
    print(f"    SSL: {config.get('use_ssl', False)}")
    print(f"    Fullscreen: {config.get('fullscreen', True)}")
    print(f"    Client ID: {config.get('client_id', 'NOT SET')}")
EOF
    else
        print_status "FAIL" "Configuration is not valid JSON"
        echo "    Fix: Check syntax of $CONFIG_FILE"
    fi
else
    print_status "WARN" "Configuration file not found (will be created on first run)"
fi

echo ""
echo ""
echo "========================================================================"
echo "6. Systemd Service"
echo "========================================================================"
echo ""

SERVICE_FILE="/etc/systemd/system/digitalsignage-client.service"

if [ -f "$SERVICE_FILE" ]; then
    print_status "OK" "Service file exists: $SERVICE_FILE"

    # Check service status
    if systemctl is-enabled --quiet digitalsignage-client 2>/dev/null; then
        print_status "OK" "Service is enabled (will start on boot)"
    else
        print_status "WARN" "Service not enabled"
        echo "    Fix: sudo systemctl enable digitalsignage-client"
    fi

    if systemctl is-active --quiet digitalsignage-client 2>/dev/null; then
        print_status "OK" "Service is currently running"

        # Show uptime
        UPTIME=$(systemctl show digitalsignage-client -p ActiveEnterTimestamp --value)
        echo "    Started: $UPTIME"
    else
        print_status "WARN" "Service is not running"

        # Check if failed
        if systemctl is-failed --quiet digitalsignage-client 2>/dev/null; then
            print_status "FAIL" "Service is in failed state"
            echo "    Check logs: sudo journalctl -u digitalsignage-client -n 50"
        fi
    fi
else
    print_status "FAIL" "Service file not found: $SERVICE_FILE"
    echo "    Fix: Run install.sh"
fi

echo ""
echo ""
echo "========================================================================"
echo "7. Recent Logs"
echo "========================================================================"
echo ""

if systemctl list-unit-files | grep -q digitalsignage-client; then
    echo "Last 10 log entries:"
    echo "------------------------------------------------------------------------"
    journalctl -u digitalsignage-client -n 10 --no-pager 2>/dev/null || echo "No logs available"
    echo "------------------------------------------------------------------------"
else
    echo "Service not installed, no logs available"
fi

echo ""
echo ""
echo "========================================================================"
echo "DIAGNOSTIC SUMMARY"
echo "========================================================================"
echo ""

if [ "$ALL_OK" = true ]; then
    echo -e "${GREEN}All critical checks passed!${NC}"
    echo ""
    echo "The client should be able to start successfully."
    echo ""
    echo "If the service is not running, start it with:"
    echo "  sudo systemctl start digitalsignage-client"
else
    echo -e "${RED}Some checks failed!${NC}"
    echo ""
    echo "Fix the issues marked as [FAIL] above before starting the service."
    echo ""
    echo "Common fixes:"
    echo "  - Install PyQt5: sudo apt-get install python3-pyqt5 python3-pyqt5.qtsvg"
    echo "  - Recreate venv: python3 -m venv --system-site-packages $VENV_DIR"
    echo "  - Install deps: $VENV_DIR/bin/pip install -r requirements.txt"
    echo "  - Set DISPLAY: export DISPLAY=:0"
    echo "  - Start X11: startx (if running on console)"
fi

echo ""
echo "Additional commands:"
echo "  Test client:      $VENV_DIR/bin/python3 $INSTALL_DIR/client.py --test"
echo "  View live logs:   sudo journalctl -u digitalsignage-client -f"
echo "  Service status:   sudo systemctl status digitalsignage-client"
echo "  Restart service:  sudo systemctl restart digitalsignage-client"
echo ""
echo "========================================================================"
echo ""
