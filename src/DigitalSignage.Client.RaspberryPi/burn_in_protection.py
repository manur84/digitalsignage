"""
Anti-Burn-In Protection Module for Digital Signage Client

Implements pixel-shifting and screensaver functionality to prevent OLED/LCD burn-in.
"""

import logging
from typing import Optional, Tuple
from PyQt5.QtCore import QTimer, QPropertyAnimation, QEasingCurve, QPoint
from PyQt5.QtWidgets import QWidget
from PyQt5.QtGui import QColor, QPainter, QLinearGradient
import random
import time

logger = logging.getLogger(__name__)


class BurnInProtection:
    """
    Provides anti-burn-in protection through pixel-shifting and screensaver
    """

    def __init__(
        self,
        widget: QWidget,
        enabled: bool = True,
        pixel_shift_interval: int = 300,  # 5 minutes in seconds
        pixel_shift_max: int = 5,  # Maximum pixels to shift
        screensaver_timeout: int = 3600,  # 1 hour in seconds
    ):
        """
        Initialize burn-in protection

        Args:
            widget: The QWidget to protect (usually the main display widget)
            enabled: Whether burn-in protection is enabled
            pixel_shift_interval: How often to shift pixels (seconds)
            pixel_shift_max: Maximum pixel shift distance
            screensaver_timeout: Time of inactivity before screensaver activates (seconds)
        """
        self.widget = widget
        self.enabled = enabled
        self.pixel_shift_interval = pixel_shift_interval
        self.pixel_shift_max = pixel_shift_max
        self.screensaver_timeout = screensaver_timeout

        # Current pixel shift offset
        self.current_offset = QPoint(0, 0)

        # Screensaver state
        self.screensaver_active = False
        self.screensaver_widget: Optional[ScreenSaverWidget] = None

        # Last activity timestamp
        self.last_activity_time = time.time()

        # Timers
        self.pixel_shift_timer: Optional[QTimer] = None
        self.screensaver_timer: Optional[QTimer] = None

        if self.enabled:
            self._setup_timers()
            logger.info(
                "Burn-in protection initialized (PixelShift: %ds, MaxShift: %dpx, Screensaver: %ds)",
                pixel_shift_interval,
                pixel_shift_max,
                screensaver_timeout,
            )
        else:
            logger.info("Burn-in protection disabled")

    def _setup_timers(self):
        """Setup timers for pixel shifting and screensaver"""
        # Pixel shift timer
        self.pixel_shift_timer = QTimer()
        self.pixel_shift_timer.timeout.connect(self._perform_pixel_shift)
        self.pixel_shift_timer.start(self.pixel_shift_interval * 1000)

        # Screensaver check timer (check every minute)
        self.screensaver_timer = QTimer()
        self.screensaver_timer.timeout.connect(self._check_screensaver)
        self.screensaver_timer.start(60 * 1000)  # Check every 60 seconds

        logger.debug("Burn-in protection timers started")

    def _perform_pixel_shift(self):
        """Perform pixel shifting by randomly offsetting the display widget"""
        if not self.enabled or self.screensaver_active:
            return

        try:
            # Generate random offset within max range
            x_offset = random.randint(-self.pixel_shift_max, self.pixel_shift_max)
            y_offset = random.randint(-self.pixel_shift_max, self.pixel_shift_max)

            # Store current offset
            self.current_offset = QPoint(x_offset, y_offset)

            # Move widget to new position
            current_pos = self.widget.pos()
            new_pos = QPoint(current_pos.x() + x_offset, current_pos.y() + y_offset)
            self.widget.move(new_pos)

            logger.debug(
                "Pixel shift applied (offset: %d, %d)", x_offset, y_offset
            )

        except Exception as e:
            logger.error(f"Error during pixel shift: {e}")

    def _check_screensaver(self):
        """Check if screensaver should be activated"""
        if not self.enabled:
            return

        try:
            current_time = time.time()
            inactive_time = current_time - self.last_activity_time

            if not self.screensaver_active and inactive_time >= self.screensaver_timeout:
                self._activate_screensaver()
            elif self.screensaver_active and inactive_time < self.screensaver_timeout:
                self._deactivate_screensaver()

        except Exception as e:
            logger.error(f"Error checking screensaver: {e}")

    def _activate_screensaver(self):
        """Activate the screensaver"""
        if self.screensaver_active:
            return

        try:
            logger.info("Activating screensaver")

            # Create screensaver widget
            self.screensaver_widget = ScreenSaverWidget(self.widget.parent())
            self.screensaver_widget.setGeometry(self.widget.parent().geometry())
            self.screensaver_widget.show()
            self.screensaver_widget.raise_()

            # Hide main content
            self.widget.hide()

            self.screensaver_active = True

        except Exception as e:
            logger.error(f"Error activating screensaver: {e}")

    def _deactivate_screensaver(self):
        """Deactivate the screensaver"""
        if not self.screensaver_active:
            return

        try:
            logger.info("Deactivating screensaver")

            # Remove screensaver widget
            if self.screensaver_widget:
                self.screensaver_widget.hide()
                self.screensaver_widget.deleteLater()
                self.screensaver_widget = None

            # Show main content
            self.widget.show()

            self.screensaver_active = False

        except Exception as e:
            logger.error(f"Error deactivating screensaver: {e}")

    def report_activity(self):
        """Report user/system activity to reset screensaver timer"""
        self.last_activity_time = time.time()

        # If screensaver is active, deactivate it
        if self.screensaver_active:
            self._deactivate_screensaver()

    def reset_pixel_shift(self):
        """Reset pixel shift to original position"""
        if self.current_offset.x() == 0 and self.current_offset.y() == 0:
            return

        try:
            current_pos = self.widget.pos()
            original_pos = QPoint(
                current_pos.x() - self.current_offset.x(),
                current_pos.y() - self.current_offset.y(),
            )
            self.widget.move(original_pos)
            self.current_offset = QPoint(0, 0)
            logger.debug("Pixel shift reset to original position")
        except Exception as e:
            logger.error(f"Error resetting pixel shift: {e}")

    def enable(self):
        """Enable burn-in protection"""
        if self.enabled:
            return

        self.enabled = True
        self._setup_timers()
        logger.info("Burn-in protection enabled")

    def disable(self):
        """Disable burn-in protection"""
        if not self.enabled:
            return

        self.enabled = False

        # Stop timers
        if self.pixel_shift_timer:
            self.pixel_shift_timer.stop()
            self.pixel_shift_timer = None

        if self.screensaver_timer:
            self.screensaver_timer.stop()
            self.screensaver_timer = None

        # Reset pixel shift
        self.reset_pixel_shift()

        # Deactivate screensaver
        if self.screensaver_active:
            self._deactivate_screensaver()

        logger.info("Burn-in protection disabled")

    def cleanup(self):
        """Cleanup resources"""
        self.disable()


