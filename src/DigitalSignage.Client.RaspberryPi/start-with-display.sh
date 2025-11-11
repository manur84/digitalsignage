#!/bin/bash
#
# Digital Signage Client - Display Manager
# Automatically detects and configures display (X11 or Xvfb)
#

# Check if X11 is already running and accessible
if xset q &>/dev/null; then
    echo "X11 display detected, using existing display"
    export DISPLAY=${DISPLAY:-:0}
    echo "DISPLAY set to: $DISPLAY"
else
    echo "No X11 display found, starting virtual framebuffer (Xvfb)"
    echo "This is normal for headless/testing environments"

    # Start Xvfb on :99 with 1920x1080 resolution, 24-bit color
    Xvfb :99 -screen 0 1920x1080x24 &
    XVFB_PID=$!
    export DISPLAY=:99

    # Wait for Xvfb to start
    echo "Waiting for Xvfb to initialize..."
    sleep 2

    # Verify Xvfb started successfully
    if xset -display :99 q &>/dev/null; then
        echo "Xvfb started successfully on DISPLAY=:99"
    else
        echo "WARNING: Xvfb may not have started correctly"
    fi

    # Cleanup function to stop Xvfb on exit
    cleanup() {
        echo "Stopping Xvfb (PID: $XVFB_PID)..."
        kill $XVFB_PID 2>/dev/null
        wait $XVFB_PID 2>/dev/null
    }
    trap cleanup EXIT SIGTERM SIGINT
fi

# Start the client
echo "Starting Digital Signage Client..."
exec /opt/digitalsignage-client/venv/bin/python3 /opt/digitalsignage-client/client.py "$@"
