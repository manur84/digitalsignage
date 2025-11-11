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

    async def get_device_info(self) -> Dict[str, Any]:
        """Get comprehensive device information"""
        return {
            "hostname": self.hostname,
            "model": self.get_rpi_model(),
            "os_version": self.get_os_version(),
            "ip_address": self.get_ip_address(),
            "mac_address": self.get_mac_address(),
            "cpu_temp": self.get_cpu_temperature(),
            "cpu_usage": psutil.cpu_percent(interval=1),
            "memory_total": psutil.virtual_memory().total,
            "memory_used": psutil.virtual_memory().used,
            "disk_total": psutil.disk_usage('/').total,
            "disk_used": psutil.disk_usage('/').used,
            "screen_width": self.get_screen_width(),
            "screen_height": self.get_screen_height(),
            "uptime": int(psutil.boot_time())
        }

    def get_rpi_model(self) -> str:
        """Get Raspberry Pi model"""
        try:
            with open('/proc/device-tree/model', 'r') as f:
                return f.read().strip().replace('\x00', '')
        except:
            return platform.machine()

    def get_os_version(self) -> str:
        """Get OS version"""
        return platform.platform()

    def get_ip_address(self) -> str:
        """Get primary IP address"""
        import socket
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            s.connect(("8.8.8.8", 80))
            ip = s.getsockname()[0]
            s.close()
            return ip
        except:
            return "127.0.0.1"

    def get_mac_address(self) -> str:
        """Get MAC address"""
        import uuid
        mac = ':'.join(['{:02x}'.format((uuid.getnode() >> elements) & 0xff)
                       for elements in range(0, 2*6, 2)][::-1])
        return mac

    def get_cpu_temperature(self) -> float:
        """Get CPU temperature (Raspberry Pi specific)"""
        try:
            temp_file = Path('/sys/class/thermal/thermal_zone0/temp')
            if temp_file.exists():
                temp = float(temp_file.read_text()) / 1000.0
                return temp
        except:
            pass
        return 0.0

    def get_screen_width(self) -> int:
        """Get screen width"""
        try:
            result = subprocess.run(
                ['xrandr'],
                capture_output=True,
                text=True
            )
            for line in result.stdout.split('\n'):
                if '*' in line:
                    resolution = line.split()[0]
                    return int(resolution.split('x')[0])
        except:
            pass
        return 1920

    def get_screen_height(self) -> int:
        """Get screen height"""
        try:
            result = subprocess.run(
                ['xrandr'],
                capture_output=True,
                text=True
            )
            for line in result.stdout.split('\n'):
                if '*' in line:
                    resolution = line.split()[0]
                    return int(resolution.split('x')[1])
        except:
            pass
        return 1080

    async def restart_system(self):
        """Restart the system"""
        logger.warning("Restarting system...")
        subprocess.run(['sudo', 'reboot'])

    async def screen_on(self):
        """Turn screen on"""
        try:
            subprocess.run(['vcgencmd', 'display_power', '1'])
            logger.info("Screen turned on")
        except Exception as e:
            logger.error(f"Failed to turn screen on: {e}")

    async def screen_off(self):
        """Turn screen off"""
        try:
            subprocess.run(['vcgencmd', 'display_power', '0'])
            logger.info("Screen turned off")
        except Exception as e:
            logger.error(f"Failed to turn screen off: {e}")

    async def set_volume(self, volume: int):
        """Set audio volume (0-100)"""
        try:
            subprocess.run(['amixer', 'set', 'Master', f'{volume}%'])
            logger.info(f"Volume set to {volume}%")
        except Exception as e:
            logger.error(f"Failed to set volume: {e}")
