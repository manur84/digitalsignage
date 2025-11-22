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
from logging.handlers import RotatingFileHandler
from pathlib import Path

# Create logs directory
log_dir = Path.home() / '.digitalsignage' / 'logs'
log_dir.mkdir(parents=True, exist_ok=True)
log_file = log_dir / 'client.log'

# Configure logging with both file and console handlers
handlers = [
    logging.StreamHandler(sys.stdout),
    RotatingFileHandler(
        log_file,
        maxBytes=5*1024*1024,  # 5 MB
        backupCount=3,
        encoding='utf-8'
    )
]

logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=handlers
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

# Auto-Discovery Configuration Constants
MAX_DISCOVERY_ATTEMPTS = 10  # Max 10 scans à 5 seconds = 50 seconds timeout
DISCOVERY_RETRY_DELAY = 2    # 2 seconds delay between discovery scans

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
        self.message_lock = threading.Lock()  # Protects pending_messages list
        self.send_lock = threading.Lock()  # CRITICAL: Protects WebSocket send() from race conditions
        self.connection_event = threading.Event()  # Signals when connection is established

        # Reconnection state
        self.reconnection_in_progress = False
        self.reconnection_task = None
        self.stop_reconnection = False

        # Web interface
        self.web_interface: Optional[WebInterface] = None

        # CRITICAL FIX: Track cached layout display to prevent flickering
        self._cached_layout_displayed = False

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
        """Send a message to the server (used by remote log handler)

        CRITICAL: Thread-safe implementation!
        - Uses send_lock to prevent race conditions when multiple threads send simultaneously
        - websocket-client library is NOT thread-safe for concurrent sends
        """
        try:
            # Check connection WITHOUT blocking on lock first (fast path)
            if self.connection_event.is_set() and self.ws_app:
                # CRITICAL: Acquire send lock to prevent race conditions
                # Multiple threads (heartbeat, remote logging, commands) can call this simultaneously
                with self.send_lock:
                    # Double-check connection state after acquiring lock
                    if self.connected and self.ws_app:
                        message_json = json.dumps(message)
                        self.ws_app.send(message_json)
                        return  # Success

            # Not connected - queue message for later
            with self.message_lock:
                self.pending_messages.append(message)
                logger.debug(f"Message queued (not connected): {len(self.pending_messages)} pending")
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
        """Send any messages that were queued while disconnected

        CRITICAL: Thread-safe implementation with proper locking!
        """
        # Copy pending messages under lock
        with self.message_lock:
            if not self.pending_messages:
                return  # Nothing to send
            messages_to_send = self.pending_messages.copy()
            self.pending_messages.clear()

        # Send messages with send_lock (NOT message_lock to avoid deadlock)
        if self.connection_event.is_set() and self.ws_app:
            logger.info(f"Flushing {len(messages_to_send)} pending messages")
            for message in messages_to_send:
                try:
                    with self.send_lock:
                        if self.connected and self.ws_app:
                            message_json = json.dumps(message)
                            self.ws_app.send(message_json)
                except Exception as e:
                    logger.error(f"Failed to send pending message: {e}")
                    # Re-queue failed message
                    with self.message_lock:
                        self.pending_messages.append(message)

    def on_open(self, ws):
        """WebSocket connection opened"""
        logger.info("WebSocket connection opened")
        self.connected = True
        self.offline_mode = False
        self.connection_event.set()  # CRITICAL: Signal connection is ready for sending
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
            # CRITICAL DEBUG LOGGING: Track ALL incoming messages
            logger.info("=" * 70)
            logger.info("RAW MESSAGE RECEIVED FROM SERVER")
            logger.info("=" * 70)

            # Log message type and size
            if isinstance(message, bytes):
                logger.info(f"Message Type: BINARY ({len(message)} bytes)")
            else:
                logger.info(f"Message Type: TEXT ({len(message)} chars)")

            # Check if message is binary (compressed)
            if isinstance(message, bytes):
                # Check for gzip header (0x1F 0x8B)
                if len(message) >= 2 and message[0] == 0x1F and message[1] == 0x8B:
                    # Decompress gzip data
                    try:
                        decompressed = gzip.decompress(message)
                        message_str = decompressed.decode('utf-8')
                        logger.info(f"Decompressed: {len(message)} → {len(decompressed)} bytes")
                    except Exception as e:
                        logger.error(f"Failed to decompress message: {e}")
                        return
                else:
                    # Binary but not compressed, decode as UTF-8
                    message_str = message.decode('utf-8')
                    logger.info("Binary message (not compressed) - decoded as UTF-8")
            else:
                # Text message
                message_str = message
                logger.info("Text message received")

            # Log first 500 chars of message
            logger.info(f"Message Content (first 500 chars): {message_str[:500]}")

            data = json.loads(message_str)
            message_type = data.get("Type", "UNKNOWN")

            logger.info(f"Parsed Message Type: {message_type}")
            logger.info("=" * 70)

            # Schedule message handling in asyncio loop
            future = asyncio.run_coroutine_threadsafe(
                self.handle_message(data),
                self.event_loop
            )
            # Add a callback to handle any exceptions in the coroutine
            def handle_future_exception(fut):
                try:
                    fut.result()
                    logger.info(f"✓ Message {message_type} handled successfully")
                except Exception as e:
                    logger.error(f"✗ Error in message handler coroutine for {message_type}: {e}", exc_info=True)
            future.add_done_callback(handle_future_exception)
        except json.JSONDecodeError as e:
            logger.error(f"Failed to parse JSON message: {e}")
            logger.error(f"Raw message: {message_str if 'message_str' in locals() else message}")
        except Exception as e:
            logger.error(f"Error processing message: {e}", exc_info=True)

    def on_error(self, ws, error):
        """WebSocket error occurred"""
        logger.error("=" * 70)
        logger.error("WEBSOCKET ERROR OCCURRED")
        logger.error("=" * 70)
        logger.error(f"Error type: {type(error).__name__}")
        logger.error(f"Error message: {error}")

        # Provide specific error details
        import errno
        import ssl

        if isinstance(error, ConnectionRefusedError):
            logger.error("Connection refused - server not accepting connections")
            logger.error("  Check if server is running")
            logger.error("  Check firewall settings")
        elif isinstance(error, ssl.SSLError):
            logger.error("SSL/TLS error - certificate or encryption issue")
            logger.error(f"  SSL Details: {error}")
            logger.error("  Try disabling SSL verification in config.json")
        elif isinstance(error, OSError):
            if hasattr(error, 'errno'):
                if error.errno == errno.ENETUNREACH:
                    logger.error("Network unreachable - check network connection")
                elif error.errno == errno.ETIMEDOUT:
                    logger.error("Connection timed out - server not responding")
                elif error.errno == errno.ECONNREFUSED:
                    logger.error("Connection refused - server not listening on this port")

        logger.error("=" * 70)

    def on_close(self, ws, close_status_code, close_msg):
        """WebSocket connection closed"""
        logger.warning(f"WebSocket connection closed (code: {close_status_code}, msg: {close_msg})")
        self.connected = False
        self.connection_event.clear()  # CRITICAL: Clear connection state

        if not self.reconnect_requested:
            # Unexpected disconnect - enter offline mode and start reconnection
            self.offline_mode = True
            self.watchdog.notify_status("Disconnected - attempting reconnection")

            # Check config to determine disconnect behavior
            if self.config.show_cached_layout_on_disconnect:
                # Show cached layout continuously (no status screen switching)
                logger.info("Disconnect behavior: Showing cached layout (no status screen)")

                # CRITICAL FIX: Load cached layout FIRST before starting reconnection
                # This ensures the layout is displayed and visible BEFORE reconnection attempts
                # which might show status screens that would cover the cached layout
                future = asyncio.run_coroutine_threadsafe(
                    self.load_cached_layout(),
                    self.event_loop
                )

                # Add a callback to handle any exceptions in the coroutine
                def handle_future_exception(fut):
                    try:
                        fut.result()
                        logger.info("Cached layout loaded successfully - now starting reconnection in background")
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
            logger.info("=" * 70)
            logger.info("WEBSOCKET CONNECTION ATTEMPT")
            logger.info("=" * 70)
            logger.info(f"Input server_url: {server_url}")

            # Parse URL to extract components
            # server_url format: https://host:port/path (ALWAYS HTTPS - no HTTP support)
            # We need: wss://host:port/path (ALWAYS WSS - no WS support)
            # CRITICAL: FORCE WSS-only connection regardless of input URL
            # Even if someone manually set http:// in config, we convert to wss://
            ws_url = server_url.replace('https://', 'wss://').replace('http://', 'wss://')  # FORCE WSS

            logger.info(f"Converted to WebSocket URL: {ws_url}")

            # Extract components for detailed logging
            import re
            match = re.match(r'(wss?)://([^/:]+):(\d+)/(.+)', ws_url)
            if match:
                protocol, host, port, endpoint = match.groups()
                logger.info(f"  Protocol: {protocol}")
                logger.info(f"  Host: {host}")
                logger.info(f"  Port: {port}")
                logger.info(f"  Endpoint: {endpoint}")
            else:
                logger.warning(f"Could not parse WebSocket URL: {ws_url}")
                logger.warning("  URL format should be: ws[s]://host:port/endpoint")

            logger.info("=" * 70)

            # Configure SSL options
            sslopt = None
            if ws_url.startswith('wss://'):
                if not self.config.verify_ssl:
                    import ssl
                    # CRITICAL FIX: Accept self-signed certificates
                    # cert_reqs=CERT_NONE disables certificate validation
                    # check_hostname=False disables hostname verification
                    sslopt = {
                        "cert_reqs": ssl.CERT_NONE,
                        "check_hostname": False
                    }
                    logger.warning("SSL certificate verification disabled (self-signed certificates accepted)")
                    logger.info("  SSL config: cert_reqs=NONE, check_hostname=False")

            # CRITICAL DEBUG: Enable WebSocket trace logging to diagnose why messages are not received
            # This will show EVERY frame received/sent at the lowest level
            websocket.enableTrace(True)
            logger.info("WebSocket trace logging ENABLED - all frames will be logged")

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
                logger.error("=" * 70)
                logger.error("WebSocket CONNECTION TIMEOUT")
                logger.error("=" * 70)
                logger.error(f"Failed to connect within {connection_timeout} seconds")
                logger.error(f"URL attempted: {ws_url}")
                logger.error("Possible causes:")
                logger.error("  1. Server not running or not reachable")
                logger.error("  2. Wrong host/port/endpoint")
                logger.error("  3. Firewall blocking connection")
                logger.error("  4. SSL/TLS certificate issue")
                logger.error("=" * 70)
                raise ConnectionError(f"WebSocket connection timeout after {connection_timeout}s")

            logger.info("=" * 70)
            logger.info("✓ WEBSOCKET CONNECTION SUCCESSFUL")
            logger.info("=" * 70)
            logger.info(f"Connected to: {ws_url}")
            logger.info("=" * 70)

        except Exception as e:
            logger.error("=" * 70)
            logger.error("✗ WEBSOCKET CONNECTION FAILED")
            logger.error("=" * 70)
            logger.error(f"Error type: {type(e).__name__}")
            logger.error(f"Error message: {e}")
            logger.error(f"URL: {ws_url if 'ws_url' in locals() else server_url}")
            logger.error("=" * 70)
            import traceback
            logger.debug(f"Full traceback:\n{traceback.format_exc()}")
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

            # CRITICAL FIX: Send COMPLETE device_info dictionary instead of manually selecting fields
            # This ensures all fields (Model, OsVersion, ClientVersion, Resolution, etc.) are sent
            # Previously, we were manually building DeviceInfo which could miss fields
            register_message = {
                "Type": "REGISTER",
                "ClientId": self.config.client_id,
                "MacAddress": device_info["MacAddress"],
                "IpAddress": device_info["IpAddress"],
                "DeviceInfo": device_info,  # Send complete device info
                "Timestamp": datetime.utcnow().isoformat()
            }

            # Add registration token if configured
            if self.config.registration_token:
                register_message["RegistrationToken"] = self.config.registration_token
                logger.info("Sending registration with token")
            else:
                logger.info("Sending registration without token (for existing clients)")

            # DEBUG: Log what we're sending
            logger.info(f"Registering with DeviceInfo: Model={device_info.get('Model')}, OS={device_info.get('OsVersion')}, Version={device_info.get('ClientVersion')}, Resolution={device_info.get('ScreenWidth')}x{device_info.get('ScreenHeight')}")

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
                    logger.info("[DEBUG] check_for_no_layout() - Starting 10 second wait...")
                    await asyncio.sleep(10)

                    # CRITICAL FIX: Check if layout was assigned DURING the wait period
                    # This prevents showing "No Layout Assigned" when a layout is already displayed
                    logger.info("[DEBUG] check_for_no_layout() - Wait completed, checking conditions...")
                    logger.info(f"[DEBUG]   connected: {self.connected}")
                    logger.info(f"[DEBUG]   current_layout: {self.current_layout}")
                    logger.info(f"[DEBUG]   display_renderer: {self.display_renderer is not None}")

                    if self.connected and not self.current_layout and self.display_renderer:
                        # ADDITIONAL CHECK: Ensure status screen is not already cleared
                        # If status screen was cleared, it means a layout was rendered
                        is_showing_status = self.display_renderer.status_screen_manager.is_showing_status
                        logger.info(f"[DEBUG]   is_showing_status: {is_showing_status}")

                        if not is_showing_status:
                            logger.info("Status screen already cleared by layout rendering - skipping 'No Layout Assigned' screen")
                            return

                        logger.info("✓ CONDITIONS MET - No layout received after 10 seconds - showing 'No Layout Assigned' screen")

                        # Get IP address
                        logger.info("[DEBUG] Getting device info for IP address...")
                        device_info = await self.device_manager.get_device_info()
                        ip_address = device_info.get("IpAddress", "Unknown")
                        server_url = self.config.get_server_url()

                        logger.info(f"[DEBUG] Calling show_no_layout_assigned():")
                        logger.info(f"[DEBUG]   client_id: {self.config.client_id}")
                        logger.info(f"[DEBUG]   server_url: {server_url}")

                        try:
                            self.display_renderer.status_screen_manager.show_no_layout_assigned(
                                client_id=self.config.client_id,
                                server_url=server_url
                            )
                            logger.info("[DEBUG] ✓ show_no_layout_assigned() completed successfully")
                        except Exception as e:
                            logger.error(f"[DEBUG] ✗ ERROR calling show_no_layout_assigned(): {e}", exc_info=True)
                    else:
                        logger.debug("Layout assigned during registration wait period - not showing 'No Layout Assigned' screen")
                        logger.debug(f"  Reason: connected={self.connected}, has_layout={self.current_layout is not None}, has_renderer={self.display_renderer is not None}")

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

                # CRITICAL FIX: Reset cached layout flag when receiving new layout from server
                # This allows cached layout to be displayed again on next disconnect
                self._cached_layout_displayed = False

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
                    "Hostname": device_info.get("Hostname"),
                    "MdnsName": device_info.get("MdnsName"),
                    "CpuTemperature": device_info["CpuTemperature"],
                    "CpuUsage": device_info["CpuUsage"],
                    "MemoryUsed": device_info["MemoryUsed"],
                    "Uptime": device_info["Uptime"]
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
        """Load cached layout for offline operation

        CRITICAL FIX: Prevents flickering when showing cached layout on disconnect.
        Sets a flag to prevent repeated re-rendering of the same cached layout.
        """
        try:
            # CRITICAL FIX: Check if we're already showing this cached layout
            # This prevents repeated rendering that causes flickering
            cached = self.cache_manager.get_current_layout()
            if cached:
                layout, layout_data = cached
                layout_id = layout.get('Id')

                # CRITICAL: Check if this layout is ALREADY displayed
                # Prevents flickering by avoiding repeated render calls
                if (self.current_layout and
                    self.current_layout.get('Id') == layout_id and
                    hasattr(self, '_cached_layout_displayed') and
                    self._cached_layout_displayed):
                    logger.debug(f"Cached layout '{layout.get('Name')}' already displayed - skipping re-render to prevent flicker")
                    return

                self.current_layout = layout
                logger.info(f"Loaded cached layout: {layout.get('Name')} (Offline Mode)")

                if self.display_renderer:
                    await self.display_renderer.render_layout(layout, layout_data)
                    # CRITICAL: Set flag to prevent re-rendering
                    self._cached_layout_displayed = True
                    logger.info("Displaying cached layout in offline mode - re-render protection enabled")
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
        """
        Start automatic reconnection with exponential backoff and server discovery

        OPTIMIZED for smooth status screen transitions:
        - No unnecessary screen switches
        - Smooth countdown updates
        - Efficient event processing

        CRITICAL FIX for cached layout mode:
        - When show_cached_layout_on_disconnect=True, NO status screens are shown
        - Reconnection happens silently in background
        - Cached layout remains visible throughout
        """
        if self.reconnection_in_progress:
            logger.info("Reconnection already in progress, skipping")
            return

        self.reconnection_in_progress = True
        self.stop_reconnection = False
        attempt = 0
        retry_delays = [10, 20, 30, 60, 120]  # LONGER delays to prevent rapid looping (10s, 20s, 30s, 60s, 120s)

        logger.info("=" * 70)
        logger.info("AUTOMATIC RECONNECTION STARTED")
        logger.info("=" * 70)

        # Log disconnect behavior mode
        if self.config.show_cached_layout_on_disconnect:
            logger.info("Reconnect mode: SILENT (no status screens, cached layout displayed)")
            logger.info("Reconnection will happen in background without disturbing cached layout display")
        else:
            logger.info("Reconnect mode: VISIBLE (status screens shown during reconnection)")

        # Show disconnected status only if configured to NOT show cached layout
        if not self.config.show_cached_layout_on_disconnect:
            if self.display_renderer and (not self.current_layout or self.display_renderer.status_screen_manager.is_showing_status):
                server_url = self.config.get_server_url()
                # Use show_server_offline with auto_discovery flag
                self.display_renderer.status_screen_manager.show_server_offline(
                    server_url=server_url,
                    attempt=0,
                    retry_in=0,
                    auto_discovery_active=self.config.auto_discover
                )

        try:
            while not self.stop_reconnection and not self.connected:
                attempt += 1
                delay_index = min(attempt - 1, len(retry_delays) - 1)
                retry_delay = retry_delays[delay_index]

                logger.info(f"Reconnection attempt #{attempt} (retry in {retry_delay}s after failure)")

                # CRITICAL FIX: Reduce auto-discovery frequency to prevent flickering
                # Discovery should NOT run on EVERY attempt when cached layout is disabled
                # Only run discovery on first attempt or every 5th attempt to prevent rapid screen switching
                discovered_url = None

                # CRITICAL: Skip discovery entirely when showing cached layout (silent mode)
                if self.config.show_cached_layout_on_disconnect:
                    logger.debug("Skipping auto-discovery (silent reconnection mode with cached layout)")
                # CRITICAL: When NOT showing cached layout, run discovery sparingly to prevent flicker
                elif self.config.auto_discover:
                    # ANTI-FLICKER: Only run discovery on first attempt or every 5th attempt
                    # This prevents rapid switching between "Auto Discovery" and "Connecting" screens
                    if attempt == 1 or attempt % 5 == 0:
                        logger.info(f"Attempting server discovery (attempt {attempt})...")
                        try:
                            from discovery import discover_server
                            # ANTI-FLICKER: Longer timeout (5s instead of 3s) for more stable discovery
                            discovered_url = discover_server(timeout=5.0)

                            if discovered_url:
                                logger.info(f"✓ Server discovered: {discovered_url}")

                                # NO "Server Found" screen - go directly to connecting
                                # This prevents rapid screen switching

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
                    else:
                        logger.debug(f"Skipping auto-discovery on attempt {attempt} to prevent screen flickering (will retry on attempt {attempt + (5 - attempt % 5)})")

                # Step 2: Attempt connection
                server_url = self.config.get_server_url()
                logger.info(f"Attempting connection to: {server_url}")

                # ANTI-FLICKER: Show connecting status ONLY on first attempt or when screen actually changes
                # Don't update status screen on every single attempt - this causes rapid flickering
                # Only show on attempt 1, 5, 10, 15, etc. (every 5 attempts)
                if self.display_renderer and not self.config.show_cached_layout_on_disconnect:
                    if attempt == 1 or attempt % 5 == 0:
                        self.display_renderer.status_screen_manager.show_connecting(
                            server_url=server_url,
                            attempt=attempt
                        )
                        logger.debug(f"Updated connecting status screen (attempt {attempt})")
                    else:
                        logger.debug(f"Skipping status screen update on attempt {attempt} to prevent flicker")

                try:
                    self.connect_websocket(server_url)

                    if self.connected:
                        logger.info("✓ Reconnection successful!")
                        logger.info("=" * 70)
                        self.reconnection_in_progress = False
                        self.offline_mode = False

                        # CRITICAL FIX: Reset cached layout flag on successful reconnection
                        # This allows fresh server layout to be displayed
                        self._cached_layout_displayed = False

                        # CRITICAL FIX: Clear status screen immediately upon successful connection
                        if self.display_renderer and self.display_renderer.status_screen_manager.is_showing_status:
                            logger.info("Clearing reconnection status screen after successful connection")
                            self.display_renderer.status_screen_manager.clear_status_screen()

                        # Wait for layout assignment
                        # The registration response handler will show "No Layout Assigned" after 10 seconds
                        # if no layout is received, OR render the assigned layout if one is sent

                        return

                except Exception as e:
                    logger.warning(f"Connection attempt failed: {e}")

                # Step 3: Wait before retry with LONGER delays to prevent rapid looping
                if not self.connected and not self.stop_reconnection:
                    logger.info(f"Server unreachable - waiting {retry_delay} seconds before next attempt...")

                    # CRITICAL FIX: When showing cached layout, NO status screen updates
                    # Just wait silently in background
                    if self.config.show_cached_layout_on_disconnect:
                        logger.debug(f"Silent wait {retry_delay}s (cached layout remains visible)")
                        # Simple sleep without status updates
                        for remaining in range(retry_delay, 0, -1):
                            if self.stop_reconnection or self.connected:
                                break
                            await asyncio.sleep(1)
                    else:
                        # ANTI-FLICKER: Update status screen every 10 seconds (was 5s, was 3s before that)
                        # This dramatically reduces flickering by minimizing screen refreshes
                        # Users don't need second-by-second countdown updates - 10s intervals are fine
                        update_interval = 10

                        for remaining in range(retry_delay, 0, -1):
                            if self.stop_reconnection or self.connected:
                                break

                            # ANTI-FLICKER: Update status screen much less frequently
                            # Only update on: first second, every 10 seconds, and last second
                            # This prevents rapid screen redraws that cause flickering
                            should_update = (
                                remaining == retry_delay or  # First second
                                remaining % update_interval == 0 or  # Every 10 seconds
                                remaining <= 3  # Last 3 seconds (3, 2, 1)
                            )

                            if should_update:
                                if self.display_renderer and not self.config.show_cached_layout_on_disconnect:
                                    # Use show_server_offline with countdown
                                    self.display_renderer.status_screen_manager.show_server_offline(
                                        server_url=server_url,
                                        attempt=attempt,
                                        retry_in=remaining,
                                        auto_discovery_active=self.config.auto_discover
                                    )
                                    logger.debug(f"Updated server offline countdown: {remaining}s remaining")

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
            logger.info("AUTO-DISCOVERY MODE ENABLED")
            logger.info("Client will ONLY connect after successfully discovering a server")
            logger.info("Discovery methods: mDNS/Zeroconf (preferred) + UDP Broadcast (fallback)")
            logger.info("Discovery will run continuously until server is found")
            logger.info("=" * 70)

            # Show auto-discovery status screen ONCE before starting discovery loop
            if self.display_renderer:
                self.display_renderer.status_screen_manager.show_auto_discovery()

                # CRITICAL FIX: Give Qt event loop time to render status screen
                # Without this delay, the status screen widget is created but never painted/displayed
                # because we immediately start the discovery loop which blocks the event loop.
                # await asyncio.sleep(0) yields to event loop, allowing Qt to process paint events.
                await asyncio.sleep(0.1)  # 100ms to ensure status screen is fully rendered

                logger.info("Auto-discovery status screen displayed - starting discovery...")

            server_discovered = False
            discovery_attempt = 0

            # Import discovery module once
            try:
                from discovery import discover_server
                logger.info("Discovery module imported successfully")
            except Exception as e:
                logger.error(f"Failed to import discovery module: {e}")
                raise

            # OPTIMIZED DISCOVERY LOOP: Run discovery with timeout and retry delay
            # CRITICAL FIX: Do NOT call QApplication.processEvents() when using qasync
            # qasync automatically integrates Qt and asyncio event loops
            while not server_discovered and discovery_attempt < MAX_DISCOVERY_ATTEMPTS:
                discovery_attempt += 1
                logger.info(f"Discovery scan #{discovery_attempt}/{MAX_DISCOVERY_ATTEMPTS} starting...")
                self.watchdog.notify_status(f"Searching for servers (scan #{discovery_attempt}/{MAX_DISCOVERY_ATTEMPTS})...")

                try:
                    # Run discovery in asyncio executor (non-blocking)
                    # qasync handles Qt event processing automatically - no manual processEvents() needed
                    import concurrent.futures

                    with concurrent.futures.ThreadPoolExecutor() as executor:
                        future = executor.submit(discover_server, self.config.discovery_timeout)

                        # Wait for discovery to complete using asyncio (qasync handles Qt events)
                        while not future.done():
                            await asyncio.sleep(0.1)  # Allow event loop to run other tasks

                        discovered_url = future.result()

                    if discovered_url:
                        logger.info(f"✓ SERVER FOUND: {discovered_url}")
                        logger.info("  Using auto-discovered server")

                        # Parse the discovered URL to update config
                        import re
                        match = re.match(r'(wss?)://([^:]+):(\d+)/(.+)', discovered_url)
                        if match:
                            protocol, host, port, endpoint = match.groups()
                            self.config.server_host = host
                            self.config.server_port = int(port)
                            self.config.endpoint_path = endpoint
                            self.config.use_ssl = (protocol == 'wss')
                            logger.info(f"  Server Host: {host}")
                            logger.info(f"  Server Port: {port}")
                            logger.info(f"  Endpoint Path: {endpoint}")
                            logger.info(f"  SSL: {'Enabled' if self.config.use_ssl else 'Disabled'}")

                            # Save discovered configuration
                            self.config.save()
                            logger.info("  Configuration saved")
                            server_discovered = True
                        else:
                            logger.error(f"Failed to parse discovered URL: {discovered_url}")
                            if discovery_attempt < MAX_DISCOVERY_ATTEMPTS:
                                logger.info(f"Retrying in {DISCOVERY_RETRY_DELAY}s (scan #{discovery_attempt + 1}/{MAX_DISCOVERY_ATTEMPTS})...")
                                await asyncio.sleep(DISCOVERY_RETRY_DELAY)
                    else:
                        logger.debug(f"Scan #{discovery_attempt}/{MAX_DISCOVERY_ATTEMPTS} complete - no servers found")
                        if discovery_attempt < MAX_DISCOVERY_ATTEMPTS:
                            logger.info(f"No server found, retrying in {DISCOVERY_RETRY_DELAY}s...")
                            await asyncio.sleep(DISCOVERY_RETRY_DELAY)

                except Exception as e:
                    logger.error(f"Discovery scan #{discovery_attempt}/{MAX_DISCOVERY_ATTEMPTS} failed: {e}")
                    logger.error(f"Exception type: {type(e).__name__}")
                    import traceback
                    logger.debug(f"Traceback:\n{traceback.format_exc()}")
                    if discovery_attempt < MAX_DISCOVERY_ATTEMPTS:
                        logger.info(f"Retrying in {DISCOVERY_RETRY_DELAY}s (scan #{discovery_attempt + 1}/{MAX_DISCOVERY_ATTEMPTS})...")
                        await asyncio.sleep(DISCOVERY_RETRY_DELAY)

            # Check if discovery succeeded or timed out
            if not server_discovered:
                # Discovery failed after MAX_ATTEMPTS
                logger.error("=" * 70)
                logger.error(f"AUTO-DISCOVERY FAILED after {MAX_DISCOVERY_ATTEMPTS} attempts")
                logger.error(f"Total discovery time: ~{MAX_DISCOVERY_ATTEMPTS * self.config.discovery_timeout}s")
                logger.error("=" * 70)

                # NO "discovery failed" screen - just proceed to manual server connection
                # The connecting screen will show the manual server URL
                logger.info("Auto-discovery failed, proceeding with manual server configuration")

                # Fallback: Disable auto_discover and try configured server
                logger.warning("FALLBACK: Disabling auto_discover and trying configured server...")
                logger.info(f"Configured server: {self.config.server_host}:{self.config.server_port}")
                self.config.auto_discover = False
                # Don't save the config change - keep auto_discover enabled for next restart
                logger.info("Note: auto_discover will remain enabled for next restart")
            else:
                logger.info("=" * 70)
                logger.info("✓ SERVER DISCOVERED SUCCESSFULLY - Proceeding to connection...")
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
                        self.display_renderer.status_screen_manager.show_connecting(
                            server_url=server_url,
                            attempt=attempt + 1
                        )

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

            # Show server offline screen during wait
            if self.display_renderer:
                server_url = self.config.get_server_url()
                self.display_renderer.status_screen_manager.show_server_offline(
                    server_url=server_url,
                    attempt=batch_number * max_retries_per_batch,
                    retry_in=batch_wait_time,
                    auto_discovery_active=self.config.auto_discover
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

        # CRITICAL FIX: Configure Qt environment BEFORE creating QApplication
        # This prevents X11/OpenGL crashes on Raspberry Pi
        logger.info("Configuring Qt environment for Raspberry Pi...")

        # Disable OpenGL acceleration - use software rendering to prevent crashes
        # This fixes "X connection broken" errors on Pi
        os.environ.setdefault('QT_XCB_GL_INTEGRATION', 'none')

        # Ensure we use XCB platform plugin (X11)
        os.environ.setdefault('QT_QPA_PLATFORM', 'xcb')

        # Disable GPU acceleration in Qt WebEngine (if used)
        os.environ.setdefault('QTWEBENGINE_CHROMIUM_FLAGS', '--disable-gpu')

        # Set Qt to use software rendering
        os.environ.setdefault('QT_QUICK_BACKEND', 'software')

        logger.info("Qt environment configured:")
        logger.info(f"  QT_XCB_GL_INTEGRATION = {os.environ.get('QT_XCB_GL_INTEGRATION')}")
        logger.info(f"  QT_QPA_PLATFORM = {os.environ.get('QT_QPA_PLATFORM')}")
        logger.info(f"  QT_QUICK_BACKEND = {os.environ.get('QT_QUICK_BACKEND')}")

        logger.info("Loading configuration...")
        config = Config.load()
        logger.info(f"Configuration loaded - Server: {config.server_host}:{config.server_port}")

        logger.info("Creating Qt application...")
        # Create Qt application for display rendering
        # CRITICAL: This must happen AFTER setting environment variables
        try:
            app = QApplication(sys.argv)
            logger.info("Qt application created successfully")
        except Exception as qt_error:
            logger.error("=" * 70)
            logger.error("FATAL: Failed to create Qt application")
            logger.error("=" * 70)
            logger.error(f"Error: {qt_error}")
            logger.error("")
            logger.error("This usually means:")
            logger.error("  1. X11 display server is not running")
            logger.error("  2. DISPLAY environment variable is not set")
            logger.error("  3. OpenGL/graphics driver issue")
            logger.error("")
            logger.error("Troubleshooting:")
            logger.error("  - Check DISPLAY: echo $DISPLAY")
            logger.error("  - Start X11: sudo systemctl start lightdm")
            logger.error("  - Check graphics: vcgencmd get_mem gpu")
            logger.error("=" * 70)
            raise

        logger.info("Initializing Digital Signage Client...")
        # Create and start client
        client = DigitalSignageClient(config)

        logger.info("Creating display renderer...")
        # Create display renderer
        client.display_renderer = DisplayRenderer(fullscreen=config.fullscreen)

        # CRITICAL FIX: Pass client reference to status screen manager for config access
        # This allows status screen manager to check show_cached_layout_on_disconnect setting
        client.display_renderer.status_screen_manager.set_client(client)
        logger.debug("Client reference passed to status screen manager")

        # BUGFIX: Don't show() here - wait for event loop to be ready
        # client.display_renderer.show()  # MOVED TO after event loop setup

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
        try:
            client.display_renderer.raise_()
            client.display_renderer.activateWindow()
            client.display_renderer.setFocus()
            logger.debug("Initial window activation completed")
        except Exception as e:
            logger.warning(f"Initial window activation failed: {e}, continuing anyway")

        # Additional fix: Use QTimer to re-raise window after event loop starts
        # This ensures window is visible even if desktop is still loading
        def ensure_window_visible():
            try:
                # BUGFIX: Only access display_renderer if it exists
                if not hasattr(client, 'display_renderer') or client.display_renderer is None:
                    logger.debug("Display renderer not initialized yet - skipping window visibility check")
                    return

                ssm = client.display_renderer.status_screen_manager

                # CRITICAL FIX: Check if status screen is ACTUALLY showing (not just flag)
                # The flag might be cleared but window still exists during deletion
                if ssm and ssm.is_showing_status and ssm.status_screen:
                    logger.debug("Status screen is active - ensuring STATUS screen stays on top (periodic check)")
                    try:
                        # BUGFIX: Check if status_screen widget is still valid (not deleted)
                        from PyQt5.QtWidgets import QWidget
                        # In PyQt5, calling methods on deleted C++ objects raises RuntimeError
                        try:
                            ssm.status_screen.isVisible()  # Test if widget is still valid
                        except RuntimeError:
                            logger.warning("Status screen widget has been deleted - clearing reference")
                            ssm.status_screen = None
                            ssm.is_showing_status = False
                            return

                        ssm.status_screen.raise_()
                        ssm.status_screen.activateWindow()
                        ssm.status_screen.showFullScreen()
                        logger.debug("  ✓ Status screen raised to stay on top")
                    except Exception as status_error:
                        logger.warning(f"  Could not re-raise status screen: {status_error}")
                    return  # Don't touch display renderer

                # CRITICAL FIX: DO NOT raise display renderer if status screen is active
                # Check both the flag AND the existence of status_screen widget
                # This prevents display renderer from covering status screens during discovery/connection
                if ssm and (ssm.is_showing_status or ssm.status_screen is not None):
                    logger.debug("Status screen exists - NOT raising display renderer to avoid covering it")
                    return

                # CRITICAL FIX: Check if display renderer has a layout to show
                # Only raise display renderer if it's actually rendering content
                # This prevents empty display renderer from covering status screens
                if client.current_layout is None:
                    logger.debug("No layout assigned yet - NOT raising display renderer")
                    return

                # If display renderer is already visible (layout rendered), just raise it
                if client.display_renderer.isVisible():
                    logger.info("Display renderer already visible - ensuring it stays on top")
                    client.display_renderer.raise_()
                    client.display_renderer.activateWindow()
                    client.display_renderer.setFocus()
                    logger.debug("  ✓ Display renderer raised")
                    return

                # Otherwise, don't do anything - wait for layout to arrive
                # The layout rendering will show the window when ready
                logger.debug("No window visible yet - waiting for layout or status screen")

            except Exception as e:
                logger.error(f"Failed to ensure window visibility: {e}")
                import traceback
                logger.error(traceback.format_exc())

        # Schedule window visibility check after 2 seconds (when event loop is running)
        try:
            QTimer.singleShot(2000, ensure_window_visible)
            logger.debug("Scheduled delayed window visibility check (2s)")
        except Exception as e:
            logger.warning(f"Failed to schedule delayed window check: {e}")

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
                # BUGFIX: Show display renderer AFTER event loop is ready
                client.display_renderer.show_and_setup()
                logger.info("Display renderer shown after event loop initialization")

                loop.create_task(client.start())
                logger.info("Starting Qt+asyncio event loop...")
                loop.run_forever()
        except ImportError:
            # Fallback: Use asyncio event loop with Qt integration
            logger.warning("qasync not available - using fallback integration")
            loop = asyncio.get_event_loop()

            # Store event loop reference for WebSocket callbacks
            client.event_loop = loop

            # BUGFIX: Show display renderer AFTER event loop is ready
            client.display_renderer.show_and_setup()
            logger.info("Display renderer shown after event loop initialization (fallback)")

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
