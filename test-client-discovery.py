#!/usr/bin/env python3
"""
Test script for Digital Signage client auto-discovery on Windows
Tests if the Python client can discover the server via UDP broadcast

Usage:
    python test-client-discovery.py

Requirements:
    - Python 3.7+
    - No special dependencies (uses only standard library)
"""

import sys
import socket
import json
import logging
from datetime import datetime
from typing import Optional, List

# Configure logging
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


class SimpleDiscoveryClient:
    """
    Simplified discovery client for testing UDP broadcast discovery
    Based on discovery.py but with minimal dependencies
    """

    DISCOVERY_PORT = 5555
    DISCOVERY_REQUEST = "DIGITALSIGNAGE_DISCOVER"
    BUFFER_SIZE = 4096

    def __init__(self):
        self.logger = logging.getLogger(self.__class__.__name__)

    def discover_servers(self, timeout: float = 5.0, broadcast_address: str = "<broadcast>") -> List[dict]:
        """
        Send UDP broadcast to discover servers on the network.

        Args:
            timeout: How long to wait for responses (seconds)
            broadcast_address: Broadcast address (use "<broadcast>" for automatic)

        Returns:
            List of discovered server info dictionaries
        """
        discovered_servers = []

        print("\n" + "=" * 70)
        print("TESTING UDP BROADCAST DISCOVERY")
        print("=" * 70)
        print(f"\nConfiguration:")
        print(f"  UDP Port: {self.DISCOVERY_PORT}")
        print(f"  Broadcast Address: {broadcast_address}")
        print(f"  Timeout: {timeout}s")
        print(f"  Discovery Message: {self.DISCOVERY_REQUEST}")
        print()

        try:
            # Create UDP socket
            print("[1/4] Creating UDP socket...")
            sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
            sock.settimeout(timeout)
            print(f"      UDP socket created successfully")

            # Get local IP for reference
            try:
                # Create a dummy connection to determine local IP
                test_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                test_sock.connect(("8.8.8.8", 80))
                local_ip = test_sock.getsockname()[0]
                test_sock.close()
                print(f"      Local IP address: {local_ip}")
            except Exception as e:
                print(f"      Could not determine local IP: {e}")
                local_ip = "unknown"

            # Send broadcast request
            print(f"\n[2/4] Sending UDP broadcast...")
            message = self.DISCOVERY_REQUEST.encode('utf-8')
            sock.sendto(message, (broadcast_address, self.DISCOVERY_PORT))
            print(f"      Broadcast sent to {broadcast_address}:{self.DISCOVERY_PORT}")
            print(f"      Message: '{self.DISCOVERY_REQUEST}'")
            print(f"      Waiting {timeout}s for responses...")

            # Listen for responses
            print(f"\n[3/4] Listening for server responses...")
            start_time = datetime.now()
            response_count = 0

            while True:
                try:
                    remaining_time = timeout - (datetime.now() - start_time).total_seconds()
                    if remaining_time <= 0:
                        break

                    sock.settimeout(remaining_time)
                    data, addr = sock.recvfrom(self.BUFFER_SIZE)
                    response_count += 1

                    print(f"\n      [Response #{response_count}]")
                    print(f"      From: {addr[0]}:{addr[1]}")
                    print(f"      Size: {len(data)} bytes")

                    # Parse response
                    try:
                        response_text = data.decode('utf-8')
                        print(f"      Raw: {response_text[:200]}{'...' if len(response_text) > 200 else ''}")

                        server_info = json.loads(response_text)
                        print(f"      Parsed JSON:")
                        for key, value in server_info.items():
                            print(f"        {key}: {value}")

                        # Check if it's a valid Digital Signage server response
                        if server_info.get("Type") == "DIGITALSIGNAGE_SERVER":
                            discovered_servers.append(server_info)
                            print(f"      Status: VALID Digital Signage Server")
                        else:
                            print(f"      Status: Invalid response type (expected DIGITALSIGNAGE_SERVER)")

                    except json.JSONDecodeError as e:
                        print(f"      ERROR: Invalid JSON - {e}")
                        print(f"      Raw data: {data}")
                    except Exception as e:
                        print(f"      ERROR: Failed to parse response - {e}")

                except socket.timeout:
                    # Normal timeout, stop listening
                    print(f"\n      Timeout reached after {timeout}s")
                    break
                except Exception as e:
                    print(f"\n      ERROR receiving data: {e}")
                    continue

            sock.close()

            # Summary
            print(f"\n[4/4] Discovery complete")
            print(f"      Total responses: {response_count}")
            print(f"      Valid servers found: {len(discovered_servers)}")

        except Exception as e:
            print(f"\nERROR: Discovery failed - {e}")
            import traceback
            print(traceback.format_exc())

        return discovered_servers


