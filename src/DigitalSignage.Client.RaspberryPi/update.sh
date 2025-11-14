#!/bin/bash

echo "========================================="
echo "Digital Signage Client - Update Script"
echo "========================================="
echo ""

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if running as root (for systemctl commands)
if [ "$EUID" -ne 0 ]; then
    echo -e "${YELLOW}Note: This script requires sudo for some operations${NC}"
    echo ""
fi

# Stop service
echo -e "${YELLOW}[1/7] Stopping digitalsignage-client service...${NC}"
sudo systemctl stop digitalsignage-client
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Service stopped${NC}"
else
    echo -e "${RED}✗ Failed to stop service${NC}"
    exit 1
fi
echo ""

# Pull latest changes
echo -e "${YELLOW}[2/7] Pulling latest changes from git...${NC}"
cd /opt/digitalsignage-client

# Show current branch
CURRENT_BRANCH=$(git branch --show-current)
echo "Current branch: $CURRENT_BRANCH"

# Pull changes
git pull
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Git pull successful${NC}"
else
    echo -e "${RED}✗ Git pull failed${NC}"
    echo "Attempting to restart service with current code..."
    sudo systemctl start digitalsignage-client
    exit 1
fi
echo ""

# Show what changed
echo -e "${YELLOW}[3/7] Recent changes:${NC}"
git log -3 --oneline
echo ""

# Copy updated Python files from repository
echo -e "${YELLOW}[4/7] Copying updated Python files...${NC}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_DIR="/opt/digitalsignage-client"

# List of Python client files that must be present
REQUIRED_FILES=(
    "client.py"
    "config.py"
    "discovery.py"
    "device_manager.py"
    "display_renderer.py"
    "cache_manager.py"
    "watchdog_monitor.py"
    "status_screen.py"
    "web_interface.py"
    "start-with-display.sh"
)

# Copy all required files
COPIED_COUNT=0
FAILED_FILES=()

for file in "${REQUIRED_FILES[@]}"; do
    if [ -f "$SCRIPT_DIR/$file" ]; then
        cp "$SCRIPT_DIR/$file" "$INSTALL_DIR/"
        if [ $? -eq 0 ]; then
            ((COPIED_COUNT++))
            echo "  ✓ Copied $file"
        else
            echo -e "  ${RED}✗ Failed to copy $file${NC}"
            FAILED_FILES+=("$file")
        fi
    else
        echo -e "  ${YELLOW}⚠ $file not found in repository${NC}"
    fi
done

# Copy optional files (don't fail if missing)
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
        if [ $? -eq 0 ]; then
            ((COPIED_COUNT++))
        fi
    fi
done

echo -e "${GREEN}✓ Copied $COPIED_COUNT files${NC}"

if [ ${#FAILED_FILES[@]} -gt 0 ]; then
    echo -e "${RED}✗ Failed to copy: ${FAILED_FILES[*]}${NC}"
    echo "Installation may be incomplete"
    exit 1
fi

# Make scripts executable
chmod +x "$INSTALL_DIR/client.py" 2>/dev/null
chmod +x "$INSTALL_DIR/start-with-display.sh" 2>/dev/null
chmod +x "$INSTALL_DIR/wait-for-x11.sh" 2>/dev/null
chmod +x "$INSTALL_DIR/diagnose.sh" 2>/dev/null
chmod +x "$INSTALL_DIR/fix-installation.sh" 2>/dev/null
chmod +x "$INSTALL_DIR/enable-autologin-x11.sh" 2>/dev/null
chmod +x "$INSTALL_DIR/check-autostart.sh" 2>/dev/null

echo ""

# Update Python dependencies if requirements.txt changed
echo -e "${YELLOW}[5/7] Checking for dependency updates...${NC}"
if [ -f "requirements.txt" ]; then
    if git diff HEAD@{1} HEAD --name-only 2>/dev/null | grep -q "requirements.txt"; then
        echo "requirements.txt changed, updating dependencies..."
        /opt/digitalsignage-client/venv/bin/pip install -r requirements.txt
        if [ $? -eq 0 ]; then
            echo -e "${GREEN}✓ Dependencies updated${NC}"
        else
            echo -e "${RED}✗ Failed to update dependencies${NC}"
            echo "Please run manually: /opt/digitalsignage-client/venv/bin/pip install -r requirements.txt"
        fi
    else
        echo "No dependency changes detected"
    fi
else
    echo -e "${YELLOW}Warning: requirements.txt not found${NC}"
fi
echo ""

# Update systemd service file
echo -e "${YELLOW}[6/8] Updating systemd service...${NC}"

# Detect the user who is running the service
ACTUAL_USER="${SUDO_USER:-$USER}"
if [ "$ACTUAL_USER" = "root" ]; then
    # If running as root without sudo, try to detect from service file
    ACTUAL_USER=$(grep "^User=" /etc/systemd/system/digitalsignage-client.service 2>/dev/null | cut -d= -f2)
    if [ -z "$ACTUAL_USER" ]; then
        echo -e "${YELLOW}Warning: Could not detect user, using 'pi' as default${NC}"
        ACTUAL_USER="pi"
    fi
fi

echo "Service will run as user: $ACTUAL_USER"

# Update service file with actual user
sed "s/INSTALL_USER/$ACTUAL_USER/g" digitalsignage-client.service > /tmp/digitalsignage-client.service
sudo cp /tmp/digitalsignage-client.service /etc/systemd/system/
sudo rm /tmp/digitalsignage-client.service

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Service file copied and configured for user $ACTUAL_USER${NC}"
else
    echo -e "${RED}✗ Failed to copy service file${NC}"
fi

sudo systemctl daemon-reload
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Systemd daemon reloaded${NC}"
else
    echo -e "${RED}✗ Failed to reload daemon${NC}"
fi
echo ""

# Restart service
echo -e "${YELLOW}[7/8] Starting digitalsignage-client service...${NC}"
sudo systemctl start digitalsignage-client
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Service started${NC}"
else
    echo -e "${RED}✗ Failed to start service${NC}"
    exit 1
fi
echo ""

# Wait a moment for service to initialize
sleep 3

# Show status
echo -e "${YELLOW}[8/8] Service status:${NC}"
echo ""
sudo systemctl status digitalsignage-client --no-pager -l
echo ""

# Final summary
echo "========================================="
echo -e "${GREEN}Update complete!${NC}"
echo "========================================="
echo ""
echo "Useful commands:"
echo "  View logs:        sudo journalctl -u digitalsignage-client -f"
echo "  Restart service:  sudo systemctl restart digitalsignage-client"
echo "  Stop service:     sudo systemctl stop digitalsignage-client"
echo "  Service status:   sudo systemctl status digitalsignage-client"
echo ""
