#!/usr/bin/env python3
"""
Test script to verify:
1. mDNS/UDP Discovery works
2. WebSocket connection works
"""

import asyncio
import socket
import json
import ssl
import websockets
from zeroconf import Zeroconf, ServiceBrowser, ServiceListener
import logging

logging.basicConfig(
    level=logging.INFO,
    format='[%(levelname)s] %(message)s'
)
logger = logging.getLogger(__name__)


class DiscoveryListener(ServiceListener):
    """Listener for mDNS service discovery"""

    def __init__(self):
        self.servers = []

    def add_service(self, zc, type_, name):
        info = zc.get_service_info(type_, name)
        if info:
            logger.info(f"✓ mDNS: Found service '{name}'")
            for addr in info.parsed_addresses():
                logger.info(f"  IP: {addr}:{info.port}")
                self.servers.append({
                    'name': name,
                    'ip': addr,
                    'port': info.port,
                    'properties': info.properties
                })

    def remove_service(self, zc, type_, name):
        pass

    def update_service(self, zc, type_, name):
        pass


async def test_udp_discovery():
    """Test UDP broadcast discovery"""
    logger.info("=" * 70)
    logger.info("TEST 1: UDP BROADCAST DISCOVERY")
    logger.info("=" * 70)

    try:
        # Create UDP socket
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        sock.settimeout(3.0)

        # Send discovery request
        discovery_port = 5555
        message = "DIGITALSIGNAGE_DISCOVER"

        logger.info(f"Sending UDP broadcast to port {discovery_port}")
        logger.info(f"Message: {message}")

        sock.sendto(message.encode(), ('<broadcast>', discovery_port))

        # Wait for response
        logger.info("Waiting for server response...")

        try:
            data, addr = sock.recvfrom(4096)
            response = data.decode('utf-8')

            logger.info(f"✓ Received response from {addr}")
            logger.info(f"Response: {response}")

            # Try to parse JSON
            if response.startswith("DIGITALSIGNAGE_SERVER"):
                try:
                    json_start = response.index('{')
                    json_data = response[json_start:]
                    server_info = json.loads(json_data)

                    logger.info("Server Information:")
                    logger.info(f"  Server Name: {server_info.get('ServerName')}")
                    logger.info(f"  IPs: {server_info.get('LocalIPs')}")
                    logger.info(f"  Port: {server_info.get('Port')}")
                    logger.info(f"  Protocol: {server_info.get('Protocol')}")
                    logger.info(f"  Endpoint: {server_info.get('EndpointPath')}")

                    return server_info
                except Exception as e:
                    logger.error(f"Failed to parse response: {e}")
                    return None

        except socket.timeout:
            logger.error("✗ No response received (timeout)")
            logger.error("  Server may not be running or firewall is blocking UDP port 5555")
            return None

    except Exception as e:
        logger.error(f"✗ UDP Discovery failed: {e}")
        return None
    finally:
        sock.close()

    return None


def test_mdns_discovery():
    """Test mDNS/Zeroconf discovery"""
    logger.info("")
    logger.info("=" * 70)
    logger.info("TEST 2: mDNS/ZEROCONF DISCOVERY")
    logger.info("=" * 70)

    try:
        zeroconf = Zeroconf()
        listener = DiscoveryListener()

        service_type = "_digitalsignage._tcp.local."
        logger.info(f"Browsing for service type: {service_type}")

        browser = ServiceBrowser(zeroconf, service_type, listener)

        # Wait for discovery
        logger.info("Waiting for mDNS responses (3 seconds)...")
        import time
        time.sleep(3)

        if listener.servers:
            logger.info(f"✓ Found {len(listener.servers)} server(s) via mDNS")
            return listener.servers[0] if listener.servers else None
        else:
            logger.error("✗ No servers found via mDNS")
            logger.error("  Server may not be advertising via mDNS")
            return None

    except Exception as e:
        logger.error(f"✗ mDNS Discovery failed: {e}")
        return None
    finally:
        try:
            zeroconf.close()
        except:
            pass


