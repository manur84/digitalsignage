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
    registration_token: str = ""  # Token for client registration (required for new clients)
    use_ssl: bool = False
    verify_ssl: bool = True
    fullscreen: bool = True
    log_level: str = "INFO"
    cache_dir: str = str(Path.home() / ".digitalsignage" / "cache")
    data_dir: str = str(Path.home() / ".digitalsignage" / "data")
    auto_discover: bool = False  # Automatically discover server via UDP broadcast
    discovery_timeout: float = 5.0  # Discovery timeout in seconds

    def get_server_url(self) -> str:
        """Get the full server URL based on SSL configuration"""
        protocol = "https" if self.use_ssl else "http"
        return f"{protocol}://{self.server_host}:{self.server_port}"

    def get_websocket_protocol(self) -> str:
        """Get the WebSocket protocol based on SSL configuration"""
        return "wss" if self.use_ssl else "ws"

    @classmethod
    def load(cls, config_path: str = "/etc/digitalsignage/config.json") -> 'Config':
        """Load configuration from file"""
        config_file = Path(config_path)

        if config_file.exists():
            with open(config_file, 'r') as f:
                data = json.load(f)
                return cls(**data)
        else:
            # Create default configuration
            config = cls(client_id=str(uuid.uuid4()))
            config.save(config_path)
            return config

    def save(self, config_path: str = "/etc/digitalsignage/config.json"):
        """Save configuration to file"""
        config_file = Path(config_path)
        config_file.parent.mkdir(parents=True, exist_ok=True)

        with open(config_file, 'w') as f:
            json.dump(asdict(self), f, indent=2)

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
            discovery_timeout=float(os.getenv("DS_DISCOVERY_TIMEOUT", "5.0"))
        )
