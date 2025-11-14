#!/bin/bash

# Digital Signage Client Installation Script for Raspberry Pi
# Usage: sudo ./install.sh
#
# This script automatically detects and configures:
# - Display mode (real HDMI vs virtual/headless)
# - Auto-login and X11 startup (for production displays)
# - Service configuration and startup
# - Only prompts for reboot if actually needed

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

# ========================================
# Display Detection Functions
# ========================================

detect_display_mode() {
    # Check if X11 is running on real display
    if sudo -u "$ACTUAL_USER" DISPLAY=:0 xset q &>/dev/null; then
        echo "X11 detected on :0 (real display)"
        DETECTED_MODE="desktop"
        return 0
    fi

    # Check if running in console mode
    if ! pgrep -x X &>/dev/null && ! pgrep -x Xorg &>/dev/null; then
        echo "No X11 server detected"
        DETECTED_MODE="console"
        return 1
    fi

    # X11 running but not accessible on :0
    echo "X11 running but not on standard display :0"
    DETECTED_MODE="other"
    return 1
}

check_hdmi_display() {
    # Method 1: tvservice (Raspberry Pi specific)
    if command -v tvservice &>/dev/null; then
        if tvservice -s 2>/dev/null | grep -q "HDMI"; then
            echo "HDMI display detected via tvservice"
            return 0
        fi
    fi

    # Method 2: Check /sys/class/drm
    if ls /sys/class/drm/*/status 2>/dev/null | xargs cat 2>/dev/null | grep -q "^connected"; then
        echo "Display connected via DRM"
        return 0
    fi

    # Method 3: xrandr (if X11 running)
    if command -v xrandr &>/dev/null && sudo -u "$ACTUAL_USER" DISPLAY=:0 xrandr 2>/dev/null | grep -q " connected"; then
        echo "Display detected via xrandr"
        return 0
    fi

    echo "No HDMI display detected"
    return 1
}

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
echo "Updating Code from Repository"
echo "========================================="
echo ""

# Check if we're in a git repository
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [ -d "../../.git" ]; then
    echo "Git repository detected - updating to latest version..."

    # Get current user's git config
    if [ -n "$ACTUAL_USER" ]; then
        # Temporarily become the actual user to do git operations
        sudo -u "$ACTUAL_USER" bash -c "cd '$SCRIPT_DIR' && git fetch origin"

        # Get current branch
        CURRENT_BRANCH=$(sudo -u "$ACTUAL_USER" git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "main")
        echo "Current branch: $CURRENT_BRANCH"

        # Pull latest changes
        echo "Pulling latest changes from origin/$CURRENT_BRANCH..."
        if sudo -u "$ACTUAL_USER" git pull origin "$CURRENT_BRANCH"; then
            echo "✓ Code updated successfully"
        else
            echo "⚠ Warning: git pull failed, continuing with current version"
            echo "  You may need to resolve conflicts manually"
        fi
    else
        echo "⚠ Warning: Could not determine user for git operations"
        echo "  Continuing with current version"
    fi
else
    echo "ℹ Not a git repository - using files in current directory"
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
    x11-utils \
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
cp discovery.py "$INSTALL_DIR/"
cp device_manager.py "$INSTALL_DIR/"
cp display_renderer.py "$INSTALL_DIR/"
cp cache_manager.py "$INSTALL_DIR/"
cp watchdog_monitor.py "$INSTALL_DIR/"
cp status_screen.py "$INSTALL_DIR/"
cp start-with-display.sh "$INSTALL_DIR/"
cp wait-for-x11.sh "$INSTALL_DIR/" 2>/dev/null || echo "Note: wait-for-x11.sh not found (optional)"
cp remote_log_handler.py "$INSTALL_DIR/" 2>/dev/null || echo "Note: remote_log_handler.py not found (optional)"
cp diagnose.sh "$INSTALL_DIR/" 2>/dev/null || echo "Note: diagnose.sh not found (optional)"
cp fix-installation.sh "$INSTALL_DIR/" 2>/dev/null || echo "Note: fix-installation.sh not found (optional)"
cp enable-autologin-x11.sh "$INSTALL_DIR/" 2>/dev/null || echo "Note: enable-autologin-x11.sh not found (optional)"
cp check-autostart.sh "$INSTALL_DIR/" 2>/dev/null || echo "Note: check-autostart.sh not found (optional)"
cp TROUBLESHOOTING.md "$INSTALL_DIR/" 2>/dev/null || echo "Note: TROUBLESHOOTING.md not found (optional)"

# Set ownership
chown -R "$ACTUAL_USER:$ACTUAL_USER" "$INSTALL_DIR"

# Convert line endings to Unix format (in case files were edited on Windows)
echo "Converting line endings to Unix format..."
if command -v dos2unix &>/dev/null; then
    dos2unix "$INSTALL_DIR/start-with-display.sh" 2>/dev/null || true
    dos2unix "$INSTALL_DIR/diagnose.sh" 2>/dev/null || true
    dos2unix "$INSTALL_DIR/fix-installation.sh" 2>/dev/null || true
    dos2unix "$INSTALL_DIR/enable-autologin-x11.sh" 2>/dev/null || true
else
    # Fallback: use sed to remove carriage returns
    sed -i 's/\r$//' "$INSTALL_DIR/start-with-display.sh" 2>/dev/null || true
    sed -i 's/\r$//' "$INSTALL_DIR/diagnose.sh" 2>/dev/null || true
    sed -i 's/\r$//' "$INSTALL_DIR/fix-installation.sh" 2>/dev/null || true
    sed -i 's/\r$//' "$INSTALL_DIR/enable-autologin-x11.sh" 2>/dev/null || true
fi

# Make scripts executable
chmod +x "$INSTALL_DIR/client.py"
chmod +x "$INSTALL_DIR/start-with-display.sh"
chmod +x "$INSTALL_DIR/wait-for-x11.sh" 2>/dev/null || true
chmod +x "$INSTALL_DIR/diagnose.sh" 2>/dev/null || true
chmod +x "$INSTALL_DIR/fix-installation.sh" 2>/dev/null || true
chmod +x "$INSTALL_DIR/enable-autologin-x11.sh" 2>/dev/null || true
chmod +x "$INSTALL_DIR/check-autostart.sh" 2>/dev/null || true

# Verify critical files
echo "Verifying installation files..."
MISSING_FILES=()

if [ ! -f "$INSTALL_DIR/client.py" ]; then
    MISSING_FILES+=("client.py")
fi

if [ ! -f "$INSTALL_DIR/config.py" ]; then
    MISSING_FILES+=("config.py")
fi

if [ ! -f "$INSTALL_DIR/discovery.py" ]; then
    MISSING_FILES+=("discovery.py")
fi

if [ ! -f "$INSTALL_DIR/device_manager.py" ]; then
    MISSING_FILES+=("device_manager.py")
fi

if [ ! -f "$INSTALL_DIR/display_renderer.py" ]; then
    MISSING_FILES+=("display_renderer.py")
fi

if [ ! -f "$INSTALL_DIR/cache_manager.py" ]; then
    MISSING_FILES+=("cache_manager.py")
fi

if [ ! -f "$INSTALL_DIR/watchdog_monitor.py" ]; then
    MISSING_FILES+=("watchdog_monitor.py")
fi

if [ ! -f "$INSTALL_DIR/start-with-display.sh" ]; then
    MISSING_FILES+=("start-with-display.sh")
fi

if [ ! -x "$INSTALL_DIR/start-with-display.sh" ]; then
    echo "WARNING: start-with-display.sh not executable, fixing..."
    chmod +x "$INSTALL_DIR/start-with-display.sh"
fi

if [ ${#MISSING_FILES[@]} -gt 0 ]; then
    echo "ERROR: Critical files missing: ${MISSING_FILES[*]}"
    exit 1
fi

echo "✓ All required files present and executable"

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
After=network-online.target graphical.target multi-user.target
Wants=network-online.target

[Service]
Type=simple
User=$ACTUAL_USER
Group=$ACTUAL_USER
WorkingDirectory=$INSTALL_DIR
Environment="PYTHONUNBUFFERED=1"
Environment="QT_QPA_PLATFORM=xcb"
Environment="DISPLAY=:0"
Environment="XAUTHORITY=$USER_HOME/.Xauthority"

# Wait for X11 to be ready (critical for autostart)
ExecStartPre=/bin/bash -c 'for i in {1..30}; do if DISPLAY=:0 xset q &>/dev/null 2>&1; then echo "X11 ready"; exit 0; fi; echo "Waiting for X11... (\$i/30)"; sleep 1; done; exit 0'
ExecStartPre=/bin/bash -c 'test -f $INSTALL_DIR/start-with-display.sh || exit 1'
ExecStart=$INSTALL_DIR/start-with-display.sh

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
echo "Pre-Flight Check"
echo "========================================="
echo ""
echo "Testing client startup manually before enabling service..."
echo "This will verify that all dependencies and configuration are correct."
echo ""

# Run manual test with timeout
if timeout 15 sudo -u "$ACTUAL_USER" "$INSTALL_DIR/start-with-display.sh" --test; then
    echo ""
    echo "✓ Pre-flight check successful!"
    echo "  The client can start successfully."
else
    TEST_EXIT_CODE=$?
    echo ""
    if [ $TEST_EXIT_CODE -eq 124 ]; then
        echo "✗ Pre-flight check timed out after 15 seconds"
        echo "  This indicates the test is hanging."
    else
        echo "✗ Pre-flight check failed with exit code: $TEST_EXIT_CODE"
    fi
    echo ""
    echo "The installation cannot proceed until the pre-flight check passes."
    echo ""
    echo "Check the startup log for details:"
    echo "  sudo cat /var/log/digitalsignage-client-startup.log"
    echo "  OR"
    echo "  sudo cat /tmp/digitalsignage-client-startup.log"
    echo ""
    echo "Common issues:"
    echo "  - Missing dependencies (PyQt5, Xvfb, etc.)"
    echo "  - Syntax errors in config.py"
    echo "  - Permission issues"
    echo ""
    echo "After fixing issues, you can:"
    echo "  1. Run the fix script: sudo ./fix-installation.sh"
    echo "  2. Or re-run install.sh: sudo ./install.sh"
    exit 1
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
# Use --no-block to avoid hanging if service takes long to start
systemctl start digitalsignage-client --no-block
sleep 3  # Give it a moment to initialize

# Check if it's running or starting
if systemctl is-active digitalsignage-client &>/dev/null || systemctl is-activating digitalsignage-client &>/dev/null; then
    echo "✓ Service started successfully"
else
    echo "⚠ Service failed to start"
    echo "  Check status: sudo systemctl status digitalsignage-client"
    echo "  Check logs: sudo journalctl -u digitalsignage-client -n 50"
fi

# Wait for service to stabilize
echo ""
echo "Waiting for service to initialize..."
sleep 2

if systemctl is-active --quiet digitalsignage-client; then
    echo "✓ Service is running successfully!"

    # Intelligent Display Configuration
    echo ""
    echo "=========================================="
    echo "Display Configuration"
    echo "=========================================="
    echo ""

    # Detect current configuration
    echo "Detecting display hardware..."
    set +e  # Temporarily disable exit on error for detection functions
    detect_display_mode
    DISPLAY_DETECTED=$?

    check_hdmi_display
    HDMI_DETECTED=$?
    set -e  # Re-enable exit on error

    echo ""
    echo "=========================================="
    echo "Recommended Configuration"
    echo "=========================================="
    echo ""

    # Determine recommendation
    if [ $HDMI_DETECTED -eq 0 ] && [ $DISPLAY_DETECTED -ne 0 ]; then
        # HDMI connected but X11 not running on :0
        RECOMMENDED_MODE=1
        echo "RECOMMENDATION: PRODUCTION MODE"
        echo ""
        echo "Reason: HDMI display detected, but X11 not configured"
        echo ""
        echo "This will:"
        echo "  ✓ Enable auto-login to desktop"
        echo "  ✓ Start X11 automatically on HDMI display"
        echo "  ✓ Disable screen blanking"
        echo "  ✓ Hide mouse cursor"
        echo "  ✓ Show digital signage on your display"
        echo "  ⚠ Requires reboot"
        echo ""
    elif [ $HDMI_DETECTED -eq 0 ] && [ $DISPLAY_DETECTED -eq 0 ]; then
        # HDMI connected and X11 already running
        RECOMMENDED_MODE=1
        echo "RECOMMENDATION: PRODUCTION MODE (Already Configured)"
        echo ""
        echo "Reason: X11 already running on display"
        echo ""
        echo "This will:"
        echo "  ✓ Verify auto-login settings"
        echo "  ✓ Ensure power management is disabled"
        echo "  ✓ Install the client service"
        echo "  ℹ May not require reboot"
        echo ""
    else
        # No HDMI or headless mode
        RECOMMENDED_MODE=2
        echo "RECOMMENDATION: DEVELOPMENT MODE (Headless)"
        echo ""
        echo "Reason: No HDMI display detected or headless environment"
        echo ""
        echo "This will:"
        echo "  ✓ Use Xvfb (virtual display)"
        echo "  ✓ Allow testing without physical display"
        echo "  ✓ No reboot required"
        echo ""
    fi

    echo "Select deployment mode:"
    echo ""
    echo "  1) PRODUCTION MODE"
    echo "     For digital signage displays with HDMI"
    echo ""
    echo "  2) DEVELOPMENT MODE"
    echo "     For headless/testing environments"
    echo ""

    read -p "Enter choice [1/2] (default: $RECOMMENDED_MODE): " DEPLOYMENT_MODE
    DEPLOYMENT_MODE=${DEPLOYMENT_MODE:-$RECOMMENDED_MODE}

    if [ "$DEPLOYMENT_MODE" = "1" ]; then
        echo ""
        echo "=========================================="
        echo "Configuring PRODUCTION MODE"
        echo "=========================================="
        echo ""

        # Track if reboot is needed
        NEEDS_REBOOT=false

        # Check 1: Auto-login
        echo "[1/5] Checking auto-login..."
        if command -v raspi-config &>/dev/null; then
            # Check current boot behavior
            CURRENT_BOOT=$(raspi-config nonint get_boot_behaviour 2>/dev/null || echo "unknown")
            if [ "$CURRENT_BOOT" != "B4" ]; then
                echo "  Enabling auto-login to desktop..."
                raspi-config nonint do_boot_behaviour B4 2>/dev/null
                if [ $? -eq 0 ]; then
                    echo "  ✓ Auto-login enabled"
                    NEEDS_REBOOT=true
                else
                    echo "  ⚠ raspi-config completed with warnings (may still be successful)"
                    NEEDS_REBOOT=true
                fi
            else
                echo "  ✓ Auto-login already enabled"
            fi
        else
            echo "  ⚠ raspi-config not found"
            echo "  Manual: Run 'sudo raspi-config' → System Options → Boot/Auto Login"
        fi

        # Check 2: LightDM configuration
        echo ""
        echo "[2/5] Configuring display manager..."
        if [ -f /etc/lightdm/lightdm.conf ]; then
            if ! grep -q "^autologin-user=$ACTUAL_USER" /etc/lightdm/lightdm.conf; then
                # Backup if not already backed up
                if [ ! -f /etc/lightdm/lightdm.conf.backup ]; then
                    cp /etc/lightdm/lightdm.conf /etc/lightdm/lightdm.conf.backup
                    echo "  ✓ LightDM config backed up"
                fi

                sed -i "s/^#autologin-user=.*/autologin-user=$ACTUAL_USER/" /etc/lightdm/lightdm.conf
                sed -i "s/^autologin-user=.*/autologin-user=$ACTUAL_USER/" /etc/lightdm/lightdm.conf
                echo "  ✓ LightDM configured"
                NEEDS_REBOOT=true
            else
                echo "  ✓ LightDM already configured"
            fi
        else
            echo "  ℹ LightDM not found (normal for Raspberry Pi OS Lite)"
        fi

        # Check 3: .xinitrc
        echo ""
        echo "[3/5] Configuring X11 startup..."
        if [ ! -f "$USER_HOME/.xinitrc" ] || ! grep -q "xset -dpms" "$USER_HOME/.xinitrc"; then
            cat > "$USER_HOME/.xinitrc" <<'EOF'
#!/bin/sh
# Digital Signage X11 Startup Configuration

# Disable power management features
xset -dpms     # Disable DPMS (Energy Star) features
xset s off     # Disable screen saver
xset s noblank # Don't blank the video device

# Hide mouse cursor after 0.1 seconds of inactivity
unclutter -idle 0.1 -root &

# Keep X11 running
exec tail -f /dev/null
EOF
            chown "$ACTUAL_USER:$ACTUAL_USER" "$USER_HOME/.xinitrc"
            chmod +x "$USER_HOME/.xinitrc"
            echo "  ✓ X11 configuration created (~/.xinitrc)"
        else
            echo "  ✓ X11 already configured"
        fi

        # Check 4: Service display environment
        echo ""
        echo "[4/5] Updating service configuration..."
        # Service file already uses start-with-display.sh which handles display detection
        echo "  ✓ Service configured for display auto-detection"

        # Check 5: Verify
        echo ""
        echo "[5/5] Verifying configuration..."
        if [ "$NEEDS_REBOOT" = true ]; then
            echo "  ⚠ Reboot required to apply changes"
        else
            echo "  ✓ System already configured for production mode"
            # Check if X11 is running
            if sudo -u "$ACTUAL_USER" DISPLAY=:0 xset q &>/dev/null; then
                echo "  ✓ X11 is running - no reboot needed"
                NEEDS_REBOOT=false
            else
                echo "  ⚠ X11 not running - reboot recommended"
                NEEDS_REBOOT=true
            fi
        fi

        echo ""
        echo "=========================================="
        echo "✓ PRODUCTION MODE configured"
        echo "=========================================="
        echo ""

        # Smart reboot prompt
        if [ "$NEEDS_REBOOT" = true ]; then
            echo "IMPORTANT: A REBOOT IS REQUIRED"
            echo ""
            echo "After reboot:"
            echo "  - System will auto-login as $ACTUAL_USER"
            echo "  - X11 will start on HDMI display"
            echo "  - Digital Signage client will start automatically"
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
            echo "✓ No reboot required - system already configured"
            echo "  Service is running and ready"
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

    # Post-Installation Verification
    echo ""
    echo "=========================================="
    echo "Installation Verification"
    echo "=========================================="
    echo ""

    echo "Current Configuration:"
    if sudo -u "$ACTUAL_USER" DISPLAY=:0 xset q &>/dev/null 2>&1; then
        echo "  Display Mode: Real X11 on :0 (HDMI)"
    elif pgrep -f "Xvfb :99" &>/dev/null; then
        echo "  Display Mode: Virtual (Xvfb)"
    else
        echo "  Display Mode: Not running (will start on boot)"
    fi

    SERVICE_STATUS=$(systemctl is-active digitalsignage-client 2>/dev/null || echo "not running")
    echo "  Service Status: $SERVICE_STATUS"
    echo "  User: $ACTUAL_USER"
    echo "  Installation: $INSTALL_DIR"
    echo ""

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
    if [ "$DEPLOYMENT_MODE" = "1" ] && [ "$NEEDS_REBOOT" = true ]; then
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
    echo "========================================="
    echo "Next Steps"
    echo "========================================="
    echo ""
    echo "1. Run the fix script to diagnose and fix common issues:"
    echo "   sudo $INSTALL_DIR/fix-installation.sh"
    echo ""
    echo "2. Read the troubleshooting guide:"
    echo "   cat $INSTALL_DIR/TROUBLESHOOTING.md"
    echo ""
    echo "3. After fixing issues, restart the service:"
    echo "   sudo systemctl restart digitalsignage-client"
    echo ""
    echo "4. View detailed logs:"
    echo "   sudo journalctl -u digitalsignage-client -n 100"
    echo "   sudo cat /var/log/digitalsignage-client-startup.log"
    echo ""
    echo "Configuration:"
    echo "  - Installation directory: $INSTALL_DIR"
    echo "  - Virtual environment: $VENV_DIR"
    echo "  - Config directory: $CONFIG_DIR"
    echo ""
    exit 1
fi
