"""
Animated dots label widget for loading/progress indicators
"""

import logging
from PyQt5.QtWidgets import QLabel
from PyQt5.QtCore import QTimer

logger = logging.getLogger(__name__)


class AnimatedDotsLabel(QLabel):
    """Label that animates dots (e.g., "Connecting..." becomes "Connecting." -> "Connecting.." -> "Connecting...")"""

    def __init__(self, base_text: str, parent=None):
        super().__init__(parent)
        self.base_text = base_text
        self.dot_count = 0
        self.max_dots = 3

        self.timer = QTimer(self)
        self.timer.timeout.connect(self.update_dots)
        self.timer.start(600)  # Update every 600ms

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
