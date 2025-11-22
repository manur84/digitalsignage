"""
Custom widget for rendering shapes (circle, ellipse, rectangle with rounded corners)
"""

import logging
from PyQt5.QtWidgets import QWidget
from PyQt5.QtCore import Qt
from PyQt5.QtGui import QColor, QPainter, QPen, QBrush

logger = logging.getLogger(__name__)


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
            new_color = QColor(color)
            if new_color != self.fill_color:  # Only update if color actually changed
                self.fill_color = new_color
                self.update()  # Trigger repaint
        except Exception as e:
            logger.warning(f"Invalid fill color {color}: {e}")
            self.fill_color = QColor('#CCCCCC')

    def set_stroke_color(self, color: str):
        """Set stroke color from hex string"""
        try:
            new_color = QColor(color)
            if new_color != self.stroke_color:  # Only update if color actually changed
                self.stroke_color = new_color
                self.update()  # Trigger repaint
        except Exception as e:
            logger.warning(f"Invalid stroke color {color}: {e}")
            self.stroke_color = QColor('#000000')

    def set_stroke_width(self, width: int):
        """Set stroke width"""
        try:
            new_width = max(0, int(width))
            if new_width != self.stroke_width:  # Only update if width actually changed
                self.stroke_width = new_width
                self.update()  # Trigger repaint
        except (ValueError, TypeError):
            self.stroke_width = 1

    def set_corner_radius(self, radius: float):
        """Set corner radius for rectangles"""
        try:
            new_radius = max(0, float(radius))
            if new_radius != self.corner_radius:  # Only update if radius actually changed
                self.corner_radius = new_radius
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
