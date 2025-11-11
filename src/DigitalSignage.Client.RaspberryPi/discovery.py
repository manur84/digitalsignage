"""
Network Discovery Module for Digital Signage Client

Handles automatic server discovery via UDP broadcast.
Clients send discovery requests on port 5555 and receive server connection info.
"""

import socket
import json
import logging
from typing import Optional, List, Dict
from dataclasses import dataclass
from datetime import datetime

logger = logging.getLogger(__name__)


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

        Args:
            timeout: How long to wait for responses (seconds)
            broadcast_address: Broadcast address or specific subnet broadcast

        Returns:
            List of discovered ServerInfo objects
        """
        discovered_servers = []

        try:
            # Create UDP socket
            sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
            sock.settimeout(timeout)

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
            except:
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
            except:
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


def discover_server(timeout: float = 5.0) -> Optional[str]:
    """
    Convenience function to discover a server and return the first WebSocket URL.

    Args:
        timeout: Discovery timeout in seconds

    Returns:
        WebSocket URL string, or None if no server found
    """
    discovery = DiscoveryClient()
    servers = discovery.discover_servers(timeout=timeout)

    if servers:
        server = servers[0]
        url = server.get_primary_url()
        logger.info(f"Auto-discovered server: {url}")
        return url
    else:
        logger.warning("No servers discovered on the network")
        return None


if __name__ == "__main__":
    # Test discovery
    logging.basicConfig(
        level=logging.INFO,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )

    print("Starting server discovery...")
    discovery = DiscoveryClient()
    servers = discovery.discover_servers(timeout=5.0)

    if servers:
        print(f"\nFound {len(servers)} server(s):")
        for i, server in enumerate(servers, 1):
            print(f"\n{i}. {server.server_name}")
            print(f"   Protocol: {server.protocol.upper()}")
            print(f"   SSL: {'Enabled' if server.ssl_enabled else 'Disabled'}")
            print(f"   URLs:")
            for url in server.get_urls():
                print(f"     - {url}")
    else:
        print("\nNo servers discovered.")
