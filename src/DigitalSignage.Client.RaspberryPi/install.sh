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
echo "----------------------------------------"
echo "  Digital Signage Client - Smart Installer"
echo "----------------------------------------"
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
echo "----------------------------------------"

MODE="INSTALL"
GIT_REPO_EXISTS=false
SERVICE_INSTALLED=false
VENV_EXISTS=false
CONFIG_EXISTS=false

# Check for existing installation
if [ -d "$INSTALL_DIR" ]; then
    echo "✓ Installation directory exists: $INSTALL_DIR"

    if [ -d "$INSTALL_DIR/.git" ]; then
        GIT_REPO_EXISTS=true
        echo "  - Git repository found"
    fi

    if [ -d "$VENV_DIR" ]; then
        VENV_EXISTS=true
        echo "  - Virtual environment found"
    fi

    if [ -f "$INSTALL_DIR/config.json" ]; then
        CONFIG_EXISTS=true
        echo "  - Configuration file found (config.json)"
    fi
else
    echo "✗ No existing installation found"
fi

if [ -f "$SERVICE_FILE" ]; then
    SERVICE_INSTALLED=true
    echo "✓ Systemd service installed"

    if systemctl is-active --quiet $SERVICE_NAME; then
        echo "  - Service is currently running"
    else
        echo "  - Service is stopped"
    fi
else
    echo "✗ Systemd service not installed"
fi

# Determine MODE (always INSTALL)
MODE="INSTALL"
echo ""
echo -e "${BLUE}Mode: Fresh INSTALL (existing installation will be replaced)${NC}"

# Non-interactive installs always run the fresh path without prompting
if [ "$NON_INTERACTIVE" = "1" ]; then
    echo -e "${BLUE}Non-interactive: enforcing fresh INSTALL${NC}"
fi
echo "----------------------------------------"
echo ""

# ========================================
# Helper Functions
# ========================================

step_counter=1
TOTAL_STEPS=11

function show_step() {
    echo ""
    echo -e "${YELLOW}[$step_counter/$TOTAL_STEPS] $1${NC}"
    ((step_counter++))
}

function check_error() {
    if [ $? -ne 0 ]; then
        echo -e "${RED}✗ ERROR: $1${NC}"
        exit 1
    fi
}

function show_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

function show_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

