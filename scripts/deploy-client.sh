#!/bin/bash
# Deployment script for Raspberry Pi Client

set -e

echo "Digital Signage Client Deployment"
echo "=================================="
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "Please run as root (use sudo)"
    exit 1
fi

# Configuration
CLIENT_DIR="/usr/local/lib/digitalsignage"
BIN_DIR="/usr/local/bin"
CONFIG_DIR="/etc/digitalsignage"
LOG_DIR="/var/log/digitalsignage"
SERVICE_FILE="/etc/systemd/system/digitalsignage.service"

echo "Installing to $CLIENT_DIR"
echo ""

# Create directories
echo "Creating directories..."
mkdir -p "$CLIENT_DIR"
mkdir -p "$CONFIG_DIR"
mkdir -p "$LOG_DIR"

# Copy Python files
echo "Copying Python files..."
cp -r ./src/DigitalSignage.Client.RaspberryPi/*.py "$CLIENT_DIR/"
cp ./src/DigitalSignage.Client.RaspberryPi/requirements.txt "$CLIENT_DIR/"

# Install Python dependencies
echo "Installing Python dependencies..."
pip3 install -r "$CLIENT_DIR/requirements.txt"

# Create startup script
echo "Creating startup script..."
cat > "$BIN_DIR/digitalsignage-client" << 'EOF'
#!/bin/bash
cd /usr/local/lib/digitalsignage
exec python3 client.py "$@"
EOF

chmod +x "$BIN_DIR/digitalsignage-client"

# Create systemd service
echo "Creating systemd service..."
cat > "$SERVICE_FILE" << 'EOF'
[Unit]
Description=Digital Signage Client
After=network.target graphical.target

[Service]
Type=simple
User=pi
Group=pi
Environment="DISPLAY=:0"
Environment="XAUTHORITY=/home/pi/.Xauthority"
WorkingDirectory=/usr/local/lib/digitalsignage
ExecStart=/usr/bin/python3 /usr/local/lib/digitalsignage/client.py
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=graphical.target
EOF

# Create default configuration if it doesn't exist
if [ ! -f "$CONFIG_DIR/config.json" ]; then
    echo "Creating default configuration..."
    cat > "$CONFIG_DIR/config.json" << EOF
{
  "client_id": "$(uuidgen)",
  "server_host": "localhost",
  "server_port": 8080,
  "fullscreen": true,
  "log_level": "INFO"
}
EOF

    echo ""
    echo "⚠️  Please edit $CONFIG_DIR/config.json with your server settings"
fi

# Set permissions
chown -R pi:pi "$LOG_DIR"
chmod 755 "$CLIENT_DIR"
chmod 644 "$CONFIG_DIR/config.json"

# Reload systemd
echo "Reloading systemd..."
systemctl daemon-reload

echo ""
echo "✓ Installation complete!"
echo ""
echo "Next steps:"
echo "1. Edit configuration: sudo nano $CONFIG_DIR/config.json"
echo "2. Enable service: sudo systemctl enable digitalsignage"
echo "3. Start service: sudo systemctl start digitalsignage"
echo "4. View logs: sudo journalctl -u digitalsignage -f"
echo ""
