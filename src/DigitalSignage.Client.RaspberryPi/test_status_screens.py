#!/opt/digitalsignage-client/venv/bin/python3
"""
Test script for status screens
Run this to test all status screen states visually

IMPORTANT: This script requires dependencies installed in the virtualenv.
Run with one of these methods:
  1. ./test_status_screens.py (if executable)
  2. /opt/digitalsignage-client/venv/bin/python3 test_status_screens.py
  3. source /opt/digitalsignage-client/venv/bin/activate && python3 test_status_screens.py
"""

import sys
import os
import asyncio

# Check if we're running in the correct environment
def check_environment():
    """Check if running with correct Python interpreter"""
    try:
        import qrcode
    except ImportError:
        print("\n" + "=" * 70)
        print("ERROR: Required dependencies not found")
        print("=" * 70)
        print("\nThe 'qrcode' module is not installed in your current Python environment.")
        print("\nThis script requires dependencies from the production virtualenv.")
        print("\nPlease run this script using one of these methods:\n")
        print("  1. Make it executable and run directly:")
        print("     chmod +x test_status_screens.py")
        print("     ./test_status_screens.py\n")
        print("  2. Use the virtualenv Python explicitly:")
        print("     /opt/digitalsignage-client/venv/bin/python3 test_status_screens.py\n")
        print("  3. Activate the virtualenv first:")
        print("     source /opt/digitalsignage-client/venv/bin/activate")
        print("     python3 test_status_screens.py\n")
        print("=" * 70)
        sys.exit(1)

# Check environment before importing other modules
check_environment()
from PyQt5.QtWidgets import QApplication
from PyQt5.QtCore import QTimer
from display_renderer import DisplayRenderer

async def test_status_screens():
    """Test all status screen states"""

    print("\n" + "=" * 70)
    print("STATUS SCREEN TEST")
    print("=" * 70)
    print("\nThis will cycle through all status screens every 5 seconds")
    print("Press Ctrl+C to exit\n")

    # Create Qt application
    app = QApplication(sys.argv)

    # Create display renderer (not fullscreen for testing)
    renderer = DisplayRenderer(fullscreen=False)
    renderer.show()

    # Test sequence
    screens = [
        ("Discovering Server", lambda: renderer.status_screen_manager.show_discovering_server("mDNS/Zeroconf + UDP Broadcast")),
        ("Connecting (Attempt 1)", lambda: renderer.status_screen_manager.show_connecting("http://192.168.0.145:8080", 1, 5)),
        ("Connecting (Attempt 3)", lambda: renderer.status_screen_manager.show_connecting("http://192.168.0.145:8080", 3, 5)),
        ("Waiting for Layout", lambda: renderer.status_screen_manager.show_waiting_for_layout("client-abc-123", "http://192.168.0.145:8080")),
        ("Connection Error", lambda: renderer.status_screen_manager.show_connection_error("http://192.168.0.145:8080", "Connection timeout: Server not responding", "client-abc-123")),
        ("No Layout Assigned", lambda: renderer.status_screen_manager.show_no_layout_assigned("client-abc-123", "http://192.168.0.145:8080", "192.168.0.200")),
    ]

    current_screen = [0]  # Use list to allow modification in nested function

    def show_next_screen():
        """Show the next screen in sequence"""
        if current_screen[0] < len(screens):
            name, func = screens[current_screen[0]]
            print(f"[{current_screen[0] + 1}/{len(screens)}] Showing: {name}")
            func()
            current_screen[0] += 1

            # Schedule next screen
            if current_screen[0] < len(screens):
                QTimer.singleShot(5000, show_next_screen)
            else:
                print("\n✓ All screens displayed")
                print("  Close the window or press Ctrl+C to exit")
        else:
            print("\nTest complete!")

    # Show first screen after 1 second
    QTimer.singleShot(1000, show_next_screen)

    # Run Qt event loop
    sys.exit(app.exec_())


if __name__ == "__main__":
    try:
        asyncio.run(test_status_screens())
    except KeyboardInterrupt:
        print("\n\nTest interrupted by user")
        sys.exit(0)
