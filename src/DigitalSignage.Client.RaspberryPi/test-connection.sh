#!/bin/bash
# Test network connectivity to Digital Signage server

echo "================================================"
echo "Digital Signage - Network Diagnostics"
echo "================================================"
echo ""

# Load config
CONFIG_FILE="/opt/digitalsignage-client/config.json"

if [ -f "$CONFIG_FILE" ]; then
    echo "Loading configuration from $CONFIG_FILE"

    # Parse JSON using python3
    SERVER_HOST=$(python3 -c "import json; print(json.load(open('$CONFIG_FILE'))['server_host'])" 2>/dev/null || echo "192.168.1.100")
    SERVER_PORT=$(python3 -c "import json; print(json.load(open('$CONFIG_FILE'))['server_port'])" 2>/dev/null || echo "8080")
    USE_SSL=$(python3 -c "import json; print(json.load(open('$CONFIG_FILE'))['use_ssl'])" 2>/dev/null || echo "False")
    REGISTRATION_TOKEN=$(python3 -c "import json; print(json.load(open('$CONFIG_FILE')).get('registration_token', 'NOT SET'))" 2>/dev/null || echo "NOT SET")

    echo "  Server Host: $SERVER_HOST"
    echo "  Server Port: $SERVER_PORT"
    echo "  Use SSL: $USE_SSL"
    echo "  Registration Token: $REGISTRATION_TOKEN"
else
    echo "⚠ Config file not found, using defaults"
    SERVER_HOST="192.168.1.100"
    SERVER_PORT="8080"
    USE_SSL="False"
    REGISTRATION_TOKEN="NOT SET"
    echo "  Server Host: $SERVER_HOST (default)"
    echo "  Server Port: $SERVER_PORT (default)"
fi

echo ""
echo "================================================"
echo "Running Network Tests..."
echo "================================================"
echo ""

# Test 1: Network interface
echo "[1/7] Network Interface Information:"
echo "----------------------------------------------"
IP_ADDRESSES=$(ip addr show | grep "inet " | grep -v "127.0.0.1" | awk '{print "  " $2 " (" $NF ")"}')
if [ -z "$IP_ADDRESSES" ]; then
    echo "  ✗ No network interfaces found (except loopback)"
    echo "  Check:"
    echo "    - Network cable connected"
    echo "    - WiFi configured"
    echo "    - Run: ip addr show"
else
    echo "$IP_ADDRESSES"
    echo "  ✓ Network interface active"
fi
echo ""

# Test 2: Check default gateway
echo "[2/7] Default Gateway:"
echo "----------------------------------------------"
GATEWAY=$(ip route | grep default | awk '{print $3}')
if [ -z "$GATEWAY" ]; then
    echo "  ✗ No default gateway found"
    echo "  Check network configuration"
else
    echo "  Gateway: $GATEWAY"
    if ping -c 1 -W 2 "$GATEWAY" &>/dev/null; then
        echo "  ✓ Gateway is reachable"
    else
        echo "  ✗ Cannot reach gateway"
        echo "  Check network connection"
    fi
fi
echo ""

# Test 3: DNS resolution
echo "[3/7] DNS Resolution (google.com):"
echo "----------------------------------------------"
if ping -c 1 -W 2 google.com &>/dev/null; then
    echo "  ✓ DNS resolution working"
    echo "  ✓ Internet connection available"
else
    echo "  ⚠ Cannot resolve DNS / No internet"
    echo "  This is OK if server is on local network"
fi
echo ""

# Test 4: Ping server
echo "[4/7] Server Reachability (ping $SERVER_HOST):"
echo "----------------------------------------------"
if ping -c 3 -W 2 "$SERVER_HOST" &>/dev/null; then
    echo "  ✓ Server host is reachable"

    # Get ping statistics
    PING_STATS=$(ping -c 3 -W 2 "$SERVER_HOST" 2>&1 | tail -2)
    echo "$PING_STATS" | sed 's/^/  /'
else
    echo "  ✗ Cannot reach server at $SERVER_HOST"
    echo ""
    echo "  Possible causes:"
    echo "    1. Server is not running"
    echo "    2. Wrong IP address in config.json"
    echo "    3. Firewall blocking ICMP (ping)"
    echo "    4. Different network/subnet"
    echo ""
    echo "  Actions:"
    echo "    - Verify server IP address"
    echo "    - Check if server is on same network"
    echo "    - Try: ping $SERVER_HOST"
    echo "    - Edit config: sudo nano $CONFIG_FILE"
fi
echo ""

# Test 5: TCP port connectivity
echo "[5/7] Server Port Test (TCP $SERVER_HOST:$SERVER_PORT):"
echo "----------------------------------------------"
if timeout 3 bash -c "cat < /dev/null > /dev/tcp/$SERVER_HOST/$SERVER_PORT" 2>/dev/null; then
    echo "  ✓ Port $SERVER_PORT is open and accepting connections"
    echo "  ✓ Server is listening"
else
    echo "  ✗ Cannot connect to port $SERVER_PORT"
    echo ""
    echo "  Possible causes:"
    echo "    1. Server application not running"
    echo "    2. Server not listening on port $SERVER_PORT"
    echo "    3. Firewall blocking port $SERVER_PORT"
    echo "    4. Wrong port number in config.json"
    echo ""
    echo "  Actions:"
    echo "    - Check server logs"
    echo "    - Verify server is running"
    echo "    - Check firewall: sudo ufw status"
    echo "    - Try different port in config.json"
