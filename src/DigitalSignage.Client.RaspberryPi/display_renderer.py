"""
Display renderer using PyQt5 for rendering layouts
"""

import logging
from typing import Dict, Any, Optional
from io import BytesIO
import locale

from PyQt5.QtWidgets import QWidget, QLabel, QGraphicsDropShadowEffect
from PyQt5.QtCore import Qt, QRect, QTimer
from PyQt5.QtGui import QPixmap, QFont, QColor, QPainter, QImage, QPen, QBrush
import qrcode
from datetime import datetime

# Import status screen manager
from status_screen import StatusScreenManager

logger = logging.getLogger(__name__)

# Set German locale for datetime formatting
try:
    locale.setlocale(locale.LC_TIME, 'de_DE.UTF-8')
    logger.info("Locale set to de_DE.UTF-8 for datetime formatting")
except locale.Error:
    try:
        # Fallback to German locale on Windows
        locale.setlocale(locale.LC_TIME, 'German_Germany.1252')
        logger.info("Locale set to German_Germany.1252 for datetime formatting")
    except locale.Error:
        logger.warning("Failed to set German locale, using system default")


class ShapeWidget(QWidget):
    """Custom widget for rendering shapes (circle, ellipse, rectangle with rounded corners)"""

    def __init__(self, parent=None, shape_type='rectangle'):
        super().__init__(parent)
        self.shape_type = shape_type  # 'circle', 'ellipse', 'rectangle'
        self.fill_color = QColor('#CCCCCC')
        self.stroke_color = QColor('#000000')
        self.stroke_width = 1
        self.corner_radius = 0

        # CRITICAL FIX: Configure widget for custom painting
        # Without this, Qt won't properly render custom paintEvent drawings
        self.setAttribute(Qt.WA_OpaquePaintEvent, False)  # Widget handles transparency
        self.setAutoFillBackground(False)  # Don't auto-fill, we paint ourselves

    def set_fill_color(self, color: str):
        """Set fill color from hex string"""
        try:
            self.fill_color = QColor(color)
            self.update()  # Trigger repaint
        except Exception as e:
            logger.warning(f"Invalid fill color {color}: {e}")
            self.fill_color = QColor('#CCCCCC')

    def set_stroke_color(self, color: str):
        """Set stroke color from hex string"""
        try:
            self.stroke_color = QColor(color)
            self.update()  # Trigger repaint
        except Exception as e:
            logger.warning(f"Invalid stroke color {color}: {e}")
            self.stroke_color = QColor('#000000')

    def set_stroke_width(self, width: int):
        """Set stroke width"""
        try:
            self.stroke_width = max(0, int(width))
            self.update()  # Trigger repaint
        except (ValueError, TypeError):
            self.stroke_width = 1

    def set_corner_radius(self, radius: float):
        """Set corner radius for rectangles"""
        try:
            self.corner_radius = max(0, float(radius))
            self.update()  # Trigger repaint
        except (ValueError, TypeError):
            self.corner_radius = 0

    def paintEvent(self, event):
        """Custom paint event to draw shapes"""
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing)

        # Set brush (fill)
        brush = QBrush(self.fill_color)
        painter.setBrush(brush)

        # Set pen (stroke)
        pen = QPen(self.stroke_color)
        pen.setWidth(self.stroke_width)
        painter.setPen(pen)

        # Get widget dimensions
        rect = self.rect()

        # Adjust rect for stroke width to prevent clipping
        if self.stroke_width > 0:
            margin = self.stroke_width // 2
            rect = rect.adjusted(margin, margin, -margin, -margin)

        # Draw based on shape type
        if self.shape_type == 'circle':
            # Circle: use smaller dimension as diameter to ensure perfect circle
            size = min(rect.width(), rect.height())
            # Center the circle
            x = rect.x() + (rect.width() - size) // 2
            y = rect.y() + (rect.height() - size) // 2
            painter.drawEllipse(x, y, size, size)

        elif self.shape_type == 'ellipse':
            # Ellipse: fill the entire rect
            painter.drawEllipse(rect)

        elif self.shape_type == 'rectangle':
            # Rectangle with optional rounded corners
            if self.corner_radius > 0:
                painter.drawRoundedRect(rect, self.corner_radius, self.corner_radius)
            else:
                painter.drawRect(rect)

        painter.end()


