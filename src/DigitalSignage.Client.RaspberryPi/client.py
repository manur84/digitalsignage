#!/usr/bin/env python3
"""
Digital Signage Raspberry Pi Client
Main client application that connects to the server and displays content
"""

import sys
import os
import traceback

# Configure logging FIRST before any other imports
import logging
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stdout),
        logging.StreamHandler(sys.stderr)
    ]
)

logger = logging.getLogger(__name__)

# Suppress qasync warnings about pending tasks from zeroconf discovery
# This is expected behavior when using zeroconf in a synchronous context
logging.getLogger('qasync._QEventLoop').setLevel(logging.CRITICAL)

# Suppress Python RuntimeWarning about coroutines not being awaited
# This happens during zeroconf cleanup and is expected/harmless
import warnings
warnings.filterwarnings('ignore', message='.*coroutine.*was never awaited.*', category=RuntimeWarning)
warnings.filterwarnings('ignore', message='.*Enable tracemalloc.*', category=RuntimeWarning)

# Log startup information
logger.info("=" * 70)
logger.info("Digital Signage Client Starting...")
logger.info("=" * 70)
logger.info(f"Python Version: {sys.version}")
logger.info(f"Python Executable: {sys.executable}")
logger.info(f"Working Directory: {os.getcwd()}")
logger.info(f"DISPLAY: {os.environ.get('DISPLAY', 'NOT SET')}")
logger.info(f"XAUTHORITY: {os.environ.get('XAUTHORITY', 'NOT SET')}")
logger.info(f"User: {os.environ.get('USER', 'NOT SET')}")
logger.info("=" * 70)

# Test critical imports one by one
try:
    logger.info("Testing standard library imports...")
    import json
    import asyncio
    import gzip
    from datetime import datetime
    from typing import Optional, Dict, Any
    from pathlib import Path
    import threading
    import time
    logger.info("  Standard library imports OK")
except ImportError as e:
    logger.error(f"  FAILED - Standard library import error: {e}")
    logger.error(traceback.format_exc())
    sys.exit(1)

try:
    logger.info("Testing websocket-client import...")
    import websocket
    try:
        websocket_version = websocket.__version__
    except AttributeError:
        try:
            import pkg_resources
            websocket_version = pkg_resources.get_distribution('websocket-client').version
        except Exception:
            # Catch all normal exceptions (not SystemExit/KeyboardInterrupt)
            websocket_version = "unknown"

    logger.info(f"  websocket-client version: {websocket_version}")
except ImportError as e:
    logger.error(f"  FAILED - websocket-client import error: {e}")
    logger.error("  Install with: pip install websocket-client>=1.6.0")
    logger.error(traceback.format_exc())
    sys.exit(1)

try:
    logger.info("Testing PyQt5 imports...")
    from PyQt5.QtWidgets import QApplication
    from PyQt5.QtCore import QTimer, PYQT_VERSION_STR
    logger.info(f"  PyQt5 version: {PYQT_VERSION_STR}")
    logger.info("  PyQt5 imports OK")
except ImportError as e:
    logger.error(f"  FAILED - PyQt5 import error: {e}")
    logger.error("")
    logger.error("PyQt5 is required but not accessible. Possible causes:")
    logger.error("  1. PyQt5 not installed: sudo apt-get install python3-pyqt5")
    logger.error("  2. Virtual environment created without --system-site-packages")
    logger.error("  3. DISPLAY environment variable not set (X11 display required)")
    logger.error("")
    logger.error("Current DISPLAY setting: " + os.environ.get('DISPLAY', 'NOT SET'))
    logger.error("")
    logger.error(traceback.format_exc())
    sys.exit(1)

try:
    logger.info("Testing local module imports...")
    from display_renderer import DisplayRenderer
    from burn_in_protection import BurnInProtection
    from device_manager import DeviceManager
    from cache_manager import CacheManager
    from watchdog_monitor import WatchdogMonitor
    from config import Config
    from remote_log_handler import setup_remote_logging
    from web_interface import WebInterface
    logger.info("  Local module imports OK")
except ImportError as e:
    logger.error(f"  FAILED - Local module import error: {e}")
    logger.error(f"  Make sure all client files are in: {os.getcwd()}")
    logger.error(traceback.format_exc())
    sys.exit(1)

logger.info("=" * 70)
logger.info("All imports successful - proceeding with client initialization")
logger.info("=" * 70)


