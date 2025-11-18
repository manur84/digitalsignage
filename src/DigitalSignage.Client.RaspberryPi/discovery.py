"""
Network Discovery Module for Digital Signage Client

Handles automatic server discovery via:
1. mDNS/Zeroconf (preferred) - works across subnets with multicast routing
2. UDP broadcast (fallback) - works on local subnet only

Clients discover servers on port 5555 (UDP) or via mDNS service type _digitalsignage._tcp.local.
"""

import socket
import json
import logging
from typing import Optional, List, Dict
from dataclasses import dataclass
from datetime import datetime

logger = logging.getLogger(__name__)

# Try to import zeroconf, but make it optional for backward compatibility
try:
    from zeroconf import Zeroconf, ServiceBrowser, ServiceListener
    MDNS_AVAILABLE = True
except ImportError:
    logger.warning("zeroconf not available - mDNS discovery disabled. Install with: pip install zeroconf>=0.70.0")
    MDNS_AVAILABLE = False


def get_eth0_network_info() -> Optional[Dict[str, str]]:
    """
    Get network configuration from eth0 interface.

    Returns:
        Dict with 'ip', 'netmask', 'broadcast' or None if eth0 not found
    """
    try:
        import netifaces

        # Check if eth0 exists
        if 'eth0' not in netifaces.interfaces():
            logger.warning("eth0 interface not found, trying to find primary interface...")
            # Fallback: find first non-loopback interface
            interfaces = [i for i in netifaces.interfaces() if i != 'lo']
            if not interfaces:
                logger.error("No network interfaces found")
                return None
            interface = interfaces[0]
            logger.info(f"Using interface: {interface}")
        else:
            interface = 'eth0'

        # Get IPv4 addresses
        addrs = netifaces.ifaddresses(interface)
        if netifaces.AF_INET not in addrs:
            logger.error(f"No IPv4 address on {interface}")
            return None

        ipv4_info = addrs[netifaces.AF_INET][0]

        result = {
            'interface': interface,
            'ip': ipv4_info.get('addr'),
            'netmask': ipv4_info.get('netmask'),
            'broadcast': ipv4_info.get('broadcast')
        }

        logger.info(f"Network info from {interface}: IP={result['ip']}, Netmask={result['netmask']}, Broadcast={result['broadcast']}")
        return result

    except ImportError:
        # Fallback: use subprocess if netifaces not available
        logger.debug("netifaces not available, using ip command")
        try:
            import subprocess
            result = subprocess.run(['ip', 'addr', 'show', 'eth0'],
                                  capture_output=True, text=True, timeout=2)

            if result.returncode != 0:
                logger.warning("eth0 not found via ip command")
                return None

            # Parse output
            import re
            ip_match = re.search(r'inet\s+(\d+\.\d+\.\d+\.\d+)/(\d+)', result.stdout)
            if ip_match:
                ip = ip_match.group(1)
                cidr = int(ip_match.group(2))

                # Calculate netmask and broadcast
                import ipaddress
                network = ipaddress.IPv4Network(f"{ip}/{cidr}", strict=False)

                result_info = {
                    'interface': 'eth0',
                    'ip': ip,
                    'netmask': str(network.netmask),
                    'broadcast': str(network.broadcast_address)
                }

                logger.info(f"Network info from eth0: IP={result_info['ip']}, Netmask={result_info['netmask']}, Broadcast={result_info['broadcast']}")
                return result_info

        except Exception as e:
            logger.error(f"Failed to get eth0 info via ip command: {e}")

    except Exception as e:
        logger.error(f"Failed to get eth0 network info: {e}")

    return None


