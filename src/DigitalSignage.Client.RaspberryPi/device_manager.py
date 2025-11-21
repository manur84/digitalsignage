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
        """
        Get primary non-localhost IP address.

        Tries multiple methods to find the best IP address:
        1. Socket trick to Google DNS (fastest, most accurate)
        2. netifaces library (if available)
        3. psutil network interfaces
        4. Fallback to hostname resolution

        Filters out localhost/loopback addresses (127.x.x.x, ::1).
        Prefers private network addresses (192.168.x.x, 10.x.x.x, 172.16-31.x.x).

        Returns:
            Primary IP address, or "0.0.0.0" if none found (not localhost!)
        """
        import socket
        import ipaddress

        def is_valid_ip(ip_str: str) -> bool:
            """Check if IP is valid and not localhost/loopback"""
            try:
                ip = ipaddress.ip_address(ip_str)
                # Exclude loopback (127.0.0.0/8, ::1)
                if ip.is_loopback:
                    return False
                # Exclude unspecified (0.0.0.0, ::)
                if ip.is_unspecified:
                    return False
                # Exclude link-local (169.254.0.0/16, fe80::/10)
                if ip.is_link_local:
                    return False
                return True
            except (ValueError, TypeError):
                return False

        # Method 1: Socket trick (most reliable)
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
                # Connect to a public DNS server (doesn't actually send data)
                s.connect(("8.8.8.8", 80))
                ip = s.getsockname()[0]
                if ip and is_valid_ip(ip):
                    logger.debug(f"Got IP via socket method: {ip}")
                    return ip
        except socket.error as e:
            logger.debug(f"Socket method failed: {e}")
        except Exception as e:
            logger.debug(f"Unexpected error in socket method: {e}")

        # Method 2: netifaces library (if available)
        try:
            import netifaces
            # Get all interfaces except loopback
            interfaces = [i for i in netifaces.interfaces() if i != 'lo']

            # Prioritize eth0, wlan0
            for priority_iface in ['eth0', 'wlan0']:
                if priority_iface in interfaces:
                    try:
                        addrs = netifaces.ifaddresses(priority_iface)
                        if netifaces.AF_INET in addrs:
                            for addr_info in addrs[netifaces.AF_INET]:
                                ip = addr_info.get('addr')
                                if ip and is_valid_ip(ip):
                                    logger.debug(f"Got IP via netifaces ({priority_iface}): {ip}")
                                    return ip
                    except Exception as e:
                        logger.debug(f"Error getting IP from {priority_iface}: {e}")

            # Try other interfaces
            for iface in interfaces:
                if iface in ['eth0', 'wlan0']:
                    continue  # Already tried
                try:
                    addrs = netifaces.ifaddresses(iface)
                    if netifaces.AF_INET in addrs:
                        for addr_info in addrs[netifaces.AF_INET]:
                            ip = addr_info.get('addr')
                            if ip and is_valid_ip(ip):
                                logger.debug(f"Got IP via netifaces ({iface}): {ip}")
                                return ip
                except Exception as e:
                    logger.debug(f"Error getting IP from {iface}: {e}")

        except ImportError:
            logger.debug("netifaces not available")
        except Exception as e:
            logger.debug(f"netifaces method failed: {e}")

        # Method 3: psutil network interfaces
        try:
            import psutil
            net_if_addrs = psutil.net_if_addrs()

            # Prioritize eth0, wlan0
            for priority_iface in ['eth0', 'wlan0']:
                if priority_iface in net_if_addrs:
                    for addr in net_if_addrs[priority_iface]:
                        if addr.family == socket.AF_INET:
                            ip = addr.address
                            if ip and is_valid_ip(ip):
                                logger.debug(f"Got IP via psutil ({priority_iface}): {ip}")
                                return ip

            # Try other interfaces
            for iface, addrs in net_if_addrs.items():
                if iface in ['lo', 'eth0', 'wlan0']:
                    continue  # Skip loopback and already tried
                for addr in addrs:
                    if addr.family == socket.AF_INET:
                        ip = addr.address
                        if ip and is_valid_ip(ip):
                            logger.debug(f"Got IP via psutil ({iface}): {ip}")
                            return ip

        except Exception as e:
            logger.debug(f"psutil method failed: {e}")

        # Method 4: Hostname resolution (last resort)
        try:
            hostname = socket.gethostname()
            ip = socket.gethostbyname(hostname)
            if ip and is_valid_ip(ip):
                logger.debug(f"Got IP via hostname resolution: {ip}")
                return ip
        except Exception as e:
            logger.debug(f"Hostname resolution failed: {e}")

        # No valid IP found
        logger.warning("Could not determine valid IP address - all methods failed")
        return "0.0.0.0"  # Use 0.0.0.0 instead of 127.0.0.1 to indicate "no valid IP"

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
