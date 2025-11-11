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

logger = logging.getLogger(__name__)


class DisplayRenderer(QWidget):
    """Renders display layouts using Qt"""

    def __init__(self, fullscreen: bool = True):
        super().__init__()
        self.fullscreen = fullscreen
        self.elements: list[QWidget] = []
        self.setup_ui()

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
        logger.info(f"Rendering layout: {layout.get('Name')}")

        # Clear existing elements
        for element in self.elements:
            element.deleteLater()
        self.elements.clear()

        # Set background
        bg_color = layout.get('BackgroundColor', '#FFFFFF')
        self.setStyleSheet(f"background-color: {bg_color};")

        # Render elements
        elements = layout.get('Elements', [])
        for element_data in elements:
            element = self.create_element(element_data, data)
            if element:
                self.elements.append(element)
                element.show()

        self.update()

    def create_element(self, element_data: Dict[str, Any], data: Optional[Dict[str, Any]]) -> Optional[QWidget]:
        """Create a UI element from element data"""
        element_type = element_data.get('Type')
        position = element_data.get('Position', {})
        size = element_data.get('Size', {})
        properties = element_data.get('Properties', {})

        x = int(position.get('X', 0))
        y = int(position.get('Y', 0))
        width = int(size.get('Width', 100))
        height = int(size.get('Height', 100))

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

    def create_text_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any],
        data: Optional[Dict[str, Any]]
    ) -> QLabel:
        """Create a text element"""
        label = QLabel(self)
        label.setGeometry(x, y, width, height)

        # Get text content
        content = properties.get('Content', '')
        if data:
            # Replace variables in content
            content = self.replace_variables(content, data)

        label.setText(content)

        # Set font
        font = QFont(
            properties.get('FontFamily', 'Arial'),
            properties.get('FontSize', 16)
        )
        font_weight = properties.get('FontWeight', 'normal')
        if font_weight == 'bold':
            font.setBold(True)

        font_style = properties.get('FontStyle', 'normal')
        if font_style == 'italic':
            font.setItalic(True)

        label.setFont(font)

        # Set color
        color = properties.get('Color', '#000000')
        label.setStyleSheet(f"color: {color};")

        # Set alignment
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

        # Word wrap
        word_wrap = properties.get('WordWrap', True)
        label.setWordWrap(word_wrap)

        return label

    def create_image_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any]
    ) -> QLabel:
        """Create an image element"""
        label = QLabel(self)
        label.setGeometry(x, y, width, height)

        source = properties.get('Source', '')
        if source:
            pixmap = QPixmap(source)
            fit = properties.get('Fit', 'contain')

            if fit == 'contain':
                pixmap = pixmap.scaled(width, height, Qt.KeepAspectRatio, Qt.SmoothTransformation)
            elif fit == 'cover':
                pixmap = pixmap.scaled(width, height, Qt.KeepAspectRatioByExpanding, Qt.SmoothTransformation)
            elif fit == 'fill':
                pixmap = pixmap.scaled(width, height, Qt.IgnoreAspectRatio, Qt.SmoothTransformation)

            label.setPixmap(pixmap)

        return label

    def create_shape_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any]
    ) -> QWidget:
        """Create a shape element"""
        widget = QWidget(self)
        widget.setGeometry(x, y, width, height)

        fill_color = properties.get('FillColor', '#CCCCCC')
        stroke_color = properties.get('StrokeColor', '#000000')
        stroke_width = properties.get('StrokeWidth', 1)

        widget.setStyleSheet(f"""
            background-color: {fill_color};
            border: {stroke_width}px solid {stroke_color};
        """)

        return widget

    def create_qrcode_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any],
        data: Optional[Dict[str, Any]]
    ) -> QLabel:
        """Create a QR code element"""
        label = QLabel(self)
        label.setGeometry(x, y, width, height)

        qr_data = properties.get('Data', '')
        if data:
            qr_data = self.replace_variables(qr_data, data)

        if qr_data:
            qr = qrcode.QRCode(
                version=1,
                error_correction=qrcode.constants.ERROR_CORRECT_M,
                box_size=10,
                border=4,
            )
            qr.add_data(qr_data)
            qr.make(fit=True)

            img = qr.make_image(
                fill_color=properties.get('ForegroundColor', '#000000'),
                back_color=properties.get('BackgroundColor', '#FFFFFF')
            )

            # Convert PIL image to QPixmap
            buffer = BytesIO()
            img.save(buffer, format='PNG')
            buffer.seek(0)

            pixmap = QPixmap()
            pixmap.loadFromData(buffer.read())
            pixmap = pixmap.scaled(width, height, Qt.KeepAspectRatio, Qt.SmoothTransformation)

            label.setPixmap(pixmap)
            label.setAlignment(Qt.AlignCenter)

        return label

    def replace_variables(self, text: str, data: Dict[str, Any]) -> str:
        """Replace {{variable.name}} placeholders with actual data"""
        import re

        def replace_func(match):
            var_path = match.group(1)
            parts = var_path.split('.')

            value = data
            for part in parts:
                if isinstance(value, dict):
                    value = value.get(part, '')
                else:
                    value = ''
                    break

            return str(value)

        return re.sub(r'\{\{([^}]+)\}\}', replace_func, text)

    async def take_screenshot(self) -> bytes:
        """Take screenshot of the current display"""
        pixmap = self.grab()

        buffer = BytesIO()
        image = pixmap.toImage()

        # Convert to bytes
        byte_array = image.bits().asstring(image.sizeInBytes())

        return byte_array
