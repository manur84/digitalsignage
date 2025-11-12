"""
Status Screen Renderer for Digital Signage Client
Displays visual feedback while waiting for layouts or during connection states
"""

import logging
from typing import Optional, Dict, Any
from io import BytesIO
from datetime import datetime

from PyQt5.QtWidgets import QWidget, QLabel, QVBoxLayout, QHBoxLayout
from PyQt5.QtCore import Qt, QTimer, QPropertyAnimation, QEasingCurve, pyqtProperty
from PyQt5.QtGui import QPixmap, QFont, QPainter, QColor, QPen, QImage
import qrcode

logger = logging.getLogger(__name__)


class AnimatedDotsLabel(QLabel):
    """Label that animates dots (e.g., "Connecting..." becomes "Connecting." -> "Connecting.." -> "Connecting...")"""

    def __init__(self, base_text: str, parent=None):
        super().__init__(parent)
        self.base_text = base_text
        self.dot_count = 0
        self.max_dots = 3

        # Setup timer for animation
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.update_dots)
        self.timer.start(500)  # Update every 500ms

        self.update_dots()

    def update_dots(self):
        """Update the dots animation"""
        dots = "." * self.dot_count
        self.setText(f"{self.base_text}{dots}")
        self.dot_count = (self.dot_count + 1) % (self.max_dots + 1)

    def cleanup(self):
        """Stop the timer"""
        if self.timer:
            self.timer.stop()


class SpinnerWidget(QWidget):
    """Custom spinner widget with rotating circle"""

    def __init__(self, size: int = 80, color: str = "#4A90E2", parent=None):
        super().__init__(parent)
        self.setFixedSize(size, size)
        self._angle = 0
        self.color = QColor(color)

        # Setup animation
        self.animation = QPropertyAnimation(self, b"angle")
        self.animation.setDuration(1200)
        self.animation.setStartValue(0)
        self.animation.setEndValue(360)
        self.animation.setLoopCount(-1)  # Infinite loop
        self.animation.setEasingCurve(QEasingCurve.Linear)
        self.animation.start()

    @pyqtProperty(int)
    def angle(self):
        return self._angle

    @angle.setter
    def angle(self, value):
        self._angle = value
        self.update()

    def paintEvent(self, event):
        """Draw the spinner"""
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)

        # Calculate center
        rect = self.rect()
        center_x = rect.width() / 2
        center_y = rect.height() / 2
        radius = min(center_x, center_y) - 5

        # Draw arc
        pen = QPen(self.color)
        pen.setWidth(6)
        pen.setCapStyle(Qt.RoundCap)
        painter.setPen(pen)

        # Draw partial circle (270 degrees)
        start_angle = self._angle * 16  # Qt uses 1/16th degree units
        span_angle = 270 * 16

        painter.drawArc(
            int(center_x - radius),
            int(center_y - radius),
            int(radius * 2),
            int(radius * 2),
            start_angle,
            span_angle
        )

    def cleanup(self):
        """Stop the animation"""
        if self.animation:
            self.animation.stop()