function show_info() {
    echo -e "${BLUE}ℹ $1${NC}"
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

# ========================================
# INSTALL MODE
# ========================================

echo "----------------------------------------"
echo "  INSTALL MODE - Fresh Installation"
echo "----------------------------------------"
echo ""

# ========================================
# Set unique hostname based on MAC address
# ========================================
echo "Setting unique hostname based on MAC address..."

# Get MAC address from eth0, or wlan0 if eth0 doesn't exist
MAC_ADDR=""
if [ -e /sys/class/net/eth0/address ]; then
    MAC_ADDR=$(cat /sys/class/net/eth0/address)
    INTERFACE="eth0"
elif [ -e /sys/class/net/wlan0/address ]; then
    MAC_ADDR=$(cat /sys/class/net/wlan0/address)
    INTERFACE="wlan0"
else
    # Fallback: try to get any interface MAC
    MAC_ADDR=$(ip link show | grep -m1 "link/ether" | awk '{print $2}')
    INTERFACE="auto-detected"
fi

if [ -n "$MAC_ADDR" ]; then
<<<<<<< HEAD
    # Extract first 4 hex characters (remove colons, uppercase)
    # Example: ac:b2:cb:2e:bb:ef -> ACB2
    MAC_SHORT=$(echo "$MAC_ADDR" | tr -d ':' | tr '[:lower:]' '[:upper:]' | cut -c1-4)
    NEW_HOSTNAME="DigiSign-${MAC_SHORT}"

    CURRENT_HOSTNAME=$(hostname)

    echo "Interface: $INTERFACE"
    echo "MAC Address: $MAC_ADDR"
    echo "Hostname Suffix: $MAC_SHORT"
    echo "Generated Hostname: $NEW_HOSTNAME"
    echo ""

    if [ "$CURRENT_HOSTNAME" != "$NEW_HOSTNAME" ]; then
        echo "Changing hostname from '$CURRENT_HOSTNAME' to '$NEW_HOSTNAME'..."

        # Set hostname immediately (persistent across reboots)
        hostnamectl set-hostname "$NEW_HOSTNAME" 2>/dev/null || {
            # Fallback for systems without hostnamectl
            echo "$NEW_HOSTNAME" > /etc/hostname
            hostname "$NEW_HOSTNAME"
        }

        # Update /etc/hosts with new hostname
        # Replace any existing 127.0.1.1 entry
        sed -i "/^127\.0\.1\.1/d" /etc/hosts
        echo "127.0.1.1	$NEW_HOSTNAME" >> /etc/hosts

        # Also add localhost entries to be safe
        if ! grep -q "^127\.0\.0\.1.*localhost" /etc/hosts; then
            sed -i "1i127.0.0.1	localhost" /etc/hosts
        fi

        show_success "Hostname set to: $NEW_HOSTNAME (MAC: $MAC_ADDR, Interface: $INTERFACE)"
    else
        show_info "Hostname already set to: $NEW_HOSTNAME"
=======
    # CRITICAL FIX: Use more MAC address bytes for better uniqueness
    # Extract last 8 hex characters (last 4 bytes of MAC address)
    # This provides 65,536 unique combinations instead of just 256 with 4 chars
    MAC_LONG=$(echo "$MAC_ADDR" | tr -d ':' | tr '[:lower:]' '[:upper:]' | tail -c 9 | head -c 8)
    
    # ADDITIONAL UNIQUENESS: Add a random 2-character suffix based on current timestamp
    # This ensures absolute uniqueness even if MAC addresses are very similar
    TIMESTAMP_SUFFIX=$(date +%s | md5sum | cut -c1-2 | tr '[:lower:]' '[:upper:]')
    
    # Generate hostname: DigiSign-[8 chars from MAC]-[2 chars from timestamp]
    NEW_HOSTNAME="DigiSign-${MAC_LONG}-${TIMESTAMP_SUFFIX}"

    CURRENT_HOSTNAME=$(hostname)

    # ALWAYS set the new hostname on installation to ensure uniqueness
    # This is important even if it looks like it's already set, because we want
    # the timestamp component to be unique for each installation
    echo "Setting hostname to '$NEW_HOSTNAME'..."
    echo "  (Previous hostname: '$CURRENT_HOSTNAME')"

    # Set hostname immediately
    hostnamectl set-hostname "$NEW_HOSTNAME" 2>/dev/null || {
        # Fallback for systems without hostnamectl
        echo "$NEW_HOSTNAME" > /etc/hostname
        hostname "$NEW_HOSTNAME"
    }

    # Update /etc/hosts
    sed -i "s/127.0.1.1.*/127.0.1.1\t$NEW_HOSTNAME/g" /etc/hosts

    # Add entry if it doesn't exist
    if ! grep -q "127.0.1.1" /etc/hosts; then
        echo "127.0.1.1	$NEW_HOSTNAME" >> /etc/hosts
>>>>>>> f429e7570494b179c15ad29f8d641c5a29383e2b
    fi

    show_success "Hostname set to: $NEW_HOSTNAME (MAC: $MAC_ADDR)"
    show_info "Hostname includes timestamp for absolute uniqueness"
else
    # Fallback: Generate random hostname with timestamp
    RANDOM_SUFFIX=$(date +%s | md5sum | cut -c1-8 | tr '[:lower:]' '[:upper:]')
    NEW_HOSTNAME="DigiSign-${RANDOM_SUFFIX}"
    
    show_warning "Could not detect MAC address, using random hostname: $NEW_HOSTNAME"
    
    # Set the random hostname
    hostnamectl set-hostname "$NEW_HOSTNAME" 2>/dev/null || {
        echo "$NEW_HOSTNAME" > /etc/hostname
        hostname "$NEW_HOSTNAME"
    }
    
    # Update /etc/hosts
    sed -i "s/127.0.1.1.*/127.0.1.1\t$NEW_HOSTNAME/g" /etc/hosts
    if ! grep -q "127.0.1.1" /etc/hosts; then
        echo "127.0.1.1	$NEW_HOSTNAME" >> /etc/hosts
    fi
fi

echo ""

# Check for any existing installation artifacts and force cleanup
if [ -d "$INSTALL_DIR" ] || [ "$SERVICE_INSTALLED" = true ] || [ -d "$CONFIG_DIR" ]; then
    echo -e "${YELLOW}FORCED CLEAN INSTALL: removing previous installation and data${NC}"
    echo "  - Stop and disable the current service (if exists)"
    echo "  - Backup config.py to /tmp (if exists)"
    echo "  - Remove old installation directory: $INSTALL_DIR"
    echo "  - Remove old user data directory:   $CONFIG_DIR"
    echo "  - Reinstall service fresh"
    echo ""

    # Clean up old installation
    echo "Cleaning up old installation..."

    if systemctl is-active --quiet $SERVICE_NAME; then
        systemctl stop $SERVICE_NAME
        show_success "Service stopped"
    fi

    if systemctl is-enabled --quiet $SERVICE_NAME 2>/dev/null; then
        systemctl disable $SERVICE_NAME
        show_success "Service disabled"
    fi

    # Remove old service unit to ensure a fresh copy gets installed
    if [ -f "$SERVICE_FILE" ]; then
        rm -f "$SERVICE_FILE"
        systemctl daemon-reload || true
        show_success "Old service file removed"
    fi

    # Optional: backup legacy config.py from install dir before removal
    if [ -f "$INSTALL_DIR/config.py" ]; then
        BACKUP_FILE="/tmp/digitalsignage-config-backup-$(date +%s).py"
        cp "$INSTALL_DIR/config.py" "$BACKUP_FILE"
        show_success "Old config backed up to: $BACKUP_FILE"
    fi

    # Remove old install dir
    if [ -d "$INSTALL_DIR" ]; then
        rm -rf "$INSTALL_DIR"
        show_success "Old installation directory removed"
    fi

    # Remove user data/config/cache dir
    if [ -d "$CONFIG_DIR" ]; then
        rm -rf "$CONFIG_DIR"
        show_success "Old user data directory removed: $CONFIG_DIR"
    fi

    echo ""
fi

# Update code from repository (if in git repo)
echo "----------------------------------------"
echo "Updating code from repository..."
echo "----------------------------------------"
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
echo "----------------------------------------"
echo "  Installing Digital Signage Client"
echo "----------------------------------------"
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
echo "----------------------------------------"
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
    "boot_logo_manager.py"
    "shutdown_logo_display.py"
    "discovery.py"
    "device_manager.py"
    "display_renderer.py"
    "cache_manager.py"
    "watchdog_monitor.py"
    "status_screen.py"
    "web_interface.py"
    "burn_in_protection.py"
    "start-with-display.sh"
    "setup-splash-screen.sh"
    "debug-boot-logo.sh"
    "digisign-logo.png"
)

COPIED_COUNT=0
MISSING_FILES=()

# Disable exit on error for file copying (we handle errors manually)
set +e

