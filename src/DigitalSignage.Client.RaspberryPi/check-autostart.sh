#!/bin/bash
#
# Digital Signage Client - Autostart Diagnostic Script
# Use this script to diagnose and fix autostart issues after reboot
#

set -e

echo "========================================================"
echo "Digital Signage Client - Autostart Diagnostic"
echo "========================================================"
echo ""

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}ERROR: This script must be run as root (use sudo)${NC}"
    exit 1
fi

# Detect user
ACTUAL_USER="${SUDO_USER:-$USER}"
if [ "$ACTUAL_USER" = "root" ]; then
    ACTUAL_USER=$(grep "^User=" /etc/systemd/system/digitalsignage-client.service 2>/dev/null | cut -d= -f2)
    if [ -z "$ACTUAL_USER" ]; then
        echo -e "${YELLOW}Warning: Could not detect user from service file${NC}"
        ACTUAL_USER="pi"
    fi
fi

echo -e "${BLUE}Service User: $ACTUAL_USER${NC}"
echo ""

# CHECK 1: Service Status
echo -e "${YELLOW}[CHECK 1] Service Status${NC}"
echo "========================================================"
if systemctl is-active --quiet digitalsignage-client; then
    echo -e "${GREEN}✓ Service is running${NC}"
    SERVICE_RUNNING=true
else
    echo -e "${RED}✗ Service is NOT running${NC}"
    SERVICE_RUNNING=false
fi

if systemctl is-enabled --quiet digitalsignage-client; then
    echo -e "${GREEN}✓ Service is enabled (will start on boot)${NC}"
else
    echo -e "${RED}✗ Service is NOT enabled${NC}"
    echo -e "${YELLOW}  Fix: sudo systemctl enable digitalsignage-client${NC}"
fi
echo ""

# CHECK 2: X11 Display
echo -e "${YELLOW}[CHECK 2] X11 Display Server${NC}"
echo "========================================================"
if DISPLAY=:0 xset q &>/dev/null 2>&1; then
    echo -e "${GREEN}✓ X11 is running on :0${NC}"
    X11_RUNNING=true
else
    echo -e "${RED}✗ X11 is NOT running on :0${NC}"
    echo -e "${YELLOW}  This is likely why the display is not visible${NC}"
    X11_RUNNING=false
fi

if DISPLAY=:0 xdpyinfo &>/dev/null 2>&1; then
    echo -e "${GREEN}✓ Display is accessible${NC}"
    RESOLUTION=$(DISPLAY=:0 xdpyinfo | grep dimensions | awk '{print $2}')
    echo -e "  Resolution: $RESOLUTION"
else
    echo -e "${RED}✗ Display is NOT accessible${NC}"
fi
echo ""

# CHECK 3: HDMI Display
echo -e "${YELLOW}[CHECK 3] HDMI Display${NC}"
echo "========================================================"
if command -v tvservice &>/dev/null; then
    TVSERVICE_OUTPUT=$(tvservice -s 2>/dev/null || echo "failed")
    if echo "$TVSERVICE_OUTPUT" | grep -q "HDMI"; then
        echo -e "${GREEN}✓ HDMI display detected${NC}"
        echo "  $TVSERVICE_OUTPUT"
    else
        echo -e "${YELLOW}⚠ No HDMI display detected via tvservice${NC}"
        echo "  $TVSERVICE_OUTPUT"
    fi
else
    echo -e "${YELLOW}⚠ tvservice command not found (not a Raspberry Pi?)${NC}"
fi

