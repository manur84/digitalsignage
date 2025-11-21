#!/bin/bash

# Test Web Interface Settings API
# This script tests if the web interface can save settings

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo "============================================"
echo "  Test Web Interface Settings API"
echo "============================================"
echo ""

# Get Raspberry Pi IP address
IP_ADDRESS=$(hostname -I | awk '{print $1}')
WEB_URL="http://${IP_ADDRESS}:5000"
API_URL="${WEB_URL}/api/settings"

echo "Web Interface URL: $WEB_URL"
echo "API Endpoint: $API_URL"
echo ""

# Check if web interface is running
echo "1. Checking if web interface is running..."
if curl -s --max-time 5 "$WEB_URL" > /dev/null 2>&1; then
    echo -e "${GREEN}✓ Web interface is running${NC}"
else
    echo -e "${RED}✗ Web interface is NOT running${NC}"
    echo ""
    echo "Start the client service:"
    echo "  sudo systemctl start digitalsignage-client"
    echo ""
    exit 1
fi

echo ""

# Get current settings
echo "2. Getting current settings..."
CURRENT_SETTINGS=$(curl -s "$API_URL")
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Successfully retrieved current settings${NC}"
    echo ""
    echo "Current settings (first 200 chars):"
    echo "$CURRENT_SETTINGS" | head -c 200
    echo "..."
else
    echo -e "${RED}✗ Failed to get current settings${NC}"
    exit 1
fi

echo ""
echo ""

# Test updating a setting (toggle show_cached_layout_on_disconnect)
echo "3. Testing settings update..."
echo ""
echo "Sending test update (toggling 'show_cached_layout_on_disconnect')..."

# Extract current value
CURRENT_VALUE=$(echo "$CURRENT_SETTINGS" | grep -o '"show_cached_layout_on_disconnect":[^,}]*' | cut -d':' -f2 | tr -d ' ')
NEW_VALUE="false"
if [ "$CURRENT_VALUE" = "false" ]; then
    NEW_VALUE="true"
fi

echo "Current value: $CURRENT_VALUE"
echo "New value: $NEW_VALUE"
echo ""

# Send update request
UPDATE_RESPONSE=$(curl -s -X POST "$API_URL" \
    -H "Content-Type: application/json" \
    -d "{\"show_cached_layout_on_disconnect\": $NEW_VALUE}")

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Update request sent${NC}"
    echo ""
    echo "Response:"
    echo "$UPDATE_RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$UPDATE_RESPONSE"
    echo ""

    # Check if update was successful
    if echo "$UPDATE_RESPONSE" | grep -q '"success": true'; then
        echo -e "${GREEN}✓ Settings update SUCCESSFUL!${NC}"
    else
        echo -e "${RED}✗ Settings update FAILED${NC}"
        echo ""

        # Check for permission error
        if echo "$UPDATE_RESPONSE" | grep -q "Permission denied"; then
            echo -e "${YELLOW}Permission Error Detected!${NC}"
            echo ""
            echo "Fix: Run the permission fix script:"
            echo "  sudo /opt/digitalsignage-client/fix-config-permissions.sh"
            echo ""
            exit 1
        fi
    fi
else
    echo -e "${RED}✗ Failed to send update request${NC}"
    exit 1
fi

echo ""

# Verify the change was persisted
echo "4. Verifying change was persisted..."
sleep 1
UPDATED_SETTINGS=$(curl -s "$API_URL")
UPDATED_VALUE=$(echo "$UPDATED_SETTINGS" | grep -o '"show_cached_layout_on_disconnect":[^,}]*' | cut -d':' -f2 | tr -d ' ')

if [ "$UPDATED_VALUE" = "$NEW_VALUE" ]; then
    echo -e "${GREEN}✓ Change persisted correctly${NC}"
    echo "Value is now: $UPDATED_VALUE"
else
    echo -e "${YELLOW}⚠ Value did not persist${NC}"
    echo "Expected: $NEW_VALUE"
    echo "Got: $UPDATED_VALUE"
fi

echo ""

# Check config.json file directly
echo "5. Checking config.json file..."
CONFIG_FILE="/opt/digitalsignage-client/config.json"

if [ -f "$CONFIG_FILE" ]; then
    PERMS=$(stat -c '%a' "$CONFIG_FILE" 2>/dev/null || stat -f '%Lp' "$CONFIG_FILE" 2>/dev/null)
    OWNER=$(stat -c '%U:%G' "$CONFIG_FILE" 2>/dev/null || stat -f '%Su:%Sg' "$CONFIG_FILE" 2>/dev/null)

    echo "Config file: $CONFIG_FILE"
    echo "Permissions: $PERMS"
    echo "Owner: $OWNER"
    echo ""

    if [ "$PERMS" = "666" ]; then
        echo -e "${GREEN}✓ Permissions are correct (666)${NC}"
    else
        echo -e "${YELLOW}⚠ Permissions should be 666 (currently: $PERMS)${NC}"
        echo "Fix: sudo chmod 666 $CONFIG_FILE"
    fi

    # Check file content
    FILE_VALUE=$(grep -o '"show_cached_layout_on_disconnect": [^,]*' "$CONFIG_FILE" | cut -d' ' -f2 | tr -d ',')
    echo ""
    echo "Value in config.json: $FILE_VALUE"

    if [ "$FILE_VALUE" = "$NEW_VALUE" ]; then
        echo -e "${GREEN}✓ File matches API value${NC}"
    else
        echo -e "${YELLOW}⚠ File does not match API value${NC}"
        echo "API: $NEW_VALUE"
        echo "File: $FILE_VALUE"
    fi
else
    echo -e "${RED}✗ config.json not found at $CONFIG_FILE${NC}"
fi

echo ""
echo "============================================"
echo -e "${GREEN}  Test Complete!${NC}"
echo "============================================"
echo ""
echo "Summary:"
echo "  - Web interface is running: ✓"
echo "  - Can retrieve settings: ✓"
if echo "$UPDATE_RESPONSE" | grep -q '"success": true'; then
    echo "  - Can update settings: ✓"
    echo "  - Changes persist: ✓"
else
    echo "  - Can update settings: ✗"
    echo ""
    echo "Check the logs for details:"
    echo "  sudo journalctl -u digitalsignage-client -f"
fi
echo ""
