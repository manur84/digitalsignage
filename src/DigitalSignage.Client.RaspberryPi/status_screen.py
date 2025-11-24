"""
Status Screen Renderer for Digital Signage Client
Displays visual feedback while waiting for layouts or during connection states

REDESIGNED ARCHITECTURE (2025):
- EXACTLY 4 screens with proper state machine
- Thread-safe state transitions with locks
- QR codes on all screens with relevant information
- Professional, consistent design across all screens
- NO race conditions - atomic state changes only
"""

import logging
from typing import Optional, Dict, Any
from io import BytesIO
from datetime import datetime
import threading

from PyQt5.QtWidgets import QWidget, QLabel, QVBoxLayout, QHBoxLayout
from PyQt5.QtCore import Qt
from PyQt5.QtGui import QPixmap, QFont, QColor, QPainter
import qrcode

# Import custom widgets
from widgets import ScreenState, AnimatedDotsLabel, SpinnerWidget

logger = logging.getLogger(__name__)


class StatusScreen(QWidget):
    """Main status screen widget - displays one of 4 possible states"""

    # Color scheme
    COLOR_BACKGROUND = "#1a1a2e"
    COLOR_PRIMARY = "#4A90E2"
    COLOR_SUCCESS = "#5CB85C"
    COLOR_WARNING = "#F0AD4E"
    COLOR_ERROR = "#D9534F"
    COLOR_TEXT_PRIMARY = "#FFFFFF"
    COLOR_TEXT_SECONDARY = "#B0B0B0"

    def __init__(self, width: int = 1920, height: int = 1080, parent=None):
        super().__init__(parent)
        self.screen_width = width
        self.screen_height = height
        self.animated_widgets = []
        self.current_layout = None

        self.setObjectName("status_screen")
        self.setFixedSize(width, height)
        self.setAttribute(Qt.WA_StyledBackground, True)
        self.setAttribute(Qt.WA_OpaquePaintEvent, True)
        self.setAutoFillBackground(True)
        self.setStyleSheet(f"background-color: {self.COLOR_BACKGROUND};")
        palette = self.palette()
        palette.setColor(self.backgroundRole(), QColor(self.COLOR_BACKGROUND))
        self.setPalette(palette)

        # Hide cursor
        self.setCursor(Qt.BlankCursor)

        # Calculate scaled dimensions
        self._calculate_scaled_dimensions()

    def paintEvent(self, event):
        """Ensure the background is always painted"""
        painter = QPainter(self)
        painter.fillRect(self.rect(), QColor(self.COLOR_BACKGROUND))
        super().paintEvent(event)

    def _calculate_scaled_dimensions(self):
        """Calculate responsive dimensions based on screen resolution"""
        self.title_font_size = int(self.screen_height * 0.05)
        self.subtitle_font_size = int(self.screen_height * 0.035)
        self.body_font_size = int(self.screen_height * 0.025)
        self.small_font_size = int(self.screen_height * 0.018)
        self.icon_font_size = int(self.screen_height * 0.12)

        min_dimension = min(self.screen_width, self.screen_height)
        self.qr_size = int(min_dimension * 0.18)

        self.spinner_size = int(self.screen_height * 0.10)
        self.spacing = int(self.screen_height * 0.02)
        self.large_spacing = int(self.screen_height * 0.035)
        self.padding = int(self.screen_height * 0.015)

    def clear_screen(self):
        """Clear all widgets from the screen"""
        for widget in self.animated_widgets:
            if hasattr(widget, 'cleanup'):
                try:
                    widget.cleanup()
                except Exception as e:
                    logger.warning(f"Failed to cleanup animated widget: {e}")

        self.animated_widgets.clear()

        if self.current_layout:
            try:
                for i in reversed(range(self.current_layout.count())):
                    item = self.current_layout.takeAt(i)
                    if item.widget():
                        item.widget().deleteLater()
            except Exception as e:
                logger.warning(f"Failed to clear layout: {e}")

    def _create_layout(self):
        """Create a new layout for content"""
        if self.current_layout:
            self.clear_screen()

        layout = QVBoxLayout(self)
        layout.setAlignment(Qt.AlignCenter)
        layout.setSpacing(self.spacing)
        self.current_layout = layout
        return layout

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

    def show_auto_discovery(self, device_info: Dict[str, Any]):
        """
        Screen 1: AUTO DISCOVERY
        Shown during auto-discovery phase (searching for server via mDNS/UDP)
        QR code: Device info + search status

        ANTI-FLICKER: Only recreates screen if device info actually changed
        """
        # ANTI-FLICKER FIX: Check if we need to recreate the screen
        # Only recreate if device info changed (e.g., IP address changed)
        if hasattr(self, '_last_auto_discovery_info'):
            if self._last_auto_discovery_info == device_info:
                logger.debug("Auto-discovery screen already showing with same info - skipping recreation to prevent flicker")
                return

        # Store current info for next call
        self._last_auto_discovery_info = device_info.copy() if device_info else {}

        layout = self._create_layout()

        # Spinner
        spinner = SpinnerWidget(self.spinner_size, self.COLOR_PRIMARY, self)
        spinner_container = QWidget()
        spinner_layout = QHBoxLayout(spinner_container)
        spinner_layout.addStretch()
        spinner_layout.addWidget(spinner)
        spinner_layout.addStretch()
        layout.addWidget(spinner_container)
        self.animated_widgets.append(spinner)

        # Title with animated dots
        title_label = AnimatedDotsLabel("Suche Digital Signage Server", self)
        title_label.setStyleSheet(f"color: {self.COLOR_PRIMARY}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)
        self.animated_widgets.append(title_label)

        # Subtitle
        subtitle_label = QLabel("Auto-Discovery Aktiv (mDNS + UDP Broadcast)", self)
        subtitle_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: {self.subtitle_font_size}pt;")
        subtitle_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(subtitle_label)

        layout.addSpacing(self.large_spacing)

        # Device info
        device_info_text = [
            f"Gerät: {device_info.get('Hostname', 'Unknown')}",
            f"IP-Adresse: {device_info.get('IpAddress', 'Unknown')}",
            f"MAC-Adresse: {device_info.get('MacAddress', 'Unknown')}"
        ]
        info_label = QLabel("\n".join(device_info_text), self)
        info_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        info_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(info_label)

        layout.addSpacing(self.large_spacing)

        # QR Code with device info as JSON
        try:
            import json
            qr_data = json.dumps({
                "hostname": device_info.get('Hostname', 'Unknown'),
                "ip": device_info.get('IpAddress', 'Unknown'),
                "mac": device_info.get('MacAddress', 'Unknown'),
                "status": "discovering"
            }, indent=2)

            qr_widget = self._create_qr_code(qr_data, self.qr_size)
            if qr_widget:
                layout.addWidget(qr_widget)
                qr_label = QLabel("Geräte-Informationen (QR-Code scannen)", self)
                qr_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.small_font_size}pt;")
                qr_label.setAlignment(Qt.AlignCenter)
                layout.addWidget(qr_label)
        except Exception as e:
            logger.warning(f"Failed to create QR code: {e}")

        layout.addStretch()

        self.setLayout(layout)

        # ANTI-FLICKER: Disable updates during layout setup
        self.setUpdatesEnabled(False)
        self.setLayout(layout)
        self.setUpdatesEnabled(True)

        # ANTI-FLICKER: Single update at the end
        self.update()
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        logger.info("STATUS SCREEN: Auto Discovery")

    def show_connecting(self, server_url: str, attempt: int, device_info: Dict[str, Any]):
        """
        Screen 2: CONNECTING
        Shown when connection is being established (after discovery OR with manual server)
        QR code: Server URL being connected to + device info
        """
        layout = self._create_layout()

        # Spinner
        spinner = SpinnerWidget(self.spinner_size, self.COLOR_PRIMARY, self)
        spinner_container = QWidget()
        spinner_layout = QHBoxLayout(spinner_container)
        spinner_layout.addStretch()
        spinner_layout.addWidget(spinner)
        spinner_layout.addStretch()
        layout.addWidget(spinner_container)
        self.animated_widgets.append(spinner)

        # Title with animated dots
        title_label = AnimatedDotsLabel("Verbindung wird hergestellt", self)
        title_label.setStyleSheet(f"color: {self.COLOR_PRIMARY}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)
        self.animated_widgets.append(title_label)

        # Server URL
        server_label = QLabel(f"Server: {server_url}", self)
        server_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: {self.subtitle_font_size}pt;")
        server_label.setAlignment(Qt.AlignCenter)
        server_label.setWordWrap(True)
        layout.addWidget(server_label)

        # Attempt counter
        attempt_label = QLabel(f"Verbindungsversuch {attempt}", self)
        attempt_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        attempt_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(attempt_label)

        layout.addSpacing(self.large_spacing)

        # Device info
        device_info_text = [
            f"Gerät: {device_info.get('Hostname', 'Unknown')}",
            f"IP-Adresse: {device_info.get('IpAddress', 'Unknown')}"
        ]
        info_label = QLabel("\n".join(device_info_text), self)
        info_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        info_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(info_label)

        layout.addSpacing(self.large_spacing)

        # QR Code with connection info
        try:
            import json
            qr_data = json.dumps({
                "server": server_url,
                "hostname": device_info.get('Hostname', 'Unknown'),
                "ip": device_info.get('IpAddress', 'Unknown'),
                "status": "connecting",
                "attempt": attempt
            }, indent=2)

            qr_widget = self._create_qr_code(qr_data, self.qr_size)
            if qr_widget:
                layout.addWidget(qr_widget)
                qr_label = QLabel("Verbindungsinformationen (QR-Code scannen)", self)
                qr_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.small_font_size}pt;")
                qr_label.setAlignment(Qt.AlignCenter)
                layout.addWidget(qr_label)
        except Exception as e:
            logger.warning(f"Failed to create QR code: {e}")

        layout.addStretch()

        self.setLayout(layout)
        self.update()
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        logger.info(f"STATUS SCREEN: Connecting (attempt {attempt})")

    def show_no_layout_assigned(self, client_id: str, server_url: str, device_info: Dict[str, Any]):
        """
        Screen 3: NO LAYOUT ASSIGNED
        Shown when successfully connected to server but no layout is assigned
        QR code: Server URL + device ID + IP for admin to assign layout
        """
        layout = self._create_layout()

        # Warning icon
        warning_icon = QLabel("⚠", self)
        warning_icon.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.icon_font_size}pt; font-weight: bold;")
        warning_icon.setAlignment(Qt.AlignCenter)
        layout.addWidget(warning_icon)

        # Title
        title_label = QLabel("Kein Layout zugewiesen", self)
        title_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)

        # Message
        message_label = QLabel("Dieses Gerät ist verbunden, aber es wurde noch kein Layout zugewiesen", self)
        message_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.subtitle_font_size}pt;")
        message_label.setAlignment(Qt.AlignCenter)
        message_label.setWordWrap(True)
        layout.addWidget(message_label)

        layout.addSpacing(self.large_spacing)

        # Device info
        device_info_text = [
            f"Client-ID: {client_id}",
            f"Hostname: {device_info.get('Hostname', 'Unknown')}",
            f"IP-Adresse: {device_info.get('IpAddress', 'Unknown')}",
            f"Server: {server_url}"
        ]
        info_label = QLabel("\n".join(device_info_text), self)
        info_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        info_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(info_label)

        layout.addSpacing(self.large_spacing)

        # Instructions
        instructions = [
            "Administrator-Anweisungen:",
            "1. Am Digital Signage Management Server anmelden",
            "2. Zu Geräteverwaltung navigieren",
            "3. Dieses Gerät anhand der Client-ID oder IP-Adresse finden",
            "4. Ein Layout diesem Gerät zuweisen"
        ]
        instructions_label = QLabel("\n".join(instructions), self)
        instructions_label.setStyleSheet(
            f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt; "
            f"background-color: #2A2A2A; padding: {self.padding}px; border-radius: 10px;"
        )
        instructions_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(instructions_label)

        layout.addSpacing(self.large_spacing)

        # QR Code with device assignment info
        try:
            import json
            qr_data = json.dumps({
                "client_id": client_id,
                "hostname": device_info.get('Hostname', 'Unknown'),
                "ip": device_info.get('IpAddress', 'Unknown'),
                "server": server_url,
                "status": "no_layout_assigned",
                "action": "Assign layout to this device"
            }, indent=2)

            qr_widget = self._create_qr_code(qr_data, self.qr_size)
            if qr_widget:
                layout.addWidget(qr_widget)
                qr_label = QLabel("Geräteinformationen für Layout-Zuweisung (QR-Code scannen)", self)
                qr_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.small_font_size}pt;")
                qr_label.setAlignment(Qt.AlignCenter)
                layout.addWidget(qr_label)
        except Exception as e:
            logger.warning(f"Failed to create QR code: {e}")

        layout.addStretch()

        self.setLayout(layout)
        self.update()
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        logger.info("STATUS SCREEN: No Layout Assigned")

    def show_default_status(self):
        """Show default connection status when no specific state is active"""
        try:
            layout = self._create_layout()

            # Spinner
            spinner = SpinnerWidget(self.spinner_size, self.COLOR_PRIMARY, self)
            spinner_container = QWidget()
            spinner_layout = QHBoxLayout(spinner_container)
            spinner_layout.addStretch()
            spinner_layout.addWidget(spinner)
            spinner_layout.addStretch()
            layout.addWidget(spinner_container)
            self.animated_widgets.append(spinner)

            # Title with animated dots
            title_label = AnimatedDotsLabel("Verbindung wird hergestellt", self)
            title_label.setStyleSheet(f"color: {self.COLOR_PRIMARY}; font-size: {self.title_font_size}pt; font-weight: bold;")
            title_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(title_label)
            self.animated_widgets.append(title_label)

            layout.addSpacing(self.large_spacing)

            # Logo (if available)
            import os
            logo_path = os.path.join(os.path.dirname(__file__), 'digisign-logo.png')
            if os.path.exists(logo_path):
                logo_label = QLabel(self)
                pixmap = QPixmap(logo_path)
                # Scale to 30% of screen size
                scaled = pixmap.scaled(
                    int(self.screen_width * 0.3),
                    int(self.screen_height * 0.3),
                    Qt.KeepAspectRatio,
                    Qt.SmoothTransformation
                )
                logo_label.setPixmap(scaled)
                logo_label.setAlignment(Qt.AlignCenter)
                layout.addWidget(logo_label)

            layout.addStretch()

            self.setLayout(layout)
            self.update()
            self.showFullScreen()
            self.raise_()
            self.activateWindow()

            logger.info("STATUS SCREEN: Default Connection Status")
        except Exception as e:
            logger.error(f"Error showing default status: {e}", exc_info=True)

    def show_connection_failed(self, error_message: str):
        """Show connection failed status with error details"""
        try:
            layout = self._create_layout()

            # Error icon
            error_icon = QLabel("✗", self)
            error_icon.setStyleSheet(f"color: {self.COLOR_ERROR}; font-size: {self.icon_font_size}pt; font-weight: bold;")
            error_icon.setAlignment(Qt.AlignCenter)
            layout.addWidget(error_icon)

            # Title
            title_label = QLabel("Verbindung fehlgeschlagen", self)
            title_label.setStyleSheet(f"color: {self.COLOR_ERROR}; font-size: {self.title_font_size}pt; font-weight: bold;")
            title_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(title_label)

            layout.addSpacing(self.spacing)

            # Error message
            error_label = QLabel(error_message, self)
            error_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.subtitle_font_size}pt;")
            error_label.setAlignment(Qt.AlignCenter)
            error_label.setWordWrap(True)
            layout.addWidget(error_label)

            layout.addSpacing(self.large_spacing)

            # Retry message
            retry_label = QLabel("Automatische Wiederverbindung läuft...", self)
            retry_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: {self.body_font_size}pt;")
            retry_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(retry_label)

            layout.addStretch()

            self.setLayout(layout)
            self.update()
            self.showFullScreen()
            self.raise_()
            self.activateWindow()

            logger.info(f"STATUS SCREEN: Connection Failed - {error_message}")
        except Exception as e:
            logger.error(f"Error showing connection failed status: {e}", exc_info=True)

    def show_reconnecting(self, attempt: int, max_attempts: int, retry_in: int):
        """Show reconnecting status with attempt counter and countdown"""
        try:
            layout = self._create_layout()

            # Spinner (orange for reconnecting)
            spinner = SpinnerWidget(self.spinner_size, self.COLOR_WARNING, self)
            spinner_container = QWidget()
            spinner_layout = QHBoxLayout(spinner_container)
            spinner_layout.addStretch()
            spinner_layout.addWidget(spinner)
            spinner_layout.addStretch()
            layout.addWidget(spinner_container)
            self.animated_widgets.append(spinner)

            # Title
            title_label = AnimatedDotsLabel("Erneuter Verbindungsversuch", self)
            title_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.title_font_size}pt; font-weight: bold;")
            title_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(title_label)
            self.animated_widgets.append(title_label)

            layout.addSpacing(self.spacing)

            # Attempt counter
            attempt_label = QLabel(f"Versuch {attempt} von {max_attempts}", self)
            attempt_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: {self.subtitle_font_size}pt;")
            attempt_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(attempt_label)

            # Countdown
            if retry_in > 0:
                countdown_label = QLabel(f"Nächster Versuch in {retry_in} Sekunden", self)
                countdown_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.subtitle_font_size}pt; font-weight: bold;")
                countdown_label.setAlignment(Qt.AlignCenter)
                layout.addWidget(countdown_label)

            layout.addStretch()

            self.setLayout(layout)
            self.update()
            self.showFullScreen()
            self.raise_()
            self.activateWindow()

            logger.info(f"STATUS SCREEN: Reconnecting (attempt {attempt}/{max_attempts}, retry in {retry_in}s)")
        except Exception as e:
            logger.error(f"Error showing reconnecting status: {e}", exc_info=True)

    def show_server_offline(self, server_url: str, retry_info: Dict[str, Any], device_info: Dict[str, Any], auto_discovery_active: bool):
        """
        Screen 4: SERVER OFFLINE
        Shown when server is disconnected/unreachable
        QR code: Last known server URL + retry info + auto-discovery status
        """
        layout = self._create_layout()

        # Spinner (orange for warning)
        spinner = SpinnerWidget(self.spinner_size, self.COLOR_WARNING, self)
        spinner_container = QWidget()
        spinner_layout = QHBoxLayout(spinner_container)
        spinner_layout.addStretch()
        spinner_layout.addWidget(spinner)
        spinner_layout.addStretch()
        layout.addWidget(spinner_container)
        self.animated_widgets.append(spinner)

        # Title
        title_label = QLabel("Server Offline", self)
        title_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.title_font_size}pt; font-weight: bold;")
        title_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(title_label)

        # Searching/reconnecting message with animated dots
        if auto_discovery_active:
            reconnect_label = AnimatedDotsLabel("Suche Server im Netzwerk", self)
        else:
            reconnect_label = AnimatedDotsLabel("Verbindung wird wiederhergestellt", self)

        reconnect_label.setStyleSheet(f"color: {self.COLOR_TEXT_PRIMARY}; font-size: {self.subtitle_font_size}pt;")
        reconnect_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(reconnect_label)
        self.animated_widgets.append(reconnect_label)

        layout.addSpacing(self.spacing)

        # Last known server
        server_label = QLabel(f"Letzter bekannter Server: {server_url}", self)
        server_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        server_label.setAlignment(Qt.AlignCenter)
        server_label.setWordWrap(True)
        layout.addWidget(server_label)

        # Retry info
        attempt = retry_info.get('attempt', 0)
        retry_in = retry_info.get('retry_in', 0)

        if retry_in > 0:
            retry_label = QLabel(f"Nächster Versuch in {retry_in} Sekunden", self)
            retry_label.setStyleSheet(f"color: {self.COLOR_WARNING}; font-size: {self.subtitle_font_size}pt; font-weight: bold;")
            retry_label.setAlignment(Qt.AlignCenter)
            layout.addWidget(retry_label)

        attempt_label = QLabel(f"Verbindungsversuch {attempt}", self)
        attempt_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        attempt_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(attempt_label)

        layout.addSpacing(self.large_spacing)

        # Auto-discovery status
        if auto_discovery_active:
            discovery_label = QLabel("✓ Auto-Discovery Aktiv", self)
            discovery_label.setStyleSheet(f"color: {self.COLOR_SUCCESS}; font-size: {self.body_font_size}pt;")
        else:
            discovery_label = QLabel("Auto-Discovery Deaktiviert", self)
            discovery_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        discovery_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(discovery_label)

        # Device info
        device_info_text = [
            f"Gerät: {device_info.get('Hostname', 'Unknown')}",
            f"IP-Adresse: {device_info.get('IpAddress', 'Unknown')}"
        ]
        info_label = QLabel("\n".join(device_info_text), self)
        info_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        info_label.setAlignment(Qt.AlignCenter)
        layout.addWidget(info_label)

        layout.addSpacing(self.spacing)

        # Info message
        info_message = QLabel("Automatische Wiederverbindung läuft\nKeine Aktion erforderlich", self)
        info_message.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.body_font_size}pt;")
        info_message.setAlignment(Qt.AlignCenter)
        layout.addWidget(info_message)

        layout.addSpacing(self.large_spacing)

        # QR Code with reconnection info
        try:
            import json
            qr_data = json.dumps({
                "server": server_url,
                "hostname": device_info.get('Hostname', 'Unknown'),
                "ip": device_info.get('IpAddress', 'Unknown'),
                "status": "server_offline",
                "attempt": attempt,
                "retry_in_seconds": retry_in,
                "auto_discovery": auto_discovery_active
            }, indent=2)

            qr_widget = self._create_qr_code(qr_data, self.qr_size)
            if qr_widget:
                layout.addWidget(qr_widget)
                qr_label = QLabel("Wiederverbindungsinformationen (QR-Code scannen)", self)
                qr_label.setStyleSheet(f"color: {self.COLOR_TEXT_SECONDARY}; font-size: {self.small_font_size}pt;")
                qr_label.setAlignment(Qt.AlignCenter)
                layout.addWidget(qr_label)
        except Exception as e:
            logger.warning(f"Failed to create QR code: {e}")

        layout.addStretch()

        self.setLayout(layout)
        self.update()
        self.showFullScreen()
        self.raise_()
        self.activateWindow()

        logger.info(f"STATUS SCREEN: Server Offline (attempt {attempt}, retry in {retry_in}s, auto-discovery: {auto_discovery_active})")


