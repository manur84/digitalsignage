#!/bin/bash

# Fix Digital Signage Client config.json Permissions
# This script fixes permission issues when the web interface cannot save settings

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

CONFIG_FILE="/opt/digitalsignage-client/config.json"

echo "============================================"
echo "  Digital Signage - Fix Config Permissions"
echo "============================================"
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}ERROR: Please run as root (use sudo)${NC}"
    echo "Usage: sudo ./fix-config-permissions.sh"
    exit 1
fi

# Check if config.json exists
if [ ! -f "$CONFIG_FILE" ]; then
    echo -e "${RED}ERROR: config.json not found at $CONFIG_FILE${NC}"
    echo "Run installer first: sudo ./install.sh"
    exit 1
fi

# Get current permissions
CURRENT_PERMS=$(stat -c '%a' "$CONFIG_FILE" 2>/dev/null || stat -f '%Lp' "$CONFIG_FILE" 2>/dev/null || echo "000")
CURRENT_OWNER=$(stat -c '%U:%G' "$CONFIG_FILE" 2>/dev/null || stat -f '%Su:%Sg' "$CONFIG_FILE" 2>/dev/null || echo "unknown")

echo "Current Status:"
echo "  File:        $CONFIG_FILE"
echo "  Permissions: $CURRENT_PERMS"
echo "  Owner:       $CURRENT_OWNER"
echo ""

if [ "$CURRENT_PERMS" = "666" ]; then
    echo -e "${GREEN}✓ Permissions already correct (666 - rw-rw-rw-)${NC}"
    echo ""
    echo "The web interface should be able to save settings."
    echo "If you still see permission errors, check:"
    echo "  1. SELinux/AppArmor restrictions"
    echo "  2. Systemd service configuration"
    echo "  3. Web interface logs: sudo journalctl -u digitalsignage-client -f"
else
    echo -e "${YELLOW}⚠ Fixing permissions...${NC}"
    chmod 666 "$CONFIG_FILE"

    NEW_PERMS=$(stat -c '%a' "$CONFIG_FILE" 2>/dev/null || stat -f '%Lp' "$CONFIG_FILE" 2>/dev/null || echo "000")

    if [ "$NEW_PERMS" = "666" ]; then
        echo -e "${GREEN}✓ Permissions fixed: $CURRENT_PERMS → 666${NC}"
    else
        echo -e "${RED}✗ Failed to fix permissions (still: $NEW_PERMS)${NC}"
        exit 1
    fi
fi

echo ""
echo "Verifying web interface access..."
echo ""

# Test if a non-root user can write to the file
TEST_USER="${SUDO_USER:-pi}"
if id "$TEST_USER" &>/dev/null; then
    if sudo -u "$TEST_USER" test -w "$CONFIG_FILE"; then
        echo -e "${GREEN}✓ User '$TEST_USER' can write to config.json${NC}"
    else
        echo -e "${RED}✗ User '$TEST_USER' CANNOT write to config.json${NC}"
        echo "This may indicate a filesystem or SELinux issue."
    fi
else
    echo -e "${YELLOW}⚠ Could not test write access for user '$TEST_USER'${NC}"
fi

echo ""
echo "============================================"
echo -e "${GREEN}  Permission Fix Complete!${NC}"
echo "============================================"
echo ""
echo "You can now:"
echo "  1. Open web interface: http://$(hostname -I | awk '{print $1}'):5000"
echo "  2. Go to Settings tab"
echo "  3. Make changes and click 'Save Settings'"
echo ""
echo "If you still see errors, check the logs:"
echo "  sudo journalctl -u digitalsignage-client -f"
echo ""