def main():
    """Main entry point"""
    print("=" * 70)
    print("DIGITAL SIGNAGE CLIENT - DISCOVERY TEST")
    print("=" * 70)
    print("\nThis script tests if the Python client can discover the Digital Signage")
    print("server via UDP broadcast (same mechanism used by discovery.py)")
    print()
    print("Prerequisites:")
    print("  - Digital Signage server must be running")
    print("  - Server's UDP discovery service must be enabled on port 5555")
    print("  - Firewall must allow UDP port 5555")
    print()

    try:
        # Run discovery test
        discovery = SimpleDiscoveryClient()
        servers = discovery.discover_servers(timeout=5.0)

        # Display results
        print("\n" + "=" * 70)
        print("DISCOVERY RESULTS")
        print("=" * 70)

        if servers:
            print(f"\nSUCCESS: Found {len(servers)} server(s)")
            print()

            for i, server in enumerate(servers, 1):
                print(f"Server {i}:")
                print(f"  Name: {server.get('ServerName', 'Unknown')}")
                print(f"  IPs: {', '.join(server.get('LocalIPs', []))}")
                print(f"  Port: {server.get('Port', 8080)}")
                print(f"  Protocol: {server.get('Protocol', 'ws').upper()}")
                print(f"  SSL: {'Enabled' if server.get('SslEnabled', False) else 'Disabled'}")
                print(f"  Endpoint: /{server.get('EndpointPath', 'ws')}")

                # Construct WebSocket URLs
                protocol = server.get('Protocol', 'ws')
                port = server.get('Port', 8080)
                endpoint = server.get('EndpointPath', 'ws').lstrip('/')

                print(f"  WebSocket URLs:")
                for ip in server.get('LocalIPs', []):
                    print(f"    {protocol}://{ip}:{port}/{endpoint}")
                print()

            print("=" * 70)
            print("CONCLUSION: Discovery is working correctly!")
            print("=" * 70)
            print()
            print("The client SHOULD be able to auto-discover the server.")
            print()
            print("If the actual client isn't discovering the server, check:")
            print("  1. Is auto_discover enabled in config.py? (should be True)")
            print("  2. Is discovery.py being imported correctly?")
            print("  3. Are there any errors in client logs?")
            print("  4. Is the config.json file overriding the default?")

        else:
            print("\nFAILURE: No servers discovered")
            print()
            print("This could mean:")
            print("  1. Server is not running")
            print("  2. Server's UDP discovery service is not enabled")
            print("  3. Firewall is blocking UDP port 5555")
            print("  4. Server and client are on different networks/subnets")
            print("  5. Broadcast traffic is being filtered")
            print()
            print("=" * 70)
            print("TROUBLESHOOTING STEPS")
            print("=" * 70)
            print()
            print("1. Verify server is running:")
            print("   - Check server application is started")
            print("   - Check server logs for UDP discovery service")
            print()
            print("2. Test server discovery manually:")
            print("   - Run test-discovery.ps1 on the server")
            print("   - Should show: 'UDP Discovery Service: Listening on port 5555'")
            print()
            print("3. Check firewall on server:")
            print("   - Windows: Allow UDP port 5555 inbound")
            print("   - netsh advfirewall firewall add rule ...")
            print()
            print("4. Check network connectivity:")
            print("   - Ping server from client")
            print("   - Ensure both are on same subnet")
            print("   - Check router/switch settings for broadcast traffic")
            print()
            print("5. Test UDP connectivity manually:")
            print("   - Server: nc -u -l 5555")
            print("   - Client: echo 'test' | nc -u <server-ip> 5555")

        print()
        return 0 if servers else 1

    except KeyboardInterrupt:
        print("\n\nTest interrupted by user")
        return 130
    except Exception as e:
        print(f"\n\nFATAL ERROR: {e}")
        import traceback
        print(traceback.format_exc())
        return 1


if __name__ == "__main__":
    sys.exit(main())