for file in "${REQUIRED_FILES[@]}"; do
    if [ -f "$SCRIPT_DIR/$file" ]; then
        if cp "$SCRIPT_DIR/$file" "$INSTALL_DIR/" 2>/dev/null; then
            ((COPIED_COUNT++))
            echo "  ✓ Copied: $file"
        else
            echo "  ✗ Failed to copy: $file"
        fi
    else
        echo "  ⚠ Missing: $file"
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
    echo -e "${YELLOW}⚠ Warning: ${#MISSING_FILES[@]} files are missing:${NC}"
    for file in "${MISSING_FILES[@]}"; do
        echo "  - $file"
    done
    echo ""

    # Check if critical files are missing
    CRITICAL_MISSING=false
    for file in "client.py" "display_renderer.py" "config.py" "boot_logo_manager.py" "shutdown_logo_display.py"; do
        if [[ " ${MISSING_FILES[@]} " =~ " ${file} " ]]; then
            CRITICAL_MISSING=true
            break
        fi
    done

    if [ "$CRITICAL_MISSING" = true ]; then
        echo -e "${RED}✗ CRITICAL ERROR: Essential files are missing!${NC}"
        echo ""
        echo -e "${YELLOW}Please ensure you're running the installer from the correct location:${NC}"
        echo "  1. Clone repository to home directory: cd ~ && git clone https://github.com/manur84/digitalsignage.git"
        echo "  2. Run install.sh: cd digitalsignage/src/DigitalSignage.Client.RaspberryPi && sudo ./install.sh"
        echo ""
        exit 1
    else
        show_warning "Non-critical files missing - installation will continue"
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

# Copy widgets directory (required for display_renderer and status_screen)
if [ -d "$SCRIPT_DIR/widgets" ]; then
    mkdir -p "$INSTALL_DIR/widgets"
    # Copy Python files only, exclude __pycache__
    find "$SCRIPT_DIR/widgets" -maxdepth 1 -name "*.py" -exec cp {} "$INSTALL_DIR/widgets/" \; 2>/dev/null && \
        show_success "Widgets module copied" || \
        show_warning "Failed to copy widgets directory"
else
    show_warning "widgets directory not found (display may not work correctly)"
fi

# Copy renderers directory (for future element renderers)
if [ -d "$SCRIPT_DIR/renderers" ]; then
    mkdir -p "$INSTALL_DIR/renderers"
    # Copy Python files only, exclude __pycache__
    find "$SCRIPT_DIR/renderers" -maxdepth 1 -name "*.py" -exec cp {} "$INSTALL_DIR/renderers/" \; 2>/dev/null && \
        show_success "Renderers module copied" || \
        show_warning "Failed to copy renderers directory"
else
    show_warning "renderers directory not found (optional)"
fi

# Re-enable exit on error
set -e

# Create default config.json if it doesn't exist
if [ ! -f "$INSTALL_DIR/config.json" ]; then
    echo "Creating default config.json..."
    cat > "$INSTALL_DIR/config.json" <<'EOF'
{
  "client_id": "GENERATED_ON_FIRST_RUN",
  "server_host": "localhost",
  "server_port": 8080,
  "endpoint_path": "ws/",
  "registration_token": "",
  "use_ssl": false,
  "verify_ssl": true,
  "fullscreen": true,
  "log_level": "INFO",
  "cache_dir": "/home/INSTALL_USER/.digitalsignage/cache",
  "data_dir": "/home/INSTALL_USER/.digitalsignage/data",
  "auto_discover": true,
  "discovery_timeout": 5.0,
  "remote_logging_enabled": true,
  "remote_logging_level": "INFO",
  "remote_logging_batch_size": 50,
  "remote_logging_batch_interval": 5.0,
  "show_cached_layout_on_disconnect": true,
  "burn_in_protection_enabled": true,
  "burn_in_pixel_shift_interval": 300,
  "burn_in_pixel_shift_max": 5,
  "burn_in_screensaver_timeout": 3600
}
EOF
    # Replace INSTALL_USER placeholder with actual user
    sed -i "s/INSTALL_USER/$ACTUAL_USER/g" "$INSTALL_DIR/config.json"

    # Replace GENERATED_ON_FIRST_RUN with the hostname
    CURRENT_HOSTNAME=$(hostname)
    sed -i "s/GENERATED_ON_FIRST_RUN/$CURRENT_HOSTNAME/g" "$INSTALL_DIR/config.json"

    # CRITICAL: Set permissions to 666 (rw-rw-rw-) so web interface can write
    chmod 666 "$INSTALL_DIR/config.json"

    show_success "Default config.json created (client_id: $CURRENT_HOSTNAME, permissions: 666)"
else
    # CRITICAL: Fix permissions on existing config.json if needed
    CURRENT_PERMS=$(stat -c '%a' "$INSTALL_DIR/config.json" 2>/dev/null || echo "000")
    if [ "$CURRENT_PERMS" != "666" ]; then
        chmod 666 "$INSTALL_DIR/config.json"
        show_success "Fixed config.json permissions: 666 (was: $CURRENT_PERMS)"
    else
        show_info "config.json permissions already correct: 666"
    fi
fi

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
echo "----------------------------------------"
    exit 1
fi

show_success "All required files present and executable"

# [8/10] Create config directory
show_step "Creating config directory..."
mkdir -p "$CONFIG_DIR/cache"
mkdir -p "$CONFIG_DIR/logs"
chown -R "$ACTUAL_USER:$ACTUAL_USER" "$CONFIG_DIR"
show_success "Config directory created: $CONFIG_DIR"

# CRITICAL FIX: Ensure .Xauthority exists and has correct permissions
show_info "Setting up X11 authorization..."
XAUTH_FILE="$USER_HOME/.Xauthority"

# Create .Xauthority if it doesn't exist
if [ ! -f "$XAUTH_FILE" ]; then
    # Create empty .Xauthority file
    touch "$XAUTH_FILE"
    chown "$ACTUAL_USER:$ACTUAL_USER" "$XAUTH_FILE"
    chmod 600 "$XAUTH_FILE"
    show_success "Created .Xauthority file: $XAUTH_FILE"

    # Try to add localhost authorization with mcookie
    if command -v xauth &>/dev/null && command -v mcookie &>/dev/null; then
        # Generate a new magic cookie for X11 authorization
        MAGIC_COOKIE=$(mcookie)
        # Add authorization for display :0
        sudo -u "$ACTUAL_USER" XAUTHORITY="$XAUTH_FILE" xauth add :0 . "$MAGIC_COOKIE" 2>/dev/null || true
        sudo -u "$ACTUAL_USER" XAUTHORITY="$XAUTH_FILE" xauth add localhost:0 . "$MAGIC_COOKIE" 2>/dev/null || true
        sudo -u "$ACTUAL_USER" XAUTHORITY="$XAUTH_FILE" xauth add $(hostname):0 . "$MAGIC_COOKIE" 2>/dev/null || true
        show_success "Added X11 authorization entries"
    fi
