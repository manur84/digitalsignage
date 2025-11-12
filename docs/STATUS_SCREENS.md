# Status Screens Documentation

## Overview

The Digital Signage Raspberry Pi client includes a comprehensive status screen system that provides visual feedback to users during various operational states. These screens are displayed automatically when the client is waiting for server connection or layout assignment.

## Purpose

Status screens serve several critical functions:

1. **User Feedback**: Inform viewers that the display is operational and working correctly
2. **Troubleshooting**: Provide diagnostic information for administrators (QR codes, error messages)
3. **Professional Appearance**: Present a polished interface instead of blank screens or error messages
4. **State Communication**: Clearly indicate what the client is currently doing

## Status Screen States

### 1. Discovering Server Screen

**When Shown:**
- During auto-discovery phase when client searches for available servers
- Only appears if `auto_discover: true` in config.json

**Visual Elements:**
- Animated spinner (rotating blue circle)
- Large header: "Discovering Digital Signage Server..." (with animated dots)
- Discovery method indicator (e.g., "Using: mDNS/Zeroconf + UDP Broadcast")
- Informational text explaining what's happening

**Color Scheme:**
- Primary: Blue (#4A90E2) - indicates active search
- Text: White (#FFFFFF) on dark background (#1E1E1E)

**Code Example:**
```python
renderer.status_screen_manager.show_discovering_server("mDNS/Zeroconf + UDP Broadcast")
```

---

### 2. Connecting to Server Screen

**When Shown:**
- During connection attempts to the server
- Shows for each retry attempt (max 5 by default)

**Visual Elements:**
- Animated spinner (rotating blue circle)
- Large header: "Connecting to Server..." (with animated dots)
- Server URL being connected to (e.g., "http://192.168.0.145:8080")
- Attempt counter (e.g., "Attempt 3 of 5")

**Color Scheme:**
- Primary: Blue (#4A90E2) - indicates active connection process
- Text: White for main text, blue for server URL

**Code Example:**
```python
renderer.status_screen_manager.show_connecting(
    "http://192.168.0.145:8080",
    attempt=3,
    max_attempts=5
)
```

---

### 3. Waiting for Layout Screen

**When Shown:**
- After successful server connection
- Before first layout is received from server
- Automatically transitions to "No Layout Assigned" after 10 seconds if no layout received

**Visual Elements:**
- Large green checkmark (✓) indicating successful connection
- "Connected to Digital Signage Server" message
- Server URL
- Client ID display
- "Waiting for layout assignment..." (with animated dots)

**Color Scheme:**
- Success: Green (#5CB85C) - connection successful
- Warning: Orange (#F0AD4E) - waiting state
- Text: White and light gray

**Code Example:**
```python
renderer.status_screen_manager.show_waiting_for_layout(
    "client-abc-123",
    "http://192.168.0.145:8080"
)
```

---

### 4. Connection Error Screen

**When Shown:**
- After all connection retries have failed
- When client cannot reach the server
- Remains visible until connection is restored or app is restarted

**Visual Elements:**
- Large red X (✗) indicating error
- "Connection Error" title
- Failed server URL
- Error message details
- Troubleshooting checklist:
  - Network connection is active
  - Server is running and accessible
  - Firewall settings allow connection
  - Server address and port are correct
- QR code with debug information (Client ID, Server URL, Error, Timestamp)

**Color Scheme:**
- Error: Red (#D9534F) - critical error state
- Text: White and light gray
- QR Code: White on dark background

**Code Example:**
```python
renderer.status_screen_manager.show_connection_error(
    "http://192.168.0.145:8080",
    "Connection timeout: Server not responding",
    "client-abc-123"
)
```

**QR Code Data Format:**
```
Client ID: client-abc-123
Server: http://192.168.0.145:8080
Error: Connection timeout: Server not responding
Time: 2025-11-12T14:30:45.123456
```

---

### 5. No Layout Assigned Screen

**When Shown:**
- After successful connection but no layout received within 10 seconds
- When server has not assigned a layout to this device
- Provides instructions for administrator to assign layout

**Visual Elements:**
- Large warning icon (⚠) in orange
- "No Layout Assigned" title
- Message: "This device is connected but has not been assigned a layout"
- Device information:
  - Client ID
  - IP Address
  - Server URL
- Administrator instructions (step-by-step guide)
- QR code with device information for easy identification

**Color Scheme:**
- Warning: Orange (#F0AD4E) - requires attention
- Text: White and light gray
- Info box: Slightly lighter background (#2A2A2A)

**Code Example:**
```python
renderer.status_screen_manager.show_no_layout_assigned(
    "client-abc-123",
    "http://192.168.0.145:8080",
    "192.168.0.200"
)
```

**Administrator Instructions Shown:**
1. Log in to the Digital Signage Management Server
2. Navigate to Device Management
3. Find this device by Client ID or IP Address
4. Assign a layout to this device

---

## Architecture

### Files

**status_screen.py** (New)
- `StatusScreen` - Main widget for rendering status screens
- `AnimatedDotsLabel` - Label with animated dots (e.g., "Loading...")
- `SpinnerWidget` - Rotating circle animation
- `StatusScreenManager` - Simplified interface for managing status screens

**display_renderer.py** (Modified)
- Added `status_screen_manager` property
- Automatically clears status screen when real layout is received

**client.py** (Modified)
- Calls status screen manager at appropriate times:
  - During auto-discovery
  - During connection attempts
  - After connection (waiting for layout)
  - On connection errors
  - After 10 seconds if no layout assigned

### Class Hierarchy

```
StatusScreen (QWidget)
├── AnimatedDotsLabel (QLabel)
├── SpinnerWidget (QWidget)
└── Methods:
    ├── show_discovering_server()
    ├── show_connecting()
    ├── show_waiting_for_layout()
    ├── show_connection_error()
    ├── show_no_layout_assigned()
    └── clear_screen()

StatusScreenManager
├── Properties:
│   ├── display_renderer
│   ├── status_screen
│   └── is_showing_status
└── Methods:
    ├── show_discovering_server()
    ├── show_connecting()
    ├── show_waiting_for_layout()
    ├── show_connection_error()
    ├── show_no_layout_assigned()
    └── clear_status_screen()
```

### Integration Flow

```
Client Startup
    │
    ├── Auto-Discovery Enabled?
    │   ├── Yes → show_discovering_server()
    │   │         ├── Server Found → show_connecting()
    │   │         └── Not Found → show_connecting() (manual config)
    │   └── No → show_connecting() (manual config)
    │
    ├── Connection Attempts (1-5)
    │   ├── Each Attempt → show_connecting(attempt_number)
    │   ├── Success → show_waiting_for_layout()
    │   └── All Failed → show_connection_error()
    │
    ├── After Connection Success
    │   ├── Layout Received? → Clear status, show layout
    │   └── No Layout (10s timeout) → show_no_layout_assigned()
    │
    └── Layout Update Received
        └── clear_status_screen() → Render actual layout
```

## Design Guidelines

### Typography
- **Headers**: 48-120pt, bold, high contrast
- **Body Text**: 18-32pt, regular weight
- **Secondary Text**: 14-24pt, lighter color
- **Minimum**: Never below 14pt for readability

### Color Palette
```python
COLOR_BACKGROUND = "#1E1E1E"    # Dark background
COLOR_PRIMARY = "#4A90E2"        # Blue - info/active
COLOR_SUCCESS = "#5CB85C"        # Green - success
COLOR_WARNING = "#F0AD4E"        # Orange - warning
COLOR_ERROR = "#D9534F"          # Red - error
COLOR_TEXT_PRIMARY = "#FFFFFF"   # White text
COLOR_TEXT_SECONDARY = "#CCCCCC" # Light gray text
```

### Layout Principles
- **Center-aligned**: All content centered for easy viewing from distance
- **Generous spacing**: 30-40px between sections
- **Visual hierarchy**: Icon → Title → Details → QR Code
- **Breathing room**: Ample padding and margins

### Animations
- **Spinner**: Smooth 1.2s rotation, 270° arc, 6px stroke
- **Dots**: 500ms interval, cycles through 0-3 dots
- **Purpose**: Indicate activity without being distracting

## Testing

### Manual Testing

**Test Script:**
```bash
cd /var/www/html/digitalsignage/src/DigitalSignage.Client.RaspberryPi
python3 test_status_screens.py
```

This will cycle through all status screens with 5-second intervals.

### Testing in Real Client

1. **Test Discovery Screen:**
   - Set `auto_discover: true` in config.json
   - Ensure no server is broadcasting
   - Start client
   - Should show discovery screen for 5 seconds (default timeout)

2. **Test Connection Attempts:**
   - Set `server_host` to unreachable IP
   - Start client
   - Should cycle through 5 connection attempts

3. **Test Connection Error:**
   - Keep server unreachable
   - Wait for all retries to fail
   - Should show connection error screen

4. **Test Waiting for Layout:**
   - Connect to running server
   - Don't assign layout to device
   - Should show "Waiting for Layout" then "No Layout Assigned"

5. **Test Layout Display:**
   - Assign layout to device
   - Status screen should clear automatically
   - Layout should be displayed

## Troubleshooting

### Status Screen Not Appearing

**Problem**: Status screens don't show during startup

**Solutions**:
1. Check that `display_renderer` is initialized before `client.start()`
2. Verify PyQt5 is installed correctly
3. Check logs for status screen errors

**Code Check:**
```python
# In main():
client.display_renderer = DisplayRenderer(fullscreen=config.fullscreen)
client.display_renderer.show()  # Must be shown before status screens
```

### Status Screen Not Clearing

**Problem**: Status screen remains visible after layout received

**Solutions**:
1. Check that `render_layout()` is being called
2. Verify `status_screen_manager.is_showing_status` is True
3. Check for exceptions in `clear_status_screen()`

**Debug Code:**
```python
logger.info(f"Status showing: {self.status_screen_manager.is_showing_status}")
```

### Animations Not Working

**Problem**: Spinner or dots not animating

**Solutions**:
1. Ensure Qt event loop is running (qasync integration)
2. Check QTimer is not being garbage collected
3. Verify `cleanup()` wasn't called prematurely

### QR Code Not Generating

**Problem**: QR code appears as empty space

**Solutions**:
1. Verify `qrcode` package is installed: `pip install qrcode[pil]`
2. Check data string is not empty
3. Review logs for QR generation errors

## Performance Considerations

### Memory Usage
- Status screens are created on-demand and destroyed when cleared
- Animated widgets are properly cleaned up to prevent memory leaks
- QTimer objects are stopped before deletion

### CPU Usage
- Spinner animation uses QPropertyAnimation (hardware-accelerated when possible)
- Dot animation uses simple QTimer (minimal overhead)
- No continuous polling or rendering when static

### Display Latency
- Status screens appear immediately (no loading delay)
- Clearing takes <100ms typically
- No blocking operations in status screen code

## Future Enhancements

### Potential Additions
1. **Progress Bar**: For layout downloads or long operations
2. **Network Statistics**: Show network speed, latency during connection
3. **Multiple Languages**: Internationalization support
4. **Custom Branding**: Load logo/colors from config
5. **Sound Feedback**: Optional audio cues for state changes
6. **Touch Interaction**: Button to retry connection or show more info
7. **Live Status Updates**: Real-time system metrics while waiting

### Code Extensibility
The `StatusScreen` class can be easily extended with new methods:

```python
def show_custom_status(self, title: str, message: str, color: str):
    """Show a custom status screen"""
    self.clear_screen()
    layout = QVBoxLayout(self)
    # ... add custom widgets
```

## API Reference

### StatusScreenManager

#### Methods

**`show_discovering_server(discovery_method: str = "Auto-Discovery")`**
- Shows discovering server screen
- **Parameters:**
  - `discovery_method`: Description of discovery method being used

**`show_connecting(server_url: str, attempt: int = 1, max_attempts: int = 5)`**
- Shows connecting screen
- **Parameters:**
  - `server_url`: Full server URL (e.g., "http://192.168.0.145:8080")
  - `attempt`: Current attempt number
  - `max_attempts`: Total number of attempts

**`show_waiting_for_layout(client_id: str, server_url: str)`**
- Shows waiting for layout screen after successful connection
- **Parameters:**
  - `client_id`: Client identifier
  - `server_url`: Server URL

**`show_connection_error(server_url: str, error_message: str, client_id: str = "Unknown")`**
- Shows connection error screen
- **Parameters:**
  - `server_url`: Server URL that failed
  - `error_message`: Detailed error message
  - `client_id`: Client identifier (for QR code)

**`show_no_layout_assigned(client_id: str, server_url: str, ip_address: str = "Unknown")`**
- Shows no layout assigned screen
- **Parameters:**
  - `client_id`: Client identifier
  - `server_url`: Server URL
  - `ip_address`: Client IP address

**`clear_status_screen()`**
- Clears all status screens and prepares for layout display
- Automatically called by `DisplayRenderer.render_layout()`

#### Properties

**`is_showing_status: bool`**
- True if a status screen is currently visible
- False if status screen is cleared or never shown

## Examples

### Example 1: Basic Usage in Client Code

```python
# During auto-discovery
if self.config.auto_discover:
    self.display_renderer.status_screen_manager.show_discovering_server("mDNS + UDP")
    # ... discovery logic ...

# During connection
for attempt in range(max_retries):
    server_url = self.config.get_server_url()
    self.display_renderer.status_screen_manager.show_connecting(server_url, attempt + 1, max_retries)
    # ... connection attempt ...

# After successful connection
if connection_successful:
    self.display_renderer.status_screen_manager.show_waiting_for_layout(
        self.config.client_id,
        server_url
    )
```

### Example 2: Error Handling

```python
try:
    await self.sio.connect(server_url)
except Exception as e:
    self.display_renderer.status_screen_manager.show_connection_error(
        server_url,
        str(e),
        self.config.client_id
    )
```

### Example 3: Custom Status Screen

```python
# Extend StatusScreen class
class CustomStatusScreen(StatusScreen):
    def show_downloading_media(self, filename: str, progress: int):
        self.clear_screen()
        layout = QVBoxLayout(self)

        # Title
        title = QLabel(f"Downloading: {filename}")
        title.setStyleSheet(f"color: {self.COLOR_PRIMARY}; font-size: 36pt;")
        layout.addWidget(title)

        # Progress bar
        # ... add progress bar widget ...

        self.setLayout(layout)
```

## Conclusion

The status screen system provides a professional, informative interface for the Digital Signage client during various operational states. By providing clear visual feedback and diagnostic information, it enhances the user experience and simplifies troubleshooting for administrators.

For questions or issues, please refer to the main project documentation or open an issue on GitHub.