fi
echo ""

# Test 6: Check client service logs
echo "[6/7] Recent Client Connection Attempts:"
echo "----------------------------------------------"
if systemctl is-active --quiet digitalsignage-client; then
    echo "  Service Status: RUNNING"
else
    echo "  Service Status: STOPPED"
fi

echo ""
echo "  Last 10 connection-related log entries:"
journalctl -u digitalsignage-client -n 50 --no-pager 2>/dev/null | \
    grep -iE "connect|register|server|error|failed" | \
    tail -10 | \
    sed 's/^/    /' || echo "    (No logs found)"
echo ""

# Test 7: Configuration validation
echo "[7/7] Configuration Validation:"
echo "----------------------------------------------"

if [ "$REGISTRATION_TOKEN" = "NOT SET" ] || [ -z "$REGISTRATION_TOKEN" ]; then
    echo "  ⚠ Registration token not set"
    echo "    New clients require a registration token"
    echo "    Get token from server administrator"
else
    echo "  ✓ Registration token configured"
fi

if [ "$SERVER_HOST" = "localhost" ] || [ "$SERVER_HOST" = "127.0.0.1" ]; then
    echo "  ⚠ Server host is set to localhost"
    echo "    This only works if server is on this device"
    echo "    Set correct server IP in config.json"
fi

echo ""
echo "================================================"
echo "Summary & Recommendations"
echo "================================================"
echo ""

# Provide recommendations based on test results
CAN_PING=$(ping -c 1 -W 2 "$SERVER_HOST" &>/dev/null && echo "yes" || echo "no")
CAN_CONNECT=$(timeout 3 bash -c "cat < /dev/null > /dev/tcp/$SERVER_HOST/$SERVER_PORT" 2>/dev/null && echo "yes" || echo "no")

if [ "$CAN_PING" = "yes" ] && [ "$CAN_CONNECT" = "yes" ]; then
    echo "✓ Network Configuration: GOOD"
    echo "  - Server is reachable"
    echo "  - Port is open"
    echo ""
    echo "If client still can't connect:"
    echo "  1. Check registration token is correct"
    echo "  2. Check server logs for connection attempts"
    echo "  3. Restart client: sudo systemctl restart digitalsignage-client"
    echo "  4. View client logs: sudo journalctl -u digitalsignage-client -f"
elif [ "$CAN_PING" = "yes" ] && [ "$CAN_CONNECT" = "no" ]; then
    echo "⚠ Network Configuration: PARTIAL"
    echo "  - Server is reachable (ping works)"
    echo "  - But port $SERVER_PORT is not accessible"
    echo ""
    echo "Actions:"
    echo "  1. Check if server application is running"
    echo "  2. Verify server is listening on port $SERVER_PORT"
    echo "  3. Check firewall on server:"
    echo "     Windows: Allow port $SERVER_PORT in Windows Firewall"
    echo "     Linux: sudo ufw allow $SERVER_PORT"
    echo "  4. Check if port number is correct in config.json"
else
    echo "✗ Network Configuration: FAILED"
    echo "  - Cannot reach server at $SERVER_HOST"
    echo ""
    echo "Actions:"
    echo "  1. Verify server IP address is correct"
    echo "  2. Check if both devices are on same network"
    echo "  3. Check network connection (cable/WiFi)"
    echo "  4. Edit configuration with correct server IP:"
    echo "     sudo nano $CONFIG_FILE"
    echo "  5. Update 'server_host' field"
    echo "  6. Restart client: sudo systemctl restart digitalsignage-client"
fi

echo ""
echo "================================================"
echo "Manual Configuration"
echo "================================================"
echo ""
echo "Edit client configuration:"
echo "  sudo nano $CONFIG_FILE"
echo ""
echo "Required settings:"
echo '  "server_host": "YOUR_SERVER_IP",'
echo '  "server_port": 8080,'
echo '  "registration_token": "YOUR_TOKEN",'
echo ""
echo "After editing, restart service:"
echo "  sudo systemctl restart digitalsignage-client"
echo ""
echo "View logs:"
echo "  sudo journalctl -u digitalsignage-client -f"
echo ""
echo "================================================"
echo ""

# Test UDP discovery (if enabled in config)
AUTO_DISCOVER=$(python3 -c "import json; print(json.load(open('$CONFIG_FILE')).get('auto_discover', False))" 2>/dev/null || echo "False")

if [ "$AUTO_DISCOVER" = "True" ]; then
    echo ""
    echo "================================================"
    echo "Auto-Discovery Test"
    echo "================================================"
    echo ""
    echo "Auto-discovery is enabled in config"
    echo "Testing UDP broadcast (port 5555)..."

    if command -v socat &>/dev/null; then
        echo "Sending discovery broadcast..."
        echo "DIGITALSIGNAGE_DISCOVER" | socat - UDP4-DATAGRAM:255.255.255.255:5555,broadcast 2>/dev/null
        echo "  ✓ Broadcast sent"
        echo "  Check server logs for discovery requests"
    else
        echo "  ⚠ socat not installed"
        echo "  Install: sudo apt-get install socat"
    fi
    echo ""
fi
