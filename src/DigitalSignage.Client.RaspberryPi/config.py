"""
Configuration management for Digital Signage Client
"""

import json
import uuid
from pathlib import Path
from dataclasses import dataclass, asdict


@dataclass
class Config:
    """Client configuration"""
    client_id: str
    server_host: str = "localhost"
    server_port: int = 8080
    endpoint_path: str = "ws/"  # WebSocket endpoint path (default: ws/)
    registration_token: str = ""  # Token for client registration (required for new clients)
    use_ssl: bool = True  # WSS (WebSocket Secure) - REQUIRED, server only accepts WSS
    verify_ssl: bool = False  # Set to False for self-signed certificates
    fullscreen: bool = True
    log_level: str = "INFO"
    cache_dir: str = str(Path.home() / ".digitalsignage" / "cache")
    data_dir: str = str(Path.home() / ".digitalsignage" / "data")
    auto_discover: bool = True  # Automatically discover server via UDP broadcast (ENABLED BY DEFAULT)
    discovery_timeout: float = 5.0  # Discovery timeout in seconds
    remote_logging_enabled: bool = True  # Send logs to server
    remote_logging_level: str = "INFO"  # Minimum level for remote logs
    remote_logging_batch_size: int = 50  # Batch size for log messages
    remote_logging_batch_interval: float = 5.0  # Batch interval in seconds
    show_cached_layout_on_disconnect: bool = True  # Show cached layout when disconnected (True) or show reconnect status (False)

    # Anti-Burn-In Protection
    burn_in_protection_enabled: bool = True  # Enable anti-burn-in protection
    burn_in_pixel_shift_interval: int = 300  # Pixel shift interval in seconds (5 minutes)
    burn_in_pixel_shift_max: int = 5  # Maximum pixel shift distance
    burn_in_screensaver_timeout: int = 3600  # Screensaver timeout in seconds (1 hour)

    # Network Interface Selection
    preferred_network_interface: str = ""  # Preferred network interface (e.g., "eth0", "wlan0") - empty for auto-select

    def get_server_url(self) -> str:
        """Get the full server URL - ALWAYS uses HTTPS (for WSS WebSocket connections)

        CRITICAL: Raspberry Pi client ONLY supports WSS, which requires HTTPS base URL.
        This URL will be converted to WSS in connect_websocket().
        HTTP is NOT supported - server requires secure connections only.
        """
        protocol = "https"  # FORCE HTTPS-only - no insecure HTTP allowed
        # Ensure endpoint_path starts with / and has correct formatting
        endpoint = self.endpoint_path.strip('/')
        if endpoint:
            return f"{protocol}://{self.server_host}:{self.server_port}/{endpoint}"
        else:
            return f"{protocol}://{self.server_host}:{self.server_port}"

    def get_websocket_protocol(self) -> str:
        """Get the WebSocket protocol - ALWAYS returns WSS (WebSocket Secure)

        CRITICAL: Raspberry Pi client ONLY supports WSS (WebSocket Secure).
        The server requires WSS for all connections. WS (unsecure) is NOT supported.
        """
        return "wss"  # FORCE WSS-only - no insecure WS connections allowed

    @classmethod
    def load(cls, config_path: str = "/opt/digitalsignage-client/config.json") -> 'Config':
        """Load configuration from file"""
        config_file = Path(config_path)

        if config_file.exists():
            with open(config_file, 'r') as f:
                data = json.load(f)

                # Ensure all fields have defaults for backward compatibility
                # This handles cases where old config.json files don't have new fields
                defaults = {
                    'server_host': 'localhost',
                    'server_port': 8080,
                    'endpoint_path': 'ws/',  # Default WebSocket endpoint path
                    'registration_token': '',
                    'use_ssl': True,  # REQUIRED - server only accepts WSS
                    'verify_ssl': False,  # Default to False for self-signed certs
                    'fullscreen': True,
                    'log_level': 'INFO',
                    'cache_dir': str(Path.home() / ".digitalsignage" / "cache"),
                    'data_dir': str(Path.home() / ".digitalsignage" / "data"),
                    'auto_discover': True,  # IMPORTANT: Default to True for auto-discovery
                    'discovery_timeout': 5.0,
                    'remote_logging_enabled': True,
                    'remote_logging_level': 'INFO',
                    'remote_logging_batch_size': 50,
                    'remote_logging_batch_interval': 5.0,
                    'show_cached_layout_on_disconnect': True,
                    'burn_in_protection_enabled': True,
                    'burn_in_pixel_shift_interval': 300,
                    'burn_in_pixel_shift_max': 5,
                    'burn_in_screensaver_timeout': 3600,
                    'preferred_network_interface': ''
                }

                # Merge defaults with loaded data (loaded data takes precedence)
                merged_data = defaults.copy()
                merged_data.update(data)

                # CRITICAL: FORCE verify_ssl to False for Raspberry Pi client
                # Self-signed certificates are standard in local deployments
                # Even if config.json has verify_ssl=true, we IGNORE it for security and compatibility
                merged_data['verify_ssl'] = False

                # CRITICAL: FORCE use_ssl to True for Raspberry Pi client
                # Server requires WSS-only connections (no insecure WS)
                merged_data['use_ssl'] = True

                return cls(**merged_data)
        else:
            # Create default configuration
            config = cls(client_id=str(uuid.uuid4()))
            config.save(config_path)
            return config

    def save(self, config_path: str = "/opt/digitalsignage-client/config.json"):
        """Save configuration to file with atomic write and proper permissions"""
        import tempfile
        import shutil
        import os
        import stat
        import logging

        logger = logging.getLogger(__name__)
        config_file = Path(config_path)
        config_file.parent.mkdir(parents=True, exist_ok=True)

        try:
            # ATOMIC WRITE: Write to temporary file first, then move
            # This prevents corruption if write is interrupted
            temp_fd, temp_path = tempfile.mkstemp(
                dir=config_file.parent,
                prefix='.config_',
                suffix='.json.tmp',
                text=True
            )

            try:
                # Write to temp file
                with os.fdopen(temp_fd, 'w') as f:
                    json.dump(asdict(self), f, indent=2)

                # Set permissions BEFORE moving (rw-rw-rw-)
                # This allows both root and regular users to write
                os.chmod(temp_path, stat.S_IRUSR | stat.S_IWUSR | stat.S_IRGRP | stat.S_IWGRP | stat.S_IROTH | stat.S_IWOTH)

                # Atomic move (replaces old file)
                shutil.move(temp_path, config_file)

                logger.debug(f"Configuration saved to {config_file} (atomic write with permissions 666)")

            except Exception as e:
                # Clean up temp file on error
                try:
                    os.unlink(temp_path)
                except Exception:
                    pass
                raise

        except PermissionError as e:
            logger.error(f"Permission denied saving config to {config_file}: {e}")
            logger.error(f"Fix: sudo chmod 666 {config_file}")
            raise PermissionError(
                f"Cannot write to {config_file}. "
                f"Run: sudo chmod 666 {config_file} "
                f"or ensure the file has correct permissions."
            ) from e
        except Exception as e:
            logger.error(f"Failed to save configuration: {e}", exc_info=True)
            raise

    def update_from_server(self, server_config: dict) -> bool:
        """Update configuration from server UPDATE_CONFIG message

        Args:
            server_config: Dictionary containing configuration from server

        Returns:
            True if configuration was updated successfully, False otherwise
        """
        try:
            # Update configuration fields if provided
            if 'ServerHost' in server_config and server_config['ServerHost']:
                self.server_host = server_config['ServerHost']

            if 'ServerPort' in server_config and server_config['ServerPort']:
                self.server_port = int(server_config['ServerPort'])

            # CRITICAL: IGNORE UseSSL from server - Pi client ONLY supports WSS
            # Server cannot disable SSL on Pi client (security requirement)
            if 'UseSSL' in server_config:
                # Log but IGNORE - use_ssl is always True for Pi
                if not bool(server_config['UseSSL']):
                    logger = logging.getLogger(__name__)
                    logger.warning("Server attempted to disable SSL - IGNORED (Pi client requires WSS)")
                # Force use_ssl to remain True
                self.use_ssl = True

            # CRITICAL: IGNORE VerifySSL from server - Pi client ALWAYS uses verify_ssl=False
            # Self-signed certificates are standard in local deployments
            # Server cannot enable SSL verification on Pi client (compatibility requirement)
            if 'VerifySSL' in server_config:
                # Log but IGNORE - verify_ssl is always False for Pi
                if bool(server_config['VerifySSL']):
                    logger = logging.getLogger(__name__)
                    logger.warning("Server attempted to enable SSL verification - IGNORED (Pi accepts self-signed certs)")
                # Force verify_ssl to remain False
                self.verify_ssl = False

            if 'FullScreen' in server_config:
                self.fullscreen = bool(server_config['FullScreen'])

            if 'LogLevel' in server_config and server_config['LogLevel']:
                self.log_level = server_config['LogLevel']

            if 'RegistrationToken' in server_config and server_config['RegistrationToken']:
                self.registration_token = server_config['RegistrationToken']

            # Save updated configuration
            self.save()
            return True
        except Exception as e:
            print(f"Failed to update configuration from server: {e}")
            return False

    @classmethod
    def from_env(cls) -> 'Config':
        """Load configuration from environment variables"""
        import os

        # CRITICAL: Pi client ALWAYS uses use_ssl=True and verify_ssl=False
        # Ignore environment variables for these security-critical settings
        return cls(
            client_id=os.getenv("DS_CLIENT_ID", str(uuid.uuid4())),
            server_host=os.getenv("DS_SERVER_HOST", "localhost"),
            server_port=int(os.getenv("DS_SERVER_PORT", "8080")),
            registration_token=os.getenv("DS_REGISTRATION_TOKEN", ""),
            use_ssl=True,  # FORCED: Pi requires WSS-only (ignores DS_USE_SSL env var)
            verify_ssl=False,  # FORCED: Pi accepts self-signed certs (ignores DS_VERIFY_SSL env var)
            fullscreen=os.getenv("DS_FULLSCREEN", "true").lower() == "true",
            log_level=os.getenv("DS_LOG_LEVEL", "INFO"),
            auto_discover=os.getenv("DS_AUTO_DISCOVER", "false").lower() == "true",
            discovery_timeout=float(os.getenv("DS_DISCOVERY_TIMEOUT", "5.0")),
            remote_logging_enabled=os.getenv("DS_REMOTE_LOGGING", "true").lower() == "true",
            remote_logging_level=os.getenv("DS_REMOTE_LOG_LEVEL", "INFO"),
            remote_logging_batch_size=int(os.getenv("DS_REMOTE_LOG_BATCH_SIZE", "50")),
            remote_logging_batch_interval=float(os.getenv("DS_REMOTE_LOG_BATCH_INTERVAL", "5.0"))
        )