else
    # Verify permissions on existing .Xauthority
    XAUTH_OWNER=$(stat -c '%U' "$XAUTH_FILE" 2>/dev/null || stat -f '%Su' "$XAUTH_FILE" 2>/dev/null || echo "unknown")
    if [ "$XAUTH_OWNER" != "$ACTUAL_USER" ]; then
        chown "$ACTUAL_USER:$ACTUAL_USER" "$XAUTH_FILE"
        show_success "Fixed .Xauthority ownership: $ACTUAL_USER"
    fi

    XAUTH_PERMS=$(stat -c '%a' "$XAUTH_FILE" 2>/dev/null || stat -f '%Lp' "$XAUTH_FILE" 2>/dev/null || echo "000")
    if [ "$XAUTH_PERMS" != "600" ]; then
        chmod 600 "$XAUTH_FILE"
        show_success "Fixed .Xauthority permissions: 600"
    fi

    show_success ".Xauthority verified: $XAUTH_FILE"
fi

# CRITICAL: Enable X11 local connections using xhost
show_info "Enabling X11 local connections..."
if command -v xhost &>/dev/null; then
    # Try to enable local connections as the actual user
    if sudo -u "$ACTUAL_USER" DISPLAY=:0 XAUTHORITY="$XAUTH_FILE" xhost +local: &>/dev/null 2>&1; then
        show_success "X11 local connections enabled"
    else
        # Try without specifying display
        if sudo -u "$ACTUAL_USER" xhost +local: &>/dev/null 2>&1; then
            show_success "X11 local connections enabled (default display)"
        else
            show_warning "Could not enable X11 local connections (will retry during startup)"
        fi
    fi
else
    show_warning "xhost not found - X11 authorization may be limited"
fi

# Update client_id in config.json with current hostname
# CRITICAL: This runs AFTER config.json is created, ensuring the client_id is ALWAYS unique
# The client_id is set to match the hostname which includes MAC address + timestamp
if [ -f "$INSTALL_DIR/config.json" ]; then
    CURRENT_HOSTNAME=$(hostname)
    
    # CRITICAL FIX: ALWAYS update client_id to match the newly generated hostname
    # This ensures each installation gets a unique client_id, even on re-installation
    # The hostname was just set above with MAC address + timestamp, so it's guaranteed unique
    CURRENT_CLIENT_ID=$(grep '"client_id"' "$INSTALL_DIR/config.json" | cut -d'"' -f4 2>/dev/null || echo "NONE")
    
    if [ "$CURRENT_CLIENT_ID" != "$CURRENT_HOSTNAME" ]; then
        sed -i "s/\"client_id\": \".*\"/\"client_id\": \"$CURRENT_HOSTNAME\"/g" "$INSTALL_DIR/config.json"
        show_success "Client ID set to: $CURRENT_HOSTNAME (previous: $CURRENT_CLIENT_ID)"
        show_info "Client ID is unique based on MAC address + timestamp"
    else
        show_info "Client ID already set to: $CURRENT_HOSTNAME"
    fi
fi

# Configure splash screen (disable default and set branded logo)
show_step "Configuring Plymouth splash screen..."

# Auto-detect boot directory (Raspberry Pi OS changed location in newer versions)
BOOT_DIR="/boot/firmware"
if [ ! -d "$BOOT_DIR" ] || [ ! -w "$BOOT_DIR" ]; then
    BOOT_DIR="/boot"
fi

show_info "Using boot directory: $BOOT_DIR"

# Ensure Plymouth is installed
if ! command -v plymouth &>/dev/null; then
    show_info "Installing Plymouth..."
    apt-get install -y -qq plymouth plymouth-themes
    show_success "Plymouth installed"
else
    show_info "Plymouth already installed"
fi

# CRITICAL: Configure boot parameters for Plymouth
# These parameters MUST be in place for Plymouth to work correctly
CMDLINE_FILE="$BOOT_DIR/cmdline.txt"
CONFIG_TXT="$BOOT_DIR/config.txt"