class DigitalSignageClient:
    """Main client application"""

    def __init__(self, config: Config):
        self.config = config
        self.ws = None
        self.ws_app = None
        self.device_manager = DeviceManager()
        self.cache_manager = CacheManager()
        self.watchdog = WatchdogMonitor(enable=True)
        self.display_renderer: Optional[DisplayRenderer] = None
        self.burn_in_protection: Optional[BurnInProtection] = None
        self.current_layout: Optional[Dict[str, Any]] = None
        self.connected = False
        self.offline_mode = False
        self.remote_log_handler = None
        self.heartbeat_thread = None
        self.stop_heartbeat = False
        self.ws_thread = None
        self.reconnect_requested = False
        self.pending_messages = []
        self.message_lock = threading.Lock()

        # Reconnection state
        self.reconnection_in_progress = False
        self.reconnection_task = None
        self.stop_reconnection = False

        # Web interface
        self.web_interface: Optional[WebInterface] = None

        # Setup remote logging if enabled
        if self.config.remote_logging_enabled:
            self._setup_remote_logging()

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
            if self.connected and self.ws_app:
                message_json = json.dumps(message)
                self.ws_app.send(message_json)
            else:
                # Queue message for later if not connected
                with self.message_lock:
                    self.pending_messages.append(message)
        except Exception as e:
            # Log to file to avoid recursion issues with send_message
            try:
                error_log_path = '/var/log/digitalsignage-errors.log'
                with open(error_log_path, 'a') as f:
                    from datetime import datetime
                    f.write(f"{datetime.now().isoformat()}: send_message failed: {str(e)}\n")
            except Exception as log_error:
                # Absolute fallback - write to stderr
                import sys
                print(f"CRITICAL: send_message failed: {e} (log error: {log_error})", file=sys.stderr)

    def _flush_pending_messages(self):
        """Send any messages that were queued while disconnected"""
        with self.message_lock:
            if self.pending_messages and self.connected and self.ws_app:
                for message in self.pending_messages:
                    try:
                        message_json = json.dumps(message)
                        self.ws_app.send(message_json)
                    except Exception as e:
                        logger.error(f"Failed to send pending message: {e}")
                self.pending_messages.clear()

    def on_open(self, ws):
        """WebSocket connection opened"""
        logger.info("WebSocket connection opened")
        self.connected = True
        self.offline_mode = False
        self.watchdog.notify_status("Connected to server")

        # Register client
        asyncio.run_coroutine_threadsafe(
            self.register_client(),
            self.event_loop
        )

        # Start heartbeat
        self.start_heartbeat()

        # Flush pending messages
        self._flush_pending_messages()

    def on_message(self, ws, message):
        """WebSocket message received"""
        try:
            # Check if message is binary (compressed)
            if isinstance(message, bytes):
                # Check for gzip header (0x1F 0x8B)
                if len(message) >= 2 and message[0] == 0x1F and message[1] == 0x8B:
                    # Decompress gzip data
                    try:
                        decompressed = gzip.decompress(message)
                        message_str = decompressed.decode('utf-8')
                        logger.debug(f"Decompressed message: {len(message)} → {len(decompressed)} bytes")
                    except Exception as e:
                        logger.error(f"Failed to decompress message: {e}")
                        return
                else:
                    # Binary but not compressed, decode as UTF-8
                    message_str = message.decode('utf-8')
            else:
                # Text message
                message_str = message

            data = json.loads(message_str)
            # Schedule message handling in asyncio loop
            future = asyncio.run_coroutine_threadsafe(
                self.handle_message(data),
                self.event_loop
            )
            # Add a callback to handle any exceptions in the coroutine
            def handle_future_exception(fut):
                try:
                    fut.result()
                except Exception as e:
                    logger.error(f"Error in message handler coroutine: {e}", exc_info=True)
            future.add_done_callback(handle_future_exception)
        except json.JSONDecodeError as e:
            logger.error(f"Failed to parse JSON message: {e}")
        except Exception as e:
            logger.error(f"Error processing message: {e}", exc_info=True)

    def on_error(self, ws, error):
        """WebSocket error occurred"""
        logger.error(f"WebSocket error: {error}")

    def on_close(self, ws, close_status_code, close_msg):
        """WebSocket connection closed"""
        logger.warning(f"WebSocket connection closed (code: {close_status_code}, msg: {close_msg})")
        self.connected = False

        if not self.reconnect_requested:
            # Unexpected disconnect - enter offline mode and start reconnection
            self.offline_mode = True
            self.watchdog.notify_status("Disconnected - attempting reconnection")

            # Check config to determine disconnect behavior
            if self.config.show_cached_layout_on_disconnect:
                # Show cached layout continuously (no status screen switching)
                logger.info("Disconnect behavior: Showing cached layout (no status screen)")
                future = asyncio.run_coroutine_threadsafe(
                    self.load_cached_layout(),
                    self.event_loop
                )
                # Add a callback to handle any exceptions in the coroutine
                def handle_future_exception(fut):
                    try:
                        fut.result()
                    except Exception as e:
                        logger.error(f"Error loading cached layout: {e}", exc_info=True)
                future.add_done_callback(handle_future_exception)
            else:
                # Show reconnect status screen (no cached layout)
                logger.info("Disconnect behavior: Showing reconnect status screen")
                # Status screen will be shown in start_reconnection()

            # Start automatic reconnection if not already in progress
            if not self.reconnection_in_progress:
                logger.info("Starting automatic reconnection...")
                reconnect_future = asyncio.run_coroutine_threadsafe(
                    self.start_reconnection(),
                    self.event_loop
                )
                # Add error handler for reconnection task
                def handle_reconnect_exception(fut):
                    try:
                        fut.result()
                    except Exception as e:
                        logger.error(f"Error in reconnection task: {e}", exc_info=True)
                reconnect_future.add_done_callback(handle_reconnect_exception)

    def connect_websocket(self, server_url: str):
        """Connect to WebSocket server"""
        try:
            logger.info(f"Creating WebSocket connection to {server_url}")

            # Parse URL to extract components
            # server_url format: http://host:port/path or https://host:port/path
            # We need: ws://host:port/path or wss://host:port/path
            ws_url = server_url.replace('http://', 'ws://').replace('https://', 'wss://')

            # Configure SSL options
            sslopt = None
            if ws_url.startswith('wss://'):
                if not self.config.verify_ssl:
                    import ssl
                    sslopt = {"cert_reqs": ssl.CERT_NONE}
                    logger.warning("SSL certificate verification disabled")

            # Create WebSocketApp
            self.ws_app = websocket.WebSocketApp(
                ws_url,
                on_open=self.on_open,
                on_message=self.on_message,
                on_error=self.on_error,
                on_close=self.on_close
            )

            # Run WebSocket in a thread
            self.ws_thread = threading.Thread(
                target=lambda: self.ws_app.run_forever(
                    sslopt=sslopt,
                    ping_interval=30,
                    ping_timeout=10
                ),
                daemon=True
            )
            self.ws_thread.start()

            # Wait for connection to establish (with timeout)
            connection_timeout = 10
            start_time = time.time()
            while not self.connected and (time.time() - start_time) < connection_timeout:
                time.sleep(0.1)

            if not self.connected:
                raise ConnectionError("WebSocket connection timeout")

            logger.info("WebSocket connection established")

        except Exception as e:
            logger.error(f"Failed to connect WebSocket: {e}")
            raise

    def disconnect_websocket(self):
        """Disconnect WebSocket"""
        try:
            self.reconnect_requested = True
            self.stop_heartbeat = True

            if self.ws_app:
                self.ws_app.close()

            if self.ws_thread and self.ws_thread.is_alive():
                self.ws_thread.join(timeout=5)

            self.connected = False
            logger.info("WebSocket disconnected")
        except Exception as e:
            logger.error(f"Error disconnecting WebSocket: {e}")

    def start_heartbeat(self):
        """Start heartbeat thread"""
        self.stop_heartbeat = False

        def heartbeat_loop():
            while not self.stop_heartbeat:
                try:
                    if self.connected:
                        asyncio.run_coroutine_threadsafe(
                            self.send_heartbeat(),
                            self.event_loop
                        )
                except Exception as e:
                    logger.error(f"Heartbeat error: {e}")

                # Sleep in small intervals to allow quick shutdown
                for _ in range(300):  # 30 seconds = 300 * 0.1s
                    if self.stop_heartbeat:
                        break
                    time.sleep(0.1)

        self.heartbeat_thread = threading.Thread(target=heartbeat_loop, daemon=True)
        self.heartbeat_thread.start()

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

            self.send_message(register_message)
            logger.info("Client registered with server")
        except Exception as e:
            logger.error(f"Failed to register client: {e}", exc_info=True)

    async def handle_message(self, data: Dict[str, Any]):
        """Handle incoming messages from server"""
        try:
            message_type = data.get("Type")

            logger.info(f"Received message: {message_type}")

            # Report activity to burn-in protection
            if self.burn_in_protection:
                self.burn_in_protection.report_activity()

            if message_type == "REGISTRATION_RESPONSE":
                await self.handle_registration_response(data)
            elif message_type == "DISPLAY_UPDATE":
                await self.handle_display_update(data)
            elif message_type == "LAYOUT_ASSIGNED":
                await self.handle_layout_assigned(data)
            elif message_type == "DATA_UPDATE":
                await self.handle_data_update(data)
            elif message_type == "COMMAND":
                await self.handle_command(data)
            elif message_type == "HEARTBEAT":
                await self.send_heartbeat()
            elif message_type == "UPDATE_CONFIG":
                await self.handle_update_config(data)
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

                # Schedule a check for "no layout assigned" after 10 seconds
                async def check_for_no_layout():
                    await asyncio.sleep(10)
                    # If still connected but no layout received, show "no layout assigned" screen
                    if self.connected and not self.current_layout and self.display_renderer:
                        if self.display_renderer.status_screen_manager.is_showing_status:
                            logger.info("No layout received after 10 seconds - showing 'No Layout Assigned' screen")
                            # Get IP address
                            device_info = await self.device_manager.get_device_info()
                            ip_address = device_info.get("ip_address", "Unknown")
                            server_url = self.config.get_server_url()
                            self.display_renderer.status_screen_manager.show_no_layout_assigned(
                                self.config.client_id,
                                server_url,
                                ip_address
                            )

                asyncio.create_task(check_for_no_layout())

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

    async def handle_layout_assigned(self, data: Dict[str, Any]):
        """Handle layout assignment with SQL data sources"""
        try:
            layout = data.get("Layout")
            linked_data_sources = data.get("LinkedDataSources", [])

            if layout:
                self.current_layout = layout
                layout_name = layout.get('Name', 'Unknown')
                logger.info(f"Layout assigned: {layout_name} with {len(linked_data_sources)} linked data sources")

                # Cache all linked data sources
                if self.display_renderer and linked_data_sources:
                    for ds_info in linked_data_sources:
                        data_source_id = ds_info.get('DataSourceId')
                        initial_data = ds_info.get('InitialData', [])
                        ds_name = ds_info.get('Name', 'Unknown')

                        if data_source_id:
                            # Convert GUID to string
                            data_source_id_str = str(data_source_id)
                            self.display_renderer.cache_data_source(data_source_id_str, initial_data)
                            logger.info(f"Cached data source '{ds_name}' ({data_source_id_str}) with {len(initial_data)} rows")

                # Save layout to cache for offline operation
                self.cache_manager.save_layout(layout, {}, set_current=True)

                # Render the layout
                if self.display_renderer:
                    await self.display_renderer.render_layout(layout, {})
                else:
                    logger.warning("Display renderer not initialized")
            else:
                logger.warning("Received LAYOUT_ASSIGNED without layout data")
        except Exception as e:
            logger.error(f"Error handling layout assignment: {e}", exc_info=True)

    async def handle_data_update(self, data: Dict[str, Any]):
        """Handle data source update from server"""
        try:
            data_source_id = data.get("DataSourceId")
            new_data = data.get("Data", [])
            timestamp = data.get("Timestamp")

            if data_source_id:
                # Convert GUID to string
                data_source_id_str = str(data_source_id)
                logger.info(f"Received data update for source {data_source_id_str}: {len(new_data)} rows")

                # Update cached data
                if self.display_renderer:
                    self.display_renderer.update_data_source(data_source_id_str, new_data)
                    logger.info(f"Updated data source {data_source_id_str}")
                else:
                    logger.warning("Display renderer not initialized")
            else:
                logger.warning("Received DATA_UPDATE without DataSourceId")
        except Exception as e:
            logger.error(f"Error handling data update: {e}", exc_info=True)

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
            elif command == "GET_LOGS":
                await self.send_logs()
            elif command == "UPDATE":
                await self.update_client()
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

            self.send_message(heartbeat_message)
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

            self.send_message(status_message)
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

            self.send_message(screenshot_message)
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

    async def send_logs(self):
        """Send recent log entries to server"""
        try:
            import subprocess

            logger.info("Collecting logs for server...")

            # Get recent logs from journalctl (last 100 lines)
            try:
                result = subprocess.run(
                    ['journalctl', '-u', 'digitalsignage-client', '-n', '100', '--no-pager'],
                    capture_output=True,
                    text=True,
                    timeout=10
                )

                if result.returncode == 0:
                    log_data = result.stdout
                else:
                    log_data = f"Failed to get logs: {result.stderr}"
                    logger.warning(f"journalctl failed: {result.stderr}")

            except subprocess.TimeoutExpired:
                log_data = "Failed to get logs: Command timed out"
                logger.error("journalctl command timed out")
            except FileNotFoundError:
                log_data = "journalctl not available (not running as systemd service)"
                logger.warning("journalctl not available")
            except Exception as e:
                log_data = f"Failed to get logs: {e}"
                logger.error(f"Error getting logs: {e}")

            # Send logs to server
            log_message = {
                "Type": "LOG",
                "ClientId": self.config.client_id,
                "Level": "INFO",
                "Message": "Client logs requested",
                "LogData": log_data,
                "Timestamp": datetime.utcnow().isoformat()
            }

            self.send_message(log_message)
            logger.info("Logs sent to server successfully")

        except Exception as e:
            logger.error(f"Failed to send logs: {e}", exc_info=True)

    async def update_client(self):
        """Update client code from git repository"""
        try:
            import subprocess
            import os

            logger.info("Starting client update from git...")

            # Get current directory (should be /opt/digitalsignage-client)
            client_dir = os.path.dirname(os.path.abspath(__file__))
            logger.info(f"Client directory: {client_dir}")

            # Check if we're in a git repository
            if not os.path.exists(os.path.join(client_dir, '.git')):
                logger.error("Not in a git repository, cannot update")
                return

            # Perform git pull
            try:
                result = subprocess.run(
                    ['git', 'pull'],
                    cwd=client_dir,
                    capture_output=True,
                    text=True,
                    timeout=60
                )

                if result.returncode == 0:
                    logger.info(f"Git pull successful: {result.stdout}")

                    # Check if there were actual updates
                    if "Already up to date" in result.stdout or "Already up-to-date" in result.stdout:
                        logger.info("Client is already up to date")
                    else:
                        logger.info("Client code updated, restart required")
                        logger.info("To apply updates, run: sudo systemctl restart digitalsignage-client")

                        # Optionally send notification to server
                        status_message = {
                            "Type": "STATUS_REPORT",
                            "ClientId": self.config.client_id,
                            "Status": "Online",
                            "Message": "Client updated successfully, restart required",
                            "Timestamp": datetime.utcnow().isoformat()
                        }
                        self.send_message(status_message)

                else:
                    logger.error(f"Git pull failed: {result.stderr}")

            except subprocess.TimeoutExpired:
                logger.error("Git pull command timed out")
            except FileNotFoundError:
                logger.error("git command not found")
            except Exception as e:
                logger.error(f"Error running git pull: {e}")

        except Exception as e:
            logger.error(f"Failed to update client: {e}", exc_info=True)

    async def handle_update_config(self, data: Dict[str, Any]):
        """Handle configuration update from server"""
        try:
            logger.info("Received UPDATE_CONFIG message from server")

            # Update configuration
            success = self.config.update_from_server(data)

            # Send response to server
            response_message = {
                "Type": "UPDATE_CONFIG_RESPONSE",
                "ClientId": self.config.client_id,
                "Success": success,
                "ErrorMessage": None if success else "Failed to update configuration",
                "Timestamp": datetime.utcnow().isoformat()
            }

            self.send_message(response_message)

            if success:
                logger.info("Configuration updated successfully. Reconnecting to server with new settings...")
                logger.info(f"New server: {self.config.server_host}:{self.config.server_port}")
                logger.info(f"SSL: {self.config.use_ssl}, FullScreen: {self.config.fullscreen}")

                # Disconnect current connection
                self.disconnect_websocket()

                # Wait a moment for disconnect to complete
                await asyncio.sleep(2)

                # Reconnect to server with new settings
                server_url = self.config.get_server_url()
                logger.info(f"Reconnecting to new server at {server_url}")

                self.reconnect_requested = False
                self.connect_websocket(server_url)

                logger.info("Successfully reconnected with new configuration")
            else:
                logger.error("Failed to update configuration")

        except Exception as e:
            logger.error(f"Error handling UPDATE_CONFIG: {e}", exc_info=True)

            # Send error response
            try:
                error_response = {
                    "Type": "UPDATE_CONFIG_RESPONSE",
                    "ClientId": self.config.client_id,
                    "Success": False,
                    "ErrorMessage": str(e),
                    "Timestamp": datetime.utcnow().isoformat()
                }
                self.send_message(error_response)
            except Exception as response_error:
                logger.error(f"Failed to send error response: {response_error}")

    async def start_reconnection(self):
        """Start automatic reconnection with exponential backoff and server discovery"""
        if self.reconnection_in_progress:
            logger.info("Reconnection already in progress, skipping")
            return

        self.reconnection_in_progress = True
        self.stop_reconnection = False
        attempt = 0
        retry_delays = [5, 10, 20, 30, 60]  # Exponential backoff delays (seconds)

        logger.info("=" * 70)
        logger.info("AUTOMATIC RECONNECTION STARTED")
        logger.info("=" * 70)

        # Log disconnect behavior mode
        if self.config.show_cached_layout_on_disconnect:
            logger.info("Reconnect mode: SILENT (no status screens, cached layout displayed)")
        else:
            logger.info("Reconnect mode: VISIBLE (status screens shown during reconnection)")

        # Show disconnected status only if configured to NOT show cached layout
        # or if no cached layout is available
        if not self.config.show_cached_layout_on_disconnect:
            if self.display_renderer and (not self.current_layout or self.display_renderer.status_screen_manager.is_showing_status):
                server_url = self.config.get_server_url()
                self.display_renderer.status_screen_manager.show_server_disconnected(
                    server_url,
                    self.config.client_id
                )

        try:
            while not self.stop_reconnection and not self.connected:
                attempt += 1
                delay_index = min(attempt - 1, len(retry_delays) - 1)
                retry_delay = retry_delays[delay_index]

                logger.info(f"Reconnection attempt #{attempt}")

                # Step 1: Try server discovery if enabled
                discovered_url = None
                if self.config.auto_discover:
                    logger.info("Attempting server discovery...")
                    try:
                        from discovery import discover_server
                        discovered_url = discover_server(timeout=3.0)  # Quick 3s discovery

                        if discovered_url:
                            logger.info(f"✓ Server discovered: {discovered_url}")

                            # Show "Server Found" status only if not showing cached layout
                            if self.display_renderer and not self.config.show_cached_layout_on_disconnect:
                                self.display_renderer.status_screen_manager.show_server_found(discovered_url)

                            # Update config with discovered server
                            import re
                            match = re.match(r'(wss?)://([^:]+):(\d+)/(.+)', discovered_url)
                            if match:
                                protocol, host, port, endpoint = match.groups()
                                self.config.server_host = host
                                self.config.server_port = int(port)
                                self.config.endpoint_path = endpoint
                                self.config.use_ssl = (protocol == 'wss')
                                self.config.save()

                            await asyncio.sleep(1)  # Brief pause to show "Server Found" screen
                        else:
                            logger.info("No server discovered, using configured address")
                    except Exception as e:
                        logger.warning(f"Server discovery failed: {e}")

                # Step 2: Attempt connection
                server_url = self.config.get_server_url()
                logger.info(f"Attempting connection to: {server_url}")

                # Show connecting status only if not showing cached layout
                if self.display_renderer and not self.config.show_cached_layout_on_disconnect:
                    self.display_renderer.status_screen_manager.show_connecting(
                        server_url,
                        attempt,
                        5  # Max attempts before delay
                    )

                try:
                    self.connect_websocket(server_url)

                    if self.connected:
                        logger.info("✓ Reconnection successful!")
                        logger.info("=" * 70)
                        self.reconnection_in_progress = False
                        self.offline_mode = False

                        # CRITICAL FIX: Clear status screen immediately upon successful connection
                        # This prevents the "Reconnecting..." status from staying visible
                        # when the client has successfully reconnected
                        if self.display_renderer and self.display_renderer.status_screen_manager.is_showing_status:
                            logger.info("Clearing reconnection status screen after successful connection")
                            self.display_renderer.status_screen_manager.clear_status_screen()

                        # The registration response handler will show "No Layout Assigned" after 10 seconds
                        # if no layout is received, OR render the assigned layout if one is sent

                        return

                except Exception as e:
                    logger.warning(f"Connection attempt failed: {e}")

                # Step 3: Wait before retry with countdown display
                if not self.connected and not self.stop_reconnection:
                    logger.info(f"Waiting {retry_delay} seconds before next attempt...")

                    # Show reconnecting screen with countdown only if not showing cached layout
                    for remaining in range(retry_delay, 0, -1):
                        if self.stop_reconnection or self.connected:
                            break

                        if self.display_renderer and not self.config.show_cached_layout_on_disconnect and remaining % 5 == 0:  # Update every 5 seconds
                            self.display_renderer.status_screen_manager.show_reconnecting(
                                server_url,
                                attempt,
                                remaining,
                                self.config.client_id
                            )

                        await asyncio.sleep(1)

            if self.connected:
                logger.info("Reconnection successful")
            elif self.stop_reconnection:
                logger.info("Reconnection stopped by user")

        except Exception as e:
            logger.error(f"Error during reconnection: {e}", exc_info=True)
        finally:
            self.reconnection_in_progress = False
            logger.info("Reconnection process ended")

    async def start(self):
        """Start the client application"""
        logger.info("Starting Digital Signage Client...")

        # Start web interface
        try:
            logger.info("Starting web interface...")
            self.web_interface = WebInterface(self, port=5000, host='0.0.0.0')
            self.web_interface.start()
            logger.info("Web interface started successfully")
        except Exception as e:
            logger.error(f"Failed to start web interface: {e}", exc_info=True)
            logger.warning("Continuing without web interface...")

        # Start watchdog monitoring
        await self.watchdog.start()
        self.watchdog.notify_status("Initializing...")

        # DIAGNOSTIC: Check auto_discover configuration
        logger.info("=" * 70)
        logger.info("AUTO-DISCOVERY CONFIGURATION CHECK")
        logger.info("=" * 70)
        logger.info(f"config.auto_discover = {self.config.auto_discover}")
        logger.info(f"config.auto_discover type = {type(self.config.auto_discover)}")
        logger.info(f"discovery_timeout = {self.config.discovery_timeout}")
        logger.info("=" * 70)

        # AUTO-DISCOVERY: Try to find server automatically if enabled
        if self.config.auto_discover:
            logger.info("=" * 70)
            logger.info("AUTO-DISCOVERY ENABLED - Searching for servers...")
            logger.info("Discovery methods: mDNS/Zeroconf (preferred) + UDP Broadcast (fallback)")
            logger.info("=" * 70)
            self.watchdog.notify_status("Searching for servers via Auto-Discovery...")

            # Show discovering server status screen (only during initial startup, not when reconnecting)
            # Note: This is called during start(), not during reconnection, so we always show it
            if self.display_renderer:
                self.display_renderer.status_screen_manager.show_discovering_server("mDNS/Zeroconf + UDP Broadcast")

            try:
                logger.info("Importing discovery module...")
                from discovery import discover_server
                logger.info("Discovery module imported successfully")
                logger.info(f"Calling discover_server with timeout={self.config.discovery_timeout}s")

                discovered_url = discover_server(timeout=self.config.discovery_timeout)
                logger.info(f"discover_server returned: {discovered_url}")

                if discovered_url:
                    logger.info(f"✓ SERVER FOUND: {discovered_url}")
                    logger.info("  Using auto-discovered server instead of config.json")

                    # Parse the discovered URL to update config
                    # Format: ws://192.168.1.100:8080/ws or wss://...
                    import re
                    match = re.match(r'(wss?)://([^:]+):(\d+)/(.+)', discovered_url)
                    if match:
                        protocol, host, port, endpoint = match.groups()
                        self.config.server_host = host
                        self.config.server_port = int(port)
                        self.config.endpoint_path = endpoint  # Save the endpoint path (e.g., "ws/")
                        self.config.use_ssl = (protocol == 'wss')
                        logger.info(f"  Server Host: {host}")
                        logger.info(f"  Server Port: {port}")
                        logger.info(f"  Endpoint Path: {endpoint}")
                        logger.info(f"  SSL: {'Enabled' if self.config.use_ssl else 'Disabled'}")

                        # Save discovered configuration for future use
                        self.config.save()
                        logger.info("  Configuration saved")
                else:
                    logger.warning("✗ No servers found via Auto-Discovery")
                    logger.warning("  Falling back to manual configuration from config.json")
                    logger.warning(f"  Manual config: {self.config.server_host}:{self.config.server_port}")
            except Exception as e:
                logger.error(f"Auto-Discovery failed: {e}")
                logger.error(f"Exception type: {type(e).__name__}")
                logger.error(f"Exception details: {str(e)}")
                import traceback
                logger.error(f"Traceback:\n{traceback.format_exc()}")
                logger.warning("Falling back to manual configuration from config.json")

            logger.info("=" * 70)
        else:
            logger.info("=" * 70)
            logger.info("AUTO-DISCOVERY DISABLED")
            logger.info("Using manual server configuration from config.json")
            logger.info(f"Server: {self.config.server_host}:{self.config.server_port}")
            logger.info("=" * 70)

        # Connect to server with infinite retry logic
        # Each batch: 5 attempts with exponential backoff (2s, 4s, 8s, 16s, 32s)
        # Between batches: 60 second wait
        max_retries_per_batch = 5
        batch_wait_time = 60
        connection_successful = False
        last_error = None
        batch_number = 0

        while not connection_successful:
            batch_number += 1
            retry_delay = 2  # Reset delay for each batch

            logger.info("=" * 70)
            logger.info(f"CONNECTION RETRY BATCH {batch_number}")
            logger.info("=" * 70)

            for attempt in range(max_retries_per_batch):
                try:
                    server_url = self.config.get_server_url()
                    protocol = self.config.get_websocket_protocol().upper()
                    logger.info(f"Connecting to server at {server_url} using {protocol} (batch {batch_number}, attempt {attempt + 1}/{max_retries_per_batch})")

                    self.watchdog.notify_status(f"Connecting (batch {batch_number}, attempt {attempt + 1}/{max_retries_per_batch})")

                    # Show connecting status screen
                    if self.display_renderer:
                        self.display_renderer.status_screen_manager.show_connecting(server_url, attempt + 1, max_retries_per_batch)

                    if self.config.use_ssl:
                        if self.config.verify_ssl:
                            logger.info("SSL certificate verification enabled")
                        else:
                            logger.warning("SSL certificate verification disabled - not recommended for production!")

                    self.connect_websocket(server_url)
                    connection_successful = True
                    logger.info("✓ Connection successful!")
                    break
                except Exception as e:
                    last_error = str(e)
                    if attempt < max_retries_per_batch - 1:
                        logger.warning(f"Connection attempt {attempt + 1} failed: {e}")
                        logger.info(f"Retrying in {retry_delay} seconds...")
                        await asyncio.sleep(retry_delay)
                        retry_delay = min(retry_delay * 2, 32)  # Exponential backoff (max 32s)
                    else:
                        logger.error(f"Batch {batch_number}: All {max_retries_per_batch} connection attempts failed")
                        logger.error(f"Last error: {e}")

            # If connection succeeded, break out of infinite loop
            if connection_successful:
                break

            # Connection failed - wait before next batch
            logger.warning("=" * 70)
            logger.warning(f"All {max_retries_per_batch} connection attempts in batch {batch_number} failed.")
            logger.warning(f"Waiting {batch_wait_time} seconds before next batch...")
            logger.warning("=" * 70)

            self.watchdog.notify_status(f"Waiting {batch_wait_time}s before retry batch {batch_number + 1}")

            # Show connection error status screen during wait
            if self.display_renderer:
                server_url = self.config.get_server_url()
                error_msg = f"{last_error if last_error else 'Connection failed'} (Batch {batch_number})"
                self.display_renderer.status_screen_manager.show_connection_error(
                    server_url,
                    error_msg,
                    self.config.client_id
                )

            await asyncio.sleep(batch_wait_time)

        # Connection successful - update status
        self.watchdog.notify_status("Connected to server")
        self.watchdog.notify_ready()

        # CRITICAL FIX: Clear connection status screen after initial successful connection
        # This prevents the "Connecting..." status from staying visible
        if self.display_renderer and self.display_renderer.status_screen_manager.is_showing_status:
            logger.info("Clearing initial connection status screen after successful connection")
            self.display_renderer.status_screen_manager.clear_status_screen()

        # The registration response handler will show "No Layout Assigned" after 10 seconds
        # if no layout is received, OR render the assigned layout if one is sent

        # Keep the asyncio loop running
        try:
            while True:
                await asyncio.sleep(1)
        except Exception as e:
            logger.error(f"Client error: {e}", exc_info=True)

    async def stop(self):
        """Stop the client application"""
        logger.info("Stopping Digital Signage Client...")
        self.watchdog.notify_stopping()

        # Stop web interface
        if self.web_interface:
            try:
                self.web_interface.stop()
            except Exception as e:
                logger.error(f"Error stopping web interface: {e}")

        # Stop reconnection if in progress
        self.stop_reconnection = True

        # Disconnect WebSocket
        self.disconnect_websocket()

        # Stop watchdog
        await self.watchdog.stop()


