#!/bin/bash
# Digital Signage Pi Diagnostics Script
# Run this to diagnose issues with the client

echo "======================================================================"
echo "   Digital Signage Client - Raspberry Pi Diagnostics"
echo "======================================================================"
echo ""

# Use plink (PuTTY) on Windows to connect
PLINK="/mnt/c/Windows/System32/plink.exe"
HOST="pi@192.168.50.200"
PASS="mr412393"

if [ ! -f "$PLINK" ]; then
    echo "ERROR: plink.exe not found at $PLINK"
    echo "Please ensure PuTTY is installed or run commands manually on Pi"
    exit 1
fi

echo "Connecting to Raspberry Pi..."
echo ""

# Function to run command on Pi
run_cmd() {
    local title="$1"
    local cmd="$2"

    echo "======================================================================"
    echo "$title"
    echo "======================================================================"
    echo ""

    echo y | "$PLINK" -pw "$PASS" -batch "$HOST" "$cmd" 2>&1
    echo ""
}

# Run diagnostics
run_cmd "[1] SERVICE STATUS" "systemctl status digitalsignage-client --no-pager -l"
run_cmd "[2] RECENT LOGS (Last 80 lines)" "journalctl -u digitalsignage-client -n 80 --no-pager"
run_cmd "[3] CONFIGURATION CHECK" "cat /opt/digitalsignage-client/config.json 2>&1 || echo 'Config not found'"
run_cmd "[4] PYTHON TEST" "/opt/digitalsignage-client/venv/bin/python3 --version 2>&1"
run_cmd "[5] X11 DISPLAY CHECK" "export DISPLAY=:0 && xset q 2>&1 || echo 'X11 not running'"
run_cmd "[6] DISK SPACE" "df -h /opt 2>&1"
run_cmd "[7] MEMORY" "free -h 2>&1"

echo "======================================================================"
echo "Diagnostics Complete!"
echo "======================================================================"