# Step 1: Update cmdline.txt with Plymouth parameters
if [ -f "$CMDLINE_FILE" ]; then
    show_info "Configuring kernel boot parameters in cmdline.txt..."

    # Backup original cmdline.txt
    if [ ! -f "${CMDLINE_FILE}.backup-digitalsignage" ]; then
        cp "$CMDLINE_FILE" "${CMDLINE_FILE}.backup-digitalsignage"
        show_success "Backed up original cmdline.txt"
    fi

    # Read current cmdline (single line)
    # Convert newlines and tabs to spaces, then collapse multiple spaces
    CURRENT_CMDLINE=$(tr '\n\t' '  ' < "$CMDLINE_FILE" | tr -s ' ' | xargs)

    # CRITICAL: Remove console=tty1 or replace with console=tty3 to hide boot messages
    # This prevents boot messages from appearing on the main display
    if echo "$CURRENT_CMDLINE" | grep -qw "console=tty1"; then
        CURRENT_CMDLINE=$(echo "$CURRENT_CMDLINE" | sed 's/console=tty1/console=tty3/g')
        show_success "Redirected console to tty3 (hides boot messages on display)"
    fi

    # Parameters to add (if not already present)
    # CRITICAL ORDER: 'quiet' MUST come before 'loglevel=3'
    # quiet sets kernel log level to 4 (warnings), then loglevel=3 overrides it to errors only
    PLYMOUTH_PARAMS=(
        "quiet"                           # MUST BE FIRST: Sets kernel log level to warnings (4)
        "loglevel=3"                      # MUST BE AFTER quiet: Override to errors only (3)
        "splash"                          # Enable Plymouth splash screen
        "plymouth.ignore-serial-consoles" # Prevent Plymouth from showing on serial consoles
        "plymouth.nolog"                  # Disable Plymouth logging to console (prevents log breakthrough)
        "logo.nologo"                     # Remove Raspberry Pi logo overlay
        "vt.global_cursor_default=0"      # Hide blinking text cursor
        "rd.udev.log_level=3"             # Suppress initramfs udev messages (errors only)
        "udev.log_level=3"                # Suppress regular udev messages (errors only)
        "consoleblank=0"                  # Disable automatic screen blanking
        "fbcon=map:0"                     # Use default framebuffer (fb0/HDMI)
    )

    # Add missing parameters
    CMDLINE_MODIFIED=false
    for param in "${PLYMOUTH_PARAMS[@]}"; do
        # Special handling for parameters with values (e.g., loglevel=3)
        param_name=$(echo "$param" | cut -d'=' -f1)

        # Use space-padded matching for more reliable detection
        # This prevents false positives and ensures we match complete parameters
        if echo " $CURRENT_CMDLINE " | grep -q " $param "; then
            # Exact parameter already present (including value if any)
            echo "  ✓ Already present: $param"
        elif echo " $CURRENT_CMDLINE " | grep -q " ${param_name}="; then
            # Parameter exists but with different value - update it
            CURRENT_CMDLINE=$(echo "$CURRENT_CMDLINE" | sed "s/\<${param_name}=[^ ]*/${param}/g")
            CMDLINE_MODIFIED=true
            echo "  ↻ Updated: $param"
        else
            # Parameter doesn't exist, add it
            CURRENT_CMDLINE="$CURRENT_CMDLINE $param"
            CMDLINE_MODIFIED=true
            echo "  + Adding: $param"
        fi
    done

    # Write updated cmdline.txt if modified
    if [ "$CMDLINE_MODIFIED" = true ]; then
        # Trim leading/trailing spaces, ensure single line, no trailing newline
        TRIMMED_CMDLINE=$(echo "$CURRENT_CMDLINE" | xargs)
        printf "%s" "$TRIMMED_CMDLINE" > "$CMDLINE_FILE"
        show_success "Updated cmdline.txt with Plymouth parameters"

        # Verify the write was successful and no duplicates exist
        VERIFY_CMDLINE=$(cat "$CMDLINE_FILE")
        for param in "${PLYMOUTH_PARAMS[@]}"; do
            count=$(echo " $VERIFY_CMDLINE " | grep -o " $param " | wc -l)
            if [ "$count" -gt 1 ]; then
                show_warning "Duplicate detected for '$param' - this shouldn't happen!"
            fi
        done
    else
        show_info "cmdline.txt already configured"
    fi
else
    show_warning "cmdline.txt not found at $CMDLINE_FILE - Plymouth may not work correctly"
fi

# Step 2: Update config.txt for Plymouth
if [ -f "$CONFIG_TXT" ]; then
    show_info "Configuring boot config in config.txt..."

    # Backup original config.txt
    if [ ! -f "${CONFIG_TXT}.backup-digitalsignage" ]; then
        cp "$CONFIG_TXT" "${CONFIG_TXT}.backup-digitalsignage"
        show_success "Backed up original config.txt"
    fi

    # CRITICAL: Add disable_splash=1 to DISABLE the rainbow splash screen
    # This removes the colorful boot splash that appears before Plymouth logo
    if ! grep -Eq '^disable_splash=1' "$CONFIG_TXT"; then
        echo "" >> "$CONFIG_TXT"
        echo "# Digital Signage: Disable rainbow splash screen" >> "$CONFIG_TXT"
        echo "disable_splash=1" >> "$CONFIG_TXT"
        show_success "Added 'disable_splash=1' to config.txt (disables rainbow splash)"
    else
        show_info "disable_splash=1 already present (rainbow splash disabled)"
    fi

    # CRITICAL: Enable auto_initramfs (required for Plymouth on newer Raspberry Pi OS)
    # This tells the bootloader to automatically load initramfs files
    if ! grep -Eq '^auto_initramfs=1' "$CONFIG_TXT"; then
        echo "" >> "$CONFIG_TXT"
        echo "# Digital Signage: Enable automatic initramfs loading (required for Plymouth)" >> "$CONFIG_TXT"
        echo "auto_initramfs=1" >> "$CONFIG_TXT"
        show_success "Enabled auto_initramfs=1 in config.txt"
    else
        show_info "auto_initramfs already enabled"
    fi

    # Add dtoverlay for better boot performance (optional but recommended)
    if ! grep -Eq '^dtoverlay=vc4-fkms-v3d' "$CONFIG_TXT" && ! grep -Eq '^dtoverlay=vc4-kms-v3d' "$CONFIG_TXT"; then
        echo "" >> "$CONFIG_TXT"
        echo "# Digital Signage: Enable KMS graphics driver for better boot logo display" >> "$CONFIG_TXT"
        echo "dtoverlay=vc4-fkms-v3d" >> "$CONFIG_TXT"
        show_success "Enabled KMS graphics driver (vc4-fkms-v3d)"
    else
        show_info "KMS graphics driver already enabled"
    fi

    show_success "config.txt configured for Plymouth"
else
    show_warning "config.txt not found at $CONFIG_TXT - Plymouth may not work correctly"
fi

# Step 2.5: Configure /etc/rc.local to disable dmesg console output
# This prevents kernel messages from breaking through Plymouth splash
show_info "Configuring /etc/rc.local to suppress dmesg console output..."

RC_LOCAL="/etc/rc.local"
DMESG_CMD="dmesg --console-off"

# Ensure rc.local exists
if [ ! -f "$RC_LOCAL" ]; then
    cat > "$RC_LOCAL" <<'EOF'
#!/bin/sh -e
#
# rc.local
#
# This script is executed at the end of each multiuser runlevel.
# Make sure that the script will "exit 0" on success or any other
# value on error.

exit 0
EOF
    chmod +x "$RC_LOCAL"
    show_success "Created /etc/rc.local"
fi

