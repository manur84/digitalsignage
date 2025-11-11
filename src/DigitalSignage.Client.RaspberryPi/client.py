#!/usr/bin/env python3
"""
Digital Signage Raspberry Pi Client
Main client application that connects to the server and displays content
"""

import sys
import json
import asyncio
import logging
from datetime import datetime
from typing import Optional, Dict, Any
from pathlib import Path

import socketio
from PyQt5.QtWidgets import QApplication
from PyQt5.QtCore import QTimer

from display_renderer import DisplayRenderer
from device_manager import DeviceManager
from config import Config

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('/var/log/digitalsignage-client.log'),
        logging.StreamHandler(sys.stdout)
    ]
)

logger = logging.getLogger(__name__)


class DigitalSignageClient:
    """Main client application"""

    def __init__(self, config: Config):
        self.config = config
        self.sio = socketio.AsyncClient(
            reconnection=True,
            reconnection_delay=5,
            reconnection_delay_max=60
        )
        self.device_manager = DeviceManager()
        self.display_renderer: Optional[DisplayRenderer] = None
        self.current_layout: Optional[Dict[str, Any]] = None
        self.connected = False

        # Register event handlers
        self.setup_event_handlers()

    def setup_event_handlers(self):
        """Setup WebSocket event handlers"""

        @self.sio.event
        async def connect():
            logger.info("Connected to server")
            self.connected = True
            await self.register_client()

        @self.sio.event
        async def disconnect():
            logger.info("Disconnected from server")
            self.connected = False

        @self.sio.event
        async def connect_error(data):
            logger.error(f"Connection error: {data}")

        @self.sio.on('message')
        async def on_message(data):
            await self.handle_message(data)

    async def register_client(self):
        """Register this client with the server"""
        device_info = await self.device_manager.get_device_info()

        register_message = {
            "Type": "REGISTER",
            "ClientId": self.config.client_id,
            "MacAddress": device_info["mac_address"],
            "IpAddress": device_info["ip_address"],
            "DeviceInfo": {
                "Model": device_info["model"],
                "OsVersion": device_info["os_version"],
                "ClientVersion": "1.0.0",
                "CpuTemperature": device_info["cpu_temp"],
                "CpuUsage": device_info["cpu_usage"],
                "MemoryTotal": device_info["memory_total"],
                "MemoryUsed": device_info["memory_used"],
                "DiskTotal": device_info["disk_total"],
                "DiskUsed": device_info["disk_used"],
                "ScreenWidth": device_info["screen_width"],
                "ScreenHeight": device_info["screen_height"],
                "Uptime": device_info["uptime"]
            },
            "Timestamp": datetime.utcnow().isoformat()
        }

        await self.sio.emit('message', register_message)
        logger.info("Client registered with server")

    async def handle_message(self, data: Dict[str, Any]):
        """Handle incoming messages from server"""
        message_type = data.get("Type")

        logger.info(f"Received message: {message_type}")

        if message_type == "DISPLAY_UPDATE":
            await self.handle_display_update(data)
        elif message_type == "COMMAND":
            await self.handle_command(data)
        elif message_type == "HEARTBEAT":
            await self.send_heartbeat()
        else:
            logger.warning(f"Unknown message type: {message_type}")

    async def handle_display_update(self, data: Dict[str, Any]):
        """Handle display update message"""
        layout = data.get("Layout")
        layout_data = data.get("Data")

        if layout:
            self.current_layout = layout
            logger.info(f"Updating display with layout: {layout.get('Name')}")

            if self.display_renderer:
                await self.display_renderer.render_layout(layout, layout_data)

    async def handle_command(self, data: Dict[str, Any]):
        """Handle command message from server"""
        command = data.get("Command")
        parameters = data.get("Parameters", {})

        logger.info(f"Executing command: {command}")

        if command == "RESTART":
            await self.device_manager.restart_system()
        elif command == "RESTART_APP":
            await self.restart_app()
        elif command == "SCREENSHOT":
            screenshot = await self.take_screenshot()
            await self.send_screenshot(screenshot)
        elif command == "SCREEN_ON":
            await self.device_manager.screen_on()
        elif command == "SCREEN_OFF":
            await self.device_manager.screen_off()
        elif command == "SET_VOLUME":
            volume = parameters.get("volume", 50)
            await self.device_manager.set_volume(volume)
        elif command == "CLEAR_CACHE":
            await self.clear_cache()
        else:
            logger.warning(f"Unknown command: {command}")

    async def send_heartbeat(self):
        """Send heartbeat to server"""
        device_info = await self.device_manager.get_device_info()

        heartbeat_message = {
            "Type": "HEARTBEAT",
            "ClientId": self.config.client_id,
            "Status": "Online",
            "DeviceInfo": {
                "CpuTemperature": device_info["cpu_temp"],
                "CpuUsage": device_info["cpu_usage"],
                "MemoryUsed": device_info["memory_used"],
                "Uptime": device_info["uptime"]
            },
            "Timestamp": datetime.utcnow().isoformat()
        }

        await self.sio.emit('message', heartbeat_message)

    async def send_status_report(self):
        """Send status report to server"""
        device_info = await self.device_manager.get_device_info()

        status_message = {
            "Type": "STATUS_REPORT",
            "ClientId": self.config.client_id,
            "Status": "Online",
            "DeviceInfo": device_info,
            "CurrentLayoutId": self.current_layout.get("Id") if self.current_layout else None,
            "Timestamp": datetime.utcnow().isoformat()
        }

        await self.sio.emit('message', status_message)

    async def take_screenshot(self) -> bytes:
        """Take screenshot of current display"""
        if self.display_renderer:
            return await self.display_renderer.take_screenshot()
        return b''

    async def send_screenshot(self, screenshot_data: bytes):
        """Send screenshot to server"""
        import base64

        screenshot_message = {
            "Type": "SCREENSHOT",
            "ClientId": self.config.client_id,
            "ImageData": base64.b64encode(screenshot_data).decode('utf-8'),
            "Format": "png",
            "Timestamp": datetime.utcnow().isoformat()
        }

        await self.sio.emit('message', screenshot_message)

    async def restart_app(self):
        """Restart the application"""
        logger.info("Restarting application...")
        # TODO: Implement app restart logic
        pass

    async def clear_cache(self):
        """Clear local cache"""
        cache_dir = Path.home() / ".digitalsignage" / "cache"
        if cache_dir.exists():
            import shutil
            shutil.rmtree(cache_dir)
            cache_dir.mkdir(parents=True)
        logger.info("Cache cleared")

    async def start(self):
        """Start the client application"""
        logger.info("Starting Digital Signage Client...")

        # Connect to server
        server_url = f"http://{self.config.server_host}:{self.config.server_port}"
        await self.sio.connect(server_url)

        # Setup heartbeat timer
        async def heartbeat_loop():
            while True:
                if self.connected:
                    await self.send_heartbeat()
                await asyncio.sleep(30)

        asyncio.create_task(heartbeat_loop())

        # Keep the connection alive
        await self.sio.wait()

    async def stop(self):
        """Stop the client application"""
        logger.info("Stopping Digital Signage Client...")
        await self.sio.disconnect()


def main():
    """Main entry point"""
    config = Config.load()

    # Create Qt application for display rendering
    app = QApplication(sys.argv)

    # Create and start client
    client = DigitalSignageClient(config)

    # Create display renderer
    client.display_renderer = DisplayRenderer(fullscreen=config.fullscreen)
    client.display_renderer.show()

    # Start client in asyncio event loop
    loop = asyncio.get_event_loop()
    loop.create_task(client.start())

    # Run Qt event loop
    sys.exit(app.exec_())


if __name__ == "__main__":
    main()
