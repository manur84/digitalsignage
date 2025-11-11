#!/usr/bin/env python3
"""
Watchdog Monitor for systemd integration
Sends periodic watchdog pings to systemd to prevent service restart on freeze
"""

import logging
import os
import asyncio
from typing import Optional

logger = logging.getLogger(__name__)


class WatchdogMonitor:
    """
    Monitors application health and notifies systemd watchdog
    """

    def __init__(self, enable: bool = True):
        """
        Initialize watchdog monitor

        Args:
            enable: Enable watchdog notifications (default: True)
        """
        self.enable = enable
        self.watchdog_usec: Optional[int] = None
        self.watchdog_pid: Optional[int] = None
        self.notify_socket: Optional[str] = None
        self._running = False
        self._task: Optional[asyncio.Task] = None

        # Check if running under systemd with watchdog enabled
        self._detect_systemd()

    def _detect_systemd(self):
        """Detect if running under systemd with watchdog"""
        self.watchdog_usec = os.getenv("WATCHDOG_USEC")
        self.watchdog_pid = os.getenv("WATCHDOG_PID")
        self.notify_socket = os.getenv("NOTIFY_SOCKET")

        if self.watchdog_usec and self.notify_socket:
            watchdog_sec = int(self.watchdog_usec) / 1000000
            logger.info(f"systemd watchdog detected: {watchdog_sec}s interval")
            logger.info(f"NOTIFY_SOCKET: {self.notify_socket}")
        else:
            logger.info("No systemd watchdog detected, running without watchdog")
            self.enable = False

    def is_enabled(self) -> bool:
        """Check if watchdog is enabled and available"""
        return self.enable and self.watchdog_usec is not None and self.notify_socket is not None

    async def start(self):
        """Start watchdog monitoring task"""
        if not self.is_enabled():
            logger.info("Watchdog monitoring not enabled")
            return

        if self._running:
            logger.warning("Watchdog monitoring already running")
            return

        self._running = True
        self._task = asyncio.create_task(self._watchdog_loop())
        logger.info("Watchdog monitoring started")

    async def stop(self):
        """Stop watchdog monitoring task"""
        if not self._running:
            return

        self._running = False
        if self._task:
            self._task.cancel()
            try:
                await self._task
            except asyncio.CancelledError:
                pass
        logger.info("Watchdog monitoring stopped")

    async def _watchdog_loop(self):
        """Main watchdog loop that sends periodic pings"""
        if not self.watchdog_usec:
            return

        # Calculate ping interval (half of watchdog timeout for safety)
        watchdog_interval = (int(self.watchdog_usec) / 1000000) / 2

        logger.info(f"Watchdog ping interval: {watchdog_interval}s")

        while self._running:
            try:
                self.notify_watchdog()
                await asyncio.sleep(watchdog_interval)
            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error(f"Watchdog loop error: {e}", exc_info=True)
                await asyncio.sleep(watchdog_interval)

    def notify_watchdog(self):
        """Send watchdog notification to systemd"""
        if not self.is_enabled():
            return

        try:
            self._sd_notify("WATCHDOG=1")
            logger.debug("Sent watchdog ping to systemd")
        except Exception as e:
            logger.error(f"Failed to send watchdog notification: {e}")

    def notify_ready(self):
        """Notify systemd that service is ready"""
        if not self.notify_socket:
            return

        try:
            self._sd_notify("READY=1")
            logger.info("Notified systemd: service ready")
        except Exception as e:
            logger.error(f"Failed to send ready notification: {e}")

    def notify_stopping(self):
        """Notify systemd that service is stopping"""
        if not self.notify_socket:
            return

        try:
            self._sd_notify("STOPPING=1")
            logger.info("Notified systemd: service stopping")
        except Exception as e:
            logger.error(f"Failed to send stopping notification: {e}")

    def notify_status(self, status: str):
        """
        Send status message to systemd

        Args:
            status: Status message to send
        """
        if not self.notify_socket:
            return

        try:
            self._sd_notify(f"STATUS={status}")
            logger.debug(f"Notified systemd status: {status}")
        except Exception as e:
            logger.error(f"Failed to send status notification: {e}")

    def _sd_notify(self, message: str):
        """
        Send notification to systemd via NOTIFY_SOCKET

        Args:
            message: Notification message
        """
        if not self.notify_socket:
            return

        import socket

        try:
            sock = socket.socket(socket.AF_UNIX, socket.SOCK_DGRAM)
            try:
                sock.connect(self.notify_socket)
                sock.sendall(message.encode('utf-8'))
            finally:
                sock.close()
        except Exception as e:
            logger.error(f"Failed to send notification '{message}': {e}")
            raise

    async def __aenter__(self):
        """Async context manager entry"""
        await self.start()
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        """Async context manager exit"""
        await self.stop()