# Add dmesg --console-off if not already present
if ! grep -q "$DMESG_CMD" "$RC_LOCAL"; then
    # Check if file ends with 'exit 0'
    if grep -q "^exit 0$" "$RC_LOCAL"; then
        # Insert dmesg command before 'exit 0'
        sed -i "/^exit 0$/i # Digital Signage: Prevent kernel messages from breaking through Plymouth\\n$DMESG_CMD || true\\n" "$RC_LOCAL"
        show_success "Added 'dmesg --console-off' to /etc/rc.local (prevents kernel messages breakthrough)"
    else
        # File doesn't end with 'exit 0', append at end
        echo "" >> "$RC_LOCAL"
        echo "# Digital Signage: Prevent kernel messages from breaking through Plymouth" >> "$RC_LOCAL"
        echo "$DMESG_CMD || true" >> "$RC_LOCAL"
        echo "exit 0" >> "$RC_LOCAL"
        show_success "Added 'dmesg --console-off' and 'exit 0' to /etc/rc.local"
    fi
else
    show_info "dmesg --console-off already configured in /etc/rc.local"
fi

# Step 3: Run Plymouth splash screen setup script
# CRITICAL FIX: Run splash screen setup BEFORE starting the service
# This ensures the logo is embedded in initramfs and shows on boot
if [ -f "$INSTALL_DIR/setup-splash-screen.sh" ] && [ -f "$INSTALL_DIR/digisign-logo.png" ]; then
    echo ""
    echo "Setting up Plymouth boot splash screen with Digital Signage logo..."
    chmod +x "$INSTALL_DIR/setup-splash-screen.sh" 2>/dev/null || true

    # Run splash screen setup script
    # Note: initramfs rebuild is handled inside setup-splash-screen.sh
    if bash "$INSTALL_DIR/setup-splash-screen.sh" "$INSTALL_DIR/digisign-logo.png" 2>&1 | tee -a /tmp/splash-setup.log; then
        show_success "Plymouth splash screen configured (includes initramfs rebuild)"
    else
        show_warning "Splash screen setup failed - check /tmp/splash-setup.log for details"
        show_info "Boot will continue with default splash screen"
    fi
else
    if [ ! -f "$INSTALL_DIR/setup-splash-screen.sh" ]; then
        show_warning "setup-splash-screen.sh not found - skipping Plymouth setup"
    fi
    if [ ! -f "$INSTALL_DIR/digisign-logo.png" ]; then
        show_warning "digisign-logo.png not found - skipping Plymouth setup"
    fi
    show_info "Plymouth splash screen not configured (optional feature)"
fi

# Verify Plymouth configuration
echo ""
echo "Verifying Plymouth Configuration..."
echo "----------------------------------------"

# 1. Check framebuffer configuration
if [ -f "/etc/initramfs-tools/conf.d/splash" ] && grep -q "FRAMEBUFFER=y" /etc/initramfs-tools/conf.d/splash 2>/dev/null; then
    show_success "Framebuffer support enabled"
else
    show_info "Framebuffer configuration: not enabled (may affect Plymouth rendering)"
fi

# 2. Check initramfs size
KERNEL_VERSION=$(uname -r)
INITRAMFS_PATH="$BOOT_DIR/initramfs"

if [ -f "$INITRAMFS_PATH" ]; then
    INITRAMFS_SIZE=$(du -h "$INITRAMFS_PATH" | cut -f1)
    show_success "Initramfs found: $INITRAMFS_PATH ($INITRAMFS_SIZE)"
else
    show_info "Initramfs not found at standard location (may be embedded in kernel)"
fi

# 3. Plymouth configuration summary
if command -v plymouth &>/dev/null; then
    PLYMOUTH_THEME=$(plymouth-set-default-theme 2>/dev/null || echo "unknown")
    show_success "Plymouth theme: $PLYMOUTH_THEME"

    if [ -f "$INSTALL_DIR/digisign-logo.png" ]; then
        LOGO_SIZE=$(du -h "$INSTALL_DIR/digisign-logo.png" | cut -f1)
        show_success "Boot logo: digisign-logo.png ($LOGO_SIZE)"
    fi
else
    show_info "Plymouth not installed"
fi

echo ""

# [9/10] Install systemd services
show_step "Installing systemd services..."

# Main client service
if [ -f "$SCRIPT_DIR/digitalsignage-client.service" ]; then
    sed "s/INSTALL_USER/$ACTUAL_USER/g" "$SCRIPT_DIR/digitalsignage-client.service" | \
    sed "s|/usr/bin/python3|$VENV_DIR/bin/python3|g" > /tmp/digitalsignage-client.service

    cp /tmp/digitalsignage-client.service "$SERVICE_FILE"
    rm /tmp/digitalsignage-client.service
    show_success "Service file installed: $SERVICE_FILE"
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

# Shutdown logo service (for graceful shutdown with branded logo)
SHUTDOWN_SERVICE_FILE="/etc/systemd/system/digitalsignage-client-shutdown.service"
if [ -f "$SCRIPT_DIR/digitalsignage-client-shutdown.service" ]; then
    sed "s/INSTALL_USER/$ACTUAL_USER/g" "$SCRIPT_DIR/digitalsignage-client-shutdown.service" | \
    sed "s|/usr/bin/python3|$VENV_DIR/bin/python3|g" > /tmp/digitalsignage-client-shutdown.service

    cp /tmp/digitalsignage-client-shutdown.service "$SHUTDOWN_SERVICE_FILE"
    rm /tmp/digitalsignage-client-shutdown.service
    show_success "Shutdown service file installed: $SHUTDOWN_SERVICE_FILE"
else
    show_info "digitalsignage-client-shutdown.service not found (optional)"
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
echo "----------------------------------------"
echo "  Verifying Installation"
echo "----------------------------------------"
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
echo "----------------------------------------"
echo "  Pre-Flight Check"
echo "----------------------------------------"
echo ""
echo "Testing client startup before enabling service..."
echo ""

# Set proper environment for test
export DISPLAY=:0
export XAUTHORITY="$USER_HOME/.Xauthority"