class StatusScreenManager:
    """
    Manager for status screens with proper state machine

    REDESIGNED ARCHITECTURE (2025):
    - Exactly 4 screen states (AUTO_DISCOVERY, CONNECTING, NO_LAYOUT_ASSIGNED, SERVER_OFFLINE)
    - Thread-safe state transitions with lock
    - Single entry point per state with all required parameters
    - Atomic state changes to prevent race conditions
    """

    def __init__(self, display_renderer, client=None):
        """Initialize status screen manager"""
        self.display_renderer = display_renderer
        self.client = client

        # State management with lock for thread safety
        self._state_lock = threading.Lock()
        self._current_state = ScreenState.NONE

        # Keep-alive timer
        self._keep_alive_timer = None

        # Create status screen immediately (eager creation)
        logger.info("Creating status screen immediately (eager creation)...")

        from PyQt5.QtWidgets import QApplication
        screen = QApplication.primaryScreen()
        if screen:
            screen_geometry = screen.geometry()
            width = screen_geometry.width()
            height = screen_geometry.height()
        else:
            width = display_renderer.width()
            height = display_renderer.height()

        self.status_screen = StatusScreen(width, height, parent=None)

        self.status_screen.setWindowFlags(
            Qt.Window |
            Qt.FramelessWindowHint |
            Qt.WindowStaysOnTopHint
        )

        self.status_screen.setGeometry(0, 0, width, height)
        self.status_screen.showFullScreen()
        self.status_screen.lower()  # Put behind initially

        logger.info(f"Status screen created eagerly: {width}x{height}")

    def set_client(self, client):
        """Set the client reference after initialization"""
        self.client = client
        logger.debug("Client reference set in StatusScreenManager")

    @property
    def is_showing_status(self) -> bool:
        """Check if any status screen is currently shown"""
        with self._state_lock:
            return self._current_state != ScreenState.NONE

    def _get_device_info(self) -> Dict[str, Any]:
        """Get device info from client (thread-safe)"""
        try:
            if self.client and hasattr(self.client, 'device_manager'):
                import asyncio
                loop = asyncio.get_event_loop()
                if loop.is_running():
                    # Can't await in sync context - use cached info
                    return {
                        'Hostname': 'Unknown',
                        'IpAddress': self.client.device_manager.get_ip_address() if hasattr(self.client.device_manager, 'get_ip_address') else 'Unknown',
                        'MacAddress': 'Unknown'
                    }
                else:
                    # Can use async
                    return asyncio.run(self.client.device_manager.get_device_info())
            return {'Hostname': 'Unknown', 'IpAddress': 'Unknown', 'MacAddress': 'Unknown'}
        except Exception as e:
            logger.warning(f"Failed to get device info: {e}")
            return {'Hostname': 'Unknown', 'IpAddress': 'Unknown', 'MacAddress': 'Unknown'}

    def show_auto_discovery(self):
        """Show auto-discovery screen (Screen 1)

        ANTI-FLICKER: Checks state and device info before recreation
        """
        with self._state_lock:
            device_info = self._get_device_info()

            # ANTI-FLICKER FIX: Check if already showing with same data
            if self._current_state == ScreenState.AUTO_DISCOVERY:
                # Already showing auto-discovery - check if device info changed
                if hasattr(self, '_last_device_info') and self._last_device_info == device_info:
                    logger.debug("Already showing auto-discovery screen with same device info - skipping recreation to prevent flicker")
                    return
                else:
                    logger.debug("Auto-discovery screen active but device info changed - will update")

            logger.info("STATE TRANSITION: %s -> AUTO_DISCOVERY", self._current_state.value)
            self._current_state = ScreenState.AUTO_DISCOVERY
            self._last_device_info = device_info.copy() if device_info else {}

            self._clear_display_renderer()
            self.status_screen.show_auto_discovery(device_info)
            self._start_keep_alive_timer()

    def show_connecting(self, server_url: str, attempt: int = 1):
        """Show connecting screen (Screen 2)"""
        with self._state_lock:
            # Allow updates for attempt counter
            logger.info("STATE TRANSITION: %s -> CONNECTING (attempt %d)", self._current_state.value, attempt)
            self._current_state = ScreenState.CONNECTING

            self._clear_display_renderer()
            device_info = self._get_device_info()
            self.status_screen.show_connecting(server_url, attempt, device_info)
            self._start_keep_alive_timer()

    def show_no_layout_assigned(self, client_id: str, server_url: str):
        """Show no layout assigned screen (Screen 3)"""
        with self._state_lock:
            if self._current_state == ScreenState.NO_LAYOUT_ASSIGNED:
                logger.debug("Already showing no layout assigned screen - skipping")
                return

            logger.info("STATE TRANSITION: %s -> NO_LAYOUT_ASSIGNED", self._current_state.value)
            self._current_state = ScreenState.NO_LAYOUT_ASSIGNED

            self._clear_display_renderer()
            device_info = self._get_device_info()
            self.status_screen.show_no_layout_assigned(client_id, server_url, device_info)
            self._start_keep_alive_timer()

    def show_server_offline(self, server_url: str, attempt: int = 0, retry_in: int = 0, auto_discovery_active: bool = False):
        """Show server offline screen (Screen 4)"""
        with self._state_lock:
            # Allow updates for countdown
            logger.info("STATE TRANSITION: %s -> SERVER_OFFLINE (attempt %d, retry %ds)", self._current_state.value, attempt, retry_in)
            self._current_state = ScreenState.SERVER_OFFLINE

            self._clear_display_renderer()
            device_info = self._get_device_info()
            retry_info = {'attempt': attempt, 'retry_in': retry_in}
            self.status_screen.show_server_offline(server_url, retry_info, device_info, auto_discovery_active)
            self._start_keep_alive_timer()

    def show_default_status(self):
        """Show default connection status (fallback when state is unclear)"""
        with self._state_lock:
            logger.info("STATE TRANSITION: %s -> DEFAULT_STATUS", self._current_state.value)
            self._current_state = ScreenState.CONNECTING  # Treat as connecting state

            self._clear_display_renderer()
            self.status_screen.show_default_status()
            self._start_keep_alive_timer()

    def show_connection_failed(self, error_message: str):
        """Show connection failed screen with error details"""
        with self._state_lock:
            logger.info("STATE TRANSITION: %s -> CONNECTION_FAILED", self._current_state.value)
            self._current_state = ScreenState.SERVER_OFFLINE  # Treat as server offline

            self._clear_display_renderer()
            self.status_screen.show_connection_failed(error_message)
            self._start_keep_alive_timer()

    def show_reconnecting(self, attempt: int, max_attempts: int, retry_in: int):
        """Show reconnecting screen with progress"""
        with self._state_lock:
            logger.info("STATE TRANSITION: %s -> RECONNECTING (attempt %d/%d)", self._current_state.value, attempt, max_attempts)
            self._current_state = ScreenState.SERVER_OFFLINE  # Treat as server offline

            self._clear_display_renderer()
            self.status_screen.show_reconnecting(attempt, max_attempts, retry_in)
            self._start_keep_alive_timer()

    def clear_status_screen(self):
        """Clear the status screen and prepare for layout display"""
        with self._state_lock:
            if self._current_state == ScreenState.NONE:
                logger.debug("No status screen to clear")
                return

            logger.info("STATE TRANSITION: %s -> NONE (clearing)", self._current_state.value)
            self._current_state = ScreenState.NONE

            # Stop keep-alive timer
            if self._keep_alive_timer:
                self._keep_alive_timer.stop()
                self._keep_alive_timer.deleteLater()
                self._keep_alive_timer = None

            if self.status_screen:
                try:
                    self.status_screen.clear_screen()
                    self.status_screen.lower()
                    logger.debug("Status screen cleared and lowered")
                except Exception as e:
                    logger.warning(f"Failed to clear status screen: {e}")

    def _clear_display_renderer(self):
        """Clear the display renderer to allow status screen to be visible"""
        try:
            # Check if we should preserve cached layout
            if self.client and hasattr(self.client, 'config'):
                config = self.client.config
                if hasattr(config, 'show_cached_layout_on_disconnect') and config.show_cached_layout_on_disconnect:
                    logger.debug("Skipping display renderer clear - cached layout mode enabled")
                    return

            if self.display_renderer and hasattr(self.display_renderer, 'clear_layout_for_status_screen'):
                self.display_renderer.clear_layout_for_status_screen()
                logger.debug("Display renderer cleared for status screen")
        except Exception as e:
            logger.error(f"Failed to clear display renderer: {e}")

    def _start_keep_alive_timer(self):
        """Start a timer to periodically re-raise the status screen"""
        if self._keep_alive_timer:
            self._keep_alive_timer.stop()
            self._keep_alive_timer = None

        from PyQt5.QtCore import QTimer
        self._keep_alive_timer = QTimer()
        self._keep_alive_timer.timeout.connect(self._keep_status_screen_on_top)
        self._keep_alive_timer.start(3000)  # Every 3 seconds
        logger.debug("Status screen keep-alive timer started")

    def _keep_status_screen_on_top(self):
        """Periodically ensure status screen stays on top

        ANTI-FLICKER: Only performs minimal operations to keep window on top
        Avoids unnecessary showFullScreen() calls that cause redraws
        """
        with self._state_lock:
            if self._current_state != ScreenState.NONE and self.status_screen:
                try:
                    # ANTI-FLICKER FIX: Only raise/activate if not already on top
                    # Check if window is already active before making unnecessary calls
                    if not self.status_screen.isActiveWindow():
                        self.status_screen.raise_()
                        self.status_screen.activateWindow()
                        logger.debug("Status screen raised to stay on top")

                    # ANTI-FLICKER FIX: Only call showFullScreen() if actually not fullscreen
                    # This prevents constant redraws from redundant showFullScreen() calls
                    if not self.status_screen.isFullScreen():
                        self.status_screen.showFullScreen()
                        logger.debug("Status screen set to fullscreen")
                except Exception as e:
                    logger.warning(f"Failed to keep status screen on top: {e}")