@dataclass
class ServerInfo:
    """Information about a discovered server"""
    server_name: str
    local_ips: List[str]
    port: int
    protocol: str  # "ws" or "wss"
    endpoint_path: str
    ssl_enabled: bool
    timestamp: datetime

    def get_urls(self) -> List[str]:
        """Get all possible WebSocket URLs for this server"""
        return [
            f"{self.protocol}://{ip}:{self.port}/{self.endpoint_path}"
            for ip in self.local_ips
        ]

    def get_primary_url(self) -> str:
        """Get the first/primary WebSocket URL"""
        urls = self.get_urls()
        return urls[0] if urls else ""


if MDNS_AVAILABLE:
    class MdnsDiscoveryListener(ServiceListener):
        """
        Listener for mDNS service discovery events.
        Collects discovered Digital Signage servers.
        """

        def __init__(self, zeroconf: 'Zeroconf'):
            self.zeroconf = zeroconf
            self.discovered_servers: List[ServerInfo] = []
            self.logger = logging.getLogger(self.__class__.__name__)

        def add_service(self, zc: 'Zeroconf', service_type: str, name: str) -> None:
            """Called when a service is discovered"""
            try:
                info = zc.get_service_info(service_type, name)
                if info:
                    # Parse service info
                    server_info = self._parse_service_info(info)
                    if server_info:
                        # Check for duplicates
                        if not any(s.server_name == server_info.server_name for s in self.discovered_servers):
                            self.discovered_servers.append(server_info)
                            self.logger.info(f"Discovered server via mDNS: {server_info.server_name} at {server_info.get_primary_url()}")
            except Exception as e:
                self.logger.error(f"Error processing mDNS service: {e}")

        def remove_service(self, zc: 'Zeroconf', service_type: str, name: str) -> None:
            """Called when a service goes away"""
            self.logger.debug(f"Service removed: {name}")

        def update_service(self, zc: 'Zeroconf', service_type: str, name: str) -> None:
            """Called when service information is updated"""
            self.logger.debug(f"Service updated: {name}")

        def _parse_service_info(self, info) -> Optional[ServerInfo]:
            """Parse mDNS service info into ServerInfo object"""
            try:
                # Get server properties from TXT records
                properties = {}
                if info.properties:
                    for key, value in info.properties.items():
                        try:
                            # Decode bytes to string
                            prop_key = key.decode('utf-8') if isinstance(key, bytes) else key
                            prop_value = value.decode('utf-8') if isinstance(value, bytes) else value
                            properties[prop_key] = prop_value
                        except Exception as e:
                            self.logger.debug(f"Error decoding property {key}: {e}")

                # Extract server information
                server_name = properties.get('server_name', info.server or 'Unknown')
                protocol = properties.get('protocol', 'ws')
                endpoint_path = properties.get('endpoint', 'ws').lstrip('/')
                ssl_enabled = properties.get('ssl_enabled', 'false').lower() == 'true'
                port = info.port

                # Get all IP addresses
                local_ips = []
                if hasattr(info, 'parsed_addresses'):
                    # zeroconf >= 0.32.0
                    local_ips = [str(addr) for addr in info.parsed_addresses()]
                elif hasattr(info, 'addresses'):
                    # Older zeroconf versions
                    import ipaddress
                    local_ips = [str(ipaddress.ip_address(addr)) for addr in info.addresses]

                if not local_ips:
                    self.logger.warning(f"No IP addresses found for service {info.name}")
                    return None

                return ServerInfo(
                    server_name=server_name,
                    local_ips=local_ips,
                    port=port,
                    protocol=protocol,
                    endpoint_path=endpoint_path,
                    ssl_enabled=ssl_enabled,
                    timestamp=datetime.now()
                )

            except Exception as e:
                self.logger.error(f"Error parsing mDNS service info: {e}")
                return None


    class MdnsDiscoveryClient:
        """
        mDNS/Zeroconf discovery client for Digital Signage servers.

        Usage:
            if MDNS_AVAILABLE:
                discovery = MdnsDiscoveryClient()
                servers = discovery.discover_servers(timeout=5.0)
        """

        SERVICE_TYPE = "_digitalsignage._tcp.local."

        def __init__(self):
            self.logger = logging.getLogger(self.__class__.__name__)

        def discover_servers(self, timeout: float = 5.0) -> List[ServerInfo]:
            """
            Discover Digital Signage servers via mDNS.
            Uses eth0 interface if available.

            Args:
                timeout: How long to wait for responses (seconds)

            Returns:
                List of discovered ServerInfo objects
            """
            if not MDNS_AVAILABLE:
                self.logger.error("mDNS discovery not available - zeroconf package not installed")
                return []

            discovered_servers = []

            try:
                # Get eth0 network info to bind to correct interface
                net_info = get_eth0_network_info()
                if net_info:
                    self.logger.info(f"Using interface {net_info['interface']} (IP: {net_info['ip']}) for mDNS discovery")

                self.logger.info(f"Starting mDNS discovery for service type: {self.SERVICE_TYPE}")

                # Create Zeroconf instance
                # If we have eth0 info, pass the IP to bind to that interface
                if net_info and net_info.get('ip'):
                    try:
                        zeroconf = Zeroconf(interfaces=[net_info['ip']])
                        self.logger.debug(f"mDNS bound to {net_info['ip']}")
                    except Exception as e:
                        self.logger.warning(f"Could not bind to {net_info['ip']}, using default: {e}")
                        zeroconf = Zeroconf()
                else:
                    zeroconf = Zeroconf()

                # Create listener
                listener = MdnsDiscoveryListener(zeroconf)

                # Browse for services
                browser = ServiceBrowser(zeroconf, self.SERVICE_TYPE, listener)

                # Wait for discoveries
                import time
                time.sleep(timeout)

                # Get discovered servers
                discovered_servers = listener.discovered_servers

                # Cleanup - suppress async warnings by using sync methods
                # The zeroconf library creates background async tasks, but we're using
                # it in a synchronous context. We'll use the sync methods and suppress
                # any RuntimeWarnings about unclosed tasks - this is expected behavior.
                import warnings
                with warnings.catch_warnings():
                    # Suppress RuntimeWarning about coroutines not being awaited
                    warnings.filterwarnings('ignore', message='.*coroutine.*was never awaited.*')
                    warnings.filterwarnings('ignore', message='.*Task was destroyed but it is pending.*')

                    try:
                        browser.cancel()
                    except Exception as e:
                        self.logger.debug(f"Browser cleanup error (non-critical): {e}")

                    try:
                        zeroconf.close()
                    except Exception as e:
                        self.logger.debug(f"Zeroconf cleanup error (non-critical): {e}")

                self.logger.info(f"mDNS discovery complete. Found {len(discovered_servers)} server(s)")

            except Exception as e:
                self.logger.error(f"mDNS discovery failed: {e}")

            return discovered_servers


