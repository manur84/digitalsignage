#!/bin/bash
# Hard restart Digital Signage Client with cache clearing
# Run this on the Raspberry Pi to ensure latest code is loaded

echo "======================================================================"
echo "HARD RESTART DIGITAL SIGNAGE CLIENT"
echo "======================================================================"

echo "[1/5] Stopping service..."
sudo systemctl stop digitalsignage-client

echo "[2/5] Clearing Python cache..."
sudo rm -rf /opt/digitalsignage-client/__pycache__
sudo rm -rf /opt/digitalsignage-client/*.pyc
find /opt/digitalsignage-client -type d -name '__pycache__' -exec rm -rf {} + 2>/dev/null || true
find /opt/digitalsignage-client -name '*.pyc' -delete 2>/dev/null || true

echo "[3/5] Waiting 2 seconds..."
sleep 2

echo "[4/5] Starting service..."
sudo systemctl start digitalsignage-client

echo "[5/5] Waiting 3 seconds for startup..."
sleep 3

echo ""
echo "======================================================================"
echo "SERVICE STATUS"
echo "======================================================================"
systemctl status digitalsignage-client --no-pager | head -20

echo ""
echo "======================================================================"
echo "RECENT LOGS (last 30 lines)"
echo "======================================================================"
sudo journalctl -u digitalsignage-client -n 30 --no-pager

echo ""
echo "======================================================================"
echo "LIVE LOGS (Ctrl+C to exit)"
echo "======================================================================"
sudo journalctl -u digitalsignage-client -f
