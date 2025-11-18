#!/usr/bin/env python3
"""
Shutdown Logo Display for Digital Signage Client

Displays the branded logo during system shutdown for a professional
exit experience. Called by systemd during shutdown sequence.

This script:
- Acquires display resources
- Shows the shutdown logo via Plymouth or custom framebuffer
- Handles cleanup gracefully
- Logs shutdown events
"""

import os
import sys
import signal
import subprocess
import logging
import time
from pathlib import Path
from typing import Optional

logger = logging.getLogger(__name__)


class ShutdownLogoDisplay:
    """Manages logo display during system shutdown"""

    LOGO_PATH = "/opt/digitalsignage-client/digisign-logo.png"
    PLYMOUTH_CTRL = "/run/plymouth/control"
    SHUTDOWN_TIMEOUT = 30  # Maximum seconds to keep logo visible

    def __init__(self):
        """Initialize shutdown logo display"""
        self.start_time = time.time()
        self.shutdown_event = False

    def configure_logging(self, log_file: Optional[str] = None) -> None:
        """Configure logging for shutdown"""
        if log_file is None:
            log_file = "/var/log/digitalsignage-shutdown.log"

        log_path = Path(log_file)
        log_path.parent.mkdir(parents=True, exist_ok=True)

        handler = logging.FileHandler(log_file)
        formatter = logging.Formatter(
            '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
        )
        handler.setFormatter(formatter)

        logger.addHandler(handler)
        logger.setLevel(logging.INFO)

    def handle_signal(self, signum, frame):
        """Handle signals during shutdown"""
        self.shutdown_event = True
        logger.info(f"Received signal {signum}, initiating shutdown...")

    def setup_signal_handlers(self) -> None:
        """Setup signal handlers for graceful shutdown"""
        signal.signal(signal.SIGTERM, self.handle_signal)
        signal.signal(signal.SIGINT, self.handle_signal)

    def show_via_plymouth(self) -> bool:
        """
        Show logo via Plymouth during shutdown

        Returns:
            True if successful, False otherwise
        """
        try:
            logger.info("Attempting to show shutdown logo via Plymouth...")

            # Check if Plymouth is running
            if not os.path.exists(self.PLYMOUTH_CTRL):
                logger.warning("Plymouth not active during shutdown")
                return False

            # Plymouth should already be displaying the logo
            # Just ensure it stays visible
            logger.info("Plymouth shutdown logo enabled")
            return True

        except Exception as e:
            logger.error(f"Failed to show Plymouth logo: {e}")
            return False

    def show_via_console(self) -> bool:
        """
        Show logo on console framebuffer during shutdown

        Returns:
            True if successful, False otherwise
        """
        try:
            logger.info("Attempting to show shutdown logo via console...")

            if not os.path.exists(self.LOGO_PATH):
                logger.warning(f"Logo file not found: {self.LOGO_PATH}")
                return False

            # Try using fbi (framebuffer image viewer) if available
            if self._show_with_fbi():
                return True

            # Try using display (ImageMagick) if available
            if self._show_with_display():
                return True

            logger.warning("No suitable image viewer found for console display")
            return False

        except Exception as e:
            logger.error(f"Failed to show console logo: {e}")
            return False

    def _show_with_fbi(self) -> bool:
        """Try to display logo using fbi (framebuffer image viewer)"""
        try:
            if not self._command_exists("fbi"):
                return False

            logger.info("Using fbi to display logo...")
            subprocess.run(
                ["fbi", "-d", "/dev/fb0", "-a", "-n", "-t", "30", self.LOGO_PATH],
                timeout=35,
                capture_output=True
            )
            logger.info("Logo displayed via fbi")
            return True

        except subprocess.TimeoutExpired:
            logger.info("fbi timeout reached (expected)")
            return True
        except Exception as e:
            logger.debug(f"fbi display failed: {e}")
            return False

    def _show_with_display(self) -> bool:
        """Try to display logo using ImageMagick display command"""
        try:
            if not self._command_exists("display"):
                return False

            logger.info("Using ImageMagick display to show logo...")

            # Get framebuffer size
            try:
                fb_size = subprocess.check_output(
                    ["fbset", "-s"],
                    capture_output=True,
                    text=True
                ).stdout
                logger.debug(f"Framebuffer info: {fb_size}")
            except Exception:
                logger.debug("Could not determine framebuffer size")

            subprocess.run(
                ["display", "-window", "root", "-geometry", "+0+0", self.LOGO_PATH],
                timeout=35,
                capture_output=True
            )
            logger.info("Logo displayed via ImageMagick")
            return True

        except subprocess.TimeoutExpired:
            logger.info("display timeout reached (expected)")
            return True
        except Exception as e:
            logger.debug(f"ImageMagick display failed: {e}")
            return False

    @staticmethod
    def _command_exists(command: str) -> bool:
        """Check if a command exists in PATH"""
        return subprocess.run(
            ["which", command],
            capture_output=True
        ).returncode == 0

    def show_logo(self) -> bool:
        """
        Show shutdown logo using available method

        Returns:
            True if successfully shown, False otherwise
        """
        try:
            # Try Plymouth first (preferred)
            if self.show_via_plymouth():
                return True

            # Fallback to console display
            if self.show_via_console():
                return True

            logger.warning("Could not display shutdown logo")
            return False

        except Exception as e:
            logger.error(f"Error showing shutdown logo: {e}")
            return False

    def wait_for_shutdown(self) -> None:
        """Wait for shutdown signal or timeout"""
        elapsed = 0
        while elapsed < self.SHUTDOWN_TIMEOUT and not self.shutdown_event:
            time.sleep(1)
            elapsed = time.time() - self.start_time

        if elapsed >= self.SHUTDOWN_TIMEOUT:
            logger.info(f"Shutdown timeout reached ({self.SHUTDOWN_TIMEOUT}s)")
        elif self.shutdown_event:
            logger.info("Shutdown signal received")

    def run(self) -> int:
        """
        Main execution

        Returns:
            Exit code (0 = success, 1 = failure)
        """
        try:
            self.configure_logging()
            logger.info("=== Digital Signage Shutdown Logo Display Started ===")

            self.setup_signal_handlers()

            if self.show_logo():
                logger.info("Shutdown logo displayed successfully")
                self.wait_for_shutdown()
                logger.info("Shutdown sequence complete")
                return 0
            else:
                logger.warning("Failed to display shutdown logo")
                return 1

        except Exception as e:
            logger.error(f"Shutdown logo display failed: {e}")
            return 1


def main():
    """Main entry point"""
    display = ShutdownLogoDisplay()
    return display.run()


if __name__ == "__main__":
    sys.exit(main())
