#!/bin/bash

# Digital Signage Client - Smart Installer & Updater
# Usage: sudo ./install.sh
#
# This script intelligently detects whether to install or update:
# - Fresh installation: Full setup with service installation
# - Existing installation: Smart update with config preservation
#
# Features:
# - Auto-detection of installation mode
# - Config backup/restore during updates
# - Intelligent dependency management
# - Display mode detection and configuration
# - Idempotent (can be run multiple times)

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Installation constants
INSTALL_DIR="/opt/digitalsignage-client"
SERVICE_NAME="digitalsignage-client"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
# Default to non-interactive (auto-answers) unless DS_NONINTERACTIVE=0 is set from outside
NON_INTERACTIVE=${DS_NONINTERACTIVE:-1}
COMPLETE_MARKER="__DS_INSTALL_COMPLETE__"
echo "==============================================================="
echo "  Digital Signage Client - Smart Installer"
echo "==============================================================="
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}ERROR: Please run as root (use sudo)${NC}"
    exit 1
fi

# Detect the current user (the one who ran sudo)
ACTUAL_USER="${SUDO_USER:-$USER}"

# Validate user exists
if [ -z "$ACTUAL_USER" ] || [ "$ACTUAL_USER" = "root" ]; then
    echo -e "${RED}ERROR: Could not detect non-root user${NC}"
    echo "Please run this script with sudo as a regular user:"
    echo "  sudo ./install.sh"
    exit 1
fi

if ! id "$ACTUAL_USER" &>/dev/null; then
    echo -e "${RED}ERROR: User '$ACTUAL_USER' does not exist${NC}"
    exit 1
fi

USER_HOME=$(eval echo "~$ACTUAL_USER")
VENV_DIR="$INSTALL_DIR/venv"
CONFIG_DIR="$USER_HOME/.digitalsignage"

echo "User: $ACTUAL_USER"
echo "Home: $USER_HOME"
echo ""

# ========================================
# DETECTION LOGIC - Determine MODE
# ========================================

echo "Detecting installation status..."
echo "---------------------------------------------------------------"

MODE="INSTALL"
GIT_REPO_EXISTS=false
SERVICE_INSTALLED=false
VENV_EXISTS=false
CONFIG_EXISTS=false

# Check for existing installation
if [ -d "$INSTALL_DIR" ]; then
    echo -e "${GREEN}[OK]${NC} Installation directory found: $INSTALL_DIR"

    if [ -d "$INSTALL_DIR/.git" ]; then
        GIT_REPO_EXISTS=true
        echo -e "${GREEN}[OK]${NC} Git repository exists"
    fi

    if [ -d "$VENV_DIR" ]; then
        VENV_EXISTS=true
        echo -e "${GREEN}[OK]${NC} Virtual environment exists"
    fi

    if [ -f "$INSTALL_DIR/config.py" ]; then
        CONFIG_EXISTS=true
        echo -e "${GREEN}[OK]${NC} Configuration file exists"
    fi
else
    echo -e "${YELLOW}[!]${NC} No installation directory found"
fi

if [ -f "$SERVICE_FILE" ]; then
    SERVICE_INSTALLED=true
    echo -e "${GREEN}[OK]${NC} Service installed"

    if systemctl is-active --quiet $SERVICE_NAME; then
        echo -e "${GREEN}[OK]${NC} Service running"
    else
        echo -e "${YELLOW}[!]${NC} Service not running"
    fi
else
    echo -e "${YELLOW}[!]${NC} Service not installed"
fi

# Determine MODE
if [ -d "$INSTALL_DIR" ] && [ "$SERVICE_INSTALLED" = true ]; then
    MODE="UPDATE"
    echo ""
    echo -e "${BLUE}Mode: [*] UPDATE${NC}"
else
    MODE="INSTALL"
    echo ""
    echo -e "${BLUE}Mode: [*] INSTALL${NC}"
fi

# Force fresh install in non-interactive mode to avoid hanging prompts during remote installs
if [ "$NON_INTERACTIVE" = "1" ]; then
    MODE="INSTALL"
    echo -e "${BLUE}Non-interactive: forcing fresh INSTALL${NC}"
fi

echo "==============================================================="
echo ""

# ========================================
# Helper Functions
# ========================================

step_counter=1
TOTAL_STEPS=10

function show_step() {
    echo ""
    echo -e "${YELLOW}[$step_counter/$TOTAL_STEPS] $1${NC}"
    ((step_counter++))
}

function check_error() {
    if [ $? -ne 0 ]; then
        echo -e "${RED}[X] Error: $1${NC}"
        exit 1
    fi
}

function show_success() {
    echo -e "${GREEN}[OK] $1${NC}"
}

function show_warning() {
    echo -e "${YELLOW}[!] $1${NC}"
}

function show_info() {
    echo -e "${BLUE}[i] $1${NC}"
}

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

# ========================================
# UPDATE MODE
# ========================================