class DiscoveryClient:
    """
    Handles server discovery via UDP broadcast.

    Usage:
        discovery = DiscoveryClient()
        servers = discovery.discover_servers(timeout=5.0)
        if servers:
            server = servers[0]
            ws_url = server.get_primary_url()
    """

    DISCOVERY_PORT = 5555
    DISCOVERY_REQUEST = "DIGITALSIGNAGE_DISCOVER"
    DISCOVERY_RESPONSE_PREFIX = "DIGITALSIGNAGE_SERVER"
    BUFFER_SIZE = 4096

    def __init__(self):
        self.logger = logging.getLogger(self.__class__.__name__)

    def discover_servers(self, timeout: float = 5.0, broadcast_address: str = "<broadcast>") -> List[ServerInfo]:
        """
        Send UDP broadcast to discover servers on the network.
        Uses eth0 broadcast address if available.

        Args:
            timeout: How long to wait for responses (seconds)
            broadcast_address: Broadcast address or specific subnet broadcast

        Returns:
            List of discovered ServerInfo objects
        """
        discovered_servers = []

        try:
            # Get eth0 network info for targeted broadcast
            net_info = get_eth0_network_info()
            if net_info and net_info.get('broadcast') and broadcast_address == "<broadcast>":
                broadcast_address = net_info['broadcast']
                self.logger.info(f"Using eth0 broadcast address: {broadcast_address}")

            # Create UDP socket
            sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
            sock.settimeout(timeout)

            # Bind to eth0 IP if available
            if net_info and net_info.get('ip'):
                try:
                    sock.bind((net_info['ip'], 0))
                    self.logger.debug(f"UDP socket bound to {net_info['ip']}")
                except Exception as e:
                    self.logger.debug(f"Could not bind to {net_info['ip']}: {e}")

            # Send broadcast request
            message = self.DISCOVERY_REQUEST.encode('utf-8')
            sock.sendto(message, (broadcast_address, self.DISCOVERY_PORT))
            self.logger.info(f"Sent discovery broadcast to {broadcast_address}:{self.DISCOVERY_PORT}")

            # Listen for responses
            start_time = datetime.now()
            while True:
                try:
                    remaining_time = timeout - (datetime.now() - start_time).total_seconds()
                    if remaining_time <= 0:
                        break

                    sock.settimeout(remaining_time)
                    data, addr = sock.recvfrom(self.BUFFER_SIZE)

                    # Parse response
                    response_text = data.decode('utf-8')
                    server_info = self._parse_discovery_response(response_text, addr)

                    if server_info:
                        # Check for duplicates (same server name)
                        if not any(s.server_name == server_info.server_name for s in discovered_servers):
                            discovered_servers.append(server_info)
                            self.logger.info(f"Discovered server: {server_info.server_name} at {server_info.get_primary_url()}")

                except socket.timeout:
                    # Normal timeout, stop listening
                    break
                except Exception as e:
                    self.logger.warning(f"Error receiving discovery response: {e}")
                    continue

        except Exception as e:
            self.logger.error(f"Discovery failed: {e}")
        finally:
            try:
                sock.close()
            except Exception:
                # Socket already closed or invalid - safe to ignore
                pass

        self.logger.info(f"Discovery complete. Found {len(discovered_servers)} server(s)")
        return discovered_servers

    def _parse_discovery_response(self, response_text: str, addr: tuple) -> Optional[ServerInfo]:
        """Parse JSON discovery response from server"""
        try:
            data = json.loads(response_text)

            # Verify response type
            if data.get("Type") != self.DISCOVERY_RESPONSE_PREFIX:
                self.logger.debug(f"Invalid discovery response type from {addr}")
                return None

            # Parse timestamp
            timestamp_str = data.get("Timestamp", "")
            try:
                timestamp = datetime.fromisoformat(timestamp_str.replace('Z', '+00:00'))
            except (ValueError, AttributeError):
                # Invalid timestamp format - use current time
                timestamp = datetime.now()

            # Create ServerInfo
            return ServerInfo(
                server_name=data.get("ServerName", "Unknown"),
                local_ips=data.get("LocalIPs", []),
                port=data.get("Port", 8080),
                protocol=data.get("Protocol", "ws"),
                endpoint_path=data.get("EndpointPath", "ws").lstrip('/'),
                ssl_enabled=data.get("SslEnabled", False),
                timestamp=timestamp
            )

        except json.JSONDecodeError:
            self.logger.debug(f"Invalid JSON in discovery response from {addr}")
            return None
        except Exception as e:
            self.logger.error(f"Error parsing discovery response: {e}")
            return None