async def test_websocket_connection(server_info):
    """Test WebSocket connection to server"""
    logger.info("")
    logger.info("=" * 70)
    logger.info("TEST 3: WEBSOCKET CONNECTION")
    logger.info("=" * 70)

    if not server_info:
        logger.error("No server info available, skipping WebSocket test")
        return False

    # Extract connection details
    if 'LocalIPs' in server_info:  # UDP Discovery format
        ip = server_info['LocalIPs'][0] if server_info['LocalIPs'] else None
        port = server_info.get('Port', 8080)
        protocol = server_info.get('Protocol', 'ws')
        endpoint = server_info.get('EndpointPath', 'ws/')
    elif 'ip' in server_info:  # mDNS format
        ip = server_info['ip']
        port = server_info.get('port', 8080)
        props = server_info.get('properties', {})
        protocol = props.get(b'protocol', b'ws').decode('utf-8')
        endpoint = props.get(b'endpoint', b'ws/').decode('utf-8')
    else:
        logger.error("Unknown server info format")
        return False

    if not ip:
        logger.error("No IP address found in server info")
        return False

    # Build WebSocket URL
    ws_url = f"{protocol}://{ip}:{port}/{endpoint.strip('/')}"

    logger.info(f"WebSocket URL: {ws_url}")
    logger.info(f"  Protocol: {protocol}")
    logger.info(f"  Host: {ip}")
    logger.info(f"  Port: {port}")
    logger.info(f"  Endpoint: {endpoint}")

    # SSL context for wss://
    ssl_context = None
    if protocol == 'wss':
        ssl_context = ssl.create_default_context()
        ssl_context.check_hostname = False
        ssl_context.verify_mode = ssl.CERT_NONE
        logger.info("SSL: Enabled (certificate verification disabled)")

    try:
        logger.info("Attempting connection...")

        async with websockets.connect(
            ws_url,
            ssl=ssl_context,
            ping_interval=30,
            ping_timeout=10,
            close_timeout=5
        ) as websocket:
            logger.info("✓ WebSocket connection SUCCESSFUL!")

            # Try to send a ping
            logger.info("Sending test message...")
            await websocket.send(json.dumps({
                'type': 'Ping',
                'data': {}
            }))

            # Wait for response (with timeout)
            try:
                response = await asyncio.wait_for(websocket.recv(), timeout=5.0)
                logger.info(f"✓ Received response: {response[:100]}...")
                return True
            except asyncio.TimeoutError:
                logger.warning("No response received (timeout)")
                logger.warning("Server may not respond to Ping messages")
                return True  # Connection worked anyway

    except websockets.exceptions.InvalidURI as e:
        logger.error(f"✗ Invalid WebSocket URI: {e}")
        return False

    except websockets.exceptions.InvalidHandshake as e:
        logger.error(f"✗ WebSocket handshake failed: {e}")
        logger.error("  Server may not have WebSocket endpoint at this path")
        return False

    except ssl.SSLError as e:
        logger.error(f"✗ SSL/TLS error: {e}")
        return False

    except OSError as e:
        logger.error(f"✗ Connection failed: {e}")
        logger.error(f"  Error code: {e.errno}")
        if e.errno == 111:
            logger.error("  Connection refused - server not accepting connections")
        elif e.errno == 113:
            logger.error("  No route to host - network unreachable")
        return False

    except Exception as e:
        logger.error(f"✗ WebSocket connection failed: {type(e).__name__}: {e}")
        import traceback
        logger.error(traceback.format_exc())
        return False


async def main():
    """Run all tests"""
    logger.info("╔" + "═" * 68 + "╗")
    logger.info("║" + " " * 10 + "Digital Signage Discovery & WebSocket Test" + " " * 15 + "║")
    logger.info("╚" + "═" * 68 + "╝")
    logger.info("")

    # Test 1: UDP Discovery
    udp_server = await test_udp_discovery()

    # Test 2: mDNS Discovery
    mdns_server = test_mdns_discovery()

    # Test 3: WebSocket Connection
    server_to_use = udp_server or mdns_server

    if server_to_use:
        ws_success = await test_websocket_connection(server_to_use)
    else:
        logger.error("")
        logger.error("=" * 70)
        logger.error("NO SERVER DISCOVERED - Skipping WebSocket test")
        logger.error("=" * 70)
        ws_success = False

    # Summary
    logger.info("")
    logger.info("=" * 70)
    logger.info("TEST SUMMARY")
    logger.info("=" * 70)
    logger.info(f"UDP Discovery:    {'✓ PASS' if udp_server else '✗ FAIL'}")
    logger.info(f"mDNS Discovery:   {'✓ PASS' if mdns_server else '✗ FAIL'}")
    logger.info(f"WebSocket Conn:   {'✓ PASS' if ws_success else '✗ FAIL'}")
    logger.info("=" * 70)

    if udp_server or mdns_server:
        if ws_success:
            logger.info("✓ ALL TESTS PASSED - Server is reachable and working!")
        else:
            logger.warning("⚠ Discovery works but WebSocket connection failed")
    else:
        logger.error("✗ Server not discoverable - check if server is running")


if __name__ == "__main__":
    asyncio.run(main())