# Check DRM
if ls /sys/class/drm/*/status 2>/dev/null | xargs cat 2>/dev/null | grep -q "^connected"; then
    echo -e "${GREEN}✓ Display connected (DRM)${NC}"
else
    echo -e "${YELLOW}⚠ No display connected via DRM${NC}"
fi
echo ""

# CHECK 4: Process Status
echo -e "${YELLOW}[CHECK 4] Client Process${NC}"
echo "========================================================"
CLIENT_PID=$(pgrep -f "python3.*client.py" || echo "")
if [ -n "$CLIENT_PID" ]; then
    echo -e "${GREEN}✓ Client process is running (PID: $CLIENT_PID)${NC}"

    # Check if process has DISPLAY set
    PROCESS_DISPLAY=$(cat /proc/$CLIENT_PID/environ 2>/dev/null | tr '\0' '\n' | grep "^DISPLAY=" | cut -d= -f2 || echo "NOT SET")
    echo "  DISPLAY environment: $PROCESS_DISPLAY"

    # Check process user
    PROCESS_USER=$(ps -o user= -p $CLIENT_PID)
    echo "  Running as user: $PROCESS_USER"

    if [ "$PROCESS_USER" != "$ACTUAL_USER" ]; then
        echo -e "${YELLOW}  ⚠ WARNING: Process user ($PROCESS_USER) != Service user ($ACTUAL_USER)${NC}"
    fi
else
    echo -e "${RED}✗ Client process is NOT running${NC}"
fi
echo ""

# CHECK 5: Window Visibility (using xdotool if available)
echo -e "${YELLOW}[CHECK 5] Window Visibility${NC}"
echo "========================================================"
if command -v xdotool &>/dev/null; then
    WINDOWS=$(sudo -u $ACTUAL_USER DISPLAY=:0 xdotool search --name "Digital Signage" 2>/dev/null || echo "")
    if [ -n "$WINDOWS" ]; then
        echo -e "${GREEN}✓ PyQt5 window found${NC}"
        echo "  Window IDs: $WINDOWS"

        # Check if window is visible
        for WID in $WINDOWS; do
            VISIBLE=$(sudo -u $ACTUAL_USER DISPLAY=:0 xdotool getwindowgeometry $WID 2>/dev/null | grep "Position" || echo "")
            echo "  Window $WID: $VISIBLE"
        done
    else
        echo -e "${RED}✗ No PyQt5 window found${NC}"
        echo -e "${YELLOW}  This means the window was created but is not visible${NC}"
    fi
else
    echo -e "${YELLOW}ℹ xdotool not installed (cannot check window visibility)${NC}"
    echo "  Install with: sudo apt-get install xdotool"
fi
echo ""

# CHECK 6: Recent Logs
echo -e "${YELLOW}[CHECK 6] Recent Service Logs${NC}"
echo "========================================================"
echo "Last 20 lines of service logs:"
echo "--------------------------------------------------------"
journalctl -u digitalsignage-client -n 20 --no-pager 2>/dev/null || echo "No logs available"
echo "--------------------------------------------------------"
echo ""

# CHECK 7: Startup Log
echo -e "${YELLOW}[CHECK 7] Startup Script Log${NC}"
echo "========================================================"
if [ -f /var/log/digitalsignage-client-startup.log ]; then
    echo "Last 30 lines of startup log:"
    echo "--------------------------------------------------------"
    tail -n 30 /var/log/digitalsignage-client-startup.log
    echo "--------------------------------------------------------"
elif [ -f /tmp/digitalsignage-client-startup.log ]; then
    echo "Last 30 lines of startup log (from /tmp):"
    echo "--------------------------------------------------------"
    tail -n 30 /tmp/digitalsignage-client-startup.log
    echo "--------------------------------------------------------"
else
    echo -e "${YELLOW}⚠ No startup log found${NC}"
fi
echo ""

# SUMMARY & RECOMMENDATIONS
echo "========================================================"
echo -e "${BLUE}DIAGNOSIS SUMMARY${NC}"
echo "========================================================"
echo ""

ISSUES_FOUND=0

if [ "$SERVICE_RUNNING" = false ]; then
    echo -e "${RED}ISSUE: Service is not running${NC}"
    echo -e "${YELLOW}FIX: sudo systemctl start digitalsignage-client${NC}"
    echo ""
    ISSUES_FOUND=$((ISSUES_FOUND + 1))
fi

if [ "$X11_RUNNING" = false ]; then
    echo -e "${RED}CRITICAL ISSUE: X11 is not running${NC}"
    echo -e "${YELLOW}This is the most likely cause of the problem.${NC}"
    echo ""
    echo "Possible fixes:"
    echo "  1. Ensure auto-login to desktop is enabled:"
    echo "     sudo raspi-config → System Options → Boot/Auto Login → Desktop Autologin"
    echo ""
    echo "  2. Reboot the system:"
    echo "     sudo reboot"
    echo ""
    echo "  3. Manually start X11:"
    echo "     startx"
    echo ""
    ISSUES_FOUND=$((ISSUES_FOUND + 1))
fi

if [ "$SERVICE_RUNNING" = true ] && [ "$X11_RUNNING" = true ] && [ -z "$CLIENT_PID" ]; then
    echo -e "${RED}ISSUE: Service running but client process not found${NC}"
    echo -e "${YELLOW}FIX: Check logs for errors:${NC}"
    echo "  sudo journalctl -u digitalsignage-client -n 100"
    echo ""
    ISSUES_FOUND=$((ISSUES_FOUND + 1))
fi

if [ "$SERVICE_RUNNING" = true ] && [ "$X11_RUNNING" = true ] && [ -n "$CLIENT_PID" ] && [ -z "$WINDOWS" ]; then
    echo -e "${YELLOW}POSSIBLE ISSUE: Client running but window not visible${NC}"
    echo ""
    echo "This is the EXACT problem you described!"
    echo ""
    echo "Fixes to try:"
    echo ""
    echo "  1. RECOMMENDED: Update to latest code (includes fixes):"
    echo "     cd /opt/digitalsignage-client"
    echo "     sudo git pull"
    echo "     sudo ./update.sh"
    echo "     sudo reboot"
    echo ""
    echo "  2. Restart the service:"
    echo "     sudo systemctl restart digitalsignage-client"
    echo ""
    echo "  3. Manual window activation (temporary):"
    echo "     sudo -u $ACTUAL_USER DISPLAY=:0 xdotool search --name 'Digital Signage' windowactivate"
    echo ""
    ISSUES_FOUND=$((ISSUES_FOUND + 1))
fi

if [ $ISSUES_FOUND -eq 0 ]; then
    echo -e "${GREEN}✓ No major issues detected!${NC}"
    echo ""
    echo "The system appears to be configured correctly."
    echo ""
    if [ "$SERVICE_RUNNING" = true ] && [ "$X11_RUNNING" = true ] && [ -n "$CLIENT_PID" ]; then
        echo "If the display is still not visible:"
        echo "  1. Check the HDMI monitor is on and connected"
        echo "  2. Try switching HDMI input"
        echo "  3. Restart the service: sudo systemctl restart digitalsignage-client"
    fi
fi

echo ""
echo "========================================================"
echo "For more help, see logs:"
echo "  sudo journalctl -u digitalsignage-client -f"
echo "========================================================"
echo ""
