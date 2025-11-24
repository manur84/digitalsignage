"""
Status screen state enumeration
"""

from enum import Enum


class ScreenState(Enum):
    """Enum for the 4 allowed screen states"""
    AUTO_DISCOVERY = "auto_discovery"
    CONNECTING = "connecting"
    NO_LAYOUT_ASSIGNED = "no_layout_assigned"
    SERVER_OFFLINE = "server_offline"
    NONE = "none"  # No status screen shown