class StatusScreen(QWidget):
    """Main status screen widget for displaying various client states"""

    # Color scheme
    COLOR_BACKGROUND = "#FFFFFF"  # DEBUG: White background to test visibility
    COLOR_PRIMARY = "#4A90E2"  # Blue for info
    COLOR_SUCCESS = "#5CB85C"  # Green for success
    COLOR_WARNING = "#F0AD4E"  # Yellow/Orange for warnings
    COLOR_ERROR = "#D9534F"  # Red for errors
    COLOR_TEXT_PRIMARY = "#000000"  # DEBUG: Black text for white background
    COLOR_TEXT_SECONDARY = "#333333"  # DEBUG: Dark gray text

    def __init__(self, width: int = 1920, height: int = 1080, parent=None):
        super().__init__(parent)
        self.screen_width = width
        self.screen_height = height
        self.animated_widgets = []  # Track animated widgets for cleanup

        self.setFixedSize(width, height)
        self.setStyleSheet(f"background-color: {self.COLOR_BACKGROUND};")

    def clear_screen(self):
        """Clear all widgets from the screen - called before widget destruction"""
        # Cleanup animated widgets
        for widget in self.animated_widgets:
            if hasattr(widget, 'cleanup'):
                try:
                    widget.cleanup()
                except Exception as e:
                    logger.warning(f"Failed to cleanup animated widget: {e}")

        self.animated_widgets.clear()

    def show_discovering_server(self, discovery_method: str = "Auto-Discovery"):
        """Show 'Discovering Server...' screen"""
        layout = QVBoxLayout(self)
        layout.setAlignment(Qt.AlignCenter)
        layout.setSpacing(30)

        # Spinner
        spinner = SpinnerWidget(100, self.COLOR_PRIMARY, self)
        spinner_container = QWidget()
        spinner_layout = QHBoxLayout(spinner_container)
        spinner_layout.addStretch()
        spinner_layout.addWidget(spinner)
        spinner_layout.addStretch()
        layout.addWidget(spinner_container)
        self.animated_widgets.append(spinner)

        # Main text with animated dots
        title_label = AnimatedDotsLabel("Discovering Digital Signage Server", self)
        title_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: 48pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)
        self.animated_widgets.append(title_label)

        # Discovery method
        method_label = QLabel(f"Using: {discovery_method}", self)
        method_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 24pt;")
        method_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(method_label)

        # Info text
        info_label = QLabel("Please wait while we search for available servers on the network", self)
        info_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 18pt;")
        info_label.setAlignment(Qt.AlignCenter)
        info_label.setWordWrap(True)
        layout.addWidget(info_label)

        layout.addStretch()

        self.setLayout(layout)
        self.update()
        logger.info("Status screen: Discovering Server")

    def show_connecting(self, server_url: str, attempt: int = 1, max_attempts: int = 5):
        """Show 'Connecting to Server...' screen"""
        layout = QVBoxLayout(self)
        layout.setAlignment(Qt.AlignCenter)
        layout.setSpacing(30)

        # Spinner
        spinner = SpinnerWidget(100, self.COLOR_PRIMARY, self)
        spinner_container = QWidget()
        spinner_layout = QHBoxLayout(spinner_container)
        spinner_layout.addStretch()
        spinner_layout.addWidget(spinner)
        spinner_layout.addStretch()
        layout.addWidget(spinner_container)
        self.animated_widgets.append(spinner)

        # Main text with animated dots
        title_label = AnimatedDotsLabel("Connecting to Server", self)
        title_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: 48pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)
        self.animated_widgets.append(title_label)

        # Server URL
        server_label = QLabel(server_url, self)
        server_label.setStyleSheet(f"color: {self.COLOR_PRIMARY}; font-size: 32pt; font-weight: bold;")
        server_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(server_label)

        # Attempt counter
        attempt_label = QLabel(f"Attempt {attempt} of {max_attempts}", self)
        attempt_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 24pt;")
        attempt_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(attempt_label)

        layout.addStretch()

        self.setLayout(layout)
        self.update()
        logger.info(f"Status screen: Connecting (attempt {attempt}/{max_attempts})")

    def show_waiting_for_layout(self, client_id: str, server_url: str):
        """Show 'Waiting for Layout...' screen after successful connection"""
        layout = QVBoxLayout(self)
        layout.setAlignment(Qt.AlignCenter)
        layout.setSpacing(30)

        # Success checkmark (static icon)
        checkmark_label = QLabel("✓", self)
        checkmark_label.setStyleSheet(f"color: {self.COLOR_SUCCESS}; font-size: 120pt; font-weight: bold;")
        checkmark_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(checkmark_label)

        # Connection success
        connected_label = QLabel("Connected to Digital Signage Server", self)
        connected_label.setStyleSheet(f"color: {self.COLOR_SUCCESS}; font-size: 36pt; font-weight: bold;")
        connected_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(connected_label)

        # Server info
        server_label = QLabel(server_url, self)
        server_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 20pt;")
        server_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(server_label)

        # Spacer
        layout.addSpacing(40)

        # Client ID
        id_label = QLabel(f"Client ID: {client_id}", self)
        id_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 18pt;")
        id_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(id_label)

        # Spacer
        layout.addSpacing(40)

        # Waiting message with animated dots
        waiting_label = AnimatedDotsLabel("Waiting for layout assignment", self)
        waiting_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: 28pt;")
        waiting_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(waiting_label)
        self.animated_widgets.append(waiting_label)

        layout.addStretch()

        self.setLayout(layout)
        self.update()
        # Force processing of events to ensure rendering
        from PyQt5.QtWidgets import QApplication
        QApplication.processEvents()
        logger.info(f"Status screen: Waiting for Layout (widgets: {len(self.findChildren(QLabel))})")

    def show_connection_error(self, server_url: str, error_message: str, client_id: str = "Unknown"):
        """Show 'Connection Error' screen"""
        layout = QVBoxLayout(self)
        layout.setAlignment(Qt.AlignCenter)
        layout.setSpacing(30)

        # Error icon
        error_icon = QLabel("✗", self)
        error_icon.setStyleSheet(f"color: {self.COLOR_ERROR}; font-size: 120pt; font-weight: bold;")
        error_icon.setAlignment(Qt.AlignCenter)
        layout.addWidget(error_icon)

        # Error title
        title_label = QLabel("Connection Error", self)
        title_label.setStyleSheet(f"color: {self.COLOR_ERROR}; font-size: 48pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)

        # Failed server
        server_label = QLabel(f"Failed to connect to: {server_url}", self)
        server_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 24pt;")
        server_label.setAlignment(Qt.AlignCenter)
        server_label.setWordWrap(True)
        layout.addWidget(server_label)

        # Error message
        if error_message:
            error_label = QLabel(error_message, self)
            error_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 18pt;")
            error_label.setAlignment(Qt.AlignCenter)
            error_label.setWordWrap(True)
            layout.addWidget(error_label)

        # Spacer
        layout.addSpacing(40)

        # Instructions
        instructions = [
            "Please check:",
            "• Network connection is active",
            "• Server is running and accessible",
            "• Firewall settings allow connection",
            "• Server address and port are correct"
        ]
        instructions_label = QLabel("\n".join(instructions), self)
        instructions_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 18pt;")
        instructions_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(instructions_label)

        # QR code with debug info
        layout.addSpacing(30)
        qr_data = f"Client ID: {client_id}\nServer: {server_url}\nError: {error_message}\nTime: {datetime.now().isoformat()}"
        qr_widget = self._create_qr_code(qr_data, 200)
        if qr_widget:
            qr_container = QWidget()
            qr_layout = QHBoxLayout(qr_container)
            qr_layout.addStretch()
            qr_layout.addWidget(qr_widget)
            qr_layout.addStretch()
            layout.addWidget(qr_container)

            qr_label = QLabel("Scan for debug information", self)
            qr_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 14pt;")
            qr_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(qr_label)

        layout.addStretch()

        self.setLayout(layout)
        self.update()
        logger.info("Status screen: Connection Error")

    def show_no_layout_assigned(self, client_id: str, server_url: str, ip_address: str = "Unknown"):
        """Show 'No Layout Assigned' screen"""
        layout = QVBoxLayout(self)
        layout.setAlignment(Qt.AlignCenter)
        layout.setSpacing(30)

        # Warning icon
        warning_icon = QLabel("⚠", self)
        warning_icon.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: 120pt; font-weight: bold;")
        warning_icon.setAlignment(Qt.AlignCenter)
        layout.addWidget(warning_icon)

        # Title
        title_label = QLabel("No Layout Assigned", self)
        title_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: 48pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)

        # Message
        message_label = QLabel("This device is connected but has not been assigned a layout", self)
        message_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 24pt;")
        message_label.setAlignment(Qt.AlignCenter)
        message_label.setWordWrap(True)
        layout.addWidget(message_label)

        # Spacer
        layout.addSpacing(40)

        # Device info
        device_info = [
            f"Client ID: {client_id}",
            f"IP Address: {ip_address}",
            f"Server: {server_url}"
        ]
        info_label = QLabel("\n".join(device_info), self)
        info_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 20pt;")
        info_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(info_label)

        # Spacer
        layout.addSpacing(40)

        # Instructions for admin
        instructions = [
            "Administrator Actions:",
            "1. Log in to the Digital Signage Management Server",
            "2. Navigate to Device Management",
            "3. Find this device by Client ID or IP Address",
            "4. Assign a layout to this device"
        ]
        instructions_label = QLabel("\n".join(instructions), self)
        instructions_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 18pt; background-color: #2A2A2A; padding: 20px; border-radius: 10px;")
        instructions_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(instructions_label)

        # QR code with device info
        layout.addSpacing(30)
        qr_data = f"Client ID: {client_id}\nIP: {ip_address}\nServer: {server_url}\nStatus: No Layout Assigned\nTime: {datetime.now().isoformat()}"
        qr_widget = self._create_qr_code(qr_data, 200)
        if qr_widget:
            qr_container = QWidget()
            qr_layout = QHBoxLayout(qr_container)
            qr_layout.addStretch()
            qr_layout.addWidget(qr_widget)
            qr_layout.addStretch()
            layout.addWidget(qr_container)

            qr_label = QLabel("Scan for device information", self)
            qr_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: 14pt;")
            qr_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(qr_label)

        layout.addStretch()

        self.setLayout(layout)
        self.update()
        logger.info("Status screen: No Layout Assigned")

    def _create_qr_code(self, data: str, size: int = 200) -> Optional[QLabel]:
        """Create a QR code widget"""
        try:
            qr = qrcode.QRCode(
                version=1,
                error_correction=qrcode.constants.ERROR_CORRECT_M,
                box_size=10,
                border=4,
            )
            qr.add_data(data)
            qr.make(fit=True)

            img = qr.make_image(fill_color="white", back_color=self.COLOR_BACKGROUND)

            # Convert PIL image to QPixmap
            buffer = BytesIO()
            img.save(buffer, format='PNG')
            buffer.seek(0)

            pixmap = QPixmap()
            if not pixmap.loadFromData(buffer.read()):
                logger.error("Failed to load QR code image data")
                return None

            pixmap = pixmap.scaled(size, size, Qt.KeepAspectRatio, Qt.SmoothTransformation)

            label = QLabel(self)
            label.setPixmap(pixmap)
            label.setAlignment(Qt.AlignCenter)

            buffer.close()
            return label

        except Exception as e:
            logger.error(f"Failed to create QR code: {e}")
            return None