# Create a test wrapper script that sets up environment properly
cat > /tmp/test-client.sh <<EOF
#!/bin/bash
export DISPLAY=:0
export XAUTHORITY="$USER_HOME/.Xauthority"
export HOME="$USER_HOME"
export USER="$ACTUAL_USER"

# Enable X11 local connections before test
if command -v xhost &>/dev/null; then
    xhost +local: &>/dev/null 2>&1 || true
fi

# Run the actual test
exec "$INSTALL_DIR/start-with-display.sh" --test
EOF
chmod +x /tmp/test-client.sh

# Run the test with proper environment
if timeout 30 sudo -u "$ACTUAL_USER" /tmp/test-client.sh; then
    echo ""
    show_success "Pre-flight check successful!"
    rm -f /tmp/test-client.sh
else
    TEST_EXIT_CODE=$?
    echo ""
    if [ $TEST_EXIT_CODE -eq 124 ]; then
        show_warning "Pre-flight test timed out (continuing installation)"
        rm -f /tmp/test-client.sh
    elif [ $TEST_EXIT_CODE -eq 134 ]; then
        show_warning "Qt initialization failed (X11 display issue)"
        echo ""
        echo -e "${YELLOW}This is often caused by:${NC}"
        echo "  1. X11 server not running (normal for headless systems)"
        echo "  2. X11 authorization issues"
        echo "  3. Missing display configuration"
        echo ""
        echo -e "${BLUE}The installation will continue. The service will use Xvfb as fallback.${NC}"
        echo ""
        rm -f /tmp/test-client.sh
        # Don't exit on Qt errors - the service can use Xvfb
    else
        show_warning "Pre-flight test failed with exit code $TEST_EXIT_CODE"
        echo ""
        echo "Check startup log: sudo cat /var/log/digitalsignage-client-startup.log"
        echo ""
        echo "After fixing issues, re-run: sudo ./install.sh"
        rm -f /tmp/test-client.sh
        exit 1
    fi
fi

# Client Configuration
echo ""
echo "----------------------------------------"
echo "  Client Configuration"
echo "----------------------------------------"
echo ""

# Check if config.json exists and configure it
if [ -f "$INSTALL_DIR/config.json" ]; then
    if [ "$NON_INTERACTIVE" = "1" ]; then
        echo -e "${YELLOW}Non-interactive: skipping server IP/token prompts (configure later)${NC}"
        SERVER_IP=""
        REG_TOKEN=""
    else
        echo -e "${YELLOW}Configure Digital Signage Client:${NC}"
        echo ""
        echo "The client needs to know where to find the server."
        echo ""
        read -p "Enter server IP address (e.g., 192.168.0.100): " SERVER_IP
        read -p "Enter registration token (from server): " REG_TOKEN
    fi

    if [ -n "$SERVER_IP" ] && [ -n "$REG_TOKEN" ]; then
        # Update config.json
        python3 << EOF
import json
with open("$INSTALL_DIR/config.json", "r") as f:
    config = json.load(f)
config["server_host"] = "$SERVER_IP"
config["registration_token"] = "$REG_TOKEN"
config["auto_discover"] = True
with open("$INSTALL_DIR/config.json", "w") as f:
    json.dump(config, f, indent=2)
EOF
        show_success "Configuration saved"
    else
        show_warning "Configuration skipped"
        echo "You can configure manually: sudo nano $INSTALL_DIR/config.json"
    fi
    echo ""
fi

# Start service
echo ""
echo "----------------------------------------"
echo "  Starting Service"
echo "----------------------------------------"
echo ""

systemctl daemon-reload
systemctl enable $SERVICE_NAME 2>/dev/null || true
systemctl restart $SERVICE_NAME 2>/dev/null || true
show_success "Service enabled and started"
show_info "Client will auto-start via systemd (see systemctl status $SERVICE_NAME)"

# Display Configuration (only for fresh install)
echo ""
echo "----------------------------------------"
echo "  Display Configuration"
echo "----------------------------------------"
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

    # CRITICAL FIX: Use Console Autologin (B2) for proper kiosk mode
    # B2 = Console autologin - boots to text console, auto-logged in
    # Then .bash_profile automatically starts X11 (see lines 1182-1192)
    # This approach bypasses LightDM entirely and gives full control
    echo "Configuring console autologin (B2 mode) for kiosk setup..."

    if command -v raspi-config &>/dev/null; then
        CURRENT_BOOT=$(raspi-config nonint get_boot_behaviour 2>/dev/null || echo "unknown")
        if [ "$CURRENT_BOOT" != "B2" ]; then
            echo "Setting boot mode to console autologin (B2)..."
            raspi-config nonint do_boot_behaviour B2 2>/dev/null
            show_success "Console auto-login enabled (B2 mode)"
            NEEDS_REBOOT=true
        else
            show_info "Console auto-login already enabled (B2 mode)"
        fi
    else
        show_warning "raspi-config not found - using manual configuration"
    fi

    # CRITICAL: Manual getty autologin configuration (in case raspi-config fails or is not available)
    # This creates the systemd drop-in file for getty@tty1.service
    GETTY_OVERRIDE_DIR="/etc/systemd/system/getty@tty1.service.d"
    GETTY_OVERRIDE_FILE="$GETTY_OVERRIDE_DIR/autologin.conf"

    echo "Configuring getty autologin for tty1..."
    mkdir -p "$GETTY_OVERRIDE_DIR"

    cat > "$GETTY_OVERRIDE_FILE" <<EOF
