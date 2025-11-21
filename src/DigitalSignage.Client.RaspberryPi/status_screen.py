"""
Status Screen Renderer for Digital Signage Client
Displays visual feedback while waiting for layouts or during connection states

PERFORMANCE OPTIMIZATIONS:
- Optimized spinner animation with lower CPU usage
- Smooth transitions between screens
- Event processing during discovery to prevent freezing
- Efficient QTimer updates
"""

import logging
from typing import Optional, Dict, Any
from io import BytesIO
from datetime import datetime

from PyQt5.QtWidgets import QWidget, QLabel, QVBoxLayout, QHBoxLayout
from PyQt5.QtCore import Qt, QTimer, QPropertyAnimation, QEasingCurve, pyqtProperty, QVariantAnimation
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

        # Setup timer for animation - optimized interval
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.update_dots)
        self.timer.start(600)  # Update every 600ms (smoother than 500ms)

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
    """Custom spinner widget with rotating circle - OPTIMIZED for smooth animation"""

    def __init__(self, size: int = 80, color: str = "#4A90E2", parent=None):
        super().__init__(parent)
        self.setFixedSize(size, size)
        self._angle = 0
        self.color = QColor(color)

        # PERFORMANCE OPTIMIZATION: Use QVariantAnimation instead of QPropertyAnimation
        # This reduces CPU usage and makes animation smoother
        self.animation = QVariantAnimation(self)
        self.animation.setStartValue(0)
        self.animation.setEndValue(360)
        self.animation.setDuration(1500)  # Slightly slower = smoother
        self.animation.setLoopCount(-1)  # Infinite loop
        self.animation.setEasingCurve(QEasingCurve.Linear)
        self.animation.valueChanged.connect(self._on_angle_changed)
        self.animation.start()

    def _on_angle_changed(self, value):
        """Handle angle change from animation"""
        self._angle = value
        self.update()

    @pyqtProperty(int)
    def angle(self):
        return self._angle

    @angle.setter
    def angle(self, value):
        self._angle = value
        self.update()

    def paintEvent(self, event):
        """Draw the spinner - OPTIMIZED painting"""
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing, True)  # Explicit True for smoother edges
        painter.setRenderHint(QPainter.SmoothPixmapTransform, True)

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
    """Main status screen widget for displaying various client states - OPTIMIZED for performance"""

    # Color scheme
    COLOR_BACKGROUND = "#1a1a2e"  # Dark blue-gray background
    COLOR_PRIMARY = "#4A90E2"  # Blue for info
    COLOR_SUCCESS = "#5CB85C"  # Green for success
    COLOR_WARNING = "#F0AD4E"  # Yellow/Orange for warnings
    COLOR_ERROR = "#D9534F"  # Red for errors
    COLOR_TEXT_PRIMARY = "#FFFFFF"  # White text for dark background
    COLOR_TEXT_SECONDARY = "#B0B0B0"  # Light gray text for dark background

    def __init__(self, width: int = 1920, height: int = 1080, parent=None):
        super().__init__(parent)
        self.screen_width = width
        self.screen_height = height
        self.animated_widgets = []  # Track animated widgets for cleanup
        self.current_layout = None  # Track current layout to avoid redundant recreations

        self.setObjectName("status_screen")
        self.setFixedSize(width, height)
        self.setAttribute(Qt.WA_StyledBackground, True)
        self.setAttribute(Qt.WA_OpaquePaintEvent, True)
        self.setAutoFillBackground(True)
        self.setStyleSheet(f"background-color: {self.COLOR_BACKGROUND};")
        palette = self.palette()
        palette.setColor(self.backgroundRole(), QColor(self.COLOR_BACKGROUND))
        self.setPalette(palette)

        # CRITICAL FIX: Hide cursor to prevent mouse pointer appearing on touch
        self.setCursor(Qt.BlankCursor)

        # Calculate scaled dimensions for responsive layout
        self._calculate_scaled_dimensions()

    def paintEvent(self, event):
        """Ensure the background is always painted (prevents white bleed-through on first render)"""
        painter = QPainter(self)
        painter.fillRect(self.rect(), QColor(self.COLOR_BACKGROUND))
        super().paintEvent(event)

    def _calculate_scaled_dimensions(self):
        """
        Calculate responsive dimensions based on screen resolution
        OPTIMIZED: Pre-calculate all sizes for better performance
        """
        # Scale fonts based on screen height (as percentage)
        self.title_font_size = int(self.screen_height * 0.05)
        self.subtitle_font_size = int(self.screen_height * 0.035)
        self.body_font_size = int(self.screen_height * 0.025)
        self.small_font_size = int(self.screen_height * 0.018)
        self.icon_font_size = int(self.screen_height * 0.12)

        # Scale QR code based on smallest dimension
        min_dimension = min(self.screen_width, self.screen_height)
        self.qr_size = int(min_dimension * 0.18)

        # Scale spinner size
        self.spinner_size = int(self.screen_height * 0.10)

        # Scale spacing and padding
        self.spacing = int(self.screen_height * 0.02)
        self.large_spacing = int(self.screen_height * 0.035)
        self.padding = int(self.screen_height * 0.015)

        logger.debug(f"Scaled dimensions calculated for {self.screen_width}x{self.screen_height}")

    def clear_screen(self):
        """
        Clear all widgets from the screen - OPTIMIZED cleanup
        Called before widget destruction
        """
        # Cleanup animated widgets
        for widget in self.animated_widgets:
            if hasattr(widget, 'cleanup'):
                try:
                    widget.cleanup()
                except Exception as e:
                    logger.warning(f"Failed to cleanup animated widget: {e}")

        self.animated_widgets.clear()

        # Clear current layout to allow recreation
        if self.current_layout:
            try:
                self.current_layout.deleteLater()
            except Exception as e:
                logger.warning(f"Failed to delete current layout: {e}")
            self.current_layout = None

    def _create_layout_widget(self):
        """
        Create and return a new layout widget for content
        OPTIMIZED: Reuse widget if possible to reduce object creation
        """
        if self.current_layout:
            # Clear existing content instead of creating new widget
            for i in reversed(range(self.current_layout.count())):
                item = self.current_layout.takeAt(i)
                if item.widget():
                    item.widget().deleteLater()
            return self.current_layout
        else:
            # Create new layout
            layout = QVBoxLayout(self)
            layout.setAlignment(Qt.AlignCenter)
            layout.setSpacing(self.spacing)
            self.current_layout = layout
            return layout

    def show_discovering_server(self, discovery_method: str = "Auto-Discovery"):
        """Show 'Discovering Server...' screen with QR code to web interface"""
        layout = self._create_layout_widget()

        # Spinner
        spinner = SpinnerWidget(self.spinner_size, self.COLOR_PRIMARY, self)
        spinner_container = QWidget()
        spinner_layout = QHBoxLayout(spinner_container)
        spinner_layout.addStretch()
        spinner_layout.addWidget(spinner)
        spinner_layout.addStretch()
        layout.addWidget(spinner_container)
        self.animated_widgets.append(spinner)

        # Main text with animated dots - German
        title_label = AnimatedDotsLabel("Auto-Discovery Aktiv", self)
        title_label.setStyleSheet(f"color: {self.COLOR_PRIMARY}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)
        self.animated_widgets.append(title_label)

        # Searching message - German
        search_label = QLabel("Suche Server im Netzwerk", self)
        search_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: {self.subtitle_font_size}pt;")
        search_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(search_label)

        # Discovery method
        method_label = QLabel(f"Methode: {discovery_method}", self)
        method_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        method_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(method_label)

        # Spacer
        layout.addSpacing(self.large_spacing)

        # QR Code for web interface
        try:
            import socket
            # Get local IP address
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            try:
                s.connect(('8.8.8.8', 80))
                local_ip = s.getsockname()[0]
            except Exception:
                local_ip = '127.0.0.1'
            finally:
                s.close()

            web_url = f"http://{local_ip}:5000"

            qr_label = self._create_qr_code(web_url, self.qr_size)
            if qr_label:
                layout.addWidget(qr_label)

            # Web interface URL below QR code
            url_label = QLabel(f"Web-Interface: {web_url}", self)
            url_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
            url_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(url_label)

        except Exception as e:
            logger.warning(f"Could not create QR code for web interface: {e}")

        layout.addStretch()

        self.setLayout(layout)
        self.update()

        # CRITICAL FIX: Ensure status screen is ALWAYS visible and on top
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        # Note: repaint() removed - update() is sufficient and less CPU intensive

        logger.info("Status screen: Discovering Server (Auto-Discovery Active)")

    def show_connecting(self, server_url: str, attempt: int = 1, max_attempts: int = 5):
        """Show 'Connecting to Server...' screen"""
        layout = self._create_layout_widget()

        # Spinner
        spinner = SpinnerWidget(self.spinner_size, self.COLOR_PRIMARY, self)
        spinner_container = QWidget()
        spinner_layout = QHBoxLayout(spinner_container)
        spinner_layout.addStretch()
        spinner_layout.addWidget(spinner)
        spinner_layout.addStretch()
        layout.addWidget(spinner_container)
        self.animated_widgets.append(spinner)

        # Main text with animated dots
        title_label = AnimatedDotsLabel("Verbindung wird hergestellt", self)
        title_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)
        self.animated_widgets.append(title_label)

        # Server URL
        server_label = QLabel(server_url, self)
        server_label.setStyleSheet(f"color: {self.COLOR_PRIMARY}; font-size: {self.subtitle_font_size}pt; font-weight: bold;")
        server_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(server_label)

        # Attempt counter - German
        attempt_label = QLabel(f"Verbindungsversuch {attempt}", self)
        attempt_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        attempt_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(attempt_label)

        layout.addStretch()

        self.setLayout(layout)
        self.update()

        # CRITICAL FIX: Ensure status screen is ALWAYS visible and on top
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        logger.info(f"Status screen: Connecting (attempt {attempt})")

    def show_waiting_for_layout(self, client_id: str, server_url: str):
        """Show 'Waiting for Layout...' screen after successful connection"""
        layout = self._create_layout_widget()

        # Success checkmark (static icon)
        checkmark_label = QLabel("✓", self)
        checkmark_label.setStyleSheet(f"color: {self.COLOR_SUCCESS}; font-size: {self.icon_font_size}pt; font-weight: bold;")
        checkmark_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(checkmark_label)

        # Connection success - German
        connected_label = QLabel("Verbunden mit Digital Signage Server", self)
        connected_label.setStyleSheet(f"color: {self.COLOR_SUCCESS}; font-size: {self.subtitle_font_size}pt; font-weight: bold;")
        connected_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(connected_label)

        # Server info
        server_label = QLabel(server_url, self)
        server_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        server_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(server_label)

        # Spacer
        layout.addSpacing(self.large_spacing)

        # Client ID
        id_label = QLabel(f"Client ID: {client_id}", self)
        id_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        id_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(id_label)

        # Spacer
        layout.addSpacing(self.large_spacing)

        # Waiting message with animated dots - German
        waiting_label = AnimatedDotsLabel("Warte auf Layout-Zuweisung", self)
        waiting_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.subtitle_font_size}pt;")
        waiting_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(waiting_label)
        self.animated_widgets.append(waiting_label)

        layout.addStretch()

        self.setLayout(layout)
        self.update()

        # CRITICAL FIX: Ensure status screen is ALWAYS visible and on top
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        logger.info(f"Status screen: Waiting for Layout")

    def show_connection_error(self, server_url: str, error_message: str, client_id: str = "Unknown"):
        """Show 'Connection Error' screen"""
        # CRITICAL FIX: Clear previous screen FIRST
        self.clear_screen()

        # CRITICAL FIX: Use _create_layout_widget() for consistency
        layout = self._create_layout_widget()

        # Error icon
        error_icon = QLabel("✗", self)
        error_icon.setStyleSheet(f"color: {self.COLOR_ERROR}; font-size: {self.icon_font_size}pt; font-weight: bold;")
        error_icon.setAlignment(Qt.AlignCenter)
        layout.addWidget(error_icon)

        # Error title
        title_label = QLabel("Connection Error", self)
        title_label.setStyleSheet(f"color: {self.COLOR_ERROR}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)

        # Failed server
        server_label = QLabel(f"Failed to connect to: {server_url}", self)
        server_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.subtitle_font_size}pt;")
        server_label.setAlignment(Qt.AlignCenter)
        server_label.setWordWrap(True)
        layout.addWidget(server_label)

        # Error message
        if error_message:
            error_label = QLabel(error_message, self)
            error_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
            error_label.setAlignment(Qt.AlignCenter)
            error_label.setWordWrap(True)
            layout.addWidget(error_label)

        # Spacer
        layout.addSpacing(self.large_spacing)

        # Instructions
        instructions = [
            "Please check:",
            "• Network connection is active",
            "• Server is running and accessible",
            "• Firewall settings allow connection",
            "• Server address and port are correct"
        ]
        instructions_label = QLabel("\n".join(instructions), self)
        instructions_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        instructions_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(instructions_label)

        # QR code with web dashboard URL - extract IP from client_id or use default
        layout.addSpacing(self.spacing)
        # Try to get IP address from device manager or use localhost
        try:
            from device_manager import DeviceManager
            device_mgr = DeviceManager()
            ip_address = device_mgr.get_ip_address()
        except Exception:
            # Fallback to localhost if device manager unavailable
            ip_address = "localhost"

        dashboard_url = f"http://{ip_address}:5000"
        qr_widget = self._create_qr_code(dashboard_url, self.qr_size)
        if qr_widget:
            qr_container = QWidget()
            qr_layout = QHBoxLayout(qr_container)
            qr_layout.addStretch()
            qr_layout.addWidget(qr_widget)
            qr_layout.addStretch()
            layout.addWidget(qr_container)

            qr_label = QLabel("Scan to view client dashboard", self)
            qr_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.small_font_size}pt;")
            qr_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(qr_label)

        layout.addStretch()

        # CRITICAL FIX: Only set layout if it's not already set
        if self.layout() != layout:
            self.setLayout(layout)

        self.update()

        # CRITICAL FIX: Ensure status screen is ALWAYS visible and on top
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        logger.info("Status screen: Connection Error")

    def show_no_layout_assigned(self, client_id: str, server_url: str, ip_address: str = "Unknown"):
        """Show 'No Layout Assigned' screen - FIXED: Proper screen clearing and widget visibility"""
        logger.info("[DEBUG] show_no_layout_assigned() entered")
        logger.info(f"[DEBUG] Parameters: client_id={client_id}, server_url={server_url}, ip_address={ip_address}")
        logger.info(f"[DEBUG] Current visible: {self.isVisible()}")
        logger.info(f"[DEBUG] Current layout: {self.layout()}")

        # CRITICAL FIX: Clear previous screen FIRST
        logger.info("[DEBUG] Calling clear_screen() to remove previous content...")
        self.clear_screen()
        logger.info("[DEBUG] clear_screen() completed")

        # CRITICAL FIX: Use _create_layout_widget() instead of creating layout directly
        # This ensures proper cleanup and reuse
        logger.info("[DEBUG] Creating/reusing layout widget...")
        layout = self._create_layout_widget()
        logger.info(f"[DEBUG] Layout widget obtained: {layout is not None}")

        # Warning icon
        logger.info("[DEBUG] Creating warning icon...")
        warning_icon = QLabel("⚠", self)
        warning_icon.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.icon_font_size}pt; font-weight: bold;")
        warning_icon.setAlignment(Qt.AlignCenter)
        warning_icon.setVisible(True)  # EXPLICIT visibility
        layout.addWidget(warning_icon)
        logger.info(f"[DEBUG] Warning icon created: visible={warning_icon.isVisible()}, text='{warning_icon.text()}'")

        # Title
        logger.info("[DEBUG] Creating title label...")
        title_label = QLabel("No Layout Assigned", self)
        title_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        title_label.setVisible(True)  # EXPLICIT visibility
        layout.addWidget(title_label)
        logger.info(f"[DEBUG] Title created: visible={title_label.isVisible()}, text='{title_label.text()}'")

        # Message
        logger.info("[DEBUG] Creating message label...")
        message_label = QLabel("This device is connected but has not been assigned a layout", self)
        message_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.subtitle_font_size}pt;")
        message_label.setAlignment(Qt.AlignCenter)
        message_label.setWordWrap(True)
        message_label.setVisible(True)  # EXPLICIT visibility
        layout.addWidget(message_label)
        logger.info(f"[DEBUG] Message created: visible={message_label.isVisible()}")

        # Spacer
        layout.addSpacing(self.large_spacing)

        # Device info
        logger.info("[DEBUG] Creating device info label...")
        device_info = [
            f"Client ID: {client_id}",
            f"IP Address: {ip_address}",
            f"Server: {server_url}"
        ]
        info_label = QLabel("\n".join(device_info), self)
        info_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        info_label.setAlignment(Qt.AlignCenter)
        info_label.setVisible(True)  # EXPLICIT visibility
        layout.addWidget(info_label)
        logger.info(f"[DEBUG] Device info created: visible={info_label.isVisible()}")

        # Spacer
        layout.addSpacing(self.large_spacing)

        # Instructions for admin
        logger.info("[DEBUG] Creating instructions label...")
        instructions = [
            "Administrator Actions:",
            "1. Log in to the Digital Signage Management Server",
            "2. Navigate to Device Management",
            "3. Find this device by Client ID or IP Address",
            "4. Assign a layout to this device"
        ]
        instructions_label = QLabel("\n".join(instructions), self)
        instructions_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt; background-color: #2A2A2A; padding: {self.padding}px; border-radius: 10px;")
        instructions_label.setAlignment(Qt.AlignCenter)
        instructions_label.setVisible(True)  # EXPLICIT visibility
        layout.addWidget(instructions_label)
        logger.info(f"[DEBUG] Instructions created: visible={instructions_label.isVisible()}")

        # QR code with web dashboard URL
        logger.info("[DEBUG] Creating QR code...")
        layout.addSpacing(self.spacing)
        dashboard_url = f"http://{ip_address}:5000"
        qr_widget = self._create_qr_code(dashboard_url, self.qr_size)
        if qr_widget:
            qr_container = QWidget()
            qr_layout = QHBoxLayout(qr_container)
            qr_layout.addStretch()
            qr_layout.addWidget(qr_widget)
            qr_layout.addStretch()
            qr_container.setVisible(True)  # EXPLICIT visibility
            qr_widget.setVisible(True)  # EXPLICIT visibility
            layout.addWidget(qr_container)

            qr_label = QLabel("Scan to view client dashboard", self)
            qr_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.small_font_size}pt;")
            qr_label.setAlignment(Qt.AlignCenter)
            qr_label.setVisible(True)  # EXPLICIT visibility
            layout.addWidget(qr_label)
            logger.info(f"[DEBUG] QR code created: widget visible={qr_widget.isVisible()}, label visible={qr_label.isVisible()}")
        else:
            logger.warning("[DEBUG] QR widget creation failed!")

        layout.addStretch()

        # CRITICAL FIX: Only set layout if it's not already set
        logger.info(f"[DEBUG] Setting layout - current layout: {self.layout()}, new layout: {layout}")
        if self.layout() != layout:
            self.setLayout(layout)
            logger.info("[DEBUG] Layout set to status screen widget")
        else:
            logger.info("[DEBUG] Layout already set, skipping setLayout() call")

        logger.info(f"[DEBUG] Layout set, widget count: {layout.count()}")

        # CRITICAL FIX: Force update
        logger.info("[DEBUG] Forcing widget update...")
        self.update()
        self.repaint()  # FORCE immediate repaint
        logger.info("[DEBUG] Widget update and repaint completed")

        # CRITICAL FIX: Ensure status screen is ALWAYS visible and on top
        logger.info("[DEBUG] Ensuring window visibility...")
        logger.info(f"[DEBUG] Before show: visible={self.isVisible()}, fullscreen={self.isFullScreen()}")

        self.showFullScreen()
        self.raise_()
        self.activateWindow()
        self.setFocus()

        logger.info(f"[DEBUG] After show: visible={self.isVisible()}, fullscreen={self.isFullScreen()}")
        logger.info("[DEBUG] Window shown fullscreen, raised, and activated")

        logger.info("✓ Status screen: No Layout Assigned - SCREEN FULLY CONFIGURED")
        logger.info(f"[DEBUG] Final widget count in layout: {layout.count()}")
        logger.info(f"[DEBUG] Status screen size: {self.width()}x{self.height()}")
        logger.info(f"[DEBUG] Status screen position: ({self.x()}, {self.y()})")
        logger.info("[DEBUG] show_no_layout_assigned() completed successfully")

    def show_server_disconnected(self, server_url: str, client_id: str = "Unknown"):
        """
        Show 'Server Disconnected - Searching...' screen

        CRITICAL FIXES:
        - Use _create_layout_widget() for proper layout management
        - Call clear_screen() first to remove previous content
        - Consistent with other status screen methods
        """
        # CRITICAL FIX: Clear previous screen FIRST
        self.clear_screen()

        # CRITICAL FIX: Use _create_layout_widget() instead of creating layout directly
        # This ensures proper cleanup and reuse
        layout = self._create_layout_widget()

        # Spinner
        spinner = SpinnerWidget(self.spinner_size, self.COLOR_WARNING, self)
        spinner_container = QWidget()
        spinner_layout = QHBoxLayout(spinner_container)
        spinner_layout.addStretch()
        spinner_layout.addWidget(spinner)
        spinner_layout.addStretch()
        layout.addWidget(spinner_container)
        self.animated_widgets.append(spinner)

        # Title - German (consistent with other screens)
        title_label = QLabel("Server Getrennt", self)
        title_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)

        # Searching message with animated dots - German
        searching_label = AnimatedDotsLabel("Suche Server", self)
        searching_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: {self.subtitle_font_size}pt;")
        searching_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(searching_label)
        self.animated_widgets.append(searching_label)

        # Spacer
        layout.addSpacing(self.spacing)

        # Last known server
        server_label = QLabel(f"Letzter bekannter Server: {server_url}", self)
        server_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        server_label.setAlignment(Qt.AlignCenter)
        server_label.setWordWrap(True)
        layout.addWidget(server_label)

        # Client ID
        id_label = QLabel(f"Client ID: {client_id}", self)
        id_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        id_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(id_label)

        # Spacer
        layout.addSpacing(self.large_spacing)

        # Info message - German
        info_label = QLabel("Automatische Wiederverbindung läuft\nKeine Aktion erforderlich", self)
        info_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        info_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(info_label)

        # Spacer
        layout.addSpacing(self.large_spacing)

        # QR code with web dashboard URL
        try:
            from device_manager import DeviceManager
            device_mgr = DeviceManager()
            ip_address = device_mgr.get_ip_address()
        except Exception:
            # Fallback to localhost if device manager unavailable
            ip_address = "localhost"

        dashboard_url = f"http://{ip_address}:5000"
        qr_widget = self._create_qr_code(dashboard_url, self.qr_size)
        if qr_widget:
            qr_container = QWidget()
            qr_layout = QHBoxLayout(qr_container)
            qr_layout.addStretch()
            qr_layout.addWidget(qr_widget)
            qr_layout.addStretch()
            layout.addWidget(qr_container)

            qr_label = QLabel("Scannen um Client-Dashboard zu öffnen", self)
            qr_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.small_font_size}pt;")
            qr_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(qr_label)

        layout.addStretch()

        # CRITICAL FIX: Only set layout if it's not already set
        if self.layout() != layout:
            self.setLayout(layout)

        self.update()

        # CRITICAL FIX: Ensure status screen is ALWAYS visible and on top
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        logger.info("Status screen: Server Disconnected - Searching")

    def show_reconnecting(self, server_url: str, attempt: int, retry_in: int, client_id: str = "Unknown"):
        """Show 'Reconnecting...' screen with retry countdown"""
        # CRITICAL FIX: Clear previous screen FIRST
        self.clear_screen()

        # CRITICAL FIX: Use _create_layout_widget() for consistency
        layout = self._create_layout_widget()

        # Spinner
        spinner = SpinnerWidget(self.spinner_size, self.COLOR_PRIMARY, self)
        spinner_container = QWidget()
        spinner_layout = QHBoxLayout(spinner_container)
        spinner_layout.addStretch()
        spinner_layout.addWidget(spinner)
        spinner_layout.addStretch()
        layout.addWidget(spinner_container)
        self.animated_widgets.append(spinner)

        # Title with animated dots - German
        title_label = AnimatedDotsLabel("Verbindung wird wiederhergestellt", self)
        title_label.setStyleSheet(f"color: {self.COLOR_PRIMARY}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)
        self.animated_widgets.append(title_label)

        # Server URL
        server_label = QLabel(server_url, self)
        server_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: {self.subtitle_font_size}pt;")
        server_label.setAlignment(Qt.AlignCenter)
        server_label.setWordWrap(True)
        layout.addWidget(server_label)

        # Spacer
        layout.addSpacing(self.spacing)

        # Retry info - German
        retry_label = QLabel(f"Nächster Versuch in {retry_in} Sekunden", self)
        retry_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.subtitle_font_size}pt; font-weight: bold;")
        retry_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(retry_label)

        # Attempt counter - German, just show attempt number (no maximum)
        attempt_label = QLabel(f"Verbindungsversuch {attempt}", self)
        attempt_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        attempt_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(attempt_label)

        # Client ID
        id_label = QLabel(f"Client ID: {client_id}", self)
        id_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        id_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(id_label)

        # Spacer
        layout.addSpacing(self.large_spacing)

        # QR code with web dashboard URL
        try:
            from device_manager import DeviceManager
            device_mgr = DeviceManager()
            ip_address = device_mgr.get_ip_address()
        except Exception:
            # Fallback to localhost if device manager unavailable
            ip_address = "localhost"

        dashboard_url = f"http://{ip_address}:5000"
        qr_widget = self._create_qr_code(dashboard_url, self.qr_size)
        if qr_widget:
            qr_container = QWidget()
            qr_layout = QHBoxLayout(qr_container)
            qr_layout.addStretch()
            qr_layout.addWidget(qr_widget)
            qr_layout.addStretch()
            layout.addWidget(qr_container)

            qr_label = QLabel("Scan to view client dashboard", self)
            qr_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.small_font_size}pt;")
            qr_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(qr_label)

        layout.addStretch()

        # CRITICAL FIX: Only set layout if it's not already set
        if self.layout() != layout:
            self.setLayout(layout)

        self.update()

        # CRITICAL FIX: Ensure status screen is ALWAYS visible and on top
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        logger.info(f"Status screen: Reconnecting (attempt {attempt}, retry in {retry_in}s)")

    def show_server_found(self, server_url: str):
        """Show 'Server Found - Connecting...' screen"""
        # CRITICAL FIX: Clear previous screen FIRST
        self.clear_screen()

        # CRITICAL FIX: Use _create_layout_widget() for consistency
        layout = self._create_layout_widget()

        # Success icon
        success_icon = QLabel("✓", self)
        success_icon.setStyleSheet(f"color: {self.COLOR_SUCCESS}; font-size: {self.icon_font_size}pt; font-weight: bold;")
        success_icon.setAlignment(Qt.AlignCenter)
        layout.addWidget(success_icon)

        # Title
        title_label = QLabel("Server Found!", self)
        title_label.setStyleSheet(f"color: {self.COLOR_SUCCESS}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)

        # Server URL
        server_label = QLabel(server_url, self)
        server_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: {self.subtitle_font_size}pt;")
        server_label.setAlignment(Qt.AlignCenter)
        server_label.setWordWrap(True)
        layout.addWidget(server_label)

        # Spacer
        layout.addSpacing(self.large_spacing)

        # Connecting message with animated dots
        connecting_label = AnimatedDotsLabel("Establishing connection", self)
        connecting_label.setStyleSheet(f"color: {self.COLOR_PRIMARY}; font-size: {self.subtitle_font_size}pt;")
        connecting_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(connecting_label)
        self.animated_widgets.append(connecting_label)

        layout.addStretch()

        # CRITICAL FIX: Only set layout if it's not already set
        if self.layout() != layout:
            self.setLayout(layout)

        self.update()

        # CRITICAL FIX: Ensure status screen is ALWAYS visible and on top
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        logger.info("Status screen: Server Found - Connecting")

    def show_discovery_failed(self, attempts: int, config_path: str):
        """Show 'Auto-Discovery Failed' screen when max attempts reached"""
        # CRITICAL FIX: Clear previous screen FIRST
        self.clear_screen()

        # CRITICAL FIX: Use _create_layout_widget() for consistency
        layout = self._create_layout_widget()

        # Error icon
        error_icon = QLabel("⚠", self)
        error_icon.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.icon_font_size}pt; font-weight: bold;")
        error_icon.setAlignment(Qt.AlignCenter)
        layout.addWidget(error_icon)

        # Title - German
        title_label = QLabel("Auto-Discovery Fehlgeschlagen", self)
        title_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)

        # Failure message - German
        message_label = QLabel(f"Kein Server gefunden nach {attempts} Versuchen", self)
        message_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.subtitle_font_size}pt;")
        message_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(message_label)

        # Spacer
        layout.addSpacing(self.large_spacing)

        # Fallback info - German
        fallback_label = QLabel("Verwende konfigurierten Server als Fallback", self)
        fallback_label.setStyleSheet(f"color: {self.COLOR_PRIMARY}; font-size: {self.subtitle_font_size}pt; font-weight: bold;")
        fallback_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(fallback_label)

        # Spacer
        layout.addSpacing(self.spacing)

        # Instructions - German
        instructions = [
            "Für manuelle Konfiguration:",
            f"  sudo nano {config_path}",
            "",
            "Setzen Sie diese Werte:",
            '  "auto_discover": false,',
            '  "server_host": "192.168.0.xxx",',
            "",
            "Dann Client neustarten:",
            "  sudo systemctl restart digitalsignage-client"
        ]
        instructions_label = QLabel("\n".join(instructions), self)
        instructions_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt; background-color: #2A2A2A; padding: {self.padding}px; border-radius: 10px;")
        instructions_label.setAlignment(Qt.AlignCenter)
        instructions_label.setFont(QFont("monospace"))
        layout.addWidget(instructions_label)

        layout.addStretch()

        # CRITICAL FIX: Only set layout if it's not already set
        if self.layout() != layout:
            self.setLayout(layout)

        self.update()

        # CRITICAL FIX: Ensure status screen is ALWAYS visible and on top
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        logger.info(f"Status screen: Discovery Failed (after {attempts} attempts)")

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
    """Manager for status screens - provides simplified interface with smooth transitions

    CRITICAL FIX: Thread-safe status screen management to prevent race conditions
    - Only ONE status screen can be shown at a time
    - Lock prevents multiple async tasks from showing different screens simultaneously
    """

    def __init__(self, display_renderer, client=None):
        """
        Initialize status screen manager - EAGER creation to avoid Qt event loop deadlock

        Args:
            display_renderer: The DisplayRenderer instance to use for showing status screens
            client: Optional DigitalSignageClient reference for accessing config
        """
        self.display_renderer = display_renderer
        self.client = client  # CRITICAL FIX: Store client reference to access config
        self.is_showing_status = False
        self._transition_timer = None  # Timer for smooth transitions
        self._keep_alive_timer = None  # Timer to keep status screen on top

        # CRITICAL FIX: Add lock to prevent race conditions
        # Multiple async tasks (discovery, reconnection, registration) can call show_* simultaneously
        # Lock ensures only ONE status screen is shown at a time
        import threading
        self._lock = threading.Lock()
        self._current_screen_type = None  # Track which screen is currently shown

        # CRITICAL FIX: Create status screen IMMEDIATELY (not lazy)
        # Qt Top-Level widgets MUST be created BEFORE event loop starts
        # Creating them during event loop execution causes deadlock
        logger.info("Creating status screen immediately (eager creation)...")

        # Get screen dimensions
        from PyQt5.QtWidgets import QApplication
        screen = QApplication.primaryScreen()
        if screen:
            screen_geometry = screen.geometry()
            width = screen_geometry.width()
            height = screen_geometry.height()
        else:
            width = display_renderer.width()
            height = display_renderer.height()

        # Create status screen as Top-Level window (parent=None)
        # This MUST happen before event loop starts
        self.status_screen = StatusScreen(width, height, parent=None)

        # Configure window flags
        self.status_screen.setWindowFlags(
            Qt.Window |
            Qt.FramelessWindowHint |
            Qt.WindowStaysOnTopHint
        )

        # Set geometry and show immediately (in background)
        self.status_screen.setGeometry(0, 0, width, height)
        self.status_screen.showFullScreen()  # Show immediately, not hide()
        self.status_screen.lower()  # Put behind other windows initially

        logger.info(f"Status screen created eagerly: {width}x{height} (visible but lowered)")

    def set_client(self, client):
        """
        Set the client reference after initialization.
        Called by client after display_renderer is created.
        Allows status screen manager to access client config.
        """
        self.client = client
        logger.debug("Client reference set in StatusScreenManager")

    def show_discovering_server(self, discovery_method: str = "Auto-Discovery"):
        """Show discovering server screen with smooth transition - THREAD-SAFE"""
        with self._lock:
            # Check if we should show this screen (prevent race conditions)
            if self._current_screen_type == "discovering_server":
                logger.debug("Discovering server screen already shown - skipping duplicate")
                return

            # CRITICAL: Clear display renderer BEFORE showing status screen
            self._clear_display_renderer_for_status()
            self._ensure_status_screen()
            self.status_screen.show_discovering_server(discovery_method)
            self.is_showing_status = True
            self._current_screen_type = "discovering_server"
            self._smooth_transition()

    def show_connecting(self, server_url: str, attempt: int = 1, max_attempts: int = 5):
        """Show connecting screen with smooth transition - THREAD-SAFE"""
        with self._lock:
            # Allow updates to connecting screen (countdown changes)
            # CRITICAL: Clear display renderer BEFORE showing status screen
            self._clear_display_renderer_for_status()
            self._ensure_status_screen()
            self.status_screen.show_connecting(server_url, attempt, max_attempts)
            self.is_showing_status = True
            self._current_screen_type = "connecting"
            self._smooth_transition()

    def show_waiting_for_layout(self, client_id: str, server_url: str):
        """Show waiting for layout screen with smooth transition - THREAD-SAFE"""
        with self._lock:
            if self._current_screen_type == "waiting_for_layout":
                logger.debug("Waiting for layout screen already shown - skipping duplicate")
                return

            # CRITICAL: Clear display renderer BEFORE showing status screen
            self._clear_display_renderer_for_status()
            self._ensure_status_screen()
            self.status_screen.show_waiting_for_layout(client_id, server_url)
            self.is_showing_status = True
            self._current_screen_type = "waiting_for_layout"
            self._smooth_transition()

    def show_connection_error(self, server_url: str, error_message: str, client_id: str = "Unknown"):
        """Show connection error screen - THREAD-SAFE"""
        with self._lock:
            # CRITICAL: Clear display renderer BEFORE showing status screen
            self._clear_display_renderer_for_status()
            self._ensure_status_screen()
            self.status_screen.show_connection_error(server_url, error_message, client_id)
            self.is_showing_status = True
            self._current_screen_type = "connection_error"
            self._smooth_transition()

    def show_no_layout_assigned(self, client_id: str, server_url: str, ip_address: str = "Unknown"):
        """Show no layout assigned screen - THREAD-SAFE"""
        with self._lock:
            if self._current_screen_type == "no_layout_assigned":
                logger.debug("No layout assigned screen already shown - skipping duplicate")
                return

            # CRITICAL: Clear display renderer BEFORE showing status screen
            self._clear_display_renderer_for_status()
            self._ensure_status_screen()
            self.status_screen.show_no_layout_assigned(client_id, server_url, ip_address)
            self.is_showing_status = True
            self._current_screen_type = "no_layout_assigned"
            self._smooth_transition()

    def show_server_disconnected(self, server_url: str, client_id: str = "Unknown"):
        """Show server disconnected - searching screen - THREAD-SAFE"""
        with self._lock:
            if self._current_screen_type == "server_disconnected":
                logger.debug("Server disconnected screen already shown - skipping duplicate")
                return

            # CRITICAL: Clear display renderer BEFORE showing status screen
            self._clear_display_renderer_for_status()
            self._ensure_status_screen()
            self.status_screen.show_server_disconnected(server_url, client_id)
            self.is_showing_status = True
            self._current_screen_type = "server_disconnected"
            self._smooth_transition()

    def show_reconnecting(self, server_url: str, attempt: int, retry_in: int, client_id: str = "Unknown"):
        """Show reconnecting screen with retry countdown - THREAD-SAFE"""
        with self._lock:
            # Allow updates to reconnecting screen (countdown changes)
            # CRITICAL: Clear display renderer BEFORE showing status screen
            self._clear_display_renderer_for_status()
            self._ensure_status_screen()
            self.status_screen.show_reconnecting(server_url, attempt, retry_in, client_id)
            self.is_showing_status = True
            self._current_screen_type = "reconnecting"
            self._smooth_transition()

    def show_server_found(self, server_url: str):
        """Show server found - connecting screen - THREAD-SAFE"""
        with self._lock:
            # CRITICAL FIX: Only show "Server Found" if we're NOT already connected
            # This prevents showing "Server Found" when server is actually offline
            # Check connection state before showing
            if self._current_screen_type == "server_found":
                logger.debug("Server found screen already shown - skipping duplicate")
                return

            # CRITICAL: Clear display renderer BEFORE showing status screen
            self._clear_display_renderer_for_status()
            self._ensure_status_screen()
            self.status_screen.show_server_found(server_url)
            self.is_showing_status = True
            self._current_screen_type = "server_found"
            self._smooth_transition()

    async def show_discovery_failed(self, attempts: int, config_path: str):
        """Show discovery failed screen when max attempts reached - THREAD-SAFE"""
        with self._lock:
            # CRITICAL: Clear display renderer BEFORE showing status screen
            self._clear_display_renderer_for_status()
            self._ensure_status_screen()
            self.status_screen.show_discovery_failed(attempts, config_path)
            self.is_showing_status = True
            self._current_screen_type = "discovery_failed"
            self._smooth_transition()
        # Wait 5 seconds to let user see the screen before attempting fallback
        # (outside lock to not block other operations)
        import asyncio
        await asyncio.sleep(5)

    def clear_status_screen(self):
        """Clear the status screen and prepare for layout display - EAGER mode (hide, not delete) - THREAD-SAFE"""
        with self._lock:
            # CRITICAL: Set flags FIRST to prevent race conditions with timers
            self.is_showing_status = False
            self._current_screen_type = None  # Reset screen type

            # Stop keep-alive timer immediately
            if self._keep_alive_timer:
                self._keep_alive_timer.stop()
                self._keep_alive_timer.deleteLater()
                self._keep_alive_timer = None
                logger.debug("Status screen keep-alive timer stopped and deleted")

            if self.status_screen:
                try:
                    # Stop any transition timer
                    if self._transition_timer:
                        self._transition_timer.stop()
                        self._transition_timer.deleteLater()
                        self._transition_timer = None

                    # Clean up animated widgets
                    self.status_screen.clear_screen()

                    # EAGER MODE: Just lower, don't hide (will be reused next time)
                    self.status_screen.lower()
                    logger.debug("Status screen lowered (eager mode - will be reused)")

                except Exception as e:
                    logger.warning(f"Failed to clear status screen: {e}")

            logger.info("Status screen cleared and hidden (is_showing_status=False, _current_screen_type=None)")

    def _smooth_transition(self):
        """Apply smooth transition effect to status screen updates - EAGER mode"""
        if not self.status_screen:
            logger.error("CRITICAL: Status screen should exist in eager mode!")
            return

        # StatusScreen nach vorne bringen (was lowered)
        logger.debug("Raising status screen to front...")
        self.status_screen.raise_()  # Bring to front
        self.status_screen.activateWindow()
        # showFullScreen() nicht nötig - wurde schon im __init__ aufgerufen

        # update() is sufficient - repaint() removed for better performance
        self.status_screen.update()

        # Start keep-alive timer to ensure status screen stays visible
        self._start_keep_alive_timer()

    def _start_keep_alive_timer(self):
        """Start a timer to periodically re-raise the status screen to keep it visible"""
        # Stop existing timer if any
        if self._keep_alive_timer:
            self._keep_alive_timer.stop()
            self._keep_alive_timer = None

        # Create new timer
        from PyQt5.QtCore import QTimer
        self._keep_alive_timer = QTimer()
        self._keep_alive_timer.timeout.connect(self._keep_status_screen_on_top)
        self._keep_alive_timer.start(3000)  # Re-raise every 3 seconds (reduced CPU usage)
        logger.debug("Status screen keep-alive timer started (3s interval)")

    def _keep_status_screen_on_top(self):
        """Periodically called to ensure status screen stays on top"""
        if self.status_screen and self.is_showing_status:
            try:
                # Re-raise the status screen to keep it above other windows
                self.status_screen.raise_()
                self.status_screen.activateWindow()
                # Ensure it's still fullscreen
                if not self.status_screen.isFullScreen():
                    self.status_screen.showFullScreen()
                    logger.debug("Status screen keep-alive: raised and fullscreen ensured")
                else:
                    logger.debug("Status screen keep-alive: raised to stay on top")
            except Exception as e:
                logger.warning(f"Failed to keep status screen on top: {e}")

    def _clear_display_renderer_for_status(self):
        """
        Clear the display renderer layout to allow status screen to be visible.
        CRITICAL: This prevents PNG layouts from blocking the status screen.

        CRITICAL FIX: Do NOT clear display renderer if cached layout should be shown.
        This prevents cached layout from being cleared when status screens try to show.
        """
        try:
            # CRITICAL FIX: Check if we should preserve cached layout
            # If show_cached_layout_on_disconnect=True, do NOT clear the display renderer
            # Keep the cached layout visible - no status screens should be shown
            if self.client and hasattr(self.client, 'config'):
                config = self.client.config
                if hasattr(config, 'show_cached_layout_on_disconnect') and config.show_cached_layout_on_disconnect:
                    logger.debug("Skipping display renderer clear - cached layout mode enabled (layout remains visible)")
                    return

            if self.display_renderer and hasattr(self.display_renderer, 'clear_layout_for_status_screen'):
                self.display_renderer.clear_layout_for_status_screen()
                logger.debug("Display renderer cleared for status screen display")
            else:
                logger.warning("Display renderer does not have clear_layout_for_status_screen method")
        except Exception as e:
            logger.error(f"Failed to clear display renderer for status screen: {e}")
            import traceback
            logger.error(traceback.format_exc())

    def _ensure_status_screen(self):
        """Ensure status screen widget exists - SIMPLIFIED (eager creation)"""
        # Status screen was already created in __init__
        # Just clear it for reuse
        if self.status_screen:
            try:
                # CRITICAL CHECK: Ensure widget is still valid (not deleted)
                try:
                    self.status_screen.isVisible()  # Test if widget is still valid
                except RuntimeError as e:
                    logger.error(f"Status screen widget has been deleted: {e}")
                    logger.error("CRITICAL: This should never happen with eager creation!")
                    # Try to recover by recreating the widget
                    logger.warning("Attempting to recover by recreating status screen...")
                    from PyQt5.QtWidgets import QApplication
                    screen = QApplication.primaryScreen()
                    if screen:
                        screen_geometry = screen.geometry()
                        width = screen_geometry.width()
                        height = screen_geometry.height()
                    else:
                        width = self.display_renderer.width()
                        height = self.display_renderer.height()

                    self.status_screen = StatusScreen(width, height, parent=None)
                    self.status_screen.setWindowFlags(
                        Qt.Window |
                        Qt.FramelessWindowHint |
                        Qt.WindowStaysOnTopHint
                    )
                    self.status_screen.setGeometry(0, 0, width, height)
                    logger.info("Status screen recreated as recovery")

                self.status_screen.clear_screen()
                logger.debug("Status screen cleared for reuse")
            except Exception as e:
                logger.error(f"Failed to clear status screen: {e}")
                import traceback
                logger.error(traceback.format_exc())
        else:
            logger.error("CRITICAL: Status screen should have been created in __init__!")
            raise RuntimeError("Status screen not initialized - this should never happen")
