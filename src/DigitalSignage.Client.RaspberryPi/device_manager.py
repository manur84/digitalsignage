"""
Device management and system information for Raspberry Pi
"""

import os
import platform
import subprocess
import psutil
import logging
from typing import Dict, Any
from pathlib import Path

logger = logging.getLogger(__name__)


class DeviceManager:
    """Manages device information and system commands"""

    def __init__(self):
        self.hostname = platform.node()

    def get_mdns_name(self) -> str:
        """Return mDNS hostname (best effort)"""
        try:
            name = self.hostname or platform.node()
            if not name:
                return ""
            if name.endswith(".local"):
                return name
            return f"{name}.local"
        except Exception as e:
            logger.debug(f"Failed to build mDNS name: {e}")
            return ""

    async def get_device_info(self) -> Dict[str, Any]:
        """Get comprehensive device information"""
        try:
            cpu_usage = psutil.cpu_percent(interval=1)
        except Exception as e:
            logger.warning(f"Failed to get CPU usage: {e}")
            cpu_usage = 0.0

        try:
            memory = psutil.virtual_memory()
            memory_total = memory.total
            memory_used = memory.used
        except Exception as e:
            logger.warning(f"Failed to get memory info: {e}")
            memory_total = 0
            memory_used = 0

        try:
            disk = psutil.disk_usage('/')
            disk_total = disk.total
            disk_used = disk.used
        except Exception as e:
            logger.warning(f"Failed to get disk info: {e}")
            disk_total = 0
            disk_used = 0

        try:
            uptime = int(psutil.boot_time())
        except Exception as e:
            logger.warning(f"Failed to get uptime: {e}")
            uptime = 0

        return {
            "hostname": self.hostname,
            "mdns_name": self.get_mdns_name(),
            "model": self.get_rpi_model(),
            "os_version": self.get_os_version(),
            "ip_address": self.get_ip_address(),
            "mac_address": self.get_mac_address(),
            "cpu_temp": self.get_cpu_temperature(),
            "cpu_usage": cpu_usage,
            "memory_total": memory_total,
            "memory_used": memory_used,
            "disk_total": disk_total,
            "disk_used": disk_used,
            "screen_width": self.get_screen_width(),
            "screen_height": self.get_screen_height(),
            "uptime": uptime
        }

    def get_rpi_model(self) -> str:
        """Get Raspberry Pi model"""
        try:
            with open('/proc/device-tree/model', 'r') as f:
                model = f.read().strip().replace('\x00', '')
                if model:
                    return model
        except (FileNotFoundError, IOError, PermissionError) as e:
            logger.debug(f"Could not read RPi model file: {e}")
        except Exception as e:
            logger.warning(f"Unexpected error reading RPi model: {e}")

        return platform.machine()

    def get_os_version(self) -> str:
        """Get OS version"""
        return platform.platform()

    def get_ip_address(self) -> str:
        """Get primary IP address"""
        import socket
        try:
            # Use context manager for proper socket cleanup
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
                # Connect to a public DNS server (doesn't actually send data)
                s.connect(("8.8.8.8", 80))
                ip = s.getsockname()[0]
                if ip:
                    return ip
        except socket.error as e:
            logger.debug(f"Could not get IP address via socket: {e}")
        except Exception as e:
            logger.warning(f"Unexpected error getting IP address: {e}")

        return "127.0.0.1"

    def get_mac_address(self) -> str:
        """Get MAC address"""
        try:
            import uuid
            node = uuid.getnode()
            if node == 0:
                logger.warning("Could not get valid MAC address")
                return "00:00:00:00:00:00"

            mac = ':'.join(['{:02x}'.format((node >> elements) & 0xff)
                           for elements in range(0, 2*6, 2)][::-1])
            return mac
        except Exception as e:
            logger.warning(f"Failed to get MAC address: {e}")
            return "00:00:00:00:00:00"

    def get_cpu_temperature(self) -> float:
        """Get CPU temperature (Raspberry Pi specific)"""
        try:
            temp_file = Path('/sys/class/thermal/thermal_zone0/temp')
            if temp_file.exists():
                temp_str = temp_file.read_text().strip()
                temp = float(temp_str) / 1000.0
                # Sanity check: temperature should be between -50 and 150 Celsius
                if -50 <= temp <= 150:
                    return temp
                else:
                    logger.warning(f"CPU temperature out of range: {temp}°C")
        except (FileNotFoundError, IOError, PermissionError) as e:
            logger.debug(f"Could not read CPU temperature file: {e}")
        except (ValueError, TypeError) as e:
            logger.warning(f"Invalid CPU temperature value: {e}")
        except Exception as e:
            logger.warning(f"Unexpected error reading CPU temperature: {e}")

        return 0.0

    def get_screen_width(self) -> int:
        """Get screen width"""
        try:
            result = subprocess.run(
                ['xrandr'],
                capture_output=True,
                text=True,
                timeout=5
            )
            if result.returncode == 0:
                for line in result.stdout.split('\n'):
                    if '*' in line:
                        parts = line.split()
                        if parts:
                            resolution = parts[0]
                            width_str = resolution.split('x')[0]
                            width = int(width_str)
                            if width > 0:
                                return width
        except subprocess.TimeoutExpired:
            logger.warning("xrandr command timed out")
        except (FileNotFoundError, PermissionError) as e:
            logger.debug(f"xrandr not available: {e}")
        except (ValueError, IndexError) as e:
            logger.warning(f"Could not parse screen resolution: {e}")
        except Exception as e:
            logger.warning(f"Unexpected error getting screen width: {e}")

        return 1920

    def get_screen_height(self) -> int:
        """Get screen height"""
        try:
            result = subprocess.run(
                ['xrandr'],
                capture_output=True,
                text=True,
                timeout=5
            )
            if result.returncode == 0:
                for line in result.stdout.split('\n'):
                    if '*' in line:
                        parts = line.split()
                        if parts:
                            resolution = parts[0]
                            height_str = resolution.split('x')[1]
                            height = int(height_str)
                            if height > 0:
                                return height
        except subprocess.TimeoutExpired:
            logger.warning("xrandr command timed out")
        except (FileNotFoundError, PermissionError) as e:
            logger.debug(f"xrandr not available: {e}")
        except (ValueError, IndexError) as e:
            logger.warning(f"Could not parse screen resolution: {e}")
        except Exception as e:
            logger.warning(f"Unexpected error getting screen height: {e}")

        return 1080

    async def restart_system(self):
        """Restart the system"""
        logger.warning("Restarting system...")
        try:
            # Use systemctl reboot instead of sudo reboot
            # This works when the user is in the sudo group and NoNewPrivileges is disabled
            result = subprocess.run(
                ['systemctl', 'reboot'],
                capture_output=True,
                text=True,
                timeout=10
            )
            if result.returncode != 0:
                logger.error(f"Reboot command failed: {result.stderr}")
        except subprocess.TimeoutExpired:
            # Timeout is expected as system will restart
            logger.info("System restart initiated")
        except (FileNotFoundError, PermissionError) as e:
            logger.error(f"Cannot execute reboot command: {e}")
            logger.warning("Trying alternative: sudo reboot")
            try:
                subprocess.run(['sudo', 'reboot'], timeout=10)
            except Exception as e2:
                logger.error(f"Alternative reboot method also failed: {e2}")
        except Exception as e:
            logger.error(f"Failed to restart system: {e}")

    async def screen_on(self):
        """Turn screen on"""
        try:
            logger.info("Turning screen ON...")

            # Method 1: X11 DPMS (works on most systems)
            try:
                result = subprocess.run(
                    ['xset', 'dpms', 'force', 'on'],
                    capture_output=True,
                    text=True,
                    timeout=5
                )
                if result.returncode == 0:
                    logger.info("Screen turned ON via xset")
                else:
                    logger.warning(f"xset command failed: {result.stderr}")
            except Exception as e:
                logger.warning(f"xset command failed: {e}")

            # Method 2: Raspberry Pi HDMI (fallback)
            try:
                subprocess.run(
                    ['vcgencmd', 'display_power', '1'],
                    capture_output=True,
                    timeout=5
                )
                logger.info("Raspberry Pi HDMI turned ON")
            except Exception as e:
                logger.debug(f"vcgencmd not available: {e}")

        except Exception as e:
            logger.error(f"Failed to turn screen on: {e}")

    async def screen_off(self):
        """Turn screen off"""
        try:
            logger.info("Turning screen OFF...")

            # Method 1: X11 DPMS (works on most systems)
            try:
                result = subprocess.run(
                    ['xset', 'dpms', 'force', 'off'],
                    capture_output=True,
                    text=True,
                    timeout=5
                )
                if result.returncode == 0:
                    logger.info("Screen turned OFF via xset")
                else:
                    logger.warning(f"xset command failed: {result.stderr}")
            except Exception as e:
                logger.warning(f"xset command failed: {e}")

            # Method 2: Raspberry Pi HDMI (fallback)
            try:
                subprocess.run(
                    ['vcgencmd', 'display_power', '0'],
                    capture_output=True,
                    timeout=5
                )
                logger.info("Raspberry Pi HDMI turned OFF")
            except Exception as e:
                logger.debug(f"vcgencmd not available: {e}")

        except Exception as e:
            logger.error(f"Failed to turn screen off: {e}")

    async def set_volume(self, volume: int):
        """Set audio volume (0-100)"""
        # Validate volume parameter
        try:
            volume = int(volume)
        except (ValueError, TypeError):
            logger.error(f"Invalid volume value: {volume}, must be an integer")
            return

        if volume < 0 or volume > 100:
            logger.error(f"Volume out of range: {volume}, must be between 0 and 100")
            return

        try:
            result = subprocess.run(
                ['amixer', 'set', 'Master', f'{volume}%'],
                capture_output=True,
                text=True,
                timeout=5
            )
            if result.returncode == 0:
                logger.info(f"Volume set to {volume}%")
            else:
                logger.error(f"Failed to set volume: {result.stderr}")
        except subprocess.TimeoutExpired:
            logger.error("amixer command timed out")
        except (FileNotFoundError, PermissionError) as e:
            logger.error(f"Cannot execute amixer command: {e}")
        except Exception as e:
            logger.error(f"Failed to set volume: {e}")