# Digital Signage - Console Autologin Configuration
# This file enables automatic login on tty1 without password prompt
[Service]
ExecStart=
ExecStart=-/sbin/agetty --autologin $ACTUAL_USER --noclear %I \$TERM
Type=idle
EOF

    show_success "Getty autologin configured for tty1 (user: $ACTUAL_USER)"
    NEEDS_REBOOT=true

    # Reload systemd to apply getty changes
    systemctl daemon-reload
    show_success "Systemd configuration reloaded"

    # OPTIONAL: Disable LightDM (not needed for console-based kiosk)
    # This prevents conflicts and ensures clean boot to console
    if systemctl is-enabled lightdm.service 2>/dev/null; then
        echo "Disabling LightDM (not needed for console-based kiosk)..."
        systemctl disable lightdm.service 2>/dev/null || true
        show_success "LightDM disabled (console-only boot)"
        NEEDS_REBOOT=true
    else
        show_info "LightDM not enabled (console boot already configured)"
    fi

    # NOTE: LXDE/Desktop configuration is no longer needed
    # We're using console boot (B2) + auto-startx via .bash_profile
    # This bypasses LightDM and LXDE entirely for cleaner kiosk mode
    show_info "Skipping LXDE configuration (console-based boot, no desktop environment)"

    # Update $BOOT_DIR/config.txt with detected HDMI modes
    if [ -f "$INSTALL_DIR/config_txt_manager.py" ]; then
        echo ""
        echo "Updating $BOOT_DIR/config.txt with detected display resolutions..."
        if python3 "$INSTALL_DIR/config_txt_manager.py"; then
            show_success "config.txt updated with detected modes"
        else
            show_warning "Could not update config.txt automatically. Run manually: sudo python3 $INSTALL_DIR/config_txt_manager.py"
        fi

        # Setup custom boot logo (black splash screen)
        echo ""
        echo "Setting up custom boot logo..."
        python3 -c "from config_txt_manager import setup_custom_boot_logo; setup_custom_boot_logo()" 2>/dev/null || {
            show_warning "Could not setup custom boot logo"
        }
    else
        show_warning "config_txt_manager.py not found; skipping config.txt update"
    fi

    # CRITICAL: Configure .xinitrc for X11 startup
    # This file is executed when startx is called from .bash_profile
    # It starts our Digital Signage client as the only X11 application
    echo "Configuring .xinitrc for X11 startup..."
    cat > "$USER_HOME/.xinitrc" <<'EOF'
#!/bin/sh
# Digital Signage Client - X11 Startup Configuration
# This file is executed by startx and replaces desktop environment

# CRITICAL FIX: Allow local X11 connections (solves authorization issues)
# This must be done early in X11 startup
if command -v xhost &>/dev/null; then
    xhost +local: &>/dev/null
fi

# Disable screen blanking and power management
xset -dpms
xset s off
xset s noblank

# Set black background (clean look during startup)
xsetroot -solid black

# Hide mouse cursor immediately
unclutter -idle 0.1 -root &

# Wait briefly for X11 to fully initialize
sleep 2

# Start Digital Signage client as the ONLY X11 application
# This will run fullscreen and be the only thing visible
exec /opt/digitalsignage-client/start-with-display.sh
EOF
    chown "$ACTUAL_USER:$ACTUAL_USER" "$USER_HOME/.xinitrc"
    chmod +x "$USER_HOME/.xinitrc"
    show_success ".xinitrc created (X11 startup configuration)"

    # CRITICAL: Configure .bash_profile for automatic X11 startup on tty1
    # This ensures X11 starts automatically after console autologin
    echo "Configuring .bash_profile for auto-startx..."

    # Remove any existing auto-startx configuration to avoid duplicates
    if [ -f "$USER_HOME/.bash_profile" ]; then
        sed -i '/# Auto-start X11 on tty1 login for Digital Signage/,/fi/d' "$USER_HOME/.bash_profile"
    fi

    # Append auto-startx configuration
    cat >> "$USER_HOME/.bash_profile" <<'EOF'

# Digital Signage - Auto-start X11 on tty1 login
# This starts X11 automatically after console autologin
# Only runs on tty1 and only if X11 is not already running
if [ -z "$DISPLAY" ] && [ "$(tty)" = "/dev/tty1" ]; then
    # Log startup for debugging
    echo "Digital Signage: Starting X11..." | tee -a /tmp/digitalsignage-boot.log

    # Start X11 with our .xinitrc configuration
    # exec replaces the shell with startx, preventing terminal from showing
    exec startx -- -nocursor
fi
EOF
    chown "$ACTUAL_USER:$ACTUAL_USER" "$USER_HOME/.bash_profile"
    show_success ".bash_profile configured (auto-start X11 on tty1)"

    # ADDITIONAL: Configure .profile as fallback (some systems use .profile instead)
    if [ ! -f "$USER_HOME/.profile" ]; then
        cat > "$USER_HOME/.profile" <<'EOF'
# Digital Signage - Load .bash_profile if it exists
if [ -f "$HOME/.bash_profile" ]; then
    . "$HOME/.bash_profile"
fi
EOF
        chown "$ACTUAL_USER:$ACTUAL_USER" "$USER_HOME/.profile"
        show_success ".profile created (fallback configuration)"
    fi

    echo ""
    if [ "$NEEDS_REBOOT" = true ]; then
        echo -e "${YELLOW}IMPORTANT: Reboot required${NC}"
        echo ""

        # Detect Raspberry Pi OS Bookworm for Plymouth-specific message
        if [ -f /etc/os-release ]; then
            OS_VERSION=$(grep VERSION_CODENAME /etc/os-release | cut -d'=' -f2 || echo "unknown")
            if [ "$OS_VERSION" = "bookworm" ]; then
                echo -e "${BLUE}ℹ Raspberry Pi OS Bookworm detected${NC}"
                echo "  Plymouth boot logo requires a reboot to take effect."
                echo "  After reboot, you'll see the Digital Signage logo during boot."
                echo ""
            fi
        fi

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

# Final summary
echo ""
echo "----------------------------------------"
echo -e "${GREEN}  INSTALLATION COMPLETE!${NC}"
echo "----------------------------------------"
echo ""
echo "Installation Paths:"
echo "  Installation: $INSTALL_DIR"
echo "  Virtual env:  $VENV_DIR"
echo "  Config:       $CONFIG_DIR"
echo "  Service:      $SERVICE_FILE"
echo ""
echo "Next Steps:"
echo "  1. Edit config:   sudo nano $INSTALL_DIR/config.py"
echo "  2. Set server_host, server_port, registration_token"
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
echo ""