if [ "$MODE" = "UPDATE" ]; then
    TOTAL_STEPS=9

    echo "==============================================================="
    echo "  UPDATE MODE - Updating Existing Installation"
    echo "==============================================================="
    echo ""

    # [1/9] Stop service
    show_step "Stopping service..."
    if systemctl is-active --quiet $SERVICE_NAME; then
        systemctl stop $SERVICE_NAME
        show_success "Service stopped"
    else
        show_info "Service not running"
    fi

    # [2/9] Backup config
    show_step "Backing up configuration..."
    BACKUP_FILE="/tmp/digitalsignage-config-backup-$(date +%s).py"
    if [ -f "$INSTALL_DIR/config.py" ]; then
        cp "$INSTALL_DIR/config.py" "$BACKUP_FILE"
        show_success "Config backed up to: $BACKUP_FILE"
    else
        show_warning "No config.py found to backup"
        BACKUP_FILE=""
    fi

    # [3/9] Update from Git
    show_step "Updating code from repository..."

    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    cd "$SCRIPT_DIR"

    if [ -d "../../.git" ]; then
        show_info "Git repository detected"

        # Get current branch
        CURRENT_BRANCH=$(sudo -u "$ACTUAL_USER" git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "main")
        echo "Current branch: $CURRENT_BRANCH"

        # Pull latest changes
        if sudo -u "$ACTUAL_USER" git pull origin "$CURRENT_BRANCH"; then
            show_success "Code updated from git"

            # Show recent changes
            echo ""
            echo "Recent changes:"
            sudo -u "$ACTUAL_USER" git log -3 --oneline
        else
            show_warning "Git pull failed, continuing with current version"
        fi
    else
        show_info "Not a git repository - using files in current directory"
    fi

    # [4/9] Copy updated files
    show_step "Copying updated files..."

    # Required files
    REQUIRED_FILES=(
        "client.py"
        "config.py"
        "config_txt_manager.py"
        "discovery.py"
        "device_manager.py"
        "display_renderer.py"
        "cache_manager.py"
        "watchdog_monitor.py"
        "status_screen.py"
        "web_interface.py"
        "burn_in_protection.py"
        "start-with-display.sh"
    )

    COPIED_COUNT=0
    MISSING_FILES=()

    # Disable exit on error for file copying (we handle errors manually)
    set +e

    for file in "${REQUIRED_FILES[@]}"; do
        if [ -f "$SCRIPT_DIR/$file" ]; then
            if cp "$SCRIPT_DIR/$file" "$INSTALL_DIR/" 2>/dev/null; then
                ((COPIED_COUNT++))
                echo "  [OK] $file"
            else
                echo -e "  ${YELLOW}[!] Failed to copy: $file${NC}"
            fi
        else
            echo -e "  ${RED}[X] Missing: $file${NC}"
            MISSING_FILES+=("$file")
        fi
    done

    # Optional files
    OPTIONAL_FILES=(
        "wait-for-x11.sh"
        "remote_log_handler.py"
        "diagnose.sh"
        "fix-installation.sh"
        "enable-autologin-x11.sh"
        "check-autostart.sh"
        "setup-splash-screen.sh"
        "TROUBLESHOOTING.md"
    )

    for file in "${OPTIONAL_FILES[@]}"; do
        if [ -f "$SCRIPT_DIR/$file" ]; then
            cp "$SCRIPT_DIR/$file" "$INSTALL_DIR/"
            ((COPIED_COUNT++))
        fi
    done

    if [ ${#MISSING_FILES[@]} -gt 0 ]; then
        echo ""
        echo -e "${YELLOW}[!] Warning: Some files were not found in source:${NC}"
        for file in "${MISSING_FILES[@]}"; do
            echo "  - $file"
        done
        echo ""

        # Check if critical files are missing
        CRITICAL_MISSING=false
        for file in "client.py" "display_renderer.py" "config.py"; do
            if [[ " ${MISSING_FILES[@]} " =~ " ${file} " ]]; then
                CRITICAL_MISSING=true
                break
            fi
        done

        if [ "$CRITICAL_MISSING" = true ]; then
            echo -e "${RED}[X] Critical files missing - cannot continue!${NC}"
            echo ""
            echo -e "${BLUE}[*] TROUBLESHOOTING:${NC}"
            echo "  1. Go to your repository: cd ~/digitalsignage"
            echo "  2. Update repository: git pull"
            echo "  3. Run install.sh: cd src/DigitalSignage.Client.RaspberryPi && sudo ./install.sh"
            echo ""
            exit 1
        else
            echo -e "${YELLOW}[!] Non-critical files missing, continuing...${NC}"
        fi
    fi

    show_success "Copied $COPIED_COUNT files"

    # Copy templates directory for web interface
    if [ -d "$SCRIPT_DIR/templates" ]; then
        mkdir -p "$INSTALL_DIR/templates"
        cp -r "$SCRIPT_DIR/templates/"* "$INSTALL_DIR/templates/" 2>/dev/null && \
            show_success "Web interface templates copied" || \
            show_warning "Failed to copy templates directory"
    else
        show_warning "templates directory not found (web interface may not work)"
    fi

    # Re-enable exit on error
    set -e

    # Make scripts executable
    chmod +x "$INSTALL_DIR"/*.sh 2>/dev/null || true
    chmod +x "$INSTALL_DIR/client.py" 2>/dev/null || true

    # [5/9] Configure German locale
    show_step "Configuring German locale..."
    apt-get install -y -qq locales >/dev/null 2>&1 || true
    if ! locale -a | grep -q "de_DE.utf8"; then
        echo "de_DE.UTF-8 UTF-8" >> /etc/locale.gen
        locale-gen de_DE.UTF-8 >/dev/null 2>&1
        show_success "German locale (de_DE.UTF-8) generated"
    else
        show_success "German locale already installed"
    fi

    # [6/9] Update dependencies if needed
    show_step "Checking Python dependencies..."

    if [ -f "$SCRIPT_DIR/requirements.txt" ]; then
        # Check if requirements.txt changed
        if [ -d "../../.git" ] && git diff HEAD@{1} HEAD --name-only 2>/dev/null | grep -q "requirements.txt"; then
            show_info "requirements.txt changed, updating dependencies..."
            "$VENV_DIR/bin/pip" install --upgrade pip
            "$VENV_DIR/bin/pip" install -r "$SCRIPT_DIR/requirements.txt"
            check_error "Failed to update dependencies"
            show_success "Dependencies updated"
        else
            show_info "No dependency changes detected"
        fi
    else
        show_warning "requirements.txt not found"
    fi

    # [7/9] Restore config
    show_step "Restoring configuration..."

    if [ -n "$BACKUP_FILE" ] && [ -f "$BACKUP_FILE" ]; then
        cp "$BACKUP_FILE" "$INSTALL_DIR/config.py"
        show_success "Configuration restored"
    else
        show_warning "No backup to restore, using new config.py"
    fi

    # Set ownership
    chown -R "$ACTUAL_USER:$ACTUAL_USER" "$INSTALL_DIR"

    # [8/9] Update service file
    show_step "Updating systemd service..."

    if [ -f "$SCRIPT_DIR/digitalsignage-client.service" ]; then
        sed "s/INSTALL_USER/$ACTUAL_USER/g" "$SCRIPT_DIR/digitalsignage-client.service" | \
        sed "s|/usr/bin/python3|$VENV_DIR/bin/python3|g" > /tmp/digitalsignage-client.service

        cp /tmp/digitalsignage-client.service "$SERVICE_FILE"
        rm /tmp/digitalsignage-client.service

        systemctl daemon-reload
        show_success "Service configuration updated"
    else
        show_info "Service file not changed"
    fi

    # [9/9] Configure and restart service
    show_step "Configuring client..."

    # Check if config.py exists and needs configuration
    if [ -f "$INSTALL_DIR/config.py" ]; then
        # Check if server_host is still localhost (needs configuration)
        if grep -q 'server_host: str = "localhost"' "$INSTALL_DIR/config.py" 2>/dev/null; then
            if [ "$NON_INTERACTIVE" = "1" ]; then
                echo -e "${YELLOW}Non-interactive: skipping config prompts (configure later)${NC}"
                show_info "Edit config.py manually: sudo nano $INSTALL_DIR/config.py"
            else
                echo ""
                echo -e "${YELLOW}Client needs configuration:${NC}"
                echo ""
                echo "Please edit $INSTALL_DIR/config.py manually to set:"
                echo "  - server_host (currently: localhost)"
                echo "  - registration_token (required for first connection)"
                echo ""
                show_info "After editing, restart service: sudo systemctl restart $SERVICE_NAME"
            fi
        else
            show_info "Configuration appears to be set up"
        fi
    fi

    systemctl start $SERVICE_NAME --no-block
    sleep 3

    if systemctl is-active --quiet $SERVICE_NAME || systemctl is-activating --quiet $SERVICE_NAME; then
        show_success "Service started successfully"
    else
        show_warning "Service may have failed to start"
        echo "Check status: sudo systemctl status $SERVICE_NAME"
    fi

    echo ""
    echo "==============================================================="
    echo -e "${GREEN}  UPDATE COMPLETE!${NC}"
    echo "==============================================================="
    echo ""
    echo "Service Status:"
    systemctl status $SERVICE_NAME --no-pager -l || true
    echo ""
    echo "Useful commands:"
    echo "  View logs:        sudo journalctl -u $SERVICE_NAME -f"
    echo "  Restart service:  sudo systemctl restart $SERVICE_NAME"
    echo "  Service status:   sudo systemctl status $SERVICE_NAME"
    echo "  Edit config:      sudo nano $INSTALL_DIR/config.json"
    echo "  Web interface:    http://$(hostname -I | awk '{print $1}'):5000"
    echo ""

    if [ -n "$BACKUP_FILE" ] && [ -f "$BACKUP_FILE" ]; then
        echo "Config backup: $BACKUP_FILE"
        echo ""
    fi

    exit 0
fi

# ========================================
# INSTALL MODE
# ========================================

echo "==============================================================="
echo "  INSTALL MODE - Fresh Installation"
echo "==============================================================="
echo ""

# Check for existing installation and prompt
if [ -d "$INSTALL_DIR" ] || [ "$SERVICE_INSTALLED" = true ]; then
    echo -e "${YELLOW}WARNING: Partial installation detected${NC}"
    echo "This will:"
    echo "  - Stop and disable the current service (if exists)"
    echo "  - Backup config.py to /tmp (if exists)"
    echo "  - Remove the old installation completely"
    echo "  - Install fresh version"
    echo "  - Enable and start the service automatically"
    echo ""
    if [ "$NON_INTERACTIVE" = "1" ]; then
        REPLY="y"
        echo "Non-interactive: skipping fresh install confirmation"
    else
        read -p "Continue with installation? (y/N): " -n 1 -r
        echo
    fi
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Installation cancelled."
        exit 0
    fi

    # Clean up old installation
    echo ""
    echo "Cleaning up old installation..."

    if systemctl is-active --quiet $SERVICE_NAME; then
        systemctl stop $SERVICE_NAME
        show_success "Service stopped"
    fi

    if systemctl is-enabled --quiet $SERVICE_NAME 2>/dev/null; then
        systemctl disable $SERVICE_NAME
        show_success "Service disabled"
    fi

    if [ -f "$INSTALL_DIR/config.py" ]; then
        BACKUP_FILE="/tmp/digitalsignage-config-backup-$(date +%s).py"
        cp "$INSTALL_DIR/config.py" "$BACKUP_FILE"
        show_success "Old config backed up to: $BACKUP_FILE"
    fi

    if [ -d "$INSTALL_DIR" ]; then
        rm -rf "$INSTALL_DIR"
        show_success "Old installation removed"
    fi

    echo ""
fi

# Update code from repository (if in git repo)
echo "---------------------------------------------------------------"
echo "Updating code from repository..."
echo "---------------------------------------------------------------"
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [ -d "../../.git" ]; then
    show_info "Git repository detected - updating to latest version"

    CURRENT_BRANCH=$(sudo -u "$ACTUAL_USER" git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "main")
    echo "Current branch: $CURRENT_BRANCH"

    if sudo -u "$ACTUAL_USER" git pull origin "$CURRENT_BRANCH"; then
        show_success "Code updated successfully"
    else
        show_warning "Git pull failed, continuing with current version"
    fi
else
    show_info "Not a git repository - using files in current directory"
fi

echo ""
echo "==============================================================="
echo "  Installing Digital Signage Client"
echo "==============================================================="
echo ""

# [1/10] Update package lists
show_step "Updating package lists..."
apt-get update -qq
show_success "Package lists updated"

# [2/10] Install system dependencies
show_step "Installing system dependencies..."
apt-get install -y -qq \
    python3 \
    python3-pip \
    python3-venv \
    python3-pil \
    python3-pyqt5 \
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
    x11vnc \
    locales
show_success "System dependencies installed"

# Configure German locale for datetime formatting
show_info "Configuring German locale..."
if ! locale -a | grep -q "de_DE.utf8"; then
    echo "de_DE.UTF-8 UTF-8" >> /etc/locale.gen
    locale-gen de_DE.UTF-8
    show_success "German locale (de_DE.UTF-8) generated"
else
    show_success "German locale already installed"
fi

# [3/10] Verify PyQt5
show_step "Verifying PyQt5 installation..."
if python3 -c "import PyQt5" 2>/dev/null; then
    PYQT5_VERSION=$(python3 -c "from PyQt5.QtCore import PYQT_VERSION_STR; print(PYQT_VERSION_STR)" 2>/dev/null)
    show_success "PyQt5 $PYQT5_VERSION installed"
else
    echo -e "${RED}[X] PyQt5 installation failed${NC}"
    exit 1
fi

# [4/10] Create installation directory
show_step "Creating installation directory..."
mkdir -p "$INSTALL_DIR"
show_success "Directory created: $INSTALL_DIR"

# [5/10] Create virtual environment
show_step "Creating Python virtual environment..."
if [ -d "$VENV_DIR" ]; then
    show_info "Removing old virtual environment..."
    rm -rf "$VENV_DIR"
fi
python3 -m venv --system-site-packages "$VENV_DIR"
show_success "Virtual environment created"

# [6/10] Install Python dependencies
show_step "Installing Python dependencies..."
"$VENV_DIR/bin/pip" install --upgrade pip -q
if [ -f "$SCRIPT_DIR/requirements.txt" ]; then
    "$VENV_DIR/bin/pip" install -r "$SCRIPT_DIR/requirements.txt" -q
    show_success "Dependencies installed from requirements.txt"
else
    show_warning "requirements.txt not found, installing basic dependencies..."
    "$VENV_DIR/bin/pip" install -q \
        python-socketio[client]==5.10.0 \
        aiohttp==3.9.1 \
        requests==2.31.0 \
        psutil==5.9.6 \
        pillow==10.1.0 \
        qrcode==7.4.2
    show_success "Basic dependencies installed"
fi

# [7/10] Copy client files
show_step "Copying client files..."

REQUIRED_FILES=(
    "client.py"
    "config.py"
    "config_txt_manager.py"
    "discovery.py"
    "device_manager.py"
    "display_renderer.py"
    "cache_manager.py"
    "watchdog_monitor.py"
    "status_screen.py"
    "web_interface.py"
    "burn_in_protection.py"
    "start-with-display.sh"
)

COPIED_COUNT=0
MISSING_FILES=()

# Disable exit on error for file copying (we handle errors manually)
set +e

for file in "${REQUIRED_FILES[@]}"; do
    if [ -f "$SCRIPT_DIR/$file" ]; then
        if cp "$SCRIPT_DIR/$file" "$INSTALL_DIR/" 2>/dev/null; then
            ((COPIED_COUNT++))
            echo "  [OK] $file"
        else
            echo -e "  ${YELLOW}[!] Failed to copy: $file${NC}"
        fi
    else
        echo -e "  ${RED}[X] Missing: $file${NC}"
        MISSING_FILES+=("$file")
    fi
done

# Optional files
OPTIONAL_FILES=(
    "wait-for-x11.sh"
    "remote_log_handler.py"
    "diagnose.sh"
    "fix-installation.sh"
    "enable-autologin-x11.sh"
    "check-autostart.sh"
    "setup-splash-screen.sh"
    "TROUBLESHOOTING.md"
)

for file in "${OPTIONAL_FILES[@]}"; do
    if [ -f "$SCRIPT_DIR/$file" ]; then
        cp "$SCRIPT_DIR/$file" "$INSTALL_DIR/"
        ((COPIED_COUNT++))
    fi
done

# Copy digisign-logo.png if it exists (for splash screen)
if [ -f "$SCRIPT_DIR/../../digisign-logo.png" ]; then
    cp "$SCRIPT_DIR/../../digisign-logo.png" "$INSTALL_DIR/"
    show_info "Logo copied to $INSTALL_DIR/digisign-logo.png"
fi

if [ ${#MISSING_FILES[@]} -gt 0 ]; then
    echo ""
    echo -e "${YELLOW}[!] Warning: Some files were not found in source:${NC}"
    for file in "${MISSING_FILES[@]}"; do
        echo "  - $file"
    done
    echo ""

    # Check if critical files are missing
    CRITICAL_MISSING=false
    for file in "client.py" "display_renderer.py" "config.py"; do
        if [[ " ${MISSING_FILES[@]} " =~ " ${file} " ]]; then
            CRITICAL_MISSING=true
            break
        fi
    done

    if [ "$CRITICAL_MISSING" = true ]; then
        echo -e "${RED}[X] Critical files missing - cannot continue!${NC}"
        echo ""
        echo -e "${BLUE}[*] TROUBLESHOOTING:${NC}"
        echo "  1. Clone repository to home directory: cd ~ && git clone https://github.com/manur84/digitalsignage.git"
        echo "  2. Run install.sh: cd digitalsignage/src/DigitalSignage.Client.RaspberryPi && sudo ./install.sh"
        echo ""
        exit 1
    else
        echo -e "${YELLOW}[!] Non-critical files missing, continuing...${NC}"
    fi
fi

show_success "Copied $COPIED_COUNT files"

# Copy templates directory for web interface
if [ -d "$SCRIPT_DIR/templates" ]; then
    mkdir -p "$INSTALL_DIR/templates"
    cp -r "$SCRIPT_DIR/templates/"* "$INSTALL_DIR/templates/" 2>/dev/null && \
        show_success "Web interface templates copied" || \
        show_warning "Failed to copy templates directory"
else
    show_warning "templates directory not found (web interface may not work)"
fi

# Re-enable exit on error
set -e

# Set ownership
chown -R "$ACTUAL_USER:$ACTUAL_USER" "$INSTALL_DIR"

# Convert line endings
if command -v dos2unix &>/dev/null; then
    dos2unix "$INSTALL_DIR"/*.sh 2>/dev/null || true
else
    sed -i 's/\r$//' "$INSTALL_DIR"/*.sh 2>/dev/null || true
fi

# Make scripts executable
chmod +x "$INSTALL_DIR"/*.sh 2>/dev/null || true
chmod +x "$INSTALL_DIR/client.py" 2>/dev/null || true

# Verify critical files
VERIFY_MISSING=()
for file in "${REQUIRED_FILES[@]}"; do
    if [ ! -f "$INSTALL_DIR/$file" ]; then
        VERIFY_MISSING+=("$file")
    fi
done

if [ ${#VERIFY_MISSING[@]} -gt 0 ]; then
    echo -e "${RED}[X] Verification failed, missing: ${VERIFY_MISSING[*]}${NC}"
    exit 1
fi

show_success "All required files present and executable"

# [8/10] Create config directory
show_step "Creating config directory..."
mkdir -p "$CONFIG_DIR/cache"
mkdir -p "$CONFIG_DIR/logs"
chown -R "$ACTUAL_USER:$ACTUAL_USER" "$CONFIG_DIR"
show_success "Config directory created: $CONFIG_DIR"

# [9/10] Install systemd service
show_step "Installing systemd service..."
if [ -f "$SCRIPT_DIR/digitalsignage-client.service" ]; then
    sed "s/INSTALL_USER/$ACTUAL_USER/g" "$SCRIPT_DIR/digitalsignage-client.service" | \
    sed "s|/usr/bin/python3|$VENV_DIR/bin/python3|g" > /tmp/digitalsignage-client.service

    cp /tmp/digitalsignage-client.service "$SERVICE_FILE"
    rm /tmp/digitalsignage-client.service
    show_success "Service file installed"
else
    show_warning "digitalsignage-client.service not found, creating basic service..."
    cat > "$SERVICE_FILE" <<EOF
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
    show_success "Basic service file created"
fi

systemctl daemon-reload
show_success "Systemd daemon reloaded"

# [10/10] Configure autostart
show_step "Configuring autostart..."
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
show_success "Autostart configured"

# Verify installation
echo ""
echo "==============================================================="
echo "  Verifying Installation"
echo "==============================================================="
echo ""

# Check PyQt5
if "$VENV_DIR/bin/python3" -c "import PyQt5; from PyQt5.QtWidgets import QApplication; print('PyQt5 OK')" 2>/dev/null; then
    show_success "PyQt5 accessible from virtual environment"
else
    show_warning "PyQt5 not accessible from virtual environment"
fi

# Check Flask
if "$VENV_DIR/bin/python3" -c "import flask; print('Flask OK')" 2>/dev/null; then
    show_success "Flask accessible"
else
    show_warning "Flask not accessible"
fi

# Check socketio
if "$VENV_DIR/bin/python3" -c "import socketio; print('socketio OK')" 2>/dev/null; then
    show_success "python-socketio accessible"
else
    show_warning "python-socketio not accessible"
fi

# Pre-flight check
echo ""
echo "==============================================================="
echo "  Pre-Flight Check"
echo "==============================================================="
echo ""
echo "Testing client startup before enabling service..."
echo ""

if timeout 15 sudo -u "$ACTUAL_USER" "$INSTALL_DIR/start-with-display.sh" --test; then
    echo ""
    show_success "Pre-flight check successful!"
else
    TEST_EXIT_CODE=$?
    echo ""
    if [ $TEST_EXIT_CODE -eq 124 ]; then
        echo -e "${RED}[X] Pre-flight check timed out${NC}"
    else
        echo -e "${RED}[X] Pre-flight check failed (exit code: $TEST_EXIT_CODE)${NC}"
    fi
    echo ""
    echo "Check startup log: sudo cat /var/log/digitalsignage-client-startup.log"
    echo ""
    echo "After fixing issues, re-run: sudo ./install.sh"
    exit 1
fi

# Client Configuration
echo ""
echo "==============================================================="
echo "  Client Configuration"
echo "==============================================================="
echo ""

# Check if config.py exists
if [ -f "$INSTALL_DIR/config.py" ]; then
    if [ "$NON_INTERACTIVE" = "1" ]; then
        echo -e "${YELLOW}Non-interactive: skipping server IP/token prompts (configure later)${NC}"
        echo "Configure manually: sudo nano $INSTALL_DIR/config.py"
    else
        echo -e "${YELLOW}Configure Digital Signage Client:${NC}"
        echo ""
        echo "The client needs to know where to find the server."
        echo ""
        echo "Please edit the configuration file manually:"
        echo "  sudo nano $INSTALL_DIR/config.py"
        echo ""
        echo "Required settings:"
        echo "  - server_host: IP address of the server (e.g., 192.168.0.100)"
        echo "  - registration_token: Token from the server"
        echo ""
        show_info "Auto-discovery is enabled by default (auto_discover=True)"
    fi
    echo ""
fi

# Start service
echo ""
echo "==============================================================="
echo "  Starting Service"
echo "==============================================================="
echo ""

systemctl daemon-reload
systemctl enable $SERVICE_NAME 2>/dev/null || true
systemctl restart $SERVICE_NAME 2>/dev/null || true
show_success "Service enabled and started"
show_info "Client will auto-start via systemd (see systemctl status $SERVICE_NAME)"

# Display Configuration (only for fresh install)
echo ""
echo "==============================================================="
echo "  Display Configuration"
echo "==============================================================="
echo ""

set +e
detect_display_mode
DISPLAY_DETECTED=$?

check_hdmi_display
HDMI_DETECTED=$?
set -e

# Determine recommendation
if [ $HDMI_DETECTED -eq 0 ] && [ $DISPLAY_DETECTED -ne 0 ]; then
    RECOMMENDED_MODE=1
    echo -e "${BLUE}RECOMMENDATION: PRODUCTION MODE${NC}"
    echo ""
    echo "Reason: HDMI display detected, but X11 not configured"
elif [ $HDMI_DETECTED -eq 0 ] && [ $DISPLAY_DETECTED -eq 0 ]; then
    RECOMMENDED_MODE=1
    echo -e "${BLUE}RECOMMENDATION: PRODUCTION MODE (Already Configured)${NC}"
    echo ""
    echo "Reason: X11 already running on display"
else
    RECOMMENDED_MODE=2
    echo -e "${BLUE}RECOMMENDATION: DEVELOPMENT MODE (Headless)${NC}"
    echo ""
    echo "Reason: No HDMI display detected"
fi

echo "Select deployment mode:"
echo ""
echo "  1) PRODUCTION MODE - For HDMI displays"
echo "  2) DEVELOPMENT MODE - For headless/testing"
echo ""

if [ "$NON_INTERACTIVE" = "1" ]; then
    DEPLOYMENT_MODE=1
    echo "Non-interactive: selecting PRODUCTION MODE (1)"
else
    read -p "Enter choice [1/2] (default: $RECOMMENDED_MODE): " DEPLOYMENT_MODE
    DEPLOYMENT_MODE=${DEPLOYMENT_MODE:-$RECOMMENDED_MODE}
fi

if [ "$DEPLOYMENT_MODE" = "1" ]; then
    echo ""
    echo "Configuring PRODUCTION MODE..."
    echo ""

    NEEDS_REBOOT=false

    # CRITICAL FIX: Boot to Desktop (B4) for reliable X11, but disable terminal
    # B4 = Desktop with autologin - reliable X11 startup
    # We'll prevent terminal from appearing via autostart configuration
    if command -v raspi-config &>/dev/null; then
        CURRENT_BOOT=$(raspi-config nonint get_boot_behaviour 2>/dev/null || echo "unknown")
        if [ "$CURRENT_BOOT" != "B4" ]; then
            raspi-config nonint do_boot_behaviour B4 2>/dev/null
            show_success "Auto-login enabled (desktop mode)"
            NEEDS_REBOOT=true
        else
            show_info "Desktop auto-login already enabled"
        fi
    fi

    # LightDM configuration for autologin
    if [ -f /etc/lightdm/lightdm.conf ]; then
        if ! grep -q "^autologin-user=$ACTUAL_USER" /etc/lightdm/lightdm.conf; then
            [ ! -f /etc/lightdm/lightdm.conf.backup ] && cp /etc/lightdm/lightdm.conf /etc/lightdm/lightdm.conf.backup
            sed -i "s/^#autologin-user=.*/autologin-user=$ACTUAL_USER/" /etc/lightdm/lightdm.conf
            sed -i "s/^autologin-user=.*/autologin-user=$ACTUAL_USER/" /etc/lightdm/lightdm.conf
            show_success "LightDM configured for autologin"
            NEEDS_REBOOT=true
        fi
    fi

    # CRITICAL FIX: Override LXDE autostart to DISABLE terminal and desktop components
    # The default /etc/xdg/lxsession/LXDE-pi/autostart starts lxterminal automatically
    # We create user-specific override to prevent this
    LXDE_AUTOSTART_DIR="$USER_HOME/.config/lxsession/LXDE-pi"
    mkdir -p "$LXDE_AUTOSTART_DIR"

    # Create LXDE autostart override (this REPLACES default autostart behavior)
    # NOTE: The Digital Signage client is started by systemd service, NOT by LXDE autostart!
    # This prevents duplicate instances and ensures proper service management
    cat > "$LXDE_AUTOSTART_DIR/autostart" <<'EOF'
# Digital Signage - LXDE Autostart Override
# This file PREVENTS default desktop components (terminal, taskbar, etc.)
# The Digital Signage client is started by systemd, not here!

# Screen settings
@xset s off
@xset -dpms
@xset s noblank

# Hide cursor
@unclutter -idle 0.1 -root

# CRITICAL: Do NOT start lxterminal (default behavior)
# CRITICAL: Do NOT start pcmanfm desktop
# CRITICAL: Do NOT start digitalsignage client here - systemd service handles it!
EOF

    chown "$ACTUAL_USER:$ACTUAL_USER" "$LXDE_AUTOSTART_DIR/autostart"
    show_success "LXDE autostart configured (terminal/desktop DISABLED, screen settings ONLY)"

    # Also disable pcmanfm desktop (file manager/desktop icons) to keep it clean
    PCMANFM_CONFIG="$USER_HOME/.config/pcmanfm/LXDE-pi/desktop-items-0.conf"
    mkdir -p "$(dirname "$PCMANFM_CONFIG")"
    cat > "$PCMANFM_CONFIG" <<'EOF'
[*]
desktop_bg=#000000
desktop_fg=#ffffff
desktop_shadow=#000000
wallpaper_mode=color
show_documents=0
show_trash=0
show_mounts=0
EOF
    chown "$ACTUAL_USER:$ACTUAL_USER" "$PCMANFM_CONFIG"
    show_success "Desktop icons disabled"

    # Update /boot/config.txt with detected HDMI modes
    if [ -f "$INSTALL_DIR/config_txt_manager.py" ]; then
        echo ""
        echo "Updating /boot/config.txt with detected display resolutions..."
        if python3 "$INSTALL_DIR/config_txt_manager.py"; then
            show_success "config.txt updated with detected modes"
        else
            show_warning "Could not update config.txt automatically. Run manually: sudo python3 $INSTALL_DIR/config_txt_manager.py"
        fi
    else
        show_warning "config_txt_manager.py not found; skipping config.txt update"
    fi

    # BACKUP SOLUTION: .xinitrc for boot-to-console setups
    # This only starts LXDE desktop environment - systemd service will start the client!
    # NOTE: With B4 (Desktop Autologin), this file is NOT used - LXDE starts automatically
    if [ ! -f "$USER_HOME/.xinitrc" ] || ! grep -q "Digital Signage" "$USER_HOME/.xinitrc"; then
        cat > "$USER_HOME/.xinitrc" <<'EOF'
#!/bin/sh
# Digital Signage Client - X11 startup configuration
# NOTE: Client is started by systemd service, not here!
xset -dpms
xset s off
xset s noblank
unclutter -idle 0.1 -root &
# Start LXDE desktop environment (client will be started by systemd)
exec startlxde-pi
EOF
        chown "$ACTUAL_USER:$ACTUAL_USER" "$USER_HOME/.xinitrc"
        chmod +x "$USER_HOME/.xinitrc"
        show_success ".xinitrc created (X11 settings + LXDE startup)"
    fi

    # Auto-start X11 on tty1 login (works with boot-to-console mode B2)
    # NOTE: With B4 (Desktop Autologin), this is not needed but doesn't hurt
    if [ ! -f "$USER_HOME/.bash_profile" ] || ! grep -q "startx" "$USER_HOME/.bash_profile"; then
        cat >> "$USER_HOME/.bash_profile" <<'EOF'

# Auto-start X11 on tty1 login for Digital Signage
# Only used with boot-to-console mode (B2), not with Desktop Autologin (B4)
if [ -z "$DISPLAY" ] && [ "$(tty)" = "/dev/tty1" ]; then
    exec startx
fi
EOF
        chown "$ACTUAL_USER:$ACTUAL_USER" "$USER_HOME/.bash_profile"
        show_success ".bash_profile configured (auto-start X11 on tty1 for console mode)"
    fi

    echo ""
    if [ "$NEEDS_REBOOT" = true ]; then
        echo -e "${YELLOW}IMPORTANT: Reboot required${NC}"
        echo ""
        if [ "$NON_INTERACTIVE" = "1" ]; then
            REPLY="y"
            echo "Non-interactive: auto-confirm reboot"
        else
            read -p "Reboot now? (y/N): " -n 1 -r
            echo
        fi
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            echo "$COMPLETE_MARKER"
            echo "Rebooting in 3 seconds..."
            sleep 3
            reboot
        else
            echo "Please reboot manually: sudo reboot"
        fi
    else
        show_success "No reboot required - system ready"
        echo "$COMPLETE_MARKER"
    fi
else
    echo ""
    show_info "DEVELOPMENT MODE selected"
    echo "Service uses Xvfb virtual display (via start-with-display.sh)"
    echo "$COMPLETE_MARKER"
fi


# ========================================
# [OPTIONAL] Configure Splash Screen
# ========================================

if [ "$DEPLOYMENT_MODE" = "1" ] && [ -f "$INSTALL_DIR/setup-splash-screen.sh" ] && [ -f "$INSTALL_DIR/digisign-logo.png" ]; then
    echo ""
    echo "---------------------------------------------------------------"
    echo "Optional: Configure Boot Splash Screen"
    echo "---------------------------------------------------------------"
    echo ""
    echo "A boot splash screen can display your logo during startup instead of"
    echo "the default Raspberry Pi boot messages."
    echo ""

    if [ "$NON_INTERACTIVE" = "1" ]; then
        REPLY="n"
        echo "Non-interactive mode: Skipping splash screen setup (run manually later)"
    else
        read -p "Configure boot splash screen now? (y/N): " -n 1 -r
        echo ""
    fi

    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo ""
        echo "Setting up splash screen..."

        # Copy logo to root for Plymouth
        cp "$INSTALL_DIR/digisign-logo.png" /digisign-logo.png
        chmod 644 /digisign-logo.png

        # Run splash screen setup
        if bash "$INSTALL_DIR/setup-splash-screen.sh" /digisign-logo.png; then
            show_success "Splash screen configured successfully!"
            echo ""
            echo "Your logo will appear during boot after reboot."
            NEEDS_REBOOT=true
        else
            show_warning "Splash screen setup failed. You can run it manually later:"
            echo "  sudo $INSTALL_DIR/setup-splash-screen.sh /digisign-logo.png"
        fi
    else
        echo ""
        show_info "Splash screen setup skipped."
        echo "You can configure it later with:"
        echo "  sudo $INSTALL_DIR/setup-splash-screen.sh $INSTALL_DIR/digisign-logo.png"
    fi
fi
# Final summary
echo ""
echo "==============================================================="
echo -e "${GREEN}  INSTALLATION COMPLETE!${NC}"
echo "==============================================================="
echo ""
echo "Installation Paths:"
echo "  Installation: $INSTALL_DIR"
echo "  Virtual env:  $VENV_DIR"
echo "  Config:       $CONFIG_DIR"
echo "  Service:      $SERVICE_FILE"
echo ""
echo "Next Steps:"
echo "  1. Edit config:   sudo nano $INSTALL_DIR/config.py"
echo "  2. Set server_host (e.g., 192.168.0.100) and registration_token"
echo "  3. Restart:       sudo systemctl restart $SERVICE_NAME"
if [ "$DEPLOYMENT_MODE" = "1" ] && [ "$NEEDS_REBOOT" = true ]; then
    echo "  4. Reboot:        sudo reboot"
fi
echo ""
echo "Useful Commands:"
echo "  Status:       sudo systemctl status $SERVICE_NAME"
echo "  Logs:         sudo journalctl -u $SERVICE_NAME -f"
echo "  Restart:      sudo systemctl restart $SERVICE_NAME"
echo "  Diagnose:     sudo $INSTALL_DIR/diagnose.sh"
if [ -f "$INSTALL_DIR/setup-splash-screen.sh" ]; then
echo "  Splash:       sudo $INSTALL_DIR/setup-splash-screen.sh /digisign-logo.png"
fi
echo ""