def discover_server(timeout: float = 5.0, prefer_mdns: bool = True) -> Optional[str]:
    """
    Convenience function to discover a server and return the first WebSocket URL.
    Tries mDNS first (if available), then falls back to UDP broadcast.

    Args:
        timeout: Discovery timeout in seconds
        prefer_mdns: Try mDNS first before UDP broadcast (default: True)

    Returns:
        WebSocket URL string, or None if no server found
    """
    servers = []

    # Try mDNS first if available and preferred
    if prefer_mdns and MDNS_AVAILABLE:
        logger.info("Attempting mDNS discovery...")
        mdns_discovery = MdnsDiscoveryClient()
        servers = mdns_discovery.discover_servers(timeout=timeout)

        if servers:
            server = servers[0]
            url = server.get_primary_url()
            logger.info(f"Auto-discovered server via mDNS: {url}")
            return url
        else:
            logger.info("No servers found via mDNS, falling back to UDP broadcast...")

    # Fallback to UDP broadcast
    logger.info("Attempting UDP broadcast discovery...")
    udp_discovery = DiscoveryClient()
    servers = udp_discovery.discover_servers(timeout=timeout)

    if servers:
        server = servers[0]
        url = server.get_primary_url()
        logger.info(f"Auto-discovered server via UDP broadcast: {url}")
        return url
    else:
        logger.warning("No servers discovered on the network (tried mDNS and UDP broadcast)")
        return None


