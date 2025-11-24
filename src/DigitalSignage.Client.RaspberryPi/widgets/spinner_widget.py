"""
Spinner widget for loading/progress indicators
"""

import logging
from PyQt5.QtWidgets import QWidget
from PyQt5.QtCore import QVariantAnimation, QEasingCurve, Qt
from PyQt5.QtGui import QPainter, QColor, QPen

logger = logging.getLogger(__name__)


class SpinnerWidget(QWidget):
    """Custom spinner widget with rotating circle"""

    def __init__(self, size: int = 80, color: str = "#4A90E2", parent=None):
        super().__init__(parent)
        self.setFixedSize(size, size)
        self._angle = 0
        self.color = QColor(color)

        self.animation = QVariantAnimation(self)
        self.animation.setStartValue(0)
        self.animation.setEndValue(360)
        self.animation.setDuration(1500)
        self.animation.setLoopCount(-1)
        self.animation.setEasingCurve(QEasingCurve.Linear)
        self.animation.valueChanged.connect(self._on_angle_changed)
        self.animation.start()

    def _on_angle_changed(self, value):
        """Handle angle change from animation"""
        self._angle = value
        self.update()

    def paintEvent(self, event):
        """Draw the spinner"""
        painter = QPainter(self)
        painter.setRenderHint(QPainter.Antialiasing, True)
        painter.setRenderHint(QPainter.SmoothPixmapTransform, True)

        rect = self.rect()
        center_x = rect.width() / 2
        center_y = rect.height() / 2
        radius = min(center_x, center_y) - 5

        pen = QPen(self.color)
        pen.setWidth(6)
        pen.setCapStyle(Qt.RoundCap)
        painter.setPen(pen)

        start_angle = self._angle * 16
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
