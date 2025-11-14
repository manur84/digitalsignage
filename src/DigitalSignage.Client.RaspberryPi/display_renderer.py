"""
Display renderer using PyQt5 for rendering layouts
"""

import logging
from typing import Dict, Any, Optional
from io import BytesIO
import locale

from PyQt5.QtWidgets import QWidget, QLabel, QGraphicsDropShadowEffect
from PyQt5.QtCore import Qt, QRect, QTimer
from PyQt5.QtGui import QPixmap, QFont, QColor, QPainter, QImage
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

            # Stop and clear all datetime timers
            if hasattr(self, '_datetime_timers'):
                for timer in self._datetime_timers:
                    try:
                        timer.stop()
                        timer.deleteLater()
                    except Exception as e:
                        logger.warning(f"Failed to stop datetime timer: {e}")
                self._datetime_timers.clear()

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
            elif element_type == 'datetime':
                return self.create_datetime_element(x, y, width, height, properties)
            elif element_type == 'table':
                return self.create_table_element(x, y, width, height, properties, data)
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

            # Apply common styling (shadow, opacity, rotation - border already handled above)
            # Note: We don't call apply_common_styling for border here since shapes have their own border handling
            self.apply_common_styling(widget, properties)

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

                font = QFont(font_family, int(font_size))
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
        - Columns: List of column headers (list of strings)
        - Rows: List of row data (list of lists)
        - HeaderBackground: Header background color (default: #0078D4)
        - HeaderForeground: Header text color (default: #FFFFFF)
        - RowBackground: Row background color (default: #FFFFFF)
        - AlternateRowBackground: Alternate row background color (default: #F0F0F0)
        - FontFamily: Font family (default: Arial)
        - FontSize: Font size (default: 12)
        - BorderColor: Border color (default: #CCCCCC)
        """
        try:
            from PyQt5.QtWidgets import QTableWidget, QTableWidgetItem, QHeaderView
            from PyQt5.QtGui import QBrush, QColor
            from PyQt5.QtCore import Qt

            table = QTableWidget(self)
            table.setGeometry(x, y, width, height)

            # Get columns and rows from properties
            columns = properties.get('Columns', [])
            rows = properties.get('Rows', [])

            # Validate data types
            if not isinstance(columns, list):
                logger.warning(f"Table Columns is not a list: {type(columns)}, using empty list")
                columns = []

            if not isinstance(rows, list):
                logger.warning(f"Table Rows is not a list: {type(rows)}, using empty list")
                rows = []

            # Set table dimensions
            num_columns = len(columns) if columns else 0
            num_rows = len(rows) if rows else 0

            if num_columns == 0 or num_rows == 0:
                logger.warning("Table has no columns or rows, creating placeholder")
                num_columns = 3
                num_rows = 3
                columns = ["Column 1", "Column 2", "Column 3"]
                rows = [
                    ["Row 1, Col 1", "Row 1, Col 2", "Row 1, Col 3"],
                    ["Row 2, Col 1", "Row 2, Col 2", "Row 2, Col 3"],
                    ["Row 3, Col 1", "Row 3, Col 2", "Row 3, Col 3"]
                ]

            table.setRowCount(num_rows)
            table.setColumnCount(num_columns)

            # Set column headers
            table.setHorizontalHeaderLabels([str(col) for col in columns])

            # Get style properties
            header_bg = properties.get('HeaderBackground', '#0078D4')
            header_fg = properties.get('HeaderForeground', '#FFFFFF')
            row_bg = properties.get('RowBackground', '#FFFFFF')
            alt_row_bg = properties.get('AlternateRowBackground', '#F0F0F0')
            border_color = properties.get('BorderColor', '#CCCCCC')
            font_family = properties.get('FontFamily', 'Arial')
            font_size = properties.get('FontSize', 12)

            # Set font
            try:
                if not isinstance(font_size, (int, float)):
                    font_size = 12
                font = QFont(font_family, int(font_size))
                table.setFont(font)
            except Exception as e:
                logger.warning(f"Failed to set table font: {e}")

            # Set table style
            table_style = f"""
                QTableWidget {{
                    background-color: {row_bg};
                    gridline-color: {border_color};
                    border: 1px solid {border_color};
                }}
                QTableWidget::item {{
                    padding: 5px;
                }}
                QHeaderView::section {{
                    background-color: {header_bg};
                    color: {header_fg};
                    padding: 5px;
                    border: 1px solid {border_color};
                    font-weight: bold;
                }}
            """
            table.setStyleSheet(table_style)

            # Enable alternating row colors
            table.setAlternatingRowColors(True)

            # Populate table with data
            for row_idx, row_data in enumerate(rows):
                if not isinstance(row_data, list):
                    logger.warning(f"Row {row_idx} is not a list, skipping")
                    continue

                for col_idx, cell_data in enumerate(row_data):
                    if col_idx >= num_columns:
                        break

                    # Create table item
                    item = QTableWidgetItem(str(cell_data))
                    item.setFlags(item.flags() & ~Qt.ItemIsEditable)  # Make read-only

                    # Set alternate row background
                    if row_idx % 2 == 1:
                        item.setBackground(QBrush(QColor(alt_row_bg)))
                    else:
                        item.setBackground(QBrush(QColor(row_bg)))

                    table.setItem(row_idx, col_idx, item)

            # Resize columns to content
            table.horizontalHeader().setSectionResizeMode(QHeaderView.Stretch)
            table.verticalHeader().setSectionResizeMode(QHeaderView.ResizeToContents)

            # Hide vertical header (row numbers)
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

    def apply_common_styling(self, widget: QWidget, properties: Dict[str, Any]):
        """
        Apply common styling properties to any widget.
        Supports: Opacity, Rotation, Border, Shadow, BackgroundColor
        """
        try:
            style_parts = []

            # Background Color
            background_color = properties.get('BackgroundColor')
            if background_color:
                style_parts.append(f"background-color: {background_color};")

            # Border
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

            # Shadow Effect
            shadow_enabled = properties.get('ShadowEnabled', False)
            if shadow_enabled:
                try:
                    shadow = QGraphicsDropShadowEffect()

                    # Shadow Color
                    shadow_color = properties.get('ShadowColor', '#000000')
                    shadow.setColor(QColor(shadow_color))

                    # Shadow Blur Radius
                    shadow_blur = properties.get('ShadowBlur', 10)
                    try:
                        shadow_blur = float(shadow_blur)
                        shadow.setBlurRadius(shadow_blur)
                    except (ValueError, TypeError):
                        logger.warning(f"Invalid ShadowBlur: {shadow_blur}")
                        shadow.setBlurRadius(10)

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