def discover_all_servers(timeout: float = 5.0, use_mdns: bool = True, use_udp: bool = True) -> List[ServerInfo]:
    """
    Discover all available servers using both mDNS and UDP broadcast.

    Args:
        timeout: Discovery timeout in seconds
        use_mdns: Use mDNS discovery (default: True)
        use_udp: Use UDP broadcast discovery (default: True)

    Returns:
        List of all discovered ServerInfo objects
    """
    all_servers = []
    seen_names = set()

    # Try mDNS
    if use_mdns and MDNS_AVAILABLE:
        logger.info("Scanning for servers via mDNS...")
        mdns_discovery = MdnsDiscoveryClient()
        mdns_servers = mdns_discovery.discover_servers(timeout=timeout)

        for server in mdns_servers:
            if server.server_name not in seen_names:
                all_servers.append(server)
                seen_names.add(server.server_name)
                logger.info(f"Found server via mDNS: {server.server_name}")

    # Try UDP broadcast
    if use_udp:
        logger.info("Scanning for servers via UDP broadcast...")
        udp_discovery = DiscoveryClient()
        udp_servers = udp_discovery.discover_servers(timeout=timeout)

        for server in udp_servers:
            if server.server_name not in seen_names:
                all_servers.append(server)
                seen_names.add(server.server_name)
                logger.info(f"Found server via UDP: {server.server_name}")

    logger.info(f"Total servers discovered: {len(all_servers)}")
    return all_servers


if __name__ == "__main__":
    # Test discovery
    logging.basicConfig(
        level=logging.INFO,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )

    print("=" * 70)
    print("Digital Signage Server Discovery Tool")
    print("=" * 70)
    print(f"mDNS/Zeroconf Available: {MDNS_AVAILABLE}")
    print(f"UDP Broadcast Available: True")
    print("=" * 70)

    print("\nStarting comprehensive server discovery...")
    print("This will try both mDNS and UDP broadcast methods...\n")

    servers = discover_all_servers(timeout=5.0)

    if servers:
        print(f"\n{'=' * 70}")
        print(f"Found {len(servers)} server(s):")
        print(f"{'=' * 70}")
        for i, server in enumerate(servers, 1):
            print(f"\n{i}. {server.server_name}")
            print(f"   Protocol: {server.protocol.upper()}")
            print(f"   SSL: {'Enabled' if server.ssl_enabled else 'Disabled'}")
            print(f"   Port: {server.port}")
            print(f"   Endpoint: {server.endpoint_path}")
            print(f"   URLs:")
            for url in server.get_urls():
                print(f"     - {url}")
    else:
        print("\n" + "=" * 70)
        print("No servers discovered.")
        print("=" * 70)
        print("\nTroubleshooting:")
        if not MDNS_AVAILABLE:
            print("  - Install zeroconf for mDNS support: pip install zeroconf>=0.70.0")
        print("  - Ensure Digital Signage server is running")
        print("  - Check firewall settings (UDP port 5555, mDNS port 5353)")
        print("  - Verify network connectivity")
