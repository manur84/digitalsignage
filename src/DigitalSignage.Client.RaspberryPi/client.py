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
from cache_manager import CacheManager
from watchdog_monitor import WatchdogMonitor
from config import Config
from remote_log_handler import setup_remote_logging

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

        # Configure SSL verification
        ssl_verify = self.config.verify_ssl if self.config.use_ssl else False

        self.sio = socketio.AsyncClient(
            reconnection=True,
            reconnection_delay=5,
            reconnection_delay_max=60,
            ssl_verify=ssl_verify
        )
        self.device_manager = DeviceManager()
        self.cache_manager = CacheManager()
        self.watchdog = WatchdogMonitor(enable=True)
        self.display_renderer: Optional[DisplayRenderer] = None
        self.current_layout: Optional[Dict[str, Any]] = None
        self.connected = False
        self.offline_mode = False
        self.remote_log_handler = None

        # Setup remote logging if enabled
        if self.config.remote_logging_enabled:
            self._setup_remote_logging()

        # Register event handlers
        self.setup_event_handlers()

    def _setup_remote_logging(self):
        """Setup remote logging to send logs to server"""
        try:
            # Get log level from config
            log_level_map = {
                "DEBUG": logging.DEBUG,
                "INFO": logging.INFO,
                "WARNING": logging.WARNING,
                "ERROR": logging.ERROR,
                "CRITICAL": logging.CRITICAL
            }
            level = log_level_map.get(self.config.remote_logging_level.upper(), logging.INFO)

            # Setup remote logging handler
            self.remote_log_handler = setup_remote_logging(
                logger=logging.getLogger(),  # Root logger
                websocket_client=self,
                client_id=self.config.client_id,
                level=level,
                batch_size=self.config.remote_logging_batch_size,
                batch_interval=self.config.remote_logging_batch_interval
            )
            logger.info("Remote logging enabled - sending logs to server")
        except Exception as e:
            logger.error(f"Failed to setup remote logging: {e}")

    def send_message(self, message: Dict[str, Any]):
        """Send a message to the server (used by remote log handler)"""
        try:
            if self.connected:
                asyncio.create_task(self.sio.emit('message', message))
        except Exception as e:
            # Don't log errors here to avoid recursion
            pass

    def setup_event_handlers(self):
        """Setup WebSocket event handlers"""

        @self.sio.event
        async def connect():
            logger.info("Connected to server")
            self.connected = True
            self.offline_mode = False
            self.watchdog.notify_status("Connected to server")
            await self.register_client()

        @self.sio.event
        async def disconnect():
            logger.warning("Disconnected from server - entering offline mode")
            self.connected = False
            self.offline_mode = True
            self.watchdog.notify_status("Disconnected - running in offline mode")
            # Load cached layout to continue operation
            await self.load_cached_layout()

        @self.sio.event
        async def connect_error(data):
            logger.error(f"Connection error: {data}")

        @self.sio.on('message')
        async def on_message(data):
            await self.handle_message(data)

    async def register_client(self):
        """Register this client with the server"""
        try:
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

            # Add registration token if configured
            if self.config.registration_token:
                register_message["RegistrationToken"] = self.config.registration_token
                logger.info("Sending registration with token")
            else:
                logger.info("Sending registration without token (for existing clients)")

            await self.sio.emit('message', register_message)
            logger.info("Client registered with server")
        except Exception as e:
            logger.error(f"Failed to register client: {e}", exc_info=True)

    async def handle_message(self, data: Dict[str, Any]):
        """Handle incoming messages from server"""
        try:
            message_type = data.get("Type")

            logger.info(f"Received message: {message_type}")

            if message_type == "REGISTRATION_RESPONSE":
                await self.handle_registration_response(data)
            elif message_type == "DISPLAY_UPDATE":
                await self.handle_display_update(data)
            elif message_type == "COMMAND":
                await self.handle_command(data)
            elif message_type == "HEARTBEAT":
                await self.send_heartbeat()
            else:
                logger.warning(f"Unknown message type: {message_type}")
        except Exception as e:
            logger.error(f"Error handling message: {e}", exc_info=True)

    async def handle_registration_response(self, data: Dict[str, Any]):
        """Handle registration response from server"""
        try:
            success = data.get("Success", False)
            if success:
                assigned_id = data.get("AssignedClientId")
                assigned_group = data.get("AssignedGroup")
                assigned_location = data.get("AssignedLocation")

                logger.info(f"Registration successful - ID: {assigned_id}, Group: {assigned_group}, Location: {assigned_location}")

                # Update client ID if server assigned a different one
                if assigned_id and assigned_id != self.config.client_id:
                    logger.info(f"Server assigned new client ID: {assigned_id}")
                    self.config.client_id = assigned_id
                    # Save updated config
                    try:
                        self.config.save()
                    except Exception as e:
                        logger.warning(f"Failed to save updated config: {e}")
            else:
                error_message = data.get("ErrorMessage", "Unknown error")
                logger.error(f"Registration failed: {error_message}")
        except Exception as e:
            logger.error(f"Error handling registration response: {e}", exc_info=True)

    async def handle_display_update(self, data: Dict[str, Any]):
        """Handle display update message"""
        try:
            layout = data.get("Layout")
            layout_data = data.get("Data")

            if layout:
                self.current_layout = layout
                logger.info(f"Updating display with layout: {layout.get('Name')}")

                # Save layout and data to cache for offline operation
                self.cache_manager.save_layout(layout, layout_data, set_current=True)

                if self.display_renderer:
                    await self.display_renderer.render_layout(layout, layout_data)
                else:
                    logger.warning("Display renderer not initialized")
            else:
                logger.warning("Received DISPLAY_UPDATE without layout data")
        except Exception as e:
            logger.error(f"Error updating display: {e}", exc_info=True)

    async def handle_command(self, data: Dict[str, Any]):
        """Handle command message from server"""
        command = data.get("Command")
        parameters = data.get("Parameters", {})

        logger.info(f"Executing command: {command}")

        try:
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
        except Exception as e:
            logger.error(f"Error executing command {command}: {e}", exc_info=True)

    async def send_heartbeat(self):
        """Send heartbeat to server"""
        try:
            device_info = await self.device_manager.get_device_info()

            # Get cache info
            cache_info = self.cache_manager.get_cache_info()

            heartbeat_message = {
                "Type": "HEARTBEAT",
                "ClientId": self.config.client_id,
                "Status": "Online" if not self.offline_mode else "OfflineRecovery",
                "DeviceInfo": {
                    "CpuTemperature": device_info["cpu_temp"],
                    "CpuUsage": device_info["cpu_usage"],
                    "MemoryUsed": device_info["memory_used"],
                    "Uptime": device_info["uptime"]
                },
                "CacheInfo": {
                    "LayoutCount": cache_info.get("layout_count", 0),
                    "CurrentLayoutId": cache_info.get("current_layout_id")
                },
                "Timestamp": datetime.utcnow().isoformat()
            }

            await self.sio.emit('message', heartbeat_message)
        except Exception as e:
            logger.error(f"Failed to send heartbeat: {e}", exc_info=True)

    async def send_status_report(self):
        """Send status report to server"""
        try:
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
            logger.debug("Status report sent successfully")
        except Exception as e:
            logger.error(f"Failed to send status report: {e}", exc_info=True)

    async def take_screenshot(self) -> bytes:
        """Take screenshot of current display"""
        if self.display_renderer:
            return await self.display_renderer.take_screenshot()
        return b''

    async def send_screenshot(self, screenshot_data: bytes):
        """Send screenshot to server"""
        try:
            if not screenshot_data:
                logger.warning("Cannot send empty screenshot")
                return

            import base64

            try:
                encoded_data = base64.b64encode(screenshot_data).decode('utf-8')
            except Exception as e:
                logger.error(f"Failed to encode screenshot data: {e}")
                return

            screenshot_message = {
                "Type": "SCREENSHOT",
                "ClientId": self.config.client_id,
                "ImageData": encoded_data,
                "Format": "png",
                "Timestamp": datetime.utcnow().isoformat()
            }

            await self.sio.emit('message', screenshot_message)
            logger.info(f"Screenshot sent successfully ({len(screenshot_data)} bytes)")
        except Exception as e:
            logger.error(f"Failed to send screenshot: {e}", exc_info=True)

    async def restart_app(self):
        """Restart the application"""
        logger.info("Restarting application...")
        # TODO: Implement app restart logic
        pass

    async def load_cached_layout(self):
        """Load cached layout for offline operation"""
        try:
            cached = self.cache_manager.get_current_layout()
            if cached:
                layout, layout_data = cached
                self.current_layout = layout
                logger.info(f"Loaded cached layout: {layout.get('Name')} (Offline Mode)")

                if self.display_renderer:
                    await self.display_renderer.render_layout(layout, layout_data)
                    logger.info("Displaying cached layout in offline mode")
            else:
                logger.warning("No cached layout available for offline mode")
        except Exception as e:
            logger.error(f"Failed to load cached layout: {e}", exc_info=True)

    async def clear_cache(self):
        """Clear local cache"""
        try:
            if self.cache_manager.clear_cache():
                logger.info("Cache cleared successfully")
                self.current_layout = None
            else:
                logger.error("Failed to clear cache")
        except Exception as e:
            logger.error(f"Failed to clear cache: {e}", exc_info=True)

    async def start(self):
        """Start the client application"""
        logger.info("Starting Digital Signage Client...")

        # Start watchdog monitoring
        await self.watchdog.start()
        self.watchdog.notify_status("Initializing...")

        # Connect to server with retry logic
        max_retries = 5
        retry_delay = 2
        connection_successful = False

        for attempt in range(max_retries):
            try:
                server_url = self.config.get_server_url()
                protocol = self.config.get_websocket_protocol().upper()
                logger.info(f"Connecting to server at {server_url} using {protocol} (attempt {attempt + 1}/{max_retries})")

                self.watchdog.notify_status(f"Connecting to server (attempt {attempt + 1}/{max_retries})")

                if self.config.use_ssl:
                    if self.config.verify_ssl:
                        logger.info("SSL certificate verification enabled")
                    else:
                        logger.warning("SSL certificate verification disabled - not recommended for production!")

                await self.sio.connect(server_url)
                connection_successful = True
                break
            except Exception as e:
                if attempt < max_retries - 1:
                    logger.warning(f"Connection attempt {attempt + 1} failed: {e}. Retrying in {retry_delay}s...")
                    await asyncio.sleep(retry_delay)
                    retry_delay = min(retry_delay * 2, 60)  # Exponential backoff
                else:
                    logger.error(f"Failed to connect after {max_retries} attempts")

        # If connection failed, enter offline mode with cached layout
        if not connection_successful:
            logger.warning("Starting in offline mode with cached data")
            self.offline_mode = True
            self.watchdog.notify_status("Running in offline mode")
            await self.load_cached_layout()
        else:
            self.watchdog.notify_status("Connected to server")
            self.watchdog.notify_ready()

        # Setup heartbeat timer
        async def heartbeat_loop():
            while True:
                try:
                    if self.connected:
                        await self.send_heartbeat()
                except Exception as e:
                    logger.error(f"Heartbeat error: {e}")
                await asyncio.sleep(30)

        asyncio.create_task(heartbeat_loop())

        # Keep the connection alive
        try:
            await self.sio.wait()
        except Exception as e:
            logger.error(f"Client error: {e}", exc_info=True)

    async def stop(self):
        """Stop the client application"""
        logger.info("Stopping Digital Signage Client...")
        self.watchdog.notify_stopping()
        await self.sio.disconnect()
        await self.watchdog.stop()


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
