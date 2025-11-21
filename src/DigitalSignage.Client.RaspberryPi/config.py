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
    use_ssl: bool = False
    verify_ssl: bool = True
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
        """Get the full server URL based on SSL configuration including endpoint path"""
        protocol = "https" if self.use_ssl else "http"
        # Ensure endpoint_path starts with / and has correct formatting
        endpoint = self.endpoint_path.strip('/')
        if endpoint:
            return f"{protocol}://{self.server_host}:{self.server_port}/{endpoint}"
        else:
            return f"{protocol}://{self.server_host}:{self.server_port}"

    def get_websocket_protocol(self) -> str:
        """Get the WebSocket protocol based on SSL configuration"""
        return "wss" if self.use_ssl else "ws"

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
                    'use_ssl': False,
                    'verify_ssl': True,
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

                return cls(**merged_data)
        else:
            # Create default configuration
            config = cls(client_id=str(uuid.uuid4()))
            config.save(config_path)
            return config

    def save(self, config_path: str = "/opt/digitalsignage-client/config.json"):
        """Save configuration to file"""
        config_file = Path(config_path)
        config_file.parent.mkdir(parents=True, exist_ok=True)

        with open(config_file, 'w') as f:
            json.dump(asdict(self), f, indent=2)

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

            if 'UseSSL' in server_config:
                self.use_ssl = bool(server_config['UseSSL'])

            if 'VerifySSL' in server_config:
                self.verify_ssl = bool(server_config['VerifySSL'])

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

        return cls(
            client_id=os.getenv("DS_CLIENT_ID", str(uuid.uuid4())),
            server_host=os.getenv("DS_SERVER_HOST", "localhost"),
            server_port=int(os.getenv("DS_SERVER_PORT", "8080")),
            registration_token=os.getenv("DS_REGISTRATION_TOKEN", ""),
            use_ssl=os.getenv("DS_USE_SSL", "false").lower() == "true",
            verify_ssl=os.getenv("DS_VERIFY_SSL", "true").lower() == "true",
            fullscreen=os.getenv("DS_FULLSCREEN", "true").lower() == "true",
            log_level=os.getenv("DS_LOG_LEVEL", "INFO"),
            auto_discover=os.getenv("DS_AUTO_DISCOVER", "false").lower() == "true",
            discovery_timeout=float(os.getenv("DS_DISCOVERY_TIMEOUT", "5.0")),
            remote_logging_enabled=os.getenv("DS_REMOTE_LOGGING", "true").lower() == "true",
            remote_logging_level=os.getenv("DS_REMOTE_LOG_LEVEL", "INFO"),
            remote_logging_batch_size=int(os.getenv("DS_REMOTE_LOG_BATCH_SIZE", "50")),
            remote_logging_batch_interval=float(os.getenv("DS_REMOTE_LOG_BATCH_INTERVAL", "5.0"))
        )
