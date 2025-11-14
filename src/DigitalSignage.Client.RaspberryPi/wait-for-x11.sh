#!/bin/bash
#
# Wait for X11 Display Server to be Ready
# Used by systemd service to ensure X11 is fully initialized before starting client
#

MAX_WAIT=30  # Maximum wait time in seconds
DISPLAY_TO_CHECK="${DISPLAY:-:0}"

echo "Waiting for X11 display server on $DISPLAY_TO_CHECK..."
echo "Maximum wait time: ${MAX_WAIT} seconds"

for i in $(seq 1 $MAX_WAIT); do
    # Test if X11 is responsive
    if DISPLAY="$DISPLAY_TO_CHECK" xset q &>/dev/null 2>&1; then
        echo "✓ X11 display server is ready on $DISPLAY_TO_CHECK (after ${i} seconds)"

        # Additional verification: Check if display info is accessible
        if DISPLAY="$DISPLAY_TO_CHECK" xdpyinfo &>/dev/null 2>&1; then
            echo "✓ X11 display server is fully functional"
            exit 0
        else
            echo "⚠ X11 display server responding but xdpyinfo failed"
            echo "  This is usually not critical, continuing..."
            exit 0
        fi
    fi

    # Show progress every 5 seconds
    if [ $((i % 5)) -eq 0 ]; then
        echo "  Still waiting... (${i}/${MAX_WAIT} seconds elapsed)"
    fi

    sleep 1
done

echo "⚠ WARNING: X11 display server not detected after ${MAX_WAIT} seconds"
echo "  The client will fall back to Xvfb (virtual display)"
echo "  This is normal for headless/testing environments"
echo "  If you have HDMI connected, check:"
echo "    1. Auto-login is enabled: sudo raspi-config"
echo "    2. Desktop boots automatically"
echo "    3. X11 logs: cat /var/log/Xorg.0.log"

# Exit 0 to allow service to continue with Xvfb fallback
exit 0
