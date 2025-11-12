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

    def _get_status_data(self) -> Dict[str, Any]:
        """Get current client status data"""
        try:
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

            # Get cache info
            cache_info = self.client.cache_manager.get_cache_info()

            return {
                'client_id': self.client.config.client_id,
                'ip_address': device_info.get('ip_address', 'Unknown'),
                'mac_address': device_info.get('mac_address', 'Unknown'),
                'hostname': device_info.get('hostname', 'Unknown'),
                'connected': self.client.connected,
                'offline_mode': self.client.offline_mode,
                'server_url': self.client.config.get_server_url(),
                'last_heartbeat': datetime.utcnow().isoformat() if self.client.connected else None,
                'websocket_status': 'Connected' if self.client.connected else 'Disconnected',
                'assigned_layout': self.client.current_layout.get('Name') if self.client.current_layout else None,
                'layout_id': self.client.current_layout.get('Id') if self.client.current_layout else None,
                'cache_info': cache_info,
                'timestamp': datetime.utcnow().isoformat()
            }
        except Exception as e:
            logger.error(f"Error getting status data: {e}", exc_info=True)
            return {
                'client_id': self.client.config.client_id,
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

            # CPU temperature
            cpu_temp = self.client.device_manager.get_cpu_temperature()

            # Display resolution
            screen_width = self.client.device_manager.get_screen_width()
            screen_height = self.client.device_manager.get_screen_height()

            # OS version
            model = self.client.device_manager.get_rpi_model()
            os_version = self.client.device_manager.get_os_version()

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
        """Get recent log entries"""
        try:
            # Get logs from journalctl
            cmd = ['journalctl', '-u', 'digitalsignage-client', '-n', str(lines), '--no-pager']

            result = subprocess.run(cmd, capture_output=True, text=True, timeout=5)

            if result.returncode != 0:
                return {'error': 'Failed to retrieve logs', 'logs': []}

            # Parse log entries
            log_entries = []
            for line in result.stdout.strip().split('\n'):
                if not line:
                    continue

                # Filter by level if specified
                if level != 'ALL':
                    if level not in line:
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
                'logs': log_entries,
                'count': len(log_entries),
                'filtered_by': level,
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
