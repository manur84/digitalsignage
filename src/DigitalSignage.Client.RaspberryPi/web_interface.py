"""
Web Interface for Digital Signage Client
Provides a web dashboard for viewing client information, logs, and system status
"""

import os
import sys
import json
import logging
import psutil
import subprocess
from pathlib import Path
from datetime import datetime, timedelta
from typing import Optional, Dict, Any, List
from flask import Flask, render_template, jsonify, request, abort
import threading

logger = logging.getLogger(__name__)


class WebInterface:
    """Web interface server for client dashboard"""

    def __init__(self, client, port: int = 5000, host: str = '0.0.0.0'):
        """
        Initialize web interface

        Args:
            client: DigitalSignageClient instance
            port: Port to run Flask server on (default: 5000)
            host: Host to bind to (default: 0.0.0.0 for all interfaces)
        """
        self.client = client
        self.port = port
        self.host = host
        self.app = Flask(__name__,
                        template_folder=str(Path(__file__).parent / 'templates'))
        self.server_thread: Optional[threading.Thread] = None
        self.is_running = False

        # Configure Flask logging to use our logger
        self.app.logger.handlers = logger.handlers
        self.app.logger.setLevel(logger.level)

        # Disable Flask's default logging
        import logging as flask_logging
        flask_logging.getLogger('werkzeug').setLevel(flask_logging.ERROR)

        # Setup routes
        self._setup_routes()

    def _setup_routes(self):
        """Setup Flask routes"""

        @self.app.route('/')
        def index():
            """Main dashboard page"""
            return render_template('dashboard.html')

        @self.app.route('/api/status')
        def api_status():
            """Get current client status"""
            try:
                return jsonify(self._get_status_data())
            except Exception as e:
                logger.error(f"Error getting status data: {e}", exc_info=True)
                return jsonify({'error': str(e)}), 500

        @self.app.route('/api/system')
        def api_system():
            """Get system information"""
            try:
                return jsonify(self._get_system_data())
            except Exception as e:
                logger.error(f"Error getting system data: {e}", exc_info=True)
                return jsonify({'error': str(e)}), 500

        @self.app.route('/api/logs')
        def api_logs():
            """Get recent log entries"""
            try:
                level = request.args.get('level', 'ALL')
                lines = int(request.args.get('lines', 100))
                return jsonify(self._get_logs(level, lines))
            except Exception as e:
                logger.error(f"Error getting logs: {e}", exc_info=True)
                return jsonify({'error': str(e)}), 500

        @self.app.route('/api/config')
        def api_config():
            """Get client configuration (sanitized)"""
            try:
                return jsonify(self._get_config_data())
            except Exception as e:
                logger.error(f"Error getting config data: {e}", exc_info=True)
                return jsonify({'error': str(e)}), 500

        @self.app.route('/api/restart', methods=['POST'])
        def api_restart():
            """Restart the client application"""
            try:
                logger.warning("Client restart requested via web interface")
                # Schedule restart in background
                def do_restart():
                    import time
                    time.sleep(2)
                    os.system('sudo systemctl restart digitalsignage-client')

                threading.Thread(target=do_restart, daemon=True).start()
                return jsonify({'success': True, 'message': 'Client restart initiated'})
            except Exception as e:
                logger.error(f"Error restarting client: {e}", exc_info=True)
                return jsonify({'success': False, 'error': str(e)}), 500

        @self.app.route('/api/clear_cache', methods=['POST'])
        def api_clear_cache():
            """Clear client cache"""
            try:
                logger.warning("Cache clear requested via web interface")
                if self.client.cache_manager.clear_cache():
                    return jsonify({'success': True, 'message': 'Cache cleared successfully'})
                else:
                    return jsonify({'success': False, 'error': 'Failed to clear cache'}), 500
            except Exception as e:
                logger.error(f"Error clearing cache: {e}", exc_info=True)
                return jsonify({'success': False, 'error': str(e)}), 500

        @self.app.route('/api/settings', methods=['GET'])
        def api_get_settings():
            """Get client settings"""
            try:
                if not hasattr(self.client, 'config'):
                    return jsonify({'error': 'Client not initialized'}), 503

                return jsonify({
                    'show_cached_layout_on_disconnect': self.client.config.show_cached_layout_on_disconnect,
                    'auto_discover': self.client.config.auto_discover,
                    'fullscreen': self.client.config.fullscreen,
                    'server_host': self.client.config.server_host,
                    'server_port': self.client.config.server_port,
                    'endpoint_path': self.client.config.endpoint_path,
                    'use_ssl': self.client.config.use_ssl,
                    'verify_ssl': self.client.config.verify_ssl,
                    'remote_logging_enabled': self.client.config.remote_logging_enabled,
                    'remote_logging_level': self.client.config.remote_logging_level,
                    'remote_logging_batch_size': self.client.config.remote_logging_batch_size,
                    'remote_logging_batch_interval': self.client.config.remote_logging_batch_interval,
                    'log_level': self.client.config.log_level,
                    'discovery_timeout': self.client.config.discovery_timeout,
                    'burn_in_protection_enabled': self.client.config.burn_in_protection_enabled,
                    'burn_in_pixel_shift_interval': self.client.config.burn_in_pixel_shift_interval,
                    'burn_in_pixel_shift_max': self.client.config.burn_in_pixel_shift_max,
                    'burn_in_screensaver_timeout': self.client.config.burn_in_screensaver_timeout,
                    'timestamp': datetime.utcnow().isoformat()
                })
            except Exception as e:
                logger.error(f"Error getting settings: {e}", exc_info=True)
                return jsonify({'error': str(e)}), 500

        @self.app.route('/api/settings', methods=['POST'])
        def api_update_settings():
            """Update client settings"""
            try:
                if not hasattr(self.client, 'config'):
                    return jsonify({'success': False, 'error': 'Client not initialized'}), 503

                data = request.get_json()
                if not data:
                    return jsonify({'success': False, 'error': 'No data provided'}), 400

                # Update settings
                updated_fields = []

                if 'show_cached_layout_on_disconnect' in data:
                    self.client.config.show_cached_layout_on_disconnect = bool(data['show_cached_layout_on_disconnect'])
                    updated_fields.append('show_cached_layout_on_disconnect')
                    logger.info(f"Updated show_cached_layout_on_disconnect to {self.client.config.show_cached_layout_on_disconnect}")

                if 'server_host' in data and data['server_host']:
                    self.client.config.server_host = str(data['server_host'])
                    updated_fields.append('server_host')
                    logger.info(f"Updated server_host to {self.client.config.server_host}")

                if 'server_port' in data:
                    try:
                        self.client.config.server_port = int(data['server_port'])
                        updated_fields.append('server_port')
                        logger.info(f"Updated server_port to {self.client.config.server_port}")
                    except Exception:
                        logger.warning(f"Invalid server_port provided: {data['server_port']}")

                if 'endpoint_path' in data:
                    self.client.config.endpoint_path = str(data['endpoint_path'])
                    updated_fields.append('endpoint_path')
                    logger.info(f"Updated endpoint_path to {self.client.config.endpoint_path}")

                if 'use_ssl' in data:
                    self.client.config.use_ssl = bool(data['use_ssl'])
                    updated_fields.append('use_ssl')
                    logger.info(f"Updated use_ssl to {self.client.config.use_ssl}")

                if 'verify_ssl' in data:
                    self.client.config.verify_ssl = bool(data['verify_ssl'])
                    updated_fields.append('verify_ssl')
                    logger.info(f"Updated verify_ssl to {self.client.config.verify_ssl}")

                if 'auto_discover' in data:
                    self.client.config.auto_discover = bool(data['auto_discover'])
                    updated_fields.append('auto_discover')
                    logger.info(f"Updated auto_discover to {self.client.config.auto_discover}")

                if 'discovery_timeout' in data:
                    try:
                        self.client.config.discovery_timeout = float(data['discovery_timeout'])
                        updated_fields.append('discovery_timeout')
                        logger.info(f"Updated discovery_timeout to {self.client.config.discovery_timeout}")
                    except Exception:
                        logger.warning(f"Invalid discovery_timeout provided: {data['discovery_timeout']}")

                if 'fullscreen' in data:
                    self.client.config.fullscreen = bool(data['fullscreen'])
                    updated_fields.append('fullscreen')
                    logger.info(f"Updated fullscreen to {self.client.config.fullscreen}")

                if 'remote_logging_enabled' in data:
                    self.client.config.remote_logging_enabled = bool(data['remote_logging_enabled'])
                    updated_fields.append('remote_logging_enabled')
                    logger.info(f"Updated remote_logging_enabled to {self.client.config.remote_logging_enabled}")

                if 'remote_logging_level' in data:
                    self.client.config.remote_logging_level = str(data['remote_logging_level'])
                    updated_fields.append('remote_logging_level')
                    logger.info(f"Updated remote_logging_level to {self.client.config.remote_logging_level}")

                if 'remote_logging_batch_size' in data:
                    try:
                        self.client.config.remote_logging_batch_size = int(data['remote_logging_batch_size'])
                        updated_fields.append('remote_logging_batch_size')
                        logger.info(f"Updated remote_logging_batch_size to {self.client.config.remote_logging_batch_size}")
                    except Exception:
                        logger.warning(f"Invalid remote_logging_batch_size provided: {data['remote_logging_batch_size']}")

                if 'remote_logging_batch_interval' in data:
                    try:
                        self.client.config.remote_logging_batch_interval = float(data['remote_logging_batch_interval'])
                        updated_fields.append('remote_logging_batch_interval')
                        logger.info(f"Updated remote_logging_batch_interval to {self.client.config.remote_logging_batch_interval}")
                    except Exception:
                        logger.warning(f"Invalid remote_logging_batch_interval provided: {data['remote_logging_batch_interval']}")

                if 'log_level' in data:
                    self.client.config.log_level = str(data['log_level'])
                    updated_fields.append('log_level')
                    logger.info(f"Updated log_level to {self.client.config.log_level}")

                if 'burn_in_protection_enabled' in data:
                    self.client.config.burn_in_protection_enabled = bool(data['burn_in_protection_enabled'])
                    updated_fields.append('burn_in_protection_enabled')
                    logger.info(f"Updated burn_in_protection_enabled to {self.client.config.burn_in_protection_enabled}")

                if 'burn_in_pixel_shift_interval' in data:
                    try:
                        self.client.config.burn_in_pixel_shift_interval = int(data['burn_in_pixel_shift_interval'])
                        updated_fields.append('burn_in_pixel_shift_interval')
                        logger.info(f"Updated burn_in_pixel_shift_interval to {self.client.config.burn_in_pixel_shift_interval}")
                    except Exception:
                        logger.warning(f"Invalid burn_in_pixel_shift_interval provided: {data['burn_in_pixel_shift_interval']}")

                if 'burn_in_pixel_shift_max' in data:
                    try:
                        self.client.config.burn_in_pixel_shift_max = int(data['burn_in_pixel_shift_max'])
                        updated_fields.append('burn_in_pixel_shift_max')
                        logger.info(f"Updated burn_in_pixel_shift_max to {self.client.config.burn_in_pixel_shift_max}")
                    except Exception:
                        logger.warning(f"Invalid burn_in_pixel_shift_max provided: {data['burn_in_pixel_shift_max']}")

                if 'burn_in_screensaver_timeout' in data:
                    try:
                        self.client.config.burn_in_screensaver_timeout = int(data['burn_in_screensaver_timeout'])
                        updated_fields.append('burn_in_screensaver_timeout')
                        logger.info(f"Updated burn_in_screensaver_timeout to {self.client.config.burn_in_screensaver_timeout}")
                    except Exception:
                        logger.warning(f"Invalid burn_in_screensaver_timeout provided: {data['burn_in_screensaver_timeout']}")

                # Save configuration to file
                self.client.config.save()
                logger.info(f"Settings updated and saved: {', '.join(updated_fields)}")

                return jsonify({
                    'success': True,
                    'message': f'Settings updated: {", ".join(updated_fields)}',
                    'updated_fields': updated_fields,
                    'timestamp': datetime.utcnow().isoformat()
                })
            except Exception as e:
                logger.error(f"Error updating settings: {e}", exc_info=True)
                return jsonify({'success': False, 'error': str(e)}), 500

        @self.app.route('/api/cache/layouts')
        def api_get_cached_layouts():
            """Get list of all cached layouts"""
            try:
                if not hasattr(self.client, 'cache_manager'):
                    return jsonify({'error': 'Cache manager not available'}), 503

                layouts = self.client.cache_manager.get_all_layouts()

                return jsonify({
                    'success': True,
                    'layouts': layouts,
                    'count': len(layouts),
                    'timestamp': datetime.utcnow().isoformat()
                })
            except Exception as e:
                logger.error(f"Error getting cached layouts: {e}", exc_info=True)
                return jsonify({'success': False, 'error': str(e)}), 500

        @self.app.route('/api/cache/select', methods=['POST'])
        def api_select_cached_layout():
            """Set a specific layout as current for offline display"""
            try:
                if not hasattr(self.client, 'cache_manager'):
                    return jsonify({'success': False, 'error': 'Cache manager not available'}), 503

                data = request.get_json()
                if not data or 'layout_id' not in data:
                    return jsonify({'success': False, 'error': 'layout_id required'}), 400

                layout_id = str(data['layout_id'])

                # Set the layout as current
                if self.client.cache_manager.set_current_layout(layout_id):
                    # If offline mode is active and show_cached_layout_on_disconnect is enabled,
                    # reload the display with the newly selected layout
                    if (hasattr(self.client, 'offline_mode') and
                        self.client.offline_mode and
                        self.client.config.show_cached_layout_on_disconnect):

                        # Get the layout and its data
                        cached_data = self.client.cache_manager.get_current_layout()
                        if cached_data:
                            layout, layout_data = cached_data
                            # Reload the display
                            if hasattr(self.client, 'renderer'):
                                self.client.renderer.render_layout(layout, layout_data)
                                logger.info(f"Reloaded display with layout {layout_id}")

                    return jsonify({
                        'success': True,
                        'message': f'Layout {layout_id} selected as current',
                        'layout_id': layout_id,
                        'timestamp': datetime.utcnow().isoformat()
                    })
                else:
                    return jsonify({
                        'success': False,
                        'error': 'Failed to set current layout (layout may not exist in cache)'
                    }), 400

            except Exception as e:
                logger.error(f"Error selecting cached layout: {e}", exc_info=True)
                return jsonify({'success': False, 'error': str(e)}), 500

    def _get_status_data(self) -> Dict[str, Any]:
        """Get current client status data"""
        try:
            # Safety check: Ensure client is initialized
            if not hasattr(self.client, 'config') or not hasattr(self.client, 'device_manager'):
                logger.warning("Client not fully initialized yet")
                return {
                    'client_id': 'Initializing...',
                    'ip_address': 'Unknown',
                    'mac_address': 'Unknown',
                    'hostname': 'Unknown',
                    'connected': False,
                    'offline_mode': True,
                    'server_url': 'Unknown',
                    'last_heartbeat': None,
                    'websocket_status': 'Initializing',
                    'assigned_layout': None,
                    'layout_id': None,
                    'cache_info': {'layout_count': 0, 'current_layout_id': None},
                    'timestamp': datetime.utcnow().isoformat()
                }

            # Get device info synchronously (since we're in Flask context)
            import asyncio

            # Try to get device info from existing data or create new event loop
            try:
                device_info = asyncio.run(self.client.device_manager.get_device_info())
            except RuntimeError:
                # Event loop already running, get basic info without async
                device_info = {
                    'ip_address': self.client.device_manager.get_ip_address(),
                    'mac_address': self.client.device_manager.get_mac_address(),
                    'hostname': self.client.device_manager.hostname,
                    'model': self.client.device_manager.get_rpi_model(),
                    'os_version': self.client.device_manager.get_os_version(),
                }
            except Exception as e:
                logger.error(f"Failed to get device info: {e}")
                device_info = {
                    'ip_address': 'Unknown',
                    'mac_address': 'Unknown',
                    'hostname': 'Unknown',
                    'model': 'Unknown',
                    'os_version': 'Unknown',
                }

            # Get cache info safely
            try:
                cache_info = self.client.cache_manager.get_cache_info()
            except Exception as e:
                logger.error(f"Failed to get cache info: {e}")
                cache_info = {'layout_count': 0, 'current_layout_id': None}

            return {
                'client_id': self.client.config.client_id,
                'ip_address': device_info.get('ip_address', 'Unknown'),
                'mac_address': device_info.get('mac_address', 'Unknown'),
                'hostname': device_info.get('hostname', 'Unknown'),
                'connected': getattr(self.client, 'connected', False),
                'offline_mode': getattr(self.client, 'offline_mode', True),
                'server_url': self.client.config.get_server_url(),
                'last_heartbeat': datetime.utcnow().isoformat() if getattr(self.client, 'connected', False) else None,
                'websocket_status': 'Connected' if getattr(self.client, 'connected', False) else 'Disconnected',
                'assigned_layout': self.client.current_layout.get('Name') if self.client.current_layout else None,
                'layout_id': self.client.current_layout.get('Id') if self.client.current_layout else None,
                'cache_info': cache_info,
                'timestamp': datetime.utcnow().isoformat()
            }
        except Exception as e:
            logger.error(f"Error getting status data: {e}", exc_info=True)
            return {
                'client_id': getattr(self.client, 'config', type('obj', (object,), {'client_id': 'Unknown'})()).client_id,
                'error': str(e),
                'timestamp': datetime.utcnow().isoformat()
            }

    def _get_system_data(self) -> Dict[str, Any]:
        """Get system information"""
        try:
            # CPU usage
            cpu_percent = psutil.cpu_percent(interval=1)

            # Memory info
            memory = psutil.virtual_memory()
            memory_total_gb = memory.total / (1024 ** 3)
            memory_used_gb = memory.used / (1024 ** 3)
            memory_percent = memory.percent

            # Disk info
            disk = psutil.disk_usage('/')
            disk_total_gb = disk.total / (1024 ** 3)
            disk_used_gb = disk.used / (1024 ** 3)
            disk_percent = disk.percent

            # Uptime
            boot_time = datetime.fromtimestamp(psutil.boot_time())
            uptime = datetime.now() - boot_time
            uptime_str = str(uptime).split('.')[0]  # Remove microseconds

            # CPU temperature (safe access)
            try:
                cpu_temp = self.client.device_manager.get_cpu_temperature() if hasattr(self.client, 'device_manager') else 0
            except Exception:
                cpu_temp = 0

            # Display resolution (safe access)
            try:
                if hasattr(self.client, 'device_manager'):
                    screen_width = self.client.device_manager.get_screen_width()
                    screen_height = self.client.device_manager.get_screen_height()
                else:
                    screen_width = 1920
                    screen_height = 1080
            except Exception:
                screen_width = 1920
                screen_height = 1080

            # OS version (safe access)
            try:
                if hasattr(self.client, 'device_manager'):
                    model = self.client.device_manager.get_rpi_model()
                    os_version = self.client.device_manager.get_os_version()
                else:
                    model = 'Unknown'
                    os_version = 'Unknown'
            except Exception:
                model = 'Unknown'
                os_version = 'Unknown'

            return {
                'cpu_usage': round(cpu_percent, 1),
                'cpu_temperature': round(cpu_temp, 1),
                'memory_total_gb': round(memory_total_gb, 2),
                'memory_used_gb': round(memory_used_gb, 2),
                'memory_percent': round(memory_percent, 1),
                'disk_total_gb': round(disk_total_gb, 2),
                'disk_used_gb': round(disk_used_gb, 2),
                'disk_percent': round(disk_percent, 1),
                'uptime': uptime_str,
                'display_resolution': f"{screen_width}x{screen_height}",
                'model': model,
                'os_version': os_version,
                'timestamp': datetime.utcnow().isoformat()
            }
        except Exception as e:
            logger.error(f"Error getting system data: {e}", exc_info=True)
            return {'error': str(e)}

    def _get_logs(self, level: str = 'ALL', lines: int = 100) -> Dict[str, Any]:
        """Get recent log entries from log file or journalctl"""
        try:
            log_entries = []

            # First, try to read from log file
            log_file = Path.home() / '.digitalsignage' / 'logs' / 'client.log'

            if log_file.exists():
                try:
                    with open(log_file, 'r', encoding='utf-8') as f:
                        # Read all lines and get the last N lines
                        all_lines = f.readlines()
                        recent_lines = all_lines[-lines:] if len(all_lines) > lines else all_lines

                        for line in recent_lines:
                            line = line.strip()
                            if not line:
                                continue

                            # Filter by level if specified
                            if level != 'ALL' and level not in line:
                                continue

                            # Parse log line: "2025-11-17 01:02:05,974 - status_screen - DEBUG - Message"
                            try:
                                parts = line.split(' - ', 3)
                                if len(parts) >= 4:
                                    timestamp = parts[0]
                                    log_level = parts[2]
                                    message = parts[3]
                                else:
                                    timestamp = datetime.now().isoformat()
                                    log_level = 'INFO'
                                    message = line

                                log_entries.append({
                                    'timestamp': timestamp,
                                    'level': log_level,
                                    'message': message
                                })
                            except Exception as parse_error:
                                # If parsing fails, add the raw line
                                log_entries.append({
                                    'timestamp': datetime.now().isoformat(),
                                    'level': 'INFO',
                                    'message': line
                                })

                    if log_entries:
                        return {
                            'logs': list(reversed(log_entries)),
                            'count': len(log_entries),
                            'filtered_by': level,
                            'source': 'file',
                            'timestamp': datetime.utcnow().isoformat()
                        }
                except Exception as file_error:
                    logger.warning(f"Failed to read log file: {file_error}")

            # Fallback: Try journalctl if log file is empty or doesn't exist
            cmd = ['journalctl', '-u', 'digitalsignage-client', '-n', str(lines), '--no-pager']
            result = subprocess.run(cmd, capture_output=True, text=True, timeout=5)

            if result.returncode == 0 and result.stdout.strip():
                # Parse journalctl output
                for line in result.stdout.strip().split('\n'):
                    if not line:
                        continue

                    # Filter by level if specified
                    if level != 'ALL' and level not in line:
                        continue

                    # Try to parse timestamp, hostname, and message
                    parts = line.split(None, 4)
                    if len(parts) >= 5:
                        timestamp = f"{parts[0]} {parts[1]} {parts[2]}"
                        message = parts[4]
                    else:
                        timestamp = datetime.now().isoformat()
                        message = line

                    # Determine log level from message
                    log_level = 'INFO'
                    if 'ERROR' in message:
                        log_level = 'ERROR'
                    elif 'WARNING' in message:
                        log_level = 'WARNING'
                    elif 'DEBUG' in message:
                        log_level = 'DEBUG'
                    elif 'CRITICAL' in message:
                        log_level = 'CRITICAL'

                    log_entries.append({
                        'timestamp': timestamp,
                        'level': log_level,
                        'message': message
                    })

                return {
                    'logs': list(reversed(log_entries)),
                    'count': len(log_entries),
                    'filtered_by': level,
                    'source': 'journalctl',
                    'timestamp': datetime.utcnow().isoformat()
                }

            # No logs found from either source
            return {
                'logs': [],
                'count': 0,
                'filtered_by': level,
                'source': 'none',
                'timestamp': datetime.utcnow().isoformat()
            }

        except subprocess.TimeoutExpired:
            logger.error("journalctl command timed out")
            return {'error': 'Log retrieval timed out', 'logs': []}
        except Exception as e:
            logger.error(f"Error getting logs: {e}", exc_info=True)
            return {'error': str(e), 'logs': []}

    def _get_config_data(self) -> Dict[str, Any]:
        """Get client configuration (sanitized)"""
        try:
            return {
                'client_id': self.client.config.client_id,
                'server_host': self.client.config.server_host,
                'server_port': self.client.config.server_port,
                'use_ssl': self.client.config.use_ssl,
                'fullscreen': self.client.config.fullscreen,
                'log_level': self.client.config.log_level,
                'auto_discover': self.client.config.auto_discover,
                'remote_logging_enabled': self.client.config.remote_logging_enabled,
                'timestamp': datetime.utcnow().isoformat()
            }
        except Exception as e:
            logger.error(f"Error getting config data: {e}", exc_info=True)
            return {'error': str(e)}

    def start(self):
        """Start the web server in a background thread"""
        if self.is_running:
            logger.warning("Web interface already running")
            return

        def run_server():
            try:
                logger.info(f"Starting web interface on {self.host}:{self.port}")
                self.app.run(host=self.host, port=self.port, debug=False, threaded=True)
            except Exception as e:
                logger.error(f"Web interface error: {e}", exc_info=True)

        self.server_thread = threading.Thread(target=run_server, daemon=True)
        self.server_thread.start()
        self.is_running = True

        logger.info(f"Web interface started at http://{self.host}:{self.port}")

    def stop(self):
        """Stop the web server"""
        if not self.is_running:
            return

        logger.info("Stopping web interface...")
        # Flask doesn't have a clean shutdown method when running in a thread
        # The daemon thread will be terminated when the main process exits
        self.is_running = False
        logger.info("Web interface stopped")