class ScreenSaverWidget(QWidget):
    """
    Animated screensaver widget with moving gradient
    """

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowFlags(self.windowFlags() | self.Qt.WindowStaysOnTopHint)
        self.setAttribute(self.Qt.WA_TranslucentBackground, False)

        # Animation state
        self.gradient_offset = 0.0

        # Setup animation timer
        self.animation_timer = QTimer()
        self.animation_timer.timeout.connect(self._animate)
        self.animation_timer.start(50)  # 20 FPS

        # Colors for gradient
        self.colors = [
            QColor(10, 10, 20),  # Dark blue
            QColor(20, 10, 30),  # Dark purple
            QColor(10, 20, 10),  # Dark green
        ]
        self.current_color_index = 0

        logger.debug("Screensaver widget created")

    def _animate(self):
        """Animate the screensaver"""
        self.gradient_offset += 0.01
        if self.gradient_offset >= 1.0:
            self.gradient_offset = 0.0
            # Change color scheme
            self.current_color_index = (self.current_color_index + 1) % len(self.colors)

        self.update()

    def paintEvent(self, event):
        """Paint the screensaver"""
        painter = QPainter(self)

        # Create animated gradient
        gradient = QLinearGradient(0, 0, self.width(), self.height())

        # Get current and next color
        color1 = self.colors[self.current_color_index]
        color2 = self.colors[(self.current_color_index + 1) % len(self.colors)]

        # Interpolate between colors
        r = int(color1.red() * (1 - self.gradient_offset) + color2.red() * self.gradient_offset)
        g = int(color1.green() * (1 - self.gradient_offset) + color2.green() * self.gradient_offset)
        b = int(color1.blue() * (1 - self.gradient_offset) + color2.blue() * self.gradient_offset)

        gradient.setColorAt(0, QColor(r, g, b))
        gradient.setColorAt(0.5, color1.darker(120))
        gradient.setColorAt(1, QColor(r, g, b).darker(110))

        painter.fillRect(self.rect(), gradient)

    def cleanup(self):
        """Cleanup resources"""
        if self.animation_timer:
            self.animation_timer.stop()
