"""
Display renderer using PyQt5 for rendering layouts
"""

import logging
from typing import Dict, Any, Optional
from io import BytesIO

from PyQt5.QtWidgets import QWidget, QLabel
from PyQt5.QtCore import Qt, QRect
from PyQt5.QtGui import QPixmap, QFont, QColor, QPainter, QImage
import qrcode

# Import status screen manager
from status_screen import StatusScreenManager

logger = logging.getLogger(__name__)


class DisplayRenderer(QWidget):
    """Renders display layouts using Qt"""

    def __init__(self, fullscreen: bool = True):
        super().__init__()
        self.fullscreen = fullscreen
        self.elements: list[QWidget] = []
        self.setup_ui()

        # Initialize status screen manager
        self.status_screen_manager = StatusScreenManager(self)

    def setup_ui(self):
        """Setup the UI"""
        self.setWindowTitle("Digital Signage Display")
        self.setStyleSheet("background-color: white;")

        if self.fullscreen:
            self.showFullScreen()
            self.setCursor(Qt.BlankCursor)  # Hide cursor
        else:
            self.resize(1920, 1080)
            self.show()

    async def render_layout(self, layout: Dict[str, Any], data: Optional[Dict[str, Any]] = None):
        """Render a display layout"""
        if not layout:
            logger.error("render_layout called with None layout")
            return

        if not isinstance(layout, dict):
            logger.error(f"render_layout called with invalid layout type: {type(layout)}")
            return

        layout_name = layout.get('Name', 'Unnamed')
        logger.info(f"Rendering layout: {layout_name}")

        # Clear status screen when rendering actual layout
        if self.status_screen_manager.is_showing_status:
            logger.info("Clearing status screen to display layout")
            self.status_screen_manager.clear_status_screen()

        try:
            # Clear existing elements
            for element in self.elements:
                try:
                    element.deleteLater()
                except Exception as e:
                    logger.warning(f"Failed to delete element: {e}")
            self.elements.clear()

            # Set background
            bg_color = layout.get('BackgroundColor', '#FFFFFF')
            try:
                self.setStyleSheet(f"background-color: {bg_color};")
            except Exception as e:
                logger.warning(f"Failed to set background color {bg_color}: {e}")
                self.setStyleSheet("background-color: #FFFFFF;")

            # Render elements
            elements = layout.get('Elements', [])
            if not isinstance(elements, list):
                logger.warning(f"Elements is not a list: {type(elements)}")
                elements = []

            rendered_count = 0
            failed_count = 0

            for element_data in elements:
                try:
                    element = self.create_element(element_data, data)
                    if element:
                        self.elements.append(element)
                        element.show()
                        rendered_count += 1
                    else:
                        failed_count += 1
                except Exception as e:
                    logger.error(f"Failed to create element: {e}")
                    failed_count += 1

            logger.info(f"Layout '{layout_name}' rendered: {rendered_count} elements created, {failed_count} failed")
            self.update()

        except Exception as e:
            logger.error(f"Failed to render layout '{layout_name}': {e}")

    def create_element(self, element_data: Dict[str, Any], data: Optional[Dict[str, Any]]) -> Optional[QWidget]:
        """Create a UI element from element data"""
        if not element_data:
            logger.warning("create_element called with None element_data")
            return None

        if not isinstance(element_data, dict):
            logger.warning(f"create_element called with invalid element_data type: {type(element_data)}")
            return None

        element_type = element_data.get('Type')
        if not element_type:
            logger.warning("Element missing 'Type' property")
            return None

        position = element_data.get('Position', {})
        size = element_data.get('Size', {})
        properties = element_data.get('Properties', {})

        # Validate nested dictionaries
        if not isinstance(position, dict):
            logger.warning(f"Position is not a dict: {type(position)}, using defaults")
            position = {}
        if not isinstance(size, dict):
            logger.warning(f"Size is not a dict: {type(size)}, using defaults")
            size = {}
        if not isinstance(properties, dict):
            logger.warning(f"Properties is not a dict: {type(properties)}, using defaults")
            properties = {}

        # Parse dimensions with error handling
        try:
            x = int(position.get('X', 0))
            y = int(position.get('Y', 0))
            width = int(size.get('Width', 100))
            height = int(size.get('Height', 100))

            # Validate dimensions
            if width <= 0 or height <= 0:
                logger.warning(f"Invalid element dimensions: {width}x{height}, using defaults")
                width = max(width, 100)
                height = max(height, 100)

        except (ValueError, TypeError) as e:
            logger.error(f"Failed to parse element dimensions: {e}, using defaults")
            x, y, width, height = 0, 0, 100, 100

        try:
            if element_type == 'text':
                return self.create_text_element(x, y, width, height, properties, data)
            elif element_type == 'image':
                return self.create_image_element(x, y, width, height, properties)
            elif element_type == 'shape':
                return self.create_shape_element(x, y, width, height, properties)
            elif element_type == 'qrcode':
                return self.create_qrcode_element(x, y, width, height, properties, data)
            else:
                logger.warning(f"Unknown element type: {element_type}")
                return None
        except Exception as e:
            logger.error(f"Failed to create {element_type} element: {e}")
            return None

    def create_text_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any],
        data: Optional[Dict[str, Any]]
    ) -> Optional[QLabel]:
        """Create a text element"""
        try:
            label = QLabel(self)
            label.setGeometry(x, y, width, height)

            # Get text content
            content = properties.get('Content', '')
            if not isinstance(content, str):
                logger.warning(f"Content is not a string: {type(content)}, converting")
                content = str(content)

            if data:
                try:
                    # Replace variables in content
                    content = self.replace_variables(content, data)
                except Exception as e:
                    logger.warning(f"Failed to replace variables in text content: {e}")

            label.setText(content)

            # Set font
            try:
                font_family = properties.get('FontFamily', 'Arial')
                font_size = properties.get('FontSize', 16)

                # Validate font size
                if not isinstance(font_size, (int, float)):
                    logger.warning(f"Invalid font size: {font_size}, using default")
                    font_size = 16

                font = QFont(font_family, int(font_size))

                font_weight = properties.get('FontWeight', 'normal')
                if font_weight == 'bold':
                    font.setBold(True)

                font_style = properties.get('FontStyle', 'normal')
                if font_style == 'italic':
                    font.setItalic(True)

                label.setFont(font)
            except Exception as e:
                logger.warning(f"Failed to set font properties: {e}")

            # Set color
            try:
                color = properties.get('Color', '#000000')
                label.setStyleSheet(f"color: {color};")
            except Exception as e:
                logger.warning(f"Failed to set text color: {e}")

            # Set alignment
            try:
                text_align = properties.get('TextAlign', 'left')
                alignment = Qt.AlignLeft
                if text_align == 'center':
                    alignment = Qt.AlignHCenter
                elif text_align == 'right':
                    alignment = Qt.AlignRight

                vertical_align = properties.get('VerticalAlign', 'top')
                if vertical_align == 'middle':
                    alignment |= Qt.AlignVCenter
                elif vertical_align == 'bottom':
                    alignment |= Qt.AlignBottom
                else:
                    alignment |= Qt.AlignTop

                label.setAlignment(alignment)
            except Exception as e:
                logger.warning(f"Failed to set text alignment: {e}")

            # Word wrap
            try:
                word_wrap = properties.get('WordWrap', True)
                label.setWordWrap(word_wrap)
            except Exception as e:
                logger.warning(f"Failed to set word wrap: {e}")

            return label

        except Exception as e:
            logger.error(f"Failed to create text element: {e}")
            return None

    def create_image_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any]
    ) -> Optional[QLabel]:
        """Create an image element"""
        try:
            label = QLabel(self)
            label.setGeometry(x, y, width, height)

            source = properties.get('Source', '')
            if not source:
                logger.warning("Image element has no source")
                return label

            if not isinstance(source, str):
                logger.warning(f"Image source is not a string: {type(source)}")
                return label

            try:
                import os
                if not os.path.isfile(source):
                    logger.warning(f"Image file not found: {source}")
                    return label

                pixmap = QPixmap(source)
                if pixmap.isNull():
                    logger.error(f"Failed to load image from {source}")
                    return label

                fit = properties.get('Fit', 'contain')

                if fit == 'contain':
                    pixmap = pixmap.scaled(width, height, Qt.KeepAspectRatio, Qt.SmoothTransformation)
                elif fit == 'cover':
                    pixmap = pixmap.scaled(width, height, Qt.KeepAspectRatioByExpanding, Qt.SmoothTransformation)
                elif fit == 'fill':
                    pixmap = pixmap.scaled(width, height, Qt.IgnoreAspectRatio, Qt.SmoothTransformation)
                else:
                    logger.warning(f"Unknown fit mode: {fit}, using contain")
                    pixmap = pixmap.scaled(width, height, Qt.KeepAspectRatio, Qt.SmoothTransformation)

                label.setPixmap(pixmap)
                logger.debug(f"Image loaded successfully: {source}")

            except Exception as e:
                logger.error(f"Failed to load or scale image {source}: {e}")

            return label

        except Exception as e:
            logger.error(f"Failed to create image element: {e}")
            return None

    def create_shape_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any]
    ) -> Optional[QWidget]:
        """Create a shape element"""
        try:
            widget = QWidget(self)
            widget.setGeometry(x, y, width, height)

            fill_color = properties.get('FillColor', '#CCCCCC')
            stroke_color = properties.get('StrokeColor', '#000000')
            stroke_width = properties.get('StrokeWidth', 1)

            # Validate stroke width
            try:
                stroke_width = int(stroke_width)
                if stroke_width < 0:
                    logger.warning(f"Invalid stroke width: {stroke_width}, using 0")
                    stroke_width = 0
            except (ValueError, TypeError) as e:
                logger.warning(f"Failed to parse stroke width: {e}, using default")
                stroke_width = 1

            try:
                widget.setStyleSheet(f"""
                    background-color: {fill_color};
                    border: {stroke_width}px solid {stroke_color};
                """)
            except Exception as e:
                logger.warning(f"Failed to set shape style: {e}")

            return widget

        except Exception as e:
            logger.error(f"Failed to create shape element: {e}")
            return None

    def create_qrcode_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any],
        data: Optional[Dict[str, Any]]
    ) -> Optional[QLabel]:
        """Create a QR code element"""
        try:
            label = QLabel(self)
            label.setGeometry(x, y, width, height)

            qr_data = properties.get('Data', '')
            if not isinstance(qr_data, str):
                logger.warning(f"QR data is not a string: {type(qr_data)}, converting")
                qr_data = str(qr_data)

            if data:
                try:
                    qr_data = self.replace_variables(qr_data, data)
                except Exception as e:
                    logger.warning(f"Failed to replace variables in QR data: {e}")

            if not qr_data:
                logger.warning("QR code element has no data")
                return label

            try:
                qr = qrcode.QRCode(
                    version=1,
                    error_correction=qrcode.constants.ERROR_CORRECT_M,
                    box_size=10,
                    border=4,
                )
                qr.add_data(qr_data)
                qr.make(fit=True)

                foreground_color = properties.get('ForegroundColor', '#000000')
                background_color = properties.get('BackgroundColor', '#FFFFFF')

                img = qr.make_image(
                    fill_color=foreground_color,
                    back_color=background_color
                )

                # Convert PIL image to QPixmap
                buffer = BytesIO()
                try:
                    img.save(buffer, format='PNG')
                    buffer.seek(0)

                    pixmap = QPixmap()
                    if not pixmap.loadFromData(buffer.read()):
                        logger.error("Failed to load QR code image data")
                        return label

                    pixmap = pixmap.scaled(width, height, Qt.KeepAspectRatio, Qt.SmoothTransformation)
                    label.setPixmap(pixmap)
                    label.setAlignment(Qt.AlignCenter)
                    logger.debug(f"QR code created successfully with data: {qr_data[:50]}...")

                finally:
                    buffer.close()

            except Exception as e:
                logger.error(f"Failed to generate QR code: {e}")

            return label

        except Exception as e:
            logger.error(f"Failed to create QR code element: {e}")
            return None

    def replace_variables(self, text: str, data: Dict[str, Any]) -> str:
        """Replace {{variable.name}} placeholders with actual data"""
        if not text:
            return text

        if not isinstance(text, str):
            logger.warning(f"replace_variables called with non-string: {type(text)}")
            return str(text)

        if not data:
            return text

        if not isinstance(data, dict):
            logger.warning(f"replace_variables called with non-dict data: {type(data)}")
            return text

        try:
            import re

            def replace_func(match):
                try:
                    var_path = match.group(1)
                    if not var_path:
                        return match.group(0)

                    parts = var_path.split('.')
                    value = data

                    for part in parts:
                        if isinstance(value, dict):
                            value = value.get(part, '')
                        else:
                            value = ''
                            break

                    return str(value) if value is not None else ''

                except Exception as e:
                    logger.warning(f"Failed to replace variable {match.group(0)}: {e}")
                    return match.group(0)

            return re.sub(r'\{\{([^}]+)\}\}', replace_func, text)

        except Exception as e:
            logger.error(f"Failed to replace variables in text: {e}")
            return text

    async def take_screenshot(self) -> bytes:
        """Take screenshot of the current display"""
        try:
            pixmap = self.grab()
            if pixmap.isNull():
                logger.error("Failed to grab screenshot: pixmap is null")
                return b''

            buffer = BytesIO()
            try:
                image = pixmap.toImage()
                if image.isNull():
                    logger.error("Failed to convert pixmap to image")
                    return b''

                # Save as PNG to buffer
                if not image.save(buffer, 'PNG'):
                    logger.error("Failed to save screenshot to buffer")
                    return b''

                buffer.seek(0)
                byte_array = buffer.read()
                logger.debug(f"Screenshot captured: {len(byte_array)} bytes")
                return byte_array

            finally:
                buffer.close()

        except Exception as e:
            logger.error(f"Failed to take screenshot: {e}")
            return b''
