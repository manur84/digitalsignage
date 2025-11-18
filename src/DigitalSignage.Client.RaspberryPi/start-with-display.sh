#!/bin/bash
#
# Digital Signage Client - Display Manager with Comprehensive Error Handling
# Automatically detects and configures display (X11 or Xvfb)
#

set -e  # Exit on error

# Log file for debugging
LOG_FILE="/var/log/digitalsignage-client-startup.log"

# Ensure log file is writable
touch "$LOG_FILE" 2>/dev/null || LOG_FILE="/tmp/digitalsignage-client-startup.log"

log_message() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

log_error() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] ERROR: $1" | tee -a "$LOG_FILE" >&2
}

# Run a minimal Qt test against the current DISPLAY
run_qt_test() {
    QT_TEST_OUTPUT=$($PYTHON_EXE -c "
import sys
from PyQt5.QtWidgets import QApplication
try:
    app = QApplication(['test'])
    print('Qt application created successfully')
    sys.exit(0)
except Exception as e:
    print(f'Qt application creation failed: {e}')
    sys.exit(1)
" 2>&1)

    QT_TEST_RESULT=$?
    log_message "$QT_TEST_OUTPUT"
    return $QT_TEST_RESULT
}

# Start Xvfb on :99 if not already running (fallback when real display is unavailable)
start_xvfb_fallback() {
    if xdpyinfo -display :99 &>/dev/null; then
        log_message "Accessibility: Xvfb already running on :99, reusing it"
        export DISPLAY=:99
        return 0
    fi

    if ! command -v Xvfb &>/dev/null; then
        log_error "Xvfb not installed (fallback unavailable)"
        return 1
    fi

    log_message "Starting Xvfb fallback on :99..."
    Xvfb :99 -screen 0 1024x768x24 &
    XVFB_PID=$!
    export DISPLAY=:99

    for i in $(seq 1 10); do
        if xset -display :99 q &>/dev/null; then
            log_message "Xvfb fallback ready on DISPLAY=:99"
            return 0
        fi
        sleep 1
    done

    log_error "Xvfb fallback failed to start"
    return 1
}

log_message "=========================================="
log_message "Digital Signage Client Starting"
log_message "=========================================="

# Clear any inherited display to avoid stale :0 from systemd
unset DISPLAY

# Parse arguments
TEST_MODE=false
if [ "$1" = "--test" ]; then
    TEST_MODE=true
    log_message "Running in TEST MODE"
fi

# Check working directory
INSTALL_DIR="/opt/digitalsignage-client"
if [ ! -d "$INSTALL_DIR" ]; then
    log_error "Installation directory not found: $INSTALL_DIR"
    exit 1
fi

cd "$INSTALL_DIR" || {
    log_error "Failed to change to installation directory"
    exit 1
}
log_message "Working directory: $(pwd)"

# Check Python executable
PYTHON_EXE="$INSTALL_DIR/venv/bin/python3"
if [ ! -f "$PYTHON_EXE" ]; then
    log_error "Python executable not found: $PYTHON_EXE"
    log_error "Virtual environment may not be properly created"
    exit 1
fi

PYTHON_VERSION=$($PYTHON_EXE --version 2>&1)
log_message "Python: $PYTHON_VERSION"

# Check client.py exists
if [ ! -f "$INSTALL_DIR/client.py" ]; then
    log_error "client.py not found in $INSTALL_DIR"
    exit 1
fi
log_message "Client script: $INSTALL_DIR/client.py [OK]"

# Test critical Python imports
log_message "Testing Python imports..."

# Test PyQt5
log_message "  Testing PyQt5..."
if ! $PYTHON_EXE -c "import PyQt5" 2>&1 | tee -a "$LOG_FILE"; then
    log_error "PyQt5 import failed"
    log_error "Install with: sudo apt-get install python3-pyqt5"
    exit 1
fi
log_message "  ✓ PyQt5 import successful"

# Test PyQt5.QtWidgets
log_message "  Testing PyQt5.QtWidgets..."
if ! $PYTHON_EXE -c "from PyQt5.QtWidgets import QApplication" 2>&1 | tee -a "$LOG_FILE"; then
    log_error "PyQt5.QtWidgets import failed"
    exit 1
fi
log_message "  ✓ PyQt5.QtWidgets import successful"

# Test socketio
log_message "  Testing python-socketio..."
if ! $PYTHON_EXE -c "import socketio" 2>&1 | tee -a "$LOG_FILE"; then
    log_error "python-socketio import failed"
    log_error "Install with: pip install python-socketio[client]"
    exit 1
fi
log_message "  ✓ python-socketio import successful"

# Test config.py can be imported
log_message "  Testing config.py..."
if ! $PYTHON_EXE -c "import sys; sys.path.insert(0, '$INSTALL_DIR'); import config" 2>&1 | tee -a "$LOG_FILE"; then
    log_error "config.py import failed"
    log_error "Check config.py for syntax errors"
    exit 1
fi
log_message "  ✓ config.py import successful"

log_message "✓ All Python imports successful"

# Display configuration
log_message "=========================================="
log_message "Configuring Display Server"
log_message "=========================================="

# Check if X11 is already running and accessible
log_message "Checking for existing X11 display..."

# First, try to detect which display the Xorg server is actually running on
# This is more reliable than guessing :0 or :1
DETECTED_DISPLAY=""
if command -v ps &>/dev/null; then
    # Look for Xorg process and extract display number (e.g., ":1")
    XORG_DISPLAY=$(ps aux | grep '[X]org' | grep -oP ':\d+' | head -1)
    if [ -n "$XORG_DISPLAY" ]; then
        log_message "✓ Detected Xorg running on $XORG_DISPLAY"
        DETECTED_DISPLAY="$XORG_DISPLAY"
    fi
fi

# Build candidate list with detected display first, then fallbacks
if [ -n "$DETECTED_DISPLAY" ]; then
    DISPLAY_CANDIDATES=("$DETECTED_DISPLAY" ":0" ":1" "${DISPLAY}")
else
    DISPLAY_CANDIDATES=(":0" ":1" "${DISPLAY}")
fi

X11_FOUND=false

for DISPLAY_TEST in "${DISPLAY_CANDIDATES[@]}"; do
    if [ -n "$DISPLAY_TEST" ] && DISPLAY="$DISPLAY_TEST" xset q &>/dev/null 2>&1; then
        log_message "✓ X11 display detected on $DISPLAY_TEST"
        export DISPLAY="$DISPLAY_TEST"
        X11_FOUND=true

        # Verify we can query the display
        if xdpyinfo -display "$DISPLAY" &>/dev/null 2>&1; then
            log_message "✓ Display is accessible and responding"
        else
            log_message "⚠ Display exists but may have access issues"
        fi
        break
    fi
done

if [ "$X11_FOUND" = true ]; then
    # Hide mouse cursor on real display
    log_message "Hiding mouse cursor..."
    if command -v unclutter &>/dev/null; then
        unclutter -idle 0.1 -root &
        log_message "✓ Mouse cursor hidden (unclutter started)"
    else
        log_message "⚠ unclutter not found - mouse cursor will be visible"
    fi

    # Disable screen blanking on real display
    log_message "Disabling screen blanking..."
    xset s off 2>/dev/null && log_message "✓ Screen saver disabled"
    xset -dpms 2>/dev/null && log_message "✓ DPMS disabled"
    xset s noblank 2>/dev/null && log_message "✓ Screen blanking disabled"
fi

if [ "$X11_FOUND" = false ]; then
    log_message "No X11 display found on any candidate display"
    log_message "Starting virtual framebuffer (Xvfb)"
    log_message "This is normal for headless/testing environments"
    log_message "⚠ If you have HDMI connected, this indicates X11 hasn't started yet"

    # Check if Xvfb is installed
    if ! command -v Xvfb &>/dev/null; then
        log_error "Xvfb not installed"
        log_error "Install with: sudo apt-get install xvfb"
        exit 1
    fi
    log_message "✓ Xvfb is installed"

    # Check if :99 is already in use
    if xdpyinfo -display :99 &>/dev/null; then
        log_message "⚠ Display :99 already in use, reusing it"
        export DISPLAY=:99
    else
        # Start Xvfb on :99 with 1024x768 resolution, 24-bit color
        log_message "Starting Xvfb on :99..."
        Xvfb :99 -screen 0 1024x768x24 &
        XVFB_PID=$!
        export DISPLAY=:99

        log_message "Xvfb process started (PID: $XVFB_PID)"

        # Wait for Xvfb to start (with timeout)
        log_message "Waiting for Xvfb to initialize..."
        RETRY_COUNT=0
        MAX_RETRIES=10

        while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
            if xset -display :99 q &>/dev/null; then
                log_message "✓ Xvfb started successfully on DISPLAY=:99"
                break
            fi

            # Check if process is still running
            if ! kill -0 $XVFB_PID 2>/dev/null; then
                log_error "Xvfb process died unexpectedly"
                log_error "Check system logs: sudo journalctl -xe"
                exit 1
            fi

            sleep 1
            RETRY_COUNT=$((RETRY_COUNT + 1))
        done

        if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
            log_error "Xvfb failed to start after ${MAX_RETRIES} seconds"
            log_error "Process is running but not responding"
            kill $XVFB_PID 2>/dev/null || true
            exit 1
        fi

        # Cleanup function to stop Xvfb on exit
        cleanup() {
            log_message "Cleaning up..."
            if [ -n "$XVFB_PID" ] && kill -0 $XVFB_PID 2>/dev/null; then
                log_message "Stopping Xvfb (PID: $XVFB_PID)"
                kill $XVFB_PID 2>/dev/null || true
                wait $XVFB_PID 2>/dev/null || true
            fi
        }
        trap cleanup EXIT SIGTERM SIGINT
    fi
fi

# Final environment check
log_message "=========================================="
log_message "Environment Configuration"
log_message "=========================================="
log_message "DISPLAY=$DISPLAY"
log_message "USER=$USER"
log_message "HOME=$HOME"
log_message "PWD=$(pwd)"
log_message "PYTHONUNBUFFERED=${PYTHONUNBUFFERED:-not set}"
log_message "QT_QPA_PLATFORM=${QT_QPA_PLATFORM:-not set}"

# Test that we can create a QApplication
log_message "=========================================="
log_message "Testing Qt Application Creation"
log_message "=========================================="
log_message "Running Qt test..."

run_qt_test
QT_TEST_RESULT=$?

if [ $QT_TEST_RESULT -ne 0 ]; then
    log_error "Qt application test failed on DISPLAY=${DISPLAY:-unset}, attempting Xvfb fallback..."
    if start_xvfb_fallback; then
        log_message "Retrying Qt test with DISPLAY=$DISPLAY"
        run_qt_test
        QT_TEST_RESULT=$?
    fi
fi

if [ $QT_TEST_RESULT -ne 0 ]; then
    log_error "Qt application test failed after fallback"
    log_error "This will prevent the client from starting"
    exit 1
fi
log_message "Qt application test successful"

# If in test mode, exit here
if [ "$TEST_MODE" = true ]; then
    log_message "=========================================="
    log_message "TEST MODE: All checks passed successfully!"
    log_message "=========================================="
    log_message ""
    log_message "The service should start successfully."
    log_message "Log file: $LOG_FILE"
    exit 0
fi

# Start the client
log_message "=========================================="
log_message "Starting Digital Signage Client"
log_message "=========================================="
log_message "Command: $PYTHON_EXE $INSTALL_DIR/client.py"
log_message ""

# Use exec to replace this shell with the Python process
# This ensures signals are properly forwarded
exec $PYTHON_EXE $INSTALL_DIR/client.py
