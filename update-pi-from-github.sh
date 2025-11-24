#!/bin/bash
# Digital Signage Pi - Update from GitHub Script
# This script updates the installation on Raspberry Pi from latest GitHub code

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo "========================================"
echo "  Digital Signage Pi - GitHub Update"
echo "========================================"
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}ERROR: Please run as root (use sudo)${NC}"
    exit 1
fi

# 1. Update code from GitHub
echo -e "${BLUE}[1/5] Updating code from GitHub...${NC}"
cd ~/digitalsignage || { echo -e "${RED}ERROR: Repository not found at ~/digitalsignage${NC}"; exit 1; }

git pull origin main || { echo -e "${RED}ERROR: Git pull failed${NC}"; exit 1; }
echo -e "${GREEN}✓ Code updated${NC}"
echo ""

# 2. Update service file
echo -e "${BLUE}[2/5] Updating systemd service...${NC}"
cd src/DigitalSignage.Client.RaspberryPi

ACTUAL_USER="${SUDO_USER:-$USER}"
if [ -z "$ACTUAL_USER" ] || [ "$ACTUAL_USER" = "root" ]; then
    ACTUAL_USER="pi"
fi

sed "s/INSTALL_USER/$ACTUAL_USER/g" digitalsignage-client.service | \
sed "s|/usr/bin/python3|/opt/digitalsignage-client/venv/bin/python3|g" > /tmp/digitalsignage-client.service

cp /tmp/digitalsignage-client.service /etc/systemd/system/
rm /tmp/digitalsignage-client.service

systemctl daemon-reload
echo -e "${GREEN}✓ Service updated${NC}"
echo ""

# 3. Update Python files
echo -e "${BLUE}[3/5] Updating Python client files...${NC}"
cp *.py /opt/digitalsignage-client/ 2>/dev/null || true
cp -r widgets /opt/digitalsignage-client/ 2>/dev/null || true
cp -r renderers /opt/digitalsignage-client/ 2>/dev/null || true
cp -r templates /opt/digitalsignage-client/ 2>/dev/null || true
cp *.sh /opt/digitalsignage-client/ 2>/dev/null || true
chmod +x /opt/digitalsignage-client/*.sh 2>/dev/null || true

chown -R $ACTUAL_USER:$ACTUAL_USER /opt/digitalsignage-client
echo -e "${GREEN}✓ Client files updated${NC}"
echo ""

# 4. Restart service
echo -e "${BLUE}[4/5] Restarting service...${NC}"
systemctl restart digitalsignage-client
sleep 3
echo -e "${GREEN}✓ Service restarted${NC}"
echo ""

# 5. Check status
echo -e "${BLUE}[5/5] Checking service status...${NC}"
if systemctl is-active --quiet digitalsignage-client; then
    echo -e "${GREEN}✓ Service is running${NC}"
    systemctl status digitalsignage-client --no-pager -l | head -20
else
    echo -e "${RED}✗ Service is not running!${NC}"
    echo ""
    echo "Check logs:"
    echo "  sudo journalctl -u digitalsignage-client -n 50 --no-pager"
    exit 1
fi

echo ""
echo "========================================"
echo -e "${GREEN}  Update Complete!${NC}"
echo "========================================"
echo ""
echo "Service Status:"
systemctl status digitalsignage-client --no-pager | head -5
echo ""
echo "To view logs: sudo journalctl -u digitalsignage-client -f"