class StatusScreenManager:
    """Manager for status screens - provides simplified interface"""

    def __init__(self, display_renderer):
        """
        Initialize status screen manager

        Args:
            display_renderer: The DisplayRenderer instance to use for showing status screens
        """
        self.display_renderer = display_renderer
        self.status_screen: Optional[StatusScreen] = None
        self.is_showing_status = False

    def show_discovering_server(self, discovery_method: str = "Auto-Discovery"):
        """Show discovering server screen"""
        self._ensure_status_screen()
        self.status_screen.show_discovering_server(discovery_method)
        self.is_showing_status = True

    def show_connecting(self, server_url: str, attempt: int = 1, max_attempts: int = 5):
        """Show connecting screen"""
        self._ensure_status_screen()
        self.status_screen.show_connecting(server_url, attempt, max_attempts)
        self.is_showing_status = True

    def show_waiting_for_layout(self, client_id: str, server_url: str):
        """Show waiting for layout screen"""
        self._ensure_status_screen()
        self.status_screen.show_waiting_for_layout(client_id, server_url)
        self.is_showing_status = True

    def show_connection_error(self, server_url: str, error_message: str, client_id: str = "Unknown"):
        """Show connection error screen"""
        self._ensure_status_screen()
        self.status_screen.show_connection_error(server_url, error_message, client_id)
        self.is_showing_status = True

    def show_no_layout_assigned(self, client_id: str, server_url: str, ip_address: str = "Unknown"):
        """Show no layout assigned screen"""
        self._ensure_status_screen()
        self.status_screen.show_no_layout_assigned(client_id, server_url, ip_address)
        self.is_showing_status = True

    def clear_status_screen(self):
        """Clear the status screen and prepare for layout display"""
        if self.status_screen:
            try:
                self.status_screen.clear_screen()
                self.status_screen.hide()
                self.status_screen.deleteLater()
                self.status_screen = None
            except Exception as e:
                logger.warning(f"Failed to clear status screen: {e}")

        self.is_showing_status = False
        logger.info("Status screen cleared")

    def _ensure_status_screen(self):
        """Ensure status screen widget exists"""
        # Always recreate the status screen to avoid layout issues
        if self.status_screen:
            try:
                self.status_screen.clear_screen()
                self.status_screen.deleteLater()
            except Exception as e:
                logger.warning(f"Failed to cleanup old status screen: {e}")

        # Get the actual screen dimensions (not widget dimensions which may not be set yet)
        from PyQt5.QtWidgets import QApplication
        screen = QApplication.primaryScreen()
        if screen:
            screen_geometry = screen.geometry()
            width = screen_geometry.width()
            height = screen_geometry.height()
            logger.info(f"Using screen dimensions: {width}x{height}")
        else:
            # Fallback to display renderer dimensions
            width = self.display_renderer.width()
            height = self.display_renderer.height()
            logger.warning(f"Could not get screen geometry, using widget dimensions: {width}x{height}")

        # Create fresh status screen as a TOP-LEVEL window (not a child)
        # This ensures it's displayed on top and not hidden by parent widget
        self.status_screen = StatusScreen(width, height, parent=None)

        # Set window flags for frameless fullscreen overlay
        from PyQt5.QtCore import Qt
        self.status_screen.setWindowFlags(
            Qt.Window |
            Qt.FramelessWindowHint |
            Qt.WindowStaysOnTopHint
        )

        # Position at top-left and size to screen
        self.status_screen.setGeometry(0, 0, width, height)

        # Show as fullscreen
        self.status_screen.showFullScreen()
        self.status_screen.raise_()  # Bring to front
        self.status_screen.activateWindow()  # Ensure it's active
        self.status_screen.repaint()  # Force repaint

        logger.info(f"Status screen created: size={width}x{height}, visible={self.status_screen.isVisible()}, geometry={self.status_screen.geometry()}")
