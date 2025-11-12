#!/usr/bin/env python3
"""
WebSocket Client Test Script
Tests connection to Digital Signage Server from Windows machine
"""

import socketio
import time
import sys

def test_connection():
    print("=" * 70)
    print("WEBSOCKET CLIENT CONNECTION TEST")
    print("=" * 70)
    print()

    # Create Socket.IO client
    sio = socketio.Client(
        logger=True,
        engineio_logger=True,
        reconnection=False  # Disable auto-reconnect for testing
    )

    # Event handlers
    @sio.event
    def connect():
        print("✓ CONNECTED to server!")
        print()

    @sio.event
    def disconnect():
        print("✗ DISCONNECTED from server")
        print()

    @sio.event
    def connect_error(data):
        print(f"✗ CONNECTION ERROR: {data}")
        print()

    @sio.on('*')
    def catch_all(event, data):
        print(f"Received event: {event}")
        print(f"Data: {data}")
        print()

    # Test different URLs
    test_urls = [
        "http://localhost:8080/ws",
        "http://127.0.0.1:8080/ws",
        "http://192.168.0.145:8080/ws",
    ]

    for url in test_urls:
        print("-" * 70)
        print(f"Testing URL: {url}")
        print("-" * 70)

        try:
            print(f"Attempting connection...")
            sio.connect(url, wait_timeout=10)

            print("✓ Connection successful!")
            print(f"✓ Connected to: {url}")
            print(f"✓ SID: {sio.sid}")
            print()

            # Wait a bit
            time.sleep(2)

            # Send test registration message
            print("Sending test REGISTER message...")
            test_message = {
                "Type": "REGISTER",
                "MacAddress": "00:00:00:00:00:00",
                "DeviceInfo": {
                    "Hostname": "TEST-CLIENT",
                    "Platform": "Windows",
                    "Architecture": "x86_64"
                },
                "RegistrationToken": ""
            }

            sio.emit('message', test_message)
            print("✓ Message sent")
            print()

            # Wait for response
            time.sleep(3)

            # Disconnect
            sio.disconnect()
            print("✓ Disconnected cleanly")
            print()

            print("=" * 70)
            print("✓✓✓ TEST PASSED - SERVER IS WORKING! ✓✓✓")
            print("=" * 70)
            return True

        except socketio.exceptions.ConnectionError as e:
            print(f"✗ Connection failed: {e}")
            print()

            # Check if it's a 503 error
            if "503" in str(e):
                print("!!! HTTP 503 SERVICE UNAVAILABLE !!!")
                print()
                print("This means:")
                print("  - Server is reachable (network is OK)")
                print("  - HttpListener is running")
                print("  - But WebSocket handler is not accepting connections")
                print()

            continue

        except Exception as e:
            print(f"✗ Unexpected error: {e}")
            print(f"   Type: {type(e).__name__}")
            import traceback
            traceback.print_exc()
            print()
            continue

    print("=" * 70)
    print("✗✗✗ ALL TESTS FAILED ✗✗✗")
    print("=" * 70)
    return False

if __name__ == "__main__":
    print()
    print("Digital Signage WebSocket Client Test")
    print("This script tests the WebSocket server connection")
    print()

    # Check if python-socketio is installed
    try:
        import socketio
        print(f"✓ python-socketio version: {socketio.__version__}")
        print()
    except ImportError:
        print("✗ python-socketio not installed!")
        print()
        print("Install with: pip install python-socketio[client]")
        sys.exit(1)

    success = test_connection()

    if success:
        sys.exit(0)
    else:
        sys.exit(1)