def test_mode():
    """Run diagnostic tests without starting the full client"""
    print("")
    print("=" * 70)
    print("DIGITAL SIGNAGE CLIENT - DIAGNOSTIC MODE")
    print("=" * 70)
    print("")

    # Test 1: Environment variables
    print("[TEST 1] Environment Variables")
    print("-" * 70)
    display = os.environ.get('DISPLAY', 'NOT SET')
    xauth = os.environ.get('XAUTHORITY', 'NOT SET')
    user = os.environ.get('USER', 'NOT SET')

    print(f"  DISPLAY:     {display}")
    print(f"  XAUTHORITY:  {xauth}")
    print(f"  USER:        {user}")

    if display == 'NOT SET':
        print("  STATUS: FAILED - DISPLAY not set")
        print("  FIX: Export DISPLAY=:0 or check X11 is running")
    else:
        print("  STATUS: OK")
    print("")

    # Test 2: X11 Connection
    print("[TEST 2] X11 Display Server")
    print("-" * 70)
    try:
        # Test if we can create a QApplication (requires X11)
        app = QApplication([])
        print("  X11 Connection: OK")
        print("  QApplication created successfully")
        app.quit()
        print("  STATUS: OK")
    except Exception as e:
        print(f"  X11 Connection: FAILED - {e}")
        print("  STATUS: FAILED")
        print("  FIX: Make sure X11 is running and DISPLAY is set correctly")
    print("")

    # Test 3: Configuration
    print("[TEST 3] Configuration Loading")
    print("-" * 70)
    try:
        config = Config.load()
        print(f"  Config File: /opt/digitalsignage-client/config.json")
        print(f"  Client ID:   {config.client_id}")
        print(f"  Server:      {config.server_host}:{config.server_port}")
        print(f"  SSL:         {config.use_ssl}")
        print(f"  Fullscreen:  {config.fullscreen}")
        print("  STATUS: OK")
    except Exception as e:
        print(f"  Configuration Error: {e}")
        print("  STATUS: FAILED")
        print("  FIX: Check /opt/digitalsignage-client/config.json exists and is valid JSON")
    print("")

    # Test 4: Module Imports
    print("[TEST 4] Required Modules")
    print("-" * 70)
    modules = [
        ('websocket', 'websocket-client>=1.6.0'),
        ('PyQt5.QtWidgets', 'python3-pyqt5'),
        ('PyQt5.QtCore', 'python3-pyqt5'),
        ('display_renderer', 'display_renderer.py'),
        ('device_manager', 'device_manager.py'),
        ('cache_manager', 'cache_manager.py'),
        ('watchdog_monitor', 'watchdog_monitor.py'),
    ]

    all_ok = True
    for module_name, package in modules:
        try:
            __import__(module_name)
            print(f"  {module_name:25s} OK")
        except ImportError as e:
            print(f"  {module_name:25s} FAILED - {e}")
            all_ok = False

    print("")
    if all_ok:
        print("  STATUS: OK - All modules imported successfully")
    else:
        print("  STATUS: FAILED - Some modules missing")
    print("")

    # Test 5: Directories
    print("[TEST 5] Directories and Permissions")
    print("-" * 70)
    dirs_to_check = [
        Path.home() / ".digitalsignage" / "cache",
        Path.home() / ".digitalsignage" / "data",
        Path("/opt/digitalsignage-client"),
    ]

    for dir_path in dirs_to_check:
        if dir_path.exists():
            writable = os.access(dir_path, os.W_OK)
            status = "OK (writable)" if writable else "WARNING (not writable)"
            print(f"  {str(dir_path):40s} {status}")
        else:
            print(f"  {str(dir_path):40s} MISSING")
    print("")

    # Test 6: Summary
    print("=" * 70)
    print("DIAGNOSTIC SUMMARY")
    print("=" * 70)
    print("")
    print("If all tests passed, the client should start successfully.")
    print("If any tests failed, fix the issues above before starting the service.")
    print("")
    print("To start the service:")
    print("  sudo systemctl start digitalsignage-client")
    print("")
    print("To view logs:")
    print("  sudo journalctl -u digitalsignage-client -f")
    print("")
    print("=" * 70)


