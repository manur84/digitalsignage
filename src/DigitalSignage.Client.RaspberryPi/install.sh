#!/bin/bash
# Installation script for Digital Signage Raspberry Pi Client

set -e

echo "Installing Digital Signage Client for Raspberry Pi..."

# Update system
echo "Updating system packages..."
sudo apt-get update
sudo apt-get upgrade -y

# Install dependencies
echo "Installing dependencies..."
sudo apt-get install -y \
    python3 \
    python3-pip \
    python3-pyqt5 \
    chromium-browser \
    xserver-xorg \
    x11-xserver-utils \
    xinit

# Install Python packages
echo "Installing Python packages..."
pip3 install -r requirements.txt

# Create directories
echo "Creating directories..."
sudo mkdir -p /etc/digitalsignage
sudo mkdir -p /var/log/digitalsignage
mkdir -p ~/.digitalsignage/cache
mkdir -p ~/.digitalsignage/data

# Copy files
echo "Copying files..."
sudo cp client.py /usr/local/bin/digitalsignage-client
sudo cp config.py /usr/local/lib/digitalsignage/
sudo cp device_manager.py /usr/local/lib/digitalsignage/
sudo cp display_renderer.py /usr/local/lib/digitalsignage/
sudo chmod +x /usr/local/bin/digitalsignage-client

# Create systemd service
echo "Creating systemd service..."
sudo tee /etc/systemd/system/digitalsignage.service > /dev/null <<EOF
[Unit]
Description=Digital Signage Client
After=network.target

[Service]
Type=simple
User=pi
Environment="DISPLAY=:0"
ExecStart=/usr/bin/python3 /usr/local/bin/digitalsignage-client
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

# Enable and start service
echo "Enabling service..."
sudo systemctl daemon-reload
sudo systemctl enable digitalsignage.service

echo "Installation complete!"
echo "To start the service: sudo systemctl start digitalsignage"
echo "To view logs: sudo journalctl -u digitalsignage -f"
echo ""
echo "Please configure /etc/digitalsignage/config.json with your server settings"