class DisplayRenderer(QWidget):
    """Renders display layouts using Qt"""

    def __init__(self, fullscreen: bool = True):
        super().__init__()
        self.fullscreen = fullscreen
        self.elements: list[QWidget] = []
        self.setup_ui()

        # Initialize status screen manager
        self.status_screen_manager = StatusScreenManager(self)

        # Data source cache for datagrid elements
        self.data_source_cache: Dict[str, list] = {}

    def setup_ui(self):
        """Setup the UI"""
        self.setWindowTitle("Digital Signage Display")
        self.setStyleSheet("background-color: white;")

        if self.fullscreen:
            self.showFullScreen()
            self.setCursor(Qt.BlankCursor)  # Hide cursor

            # CRITICAL FIX: Force window to be visible and on top after boot
            # Problem: Window may be created but not visible on HDMI display after reboot
            # Solution: Explicitly raise, activate and ensure window is on top
            self.raise_()
            self.activateWindow()
            self.setWindowState(Qt.WindowFullScreen | Qt.WindowActive)

            # Additional fix: Set window flags to ensure it stays on top initially
            self.setWindowFlags(Qt.Window | Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint)

            logger.info("Display renderer set to fullscreen with window activation")
        else:
            self.resize(1920, 1080)
            self.show()
            self.raise_()
            self.activateWindow()
            logger.info("Display renderer set to windowed mode")

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

        # Calculate scaling factors based on layout resolution vs display resolution
        layout_resolution = layout.get('Resolution', {})
        layout_width = layout_resolution.get('Width', 1920)
        layout_height = layout_resolution.get('Height', 1080)

        # Get actual display resolution
        display_width = self.width()
        display_height = self.height()

        # Calculate scale factors
        scale_x = display_width / layout_width
        scale_y = display_height / layout_height

        logger.info(f"Layout resolution: {layout_width}x{layout_height}, Display resolution: {display_width}x{display_height}")
        logger.info(f"Scale factors: X={scale_x:.3f}, Y={scale_y:.3f}")

        # Store scale factors for element creation
        self._scale_x = scale_x
        self._scale_y = scale_y

        # Clear status screen when rendering actual layout
        if self.status_screen_manager.is_showing_status:
            logger.info("Clearing status screen to display layout")
            self.status_screen_manager.clear_status_screen()

        try:
            # === COMPLETE CLEANUP OF OLD LAYOUT ===

            # 1. Stop and clear ALL timers (datetime elements)
            if hasattr(self, '_datetime_timers'):
                for timer in self._datetime_timers:
                    try:
                        timer.stop()
                        timer.deleteLater()
                    except Exception as e:
                        logger.warning(f"Failed to stop datetime timer: {e}")
                self._datetime_timers.clear()

            # 2. Delete all tracked elements
            for element in self.elements:
                try:
                    # Remove graphics effects (shadows) to free resources
                    if element.graphicsEffect():
                        element.setGraphicsEffect(None)

                    # Hide first to prevent flicker
                    element.hide()

                    # Delete widget
                    element.deleteLater()
                except Exception as e:
                    logger.warning(f"Failed to delete element: {e}")
            self.elements.clear()

            # 3. Find and delete any orphaned child widgets not in self.elements
            # This catches widgets that may have been created but not tracked
            orphaned_widgets = self.findChildren(QWidget)
            for widget in orphaned_widgets:
                # Skip status screen widgets
                if hasattr(widget, 'objectName') and 'status_screen' in widget.objectName():
                    continue
                try:
                    widget.hide()
                    widget.deleteLater()
                except Exception as e:
                    logger.warning(f"Failed to delete orphaned widget: {e}")

            # 4. Reset background to default (clear palette and stylesheet)
            # Reset palette to clear any background images
            from PyQt5.QtGui import QPalette
            palette = QPalette()
            self.setPalette(palette)
            self.setAutoFillBackground(False)

            # 5. Clear stylesheet to default (will be set again below)
            self.setStyleSheet("background-color: white;")

            # 6. Force immediate update to clear display
            self.update()

            logger.debug("Complete layout cleanup finished")

            # Set background (color and/or image)
            bg_color = layout.get('BackgroundColor', '#FFFFFF')
            bg_image = layout.get('BackgroundImage')
            bg_image_data = layout.get('BackgroundImageData')  # Base64 from server

            try:
                style = ""

                # Background color
                if bg_color:
                    style += f"background-color: {bg_color};"

                # Background image - Priority 1: Base64 data from server
                bg_pixmap = None
                if bg_image_data:
                    try:
                        import base64
                        image_bytes = base64.b64decode(bg_image_data)
                        image = QImage()
                        if image.loadFromData(image_bytes):
                            bg_pixmap = QPixmap.fromImage(image)
                            logger.debug(f"Background image loaded from Base64 data")
                        else:
                            logger.error("Failed to load background image from Base64")
                    except Exception as e:
                        logger.error(f"Failed to decode Base64 background image: {e}")

                # Background image - Priority 2: File path (fallback)
                if bg_pixmap is None and bg_image and isinstance(bg_image, str):
                    import os
                    if os.path.isfile(bg_image):
                        # Use absolute path with file:/// protocol for Qt
                        abs_path = os.path.abspath(bg_image)
                        # Qt requires forward slashes on all platforms
                        abs_path = abs_path.replace('\\', '/')
                        style += f"background-image: url(file:///{abs_path});"
                        style += "background-repeat: no-repeat;"
                        style += "background-position: center;"
                        style += "background-size: cover;"  # Cover the entire widget
                        logger.debug(f"Set background image from file: {bg_image}")
                    else:
                        logger.warning(f"Background image file not found: {bg_image}")

                # If we have a pixmap from Base64, set it using palette
                if bg_pixmap and not bg_pixmap.isNull():
                    # Scale to widget size
                    scaled_pixmap = bg_pixmap.scaled(
                        self.width(), self.height(),
                        Qt.KeepAspectRatioByExpanding,
                        Qt.SmoothTransformation
                    )

                    # Set as background using palette
                    from PyQt5.QtGui import QPalette, QBrush
                    palette = self.palette()
                    palette.setBrush(QPalette.Background, QBrush(scaled_pixmap))
                    self.setPalette(palette)
                    self.setAutoFillBackground(True)
                    logger.debug("Background image set from Base64 data")

                if style:
                    self.setStyleSheet(style)
                elif not bg_pixmap:
                    self.setStyleSheet("background-color: #FFFFFF;")

            except Exception as e:
                logger.warning(f"Failed to set background: {e}")
                self.setStyleSheet("background-color: #FFFFFF;")

            # Render elements
            elements = layout.get('Elements', [])
            if not isinstance(elements, list):
                logger.warning(f"Elements is not a list: {type(elements)}")
                elements = []

            # Sort elements by ZIndex to render in correct order (lower ZIndex first = background)
            try:
                elements_sorted = sorted(elements, key=lambda e: e.get('ZIndex', 0) if isinstance(e, dict) else 0)
                logger.debug(f"Sorted {len(elements_sorted)} elements by ZIndex")
            except Exception as e:
                logger.warning(f"Failed to sort elements by ZIndex: {e}, using original order")
                elements_sorted = elements

            rendered_count = 0
            failed_count = 0

            for element_data in elements_sorted:
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

        # Check if element is visible
        visible = element_data.get('Visible', True)
        if not visible:
            # Element is marked as invisible, skip rendering
            logger.debug(f"Skipping invisible element of type {element_type}")
            return None

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

        # Apply scaling factors if they exist
        scale_x = getattr(self, '_scale_x', 1.0)
        scale_y = getattr(self, '_scale_y', 1.0)

        if scale_x != 1.0 or scale_y != 1.0:
            x = int(x * scale_x)
            y = int(y * scale_y)
            width = int(width * scale_x)
            height = int(height * scale_y)
            logger.debug(f"Scaled element: pos=({x},{y}), size=({width}x{height}), scale=({scale_x:.3f},{scale_y:.3f})")

        try:
            if element_type == 'text':
                return self.create_text_element(x, y, width, height, properties, data)
            elif element_type == 'image':
                return self.create_image_element(x, y, width, height, properties)
            elif element_type == 'shape' or element_type == 'rectangle':
                return self.create_shape_element(x, y, width, height, properties, shape_type='rectangle')
            elif element_type == 'circle':
                return self.create_shape_element(x, y, width, height, properties, shape_type='circle')
            elif element_type == 'ellipse':
                return self.create_shape_element(x, y, width, height, properties, shape_type='ellipse')
            elif element_type == 'qrcode':
                return self.create_qrcode_element(x, y, width, height, properties, data)
            elif element_type == 'datetime':
                return self.create_datetime_element(x, y, width, height, properties)
            elif element_type == 'table':
                return self.create_table_element(x, y, width, height, properties, data)
            elif element_type == 'datagrid':
                return self.create_datagrid_element(x, y, width, height, properties)
            elif element_type == 'datasourcetext':
                return self.create_datasourcetext_element(x, y, width, height, properties)
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

                # Apply scaling to font size (use average of both scale factors)
                scale_x = getattr(self, '_scale_x', 1.0)
                scale_y = getattr(self, '_scale_y', 1.0)
                scale_factor = (scale_x + scale_y) / 2.0
                scaled_font_size = int(font_size * scale_factor)

                font = QFont(font_family, scaled_font_size)

                font_weight = properties.get('FontWeight', 'normal')
                if font_weight == 'bold':
                    font.setBold(True)

                font_style = properties.get('FontStyle', 'normal')
                if font_style == 'italic':
                    font.setItalic(True)

                # Text decorations
                underline = properties.get('TextDecoration_Underline', False)
                if underline:
                    font.setUnderline(True)

                strikethrough = properties.get('TextDecoration_Strikethrough', False)
                if strikethrough:
                    font.setStrikeOut(True)

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

            # Apply common styling (border, shadow, opacity, rotation, background)
            self.apply_common_styling(label, properties)

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

            pixmap = None

            # Priority 1: Check for embedded MediaData (Base64 from server)
            media_data = properties.get('MediaData')
            if media_data:
                try:
                    import base64
                    # Decode Base64 to bytes
                    image_bytes = base64.b64decode(media_data)

                    # Load from bytes
                    image = QImage()
                    if image.loadFromData(image_bytes):
                        pixmap = QPixmap.fromImage(image)
                        logger.debug(f"Image loaded from Base64 data ({len(media_data)} chars)")
                    else:
                        logger.error("Failed to load image from Base64 data")
                except Exception as e:
                    logger.error(f"Failed to decode Base64 image data: {e}")

            # Priority 2: Try loading from file path (fallback)
            if pixmap is None:
                source = properties.get('Source', '')
                if source:
                    if not isinstance(source, str):
                        logger.warning(f"Image source is not a string: {type(source)}")
                    else:
                        try:
                            import os
                            if os.path.isfile(source):
                                pixmap = QPixmap(source)
                                if not pixmap.isNull():
                                    logger.debug(f"Image loaded from file: {source}")
                                else:
                                    logger.error(f"Failed to load image from {source}")
                            else:
                                logger.warning(f"Image file not found: {source}")
                        except Exception as e:
                            logger.error(f"Failed to load image from file {source}: {e}")

            # If we have a pixmap, scale it according to fit mode
            if pixmap and not pixmap.isNull():
                try:
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
                    logger.debug("Image scaled and set to label")
                except Exception as e:
                    logger.error(f"Failed to scale image: {e}")
            else:
                logger.warning("No image data available (neither Base64 nor file path)")

            # Apply common styling (border, shadow, opacity, rotation, background)
            self.apply_common_styling(label, properties)

            return label

        except Exception as e:
            logger.error(f"Failed to create image element: {e}")
            return None

    def create_shape_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any],
        shape_type: str = 'rectangle'
    ) -> Optional[QWidget]:
        """Create a shape element (rectangle, circle, or ellipse)"""
        try:
            # Create ShapeWidget with proper shape type
            widget = ShapeWidget(self, shape_type=shape_type)
            widget.setGeometry(x, y, width, height)

            # Get colors and stroke from properties
            fill_color = properties.get('FillColor', '#CCCCCC')
            stroke_color = properties.get('StrokeColor') or properties.get('BorderColor', '#000000')
            stroke_width = properties.get('StrokeWidth') or properties.get('BorderThickness', 1)

            # Apply shape properties
            widget.set_fill_color(fill_color)
            widget.set_stroke_color(stroke_color)
            widget.set_stroke_width(stroke_width)

            # Apply corner radius for rectangles (support both property names)
            if shape_type == 'rectangle':
                corner_radius = properties.get('CornerRadius') or properties.get('BorderRadius', 0)
                try:
                    widget.set_corner_radius(float(corner_radius))
                except (ValueError, TypeError):
                    widget.set_corner_radius(0)

            # Apply common styling (shadow, opacity, rotation)
            # Note: We don't use background-color/border from apply_common_styling
            # since ShapeWidget handles these via paintEvent
            self.apply_common_styling(widget, properties, skip_border=True)

            logger.debug(f"Created {shape_type} shape: fill={fill_color}, stroke={stroke_color}, width={stroke_width}")

            return widget

        except Exception as e:
            logger.error(f"Failed to create {shape_type} shape element: {e}")
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
                # Get error correction level from properties (support both property names)
                error_level_str = properties.get('ErrorCorrectionLevel') or properties.get('ErrorCorrection', 'M')
                if not isinstance(error_level_str, str):
                    error_level_str = str(error_level_str)

                # Map to qrcode constants
                error_level_map = {
                    'L': qrcode.constants.ERROR_CORRECT_L,  # Low (~7% error correction)
                    'M': qrcode.constants.ERROR_CORRECT_M,  # Medium (~15% error correction)
                    'Q': qrcode.constants.ERROR_CORRECT_Q,  # Quartile (~25% error correction)
                    'H': qrcode.constants.ERROR_CORRECT_H,  # High (~30% error correction)
                }
                error_correction = error_level_map.get(error_level_str.upper(), qrcode.constants.ERROR_CORRECT_M)

                qr = qrcode.QRCode(
                    version=1,
                    error_correction=error_correction,
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

                    # QR-Code Alignment support
                    alignment = properties.get('Alignment', 'Center')
                    if alignment == 'Left':
                        label.setAlignment(Qt.AlignLeft | Qt.AlignVCenter)
                    elif alignment == 'Right':
                        label.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
                    else:  # Center
                        label.setAlignment(Qt.AlignCenter)

                    logger.debug(f"QR code created successfully with data: {qr_data[:50]}...")

                finally:
                    buffer.close()

            except Exception as e:
                logger.error(f"Failed to generate QR code: {e}")

            # Apply common styling (border, shadow, opacity, rotation, background)
            self.apply_common_styling(label, properties)

            return label

        except Exception as e:
            logger.error(f"Failed to create QR code element: {e}")
            return None

    def create_datetime_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any]
    ) -> Optional[QLabel]:
        """
        Create a DateTime element with auto-update functionality.
        The element displays current date/time and updates automatically at specified intervals.
        """
        try:
            label = QLabel(self)
            label.setGeometry(x, y, width, height)

            # Get format string (C# DateTime format, needs conversion to Python strftime format)
            format_string = properties.get('Format', '%Y-%m-%d %H:%M:%S')
            if not isinstance(format_string, str):
                logger.warning(f"DateTime format is not a string: {type(format_string)}, using default")
                format_string = '%Y-%m-%d %H:%M:%S'

            # Convert C# DateTime format to Python strftime format if needed
            format_string = self.convert_csharp_format_to_python(format_string)

            # Get update interval (in milliseconds)
            update_interval = properties.get('UpdateInterval', 1000)
            try:
                update_interval = int(update_interval)
                if update_interval < 100:  # Minimum 100ms
                    logger.warning(f"UpdateInterval too small ({update_interval}ms), using 100ms minimum")
                    update_interval = 100
                elif update_interval > 3600000:  # Maximum 1 hour
                    logger.warning(f"UpdateInterval too large ({update_interval}ms), using 1 hour maximum")
                    update_interval = 3600000
            except (ValueError, TypeError) as e:
                logger.warning(f"Invalid UpdateInterval: {e}, using default 1000ms")
                update_interval = 1000

            # Set font properties
            try:
                font_family = properties.get('FontFamily', 'Arial')
                font_size = properties.get('FontSize', 24)

                if not isinstance(font_size, (int, float)):
                    logger.warning(f"Invalid font size: {font_size}, using default")
                    font_size = 24

                # Apply scaling to font size (use average of both scale factors)
                scale_x = getattr(self, '_scale_x', 1.0)
                scale_y = getattr(self, '_scale_y', 1.0)
                scale_factor = (scale_x + scale_y) / 2.0
                scaled_font_size = int(font_size * scale_factor)

                font = QFont(font_family, scaled_font_size)
                font_weight = properties.get('FontWeight', 'normal')
                if font_weight == 'bold':
                    font.setBold(True)

                label.setFont(font)
            except Exception as e:
                logger.warning(f"Failed to set DateTime font properties: {e}")

            # Set color
            try:
                color = properties.get('Color', '#000000')
                label.setStyleSheet(f"color: {color};")
            except Exception as e:
                logger.warning(f"Failed to set DateTime color: {e}")

            # Set alignment (support both TextAlign and TextAlignment for compatibility)
            try:
                text_align = properties.get('TextAlignment') or properties.get('TextAlign', 'center')
                alignment = Qt.AlignCenter | Qt.AlignVCenter

                if text_align.lower() == 'left':
                    alignment = Qt.AlignLeft | Qt.AlignVCenter
                elif text_align.lower() == 'right':
                    alignment = Qt.AlignRight | Qt.AlignVCenter
                elif text_align.lower() == 'justify':
                    alignment = Qt.AlignJustify | Qt.AlignVCenter
                else:  # center
                    alignment = Qt.AlignCenter | Qt.AlignVCenter

                label.setAlignment(alignment)
            except Exception as e:
                logger.warning(f"Failed to set DateTime alignment: {e}")

            # Function to update the datetime display
            def update_datetime():
                try:
                    # Ensure German locale is set for this thread
                    try:
                        locale.setlocale(locale.LC_TIME, 'de_DE.UTF-8')
                    except locale.Error:
                        try:
                            locale.setlocale(locale.LC_TIME, 'German_Germany.1252')
                        except locale.Error:
                            pass  # Use default locale

                    current_time = datetime.now()
                    formatted_time = current_time.strftime(format_string)
                    label.setText(formatted_time)
                except Exception as e:
                    logger.error(f"Failed to update datetime display: {e}")
                    label.setText("ERROR")

            # Initial update
            update_datetime()

            # Create timer for auto-update
            timer = QTimer(self)
            timer.timeout.connect(update_datetime)
            timer.start(update_interval)

            # Store timer reference to prevent garbage collection
            if not hasattr(self, '_datetime_timers'):
                self._datetime_timers = []
            self._datetime_timers.append(timer)

            # Apply common styling (border, shadow, opacity, rotation, background)
            self.apply_common_styling(label, properties)

            logger.debug(f"DateTime element created with format '{format_string}' and update interval {update_interval}ms")

            return label

        except Exception as e:
            logger.error(f"Failed to create DateTime element: {e}")
            return None

    def create_table_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any],
        data: Optional[Dict[str, Any]]
    ) -> Optional[QWidget]:
        """
        Create a Table element with rows and columns.
        Properties expected:
        - Rows: Number of rows (int)
        - Columns: Number of columns (int)
        - CellData: Cell data as JSON string (list of lists)
        - ShowHeaderRow: Whether first row is header (bool)
        - ShowHeaderColumn: Whether first column is header (bool)
        - HeaderBackgroundColor: Header background color (default: #CCCCCC)
        - TextColor: Text color (default: #000000)
        - BackgroundColor: Background color (default: #FFFFFF)
        - AlternateRowColor: Alternate row background color (default: #F5F5F5)
        - FontFamily: Font family (default: Arial)
        - FontSize: Font size (default: 14)
        - BorderColor: Border color (default: #000000)
        - BorderThickness: Border thickness (default: 1)
        - CellPadding: Cell padding (default: 5)
        """
        try:
            import json
            from PyQt5.QtWidgets import QTableWidget, QTableWidgetItem, QHeaderView
            from PyQt5.QtGui import QBrush, QColor
            from PyQt5.QtCore import Qt

            table = QTableWidget(self)
            table.setGeometry(x, y, width, height)

            # Get configuration from properties
            num_rows = properties.get('Rows', 3)
            num_columns = properties.get('Columns', 3)
            show_header_row = properties.get('ShowHeaderRow', True)
            show_header_column = properties.get('ShowHeaderColumn', False)

            # Parse cell data from JSON
            cell_data = []
            cell_data_json = properties.get('CellData', '[]')
            if isinstance(cell_data_json, str) and cell_data_json:
                try:
                    cell_data = json.loads(cell_data_json)
                except json.JSONDecodeError as e:
                    logger.warning(f"Failed to parse CellData JSON: {e}")
                    cell_data = []
            elif isinstance(cell_data_json, list):
                # Already parsed
                cell_data = cell_data_json

            # Validate cell data
            if not isinstance(cell_data, list) or not cell_data:
                logger.warning("Table has no cell data, creating placeholder")
                cell_data = []
                for i in range(num_rows):
                    row = []
                    for j in range(num_columns):
                        if i == 0 and show_header_row:
                            row.append(f"Header {j + 1}")
                        elif j == 0 and show_header_column:
                            row.append(f"Row {i + 1}")
                        else:
                            row.append(f"Cell {i + 1},{j + 1}")
                    cell_data.append(row)

            # Set table dimensions
            table.setRowCount(num_rows)
            table.setColumnCount(num_columns)

            # Configure headers
            if show_header_row:
                # First row is header
                table.horizontalHeader().setVisible(True)
                if cell_data and len(cell_data) > 0:
                    headers = [str(cell) for cell in cell_data[0][:num_columns]]
                    table.setHorizontalHeaderLabels(headers)
            else:
                # No header row
                table.horizontalHeader().setVisible(False)

            # Get style properties
            header_bg = properties.get('HeaderBackgroundColor', '#CCCCCC')
            text_color = properties.get('TextColor', '#000000')
            row_bg = properties.get('BackgroundColor', '#FFFFFF')
            alt_row_bg = properties.get('AlternateRowColor', '#F5F5F5')
            border_color = properties.get('BorderColor', '#000000')
            border_thickness = properties.get('BorderThickness', 1)
            cell_padding = properties.get('CellPadding', 5)
            font_family = properties.get('FontFamily', 'Arial')
            font_size = properties.get('FontSize', 14)

            # Set font
            try:
                if not isinstance(font_size, (int, float)):
                    font_size = 12

                # Apply scaling to font size (use average of both scale factors)
                scale_x = getattr(self, '_scale_x', 1.0)
                scale_y = getattr(self, '_scale_y', 1.0)
                scale_factor = (scale_x + scale_y) / 2.0
                scaled_font_size = int(font_size * scale_factor)

                font = QFont(font_family, scaled_font_size)
                table.setFont(font)
            except Exception as e:
                logger.warning(f"Failed to set table font: {e}")

            # Set table style
            table_style = f"""
                QTableWidget {{
                    background-color: {row_bg};
                    color: {text_color};
                    gridline-color: {border_color};
                    border: {border_thickness}px solid {border_color};
                }}
                QTableWidget::item {{
                    padding: {cell_padding}px;
                    color: {text_color};
                }}
                QHeaderView::section {{
                    background-color: {header_bg};
                    color: {text_color};
                    padding: {cell_padding}px;
                    border: {border_thickness}px solid {border_color};
                    font-weight: bold;
                }}
            """
            table.setStyleSheet(table_style)

            # Enable alternating row colors
            table.setAlternatingRowColors(True)

            # Populate table with data
            start_row = 1 if show_header_row else 0  # Skip first row if it's a header
            for row_idx, row_data in enumerate(cell_data[start_row:], start=start_row):
                if not isinstance(row_data, list):
                    logger.warning(f"Row {row_idx} is not a list, skipping")
                    continue

                actual_row_idx = row_idx - start_row  # Adjust for table rows (excluding header)
                for col_idx, cell_value in enumerate(row_data):
                    if col_idx >= num_columns:
                        break

                    # Create table item
                    item = QTableWidgetItem(str(cell_value))
                    item.setFlags(item.flags() & ~Qt.ItemIsEditable)  # Make read-only

                    # Set text color
                    item.setForeground(QBrush(QColor(text_color)))

                    # Set background color with alternating rows
                    # First column is header column if show_header_column is true
                    if col_idx == 0 and show_header_column:
                        item.setBackground(QBrush(QColor(header_bg)))
                        font = item.font()
                        font.setBold(True)
                        item.setFont(font)
                    elif actual_row_idx % 2 == 1:
                        item.setBackground(QBrush(QColor(alt_row_bg)))
                    else:
                        item.setBackground(QBrush(QColor(row_bg)))

                    table.setItem(actual_row_idx, col_idx, item)

            # Resize columns to content
            table.horizontalHeader().setSectionResizeMode(QHeaderView.Stretch)
            table.verticalHeader().setSectionResizeMode(QHeaderView.ResizeToContents)

            # Hide vertical header (row numbers) - we use first column for row headers if needed
            table.verticalHeader().setVisible(False)

            # Disable scrollbars if table fits
            table.setHorizontalScrollBarPolicy(Qt.ScrollBarAsNeeded)
            table.setVerticalScrollBarPolicy(Qt.ScrollBarAsNeeded)

            # Apply common styling (border, shadow, opacity, rotation, background)
            self.apply_common_styling(table, properties)

            logger.debug(f"Table element created with {num_rows} rows and {num_columns} columns")

            return table

        except Exception as e:
            logger.error(f"Failed to create Table element: {e}")
            return None

    def apply_common_styling(self, widget: QWidget, properties: Dict[str, Any], skip_border: bool = False):
        """
        Apply common styling properties to any widget.
        Supports: Opacity, Rotation, Border, Shadow, BackgroundColor

        Args:
            widget: The widget to style
            properties: Properties dictionary
            skip_border: If True, skip border and background styling (for ShapeWidget with paintEvent)
        """
        try:
            style_parts = []

            # Background Color (skip for ShapeWidget)
            if not skip_border:
                background_color = properties.get('BackgroundColor')
                if background_color:
                    style_parts.append(f"background-color: {background_color};")

            # Border (skip for ShapeWidget)
            if not skip_border:
                border_color = properties.get('BorderColor')
                border_thickness = properties.get('BorderThickness', 0)
                if border_color and border_thickness:
                    try:
                        border_thickness = int(border_thickness)
                        if border_thickness > 0:
                            style_parts.append(f"border: {border_thickness}px solid {border_color};")
                    except (ValueError, TypeError):
                        logger.warning(f"Invalid BorderThickness: {border_thickness}")

            # Apply stylesheet if we have style parts
            if style_parts:
                existing_style = widget.styleSheet()
                new_style = existing_style + " " + " ".join(style_parts) if existing_style else " ".join(style_parts)
                widget.setStyleSheet(new_style)

            # Opacity
            opacity = properties.get('Opacity')
            if opacity is not None:
                try:
                    opacity_val = float(opacity)
                    if 0.0 <= opacity_val <= 1.0:
                        widget.setWindowOpacity(opacity_val)
                    else:
                        logger.warning(f"Opacity value out of range (0-1): {opacity_val}")
                except (ValueError, TypeError):
                    logger.warning(f"Invalid Opacity value: {opacity}")

            # Rotation
            rotation = properties.get('Rotation')
            if rotation:
                try:
                    rotation_degrees = float(rotation)
                    # QWidget doesn't support rotation directly, need QGraphicsView
                    # For now, log warning - full implementation would require QGraphicsView
                    if rotation_degrees != 0:
                        logger.warning(f"Rotation ({rotation_degrees}°) requested but not yet fully supported in PyQt5 widgets")
                        # TODO: Implement rotation using QGraphicsView/QGraphicsProxyWidget
                except (ValueError, TypeError):
                    logger.warning(f"Invalid Rotation value: {rotation}")

            # Shadow Effect - Server uses 'EnableShadow' property
            # Support legacy property names for backwards compatibility
            enable_shadow = properties.get('EnableShadow', False)
            has_shadow = properties.get('HasShadow', False)  # Alternative property name
            shadow_enabled = properties.get('ShadowEnabled', False)  # Legacy

            if enable_shadow or has_shadow or shadow_enabled:
                try:
                    shadow = QGraphicsDropShadowEffect()

                    # Shadow Color
                    shadow_color = properties.get('ShadowColor', '#000000')
                    shadow.setColor(QColor(shadow_color))

                    # Shadow Blur Radius - Server uses 'ShadowBlur' property
                    shadow_blur = properties.get('ShadowBlur', 5.0)
                    # Fallback to alternative property names
                    if shadow_blur == 5.0:
                        shadow_blur = properties.get('ShadowBlurRadius', 5.0)
                    try:
                        shadow_blur = float(shadow_blur)
                        shadow.setBlurRadius(shadow_blur)
                    except (ValueError, TypeError):
                        logger.warning(f"Invalid ShadowBlur: {shadow_blur}")
                        shadow.setBlurRadius(5.0)

                    # Shadow Offset
                    shadow_offset_x = properties.get('ShadowOffsetX', 5)
                    shadow_offset_y = properties.get('ShadowOffsetY', 5)
                    try:
                        offset_x = float(shadow_offset_x)
                        offset_y = float(shadow_offset_y)
                        shadow.setOffset(offset_x, offset_y)
                    except (ValueError, TypeError):
                        logger.warning(f"Invalid Shadow offset: X={shadow_offset_x}, Y={shadow_offset_y}")
                        shadow.setOffset(5, 5)

                    widget.setGraphicsEffect(shadow)
                    logger.debug(f"Applied shadow effect: color={shadow_color}, blur={shadow_blur}, offset=({shadow_offset_x}, {shadow_offset_y})")

                except Exception as e:
                    logger.error(f"Failed to apply shadow effect: {e}")

        except Exception as e:
            logger.error(f"Failed to apply common styling: {e}")

    def convert_csharp_format_to_python(self, csharp_format: str) -> str:
        """
        Convert C# DateTime format strings to Python strftime format.
        Common conversions:
        - dddd → %A (full weekday name)
        - ddd → %a (abbreviated weekday name)
        - dd → %d (day of month, zero-padded)
        - d → %-d (day of month, no padding)
        - MMMM → %B (full month name)
        - MMM → %b (abbreviated month name)
        - MM → %m (month, zero-padded)
        - M → %-m (month, no padding)
        - yyyy → %Y (4-digit year)
        - yy → %y (2-digit year)
        - HH → %H (24-hour, zero-padded)
        - H → %-H (24-hour, no padding)
        - hh → %I (12-hour, zero-padded)
        - h → %-I (12-hour, no padding)
        - mm → %M (minute, zero-padded)
        - m → %-M (minute, no padding)
        - ss → %S (second, zero-padded)
        - s → %-S (second, no padding)
        - tt → %p (AM/PM)
        """
        if not csharp_format:
            return '%Y-%m-%d %H:%M:%S'

        # If it's already a Python format (starts with %), return as-is
        if '%' in csharp_format:
            return csharp_format

        try:
            # Replace C# format specifiers with Python equivalents
            # Order matters! Replace longer patterns first
            result = csharp_format
            result = result.replace('dddd', '%A')  # Full weekday name
            result = result.replace('ddd', '%a')   # Abbreviated weekday name
            result = result.replace('dd', '%d')    # Day of month (zero-padded)
            result = result.replace('MMMM', '%B')  # Full month name
            result = result.replace('MMM', '%b')   # Abbreviated month name
            result = result.replace('MM', '%m')    # Month (zero-padded)
            result = result.replace('yyyy', '%Y')  # 4-digit year
            result = result.replace('yy', '%y')    # 2-digit year
            result = result.replace('HH', '%H')    # 24-hour (zero-padded)
            result = result.replace('hh', '%I')    # 12-hour (zero-padded)
            result = result.replace('mm', '%M')    # Minute (zero-padded)
            result = result.replace('ss', '%S')    # Second (zero-padded)
            result = result.replace('tt', '%p')    # AM/PM

            logger.debug(f"Converted C# format '{csharp_format}' to Python format '{result}'")
            return result

        except Exception as e:
            logger.error(f"Failed to convert format string '{csharp_format}': {e}, using default")
            return '%Y-%m-%d %H:%M:%S'

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
            import tempfile
            import os

            pixmap = self.grab()
            if pixmap.isNull():
                logger.error("Failed to grab screenshot: pixmap is null")
                return b''

            # Use temporary file instead of BytesIO (PyQt5 doesn't support BytesIO)
            temp_fd, temp_path = tempfile.mkstemp(suffix='.png')
            try:
                os.close(temp_fd)  # Close file descriptor, we only need the path

                # Save pixmap to temporary file
                if not pixmap.save(temp_path, 'PNG'):
                    logger.error("Failed to save screenshot to temporary file")
                    return b''

                # Read the file content
                with open(temp_path, 'rb') as f:
                    byte_array = f.read()

                logger.debug(f"Screenshot captured: {len(byte_array)} bytes")
                return byte_array

            finally:
                # Clean up temporary file
                try:
                    if os.path.exists(temp_path):
                        os.unlink(temp_path)
                except Exception as cleanup_error:
                    logger.warning(f"Failed to clean up temporary screenshot file: {cleanup_error}")

        except Exception as e:
            logger.error(f"Failed to take screenshot: {e}")
            import traceback
            logger.error(traceback.format_exc())
            return b''

    def create_datagrid_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any]
    ) -> Optional[QWidget]:
        """
        Create a DataGrid element for SQL Data Source display.
        Properties expected:
        - DataSourceId: GUID of the SQL data source
        - RowsPerPage: Number of rows to display
        - ShowHeader: Whether to show column headers
        - AutoScroll: Enable automatic scrolling
        - ScrollInterval: Seconds between scrolls
        - HeaderBackgroundColor: Header background color
        - HeaderTextColor: Header text color
        - RowBackgroundColor: Row background color
        - AlternateRowColor: Alternate row background color
        - BorderColor: Border color
        - BorderThickness: Border thickness
        - CellPadding: Cell padding
        - FontFamily: Font family
        - FontSize: Font size
        - TextColor: Text color
        """
        try:
            from PyQt5.QtWidgets import QTableWidget, QTableWidgetItem, QHeaderView
            from PyQt5.QtGui import QBrush, QColor, QFont
            from PyQt5.QtCore import Qt

            # Get data source ID
            data_source_id = properties.get('DataSourceId')
            if not data_source_id or data_source_id == '00000000-0000-0000-0000-000000000000':
                logger.warning("DataGrid element has no valid DataSourceId")
                return None

            # Convert GUID to string if needed
            data_source_id_str = str(data_source_id)

            # Get cached data for this data source
            cached_data = self.data_source_cache.get(data_source_id_str, [])

            if not cached_data:
                logger.warning(f"No data found for data source {data_source_id_str}")
                # Create placeholder
                cached_data = [{"Column1": "No Data", "Column2": "Available"}]

            # Get configuration
            rows_per_page = int(properties.get('RowsPerPage', 10))
            show_header = properties.get('ShowHeader', True)

            # Create table widget
            table = QTableWidget(self)
            table.setGeometry(x, y, width, height)

            # Get columns from first data row
            if cached_data:
                columns = list(cached_data[0].keys())
                rows = min(rows_per_page, len(cached_data))

                table.setRowCount(rows)
                table.setColumnCount(len(columns))

                # Set headers
                if show_header:
                    table.setHorizontalHeaderLabels(columns)
                    table.horizontalHeader().setVisible(True)
                else:
                    table.horizontalHeader().setVisible(False)

                # Populate data
                for row_idx, row_data in enumerate(cached_data[:rows_per_page]):
                    for col_idx, col_name in enumerate(columns):
                        value = str(row_data.get(col_name, ''))
                        item = QTableWidgetItem(value)

                        # Set item as read-only
                        item.setFlags(item.flags() & ~Qt.ItemIsEditable)

                        table.setItem(row_idx, col_idx, item)

                logger.info(f"DataGrid created with {rows} rows and {len(columns)} columns from data source {data_source_id_str}")
            else:
                # No data
                table.setRowCount(1)
                table.setColumnCount(1)
                item = QTableWidgetItem("No data available")
                item.setFlags(item.flags() & ~Qt.ItemIsEditable)
                table.setItem(0, 0, item)

            # Apply styling
            header_bg = properties.get('HeaderBackgroundColor', '#2196F3')
            header_fg = properties.get('HeaderTextColor', '#FFFFFF')
            row_bg = properties.get('RowBackgroundColor', '#FFFFFF')
            alt_bg = properties.get('AlternateRowColor', '#F5F5F5')
            border_color = properties.get('BorderColor', '#CCCCCC')
            border_thickness = int(properties.get('BorderThickness', 1))
            cell_padding = int(properties.get('CellPadding', 5))
            text_color = properties.get('TextColor', '#000000')

            # Font
            font_family = properties.get('FontFamily', 'Arial')
            font_size_base = properties.get('FontSize', 14)

            # Apply scaling to font size
            scale_x = getattr(self, '_scale_x', 1.0)
            scale_y = getattr(self, '_scale_y', 1.0)
            scale_avg = (scale_x + scale_y) / 2
            font_size = int(font_size_base * scale_avg)

            font = QFont(font_family, font_size)
            table.setFont(font)

            # Header style
            if show_header:
                header_style = f"""
                    QHeaderView::section {{
                        background-color: {header_bg};
                        color: {header_fg};
                        padding: {cell_padding}px;
                        border: {border_thickness}px solid {border_color};
                        font-weight: bold;
                    }}
                """
                table.horizontalHeader().setStyleSheet(header_style)

            # Table style
            table.setStyleSheet(f"""
                QTableWidget {{
                    background-color: {row_bg};
                    color: {text_color};
                    border: {border_thickness}px solid {border_color};
                    gridline-color: {border_color};
                }}
                QTableWidget::item {{
                    padding: {cell_padding}px;
                }}
                QTableWidget::item:alternate {{
                    background-color: {alt_bg};
                }}
            """)

            # Enable alternating row colors
            table.setAlternatingRowColors(True)

            # Auto-resize columns to fit content
            table.horizontalHeader().setSectionResizeMode(QHeaderView.Stretch)

            # Disable editing
            table.setEditTriggers(QTableWidget.NoEditTriggers)

            # Hide vertical header (row numbers)
            table.verticalHeader().setVisible(False)

            table.show()
            return table

        except Exception as e:
            logger.error(f"Failed to create DataGrid element: {e}")
            import traceback
            logger.error(traceback.format_exc())
            return None

    def create_datasourcetext_element(
        self,
        x: int, y: int,
        width: int, height: int,
        properties: Dict[str, Any]
    ) -> Optional[QLabel]:
        """
        Create a DataSourceText element - displays SQL data using a template.
        Properties expected:
        - DataSourceId: GUID of the SQL data source
        - Template: Scriban/Jinja2-like template string
        - RowIndex: Which row to display (0 = first row)
        - UpdateInterval: Seconds between updates
        - FontFamily: Font family
        - FontSize: Font size
        - TextColor: Text color
        - TextAlign: Text alignment (left/center/right)
        """
        try:
            # Get data source ID
            data_source_id = properties.get('DataSourceId')
            if not data_source_id or data_source_id == '00000000-0000-0000-0000-000000000000':
                logger.warning("DataSourceText element has no valid DataSourceId")
                return None

            # Convert GUID to string if needed
            data_source_id_str = str(data_source_id)

            # Get template
            template = properties.get('Template', '{{Name}}')
            row_index = int(properties.get('RowIndex', 0))

            # Get cached data for this data source
            cached_data = self.data_source_cache.get(data_source_id_str, [])

            if not cached_data:
                logger.warning(f"No data found for data source {data_source_id_str}")
                rendered_text = "(No data available)"
            else:
                # Get the row at row_index (or first row if out of bounds)
                if row_index >= 0 and row_index < len(cached_data):
                    row_data = cached_data[row_index]
                else:
                    row_data = cached_data[0]
                    logger.warning(f"RowIndex {row_index} out of bounds, using first row")

                # Render template with simple string replacement
                # We use Python string.format() style: {ColumnName}
                rendered_text = template

                try:
                    # Replace Scriban-style {{Variable}} with Python-style {Variable}
                    import re
                    # Convert {{Variable}} to {Variable}
                    python_template = re.sub(r'\{\{(\w+)\}\}', r'{\1}', template)

                    # Also support simple conditionals (very basic)
                    # For now, we'll just do simple variable replacement
                    # Full Scriban support would require a template library

                    # Replace variables
                    rendered_text = python_template.format(**row_data)

                    logger.debug(f"Rendered template: {rendered_text}")
                except KeyError as e:
                    logger.error(f"Template variable not found in data: {e}")
                    rendered_text = f"(Template error: {e})"
                except Exception as e:
                    logger.error(f"Failed to render template: {e}")
                    rendered_text = f"(Error: {e})"

            # Create label
            label = QLabel(self)
            label.setGeometry(x, y, width, height)
            label.setText(rendered_text)

            # Set font
            try:
                font_family = properties.get('FontFamily', 'Arial')
                font_size = properties.get('FontSize', 24)

                # Validate font size
                if not isinstance(font_size, (int, float)):
                    logger.warning(f"Invalid font size: {font_size}, using default")
                    font_size = 24

                # Apply scaling to font size
                scale_x = getattr(self, '_scale_x', 1.0)
                scale_y = getattr(self, '_scale_y', 1.0)
                scale_factor = (scale_x + scale_y) / 2.0
                scaled_font_size = int(font_size * scale_factor)

                font = QFont(font_family, scaled_font_size)
                label.setFont(font)
            except Exception as e:
                logger.warning(f"Failed to set font properties: {e}")

            # Set color
            try:
                text_color = properties.get('TextColor', '#000000')
                label.setStyleSheet(f"color: {text_color};")
            except Exception as e:
                logger.warning(f"Failed to set text color: {e}")

            # Set alignment
            try:
                text_align = properties.get('TextAlign', 'left')
                alignment = Qt.AlignLeft | Qt.AlignTop

                if text_align == 'center':
                    alignment = Qt.AlignHCenter | Qt.AlignVCenter
                elif text_align == 'right':
                    alignment = Qt.AlignRight | Qt.AlignTop

                label.setAlignment(alignment)
            except Exception as e:
                logger.warning(f"Failed to set text alignment: {e}")

            # Word wrap
            label.setWordWrap(True)

            label.show()
            logger.info(f"Created DataSourceText element with template: {template}")
            return label

        except Exception as e:
            logger.error(f"Failed to create DataSourceText element: {e}")
            import traceback
            logger.error(traceback.format_exc())
            return None

    def cache_data_source(self, data_source_id: str, data: list):
        """Cache data for a SQL data source"""
        try:
            self.data_source_cache[data_source_id] = data
            logger.info(f"Cached {len(data)} rows for data source {data_source_id}")
        except Exception as e:
            logger.error(f"Failed to cache data source {data_source_id}: {e}")

    def get_data_source_data(self, data_source_id: str) -> list:
        """Get cached data for a data source"""
        return self.data_source_cache.get(data_source_id, [])

    def update_data_source(self, data_source_id: str, new_data: list):
        """Update cached data and refresh affected datagrid elements"""
        try:
            # Update cache
            self.cache_data_source(data_source_id, new_data)

            # TODO: Find and refresh datagrid elements using this data source
            # For now, we'll just log - full implementation would require tracking
            # which elements use which data sources
            logger.info(f"Data source {data_source_id} updated - refresh would happen here")

        except Exception as e:
            logger.error(f"Failed to update data source {data_source_id}: {e}")