def main():
    """Main entry point"""
    try:
        # Check for test mode flag
        if len(sys.argv) > 1 and sys.argv[1] in ['--test', '-t', '--diagnose']:
            test_mode()
            sys.exit(0)

        logger.info("Loading configuration...")
        config = Config.load()
        logger.info(f"Configuration loaded - Server: {config.server_host}:{config.server_port}")

        logger.info("Creating Qt application...")
        # Create Qt application for display rendering
        app = QApplication(sys.argv)
        logger.info("Qt application created successfully")

        logger.info("Initializing Digital Signage Client...")
        # Create and start client
        client = DigitalSignageClient(config)

        logger.info("Creating display renderer...")
        # Create display renderer
        client.display_renderer = DisplayRenderer(fullscreen=config.fullscreen)
        client.display_renderer.show()

        # Initialize burn-in protection if enabled
        if config.burn_in_protection_enabled:
            logger.info("Initializing anti-burn-in protection...")
            client.burn_in_protection = BurnInProtection(
                widget=client.display_renderer,
                enabled=True,
                pixel_shift_interval=config.burn_in_pixel_shift_interval,
                pixel_shift_max=config.burn_in_pixel_shift_max,
                screensaver_timeout=config.burn_in_screensaver_timeout
            )
            logger.info("Anti-burn-in protection initialized")
        else:
            logger.info("Anti-burn-in protection disabled in configuration")

        # CRITICAL FIX: Force window to front after creation (especially important after boot)
        # Problem: PyQt5 window may be created but not visible on HDMI after reboot
        # Solution: Explicitly raise and activate window multiple times with delay
        client.display_renderer.raise_()
        client.display_renderer.activateWindow()
        client.display_renderer.setFocus()

        # Additional fix: Use QTimer to re-raise window after event loop starts
        # This ensures window is visible even if desktop is still loading
        def ensure_window_visible():
            logger.info("Ensuring display window is visible and on top...")
            client.display_renderer.raise_()
            client.display_renderer.activateWindow()
            client.display_renderer.setFocus()
            client.display_renderer.showFullScreen() if config.fullscreen else client.display_renderer.show()
            logger.info("Display window visibility ensured")

        # Schedule window visibility check after 2 seconds (when event loop is running)
        QTimer.singleShot(2000, ensure_window_visible)

        logger.info("Display renderer created and shown")

        logger.info("Starting client event loop...")
        # Start client in asyncio event loop using qasync for Qt+asyncio integration
        try:
            import qasync
            loop = qasync.QEventLoop(app)
            asyncio.set_event_loop(loop)

            # Store event loop reference for WebSocket callbacks
            client.event_loop = loop

            with loop:
                loop.create_task(client.start())
                logger.info("Starting Qt+asyncio event loop...")
                loop.run_forever()
        except ImportError:
            # Fallback: Use asyncio event loop with Qt integration
            logger.warning("qasync not available - using fallback integration")
            loop = asyncio.get_event_loop()

            # Store event loop reference for WebSocket callbacks
            client.event_loop = loop

            # Start the asyncio task
            loop.create_task(client.start())

            # Run asyncio event loop in a separate thread
            def run_asyncio_loop():
                asyncio.set_event_loop(loop)
                loop.run_forever()

            asyncio_thread = threading.Thread(target=run_asyncio_loop, daemon=True)
            asyncio_thread.start()

            logger.info("Starting Qt event loop...")
            # Run Qt event loop
            sys.exit(app.exec_())

    except KeyboardInterrupt:
        logger.info("Received keyboard interrupt - shutting down...")
        sys.exit(0)
    except Exception as e:
        logger.error("=" * 70)
        logger.error("FATAL ERROR - Client crashed during startup")
        logger.error("=" * 70)
        logger.error(f"Error: {e}")
        logger.error("")
        logger.error("Full traceback:")
        logger.error(traceback.format_exc())
        logger.error("=" * 70)
        logger.error("")
        logger.error("Troubleshooting steps:")
        logger.error("  1. Run diagnostic mode: python3 client.py --test")
        logger.error("  2. Check logs: sudo journalctl -u digitalsignage-client -n 50")
        logger.error("  3. Verify X11 is running: echo $DISPLAY")
        logger.error("  4. Check configuration: cat /opt/digitalsignage-client/config.json")
        logger.error("=" * 70)
        sys.exit(1)


if __name__ == "__main__":
    main()
