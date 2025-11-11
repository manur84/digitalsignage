# Deployment Guide

## Windows Server Deployment

### Prerequisites
- Windows 10/11 or Windows Server 2019+
- .NET 8.0 Runtime or SDK
- SQL Server (optional)
- Administrator privileges

### Option 1: Build from Source

```powershell
# Clone repository
git clone https://github.com/yourusername/digitalsignage.git
cd digitalsignage

# Run build script
.\scripts\build.ps1 -Configuration Release -Publish

# Output will be in ./publish/DigitalSignage.Server/
```

### Option 2: Pre-built Release

1. Download latest release from GitHub Releases
2. Extract to desired location (e.g., `C:\Program Files\DigitalSignage`)
3. Run `DigitalSignage.Server.exe`

### Configuration

Edit `appsettings.json`:

```json
{
  "Server": {
    "Port": 8080,
    "Host": "0.0.0.0"
  },
  "Database": {
    "ConnectionString": "Server=localhost;Database=DigitalSignage;Integrated Security=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Firewall Configuration

```powershell
# Allow inbound connections on port 8080
New-NetFirewallRule -DisplayName "Digital Signage Server" `
    -Direction Inbound `
    -LocalPort 8080 `
    -Protocol TCP `
    -Action Allow
```

### Run as Windows Service

```powershell
# Create Windows Service
sc.exe create DigitalSignageServer `
    binPath= "C:\Program Files\DigitalSignage\DigitalSignage.Server.exe" `
    start= auto `
    DisplayName= "Digital Signage Server"

# Start service
sc.exe start DigitalSignageServer
```

## Raspberry Pi Client Deployment

### Prerequisites
- Raspberry Pi 3 or newer
- Raspberry Pi OS (Bullseye or newer)
- Python 3.9+
- Display connected via HDMI
- Network connection to server

### Automatic Installation

```bash
# Download and run installer
wget https://raw.githubusercontent.com/yourusername/digitalsignage/main/scripts/deploy-client.sh
chmod +x deploy-client.sh
sudo ./deploy-client.sh
```

### Manual Installation

```bash
# Install system dependencies
sudo apt-get update
sudo apt-get install -y python3 python3-pip python3-pyqt5

# Create directory
sudo mkdir -p /usr/local/lib/digitalsignage

# Copy files
sudo cp -r src/DigitalSignage.Client.RaspberryPi/* /usr/local/lib/digitalsignage/

# Install Python dependencies
cd /usr/local/lib/digitalsignage
sudo pip3 install -r requirements.txt

# Create configuration
sudo mkdir -p /etc/digitalsignage
sudo nano /etc/digitalsignage/config.json
```

Configuration file (`/etc/digitalsignage/config.json`):

```json
{
  "client_id": "unique-id-here",
  "server_host": "192.168.1.100",
  "server_port": 8080,
  "fullscreen": true,
  "log_level": "INFO"
}
```

### Create Systemd Service

```bash
# Create service file
sudo nano /etc/systemd/system/digitalsignage.service
```

Content:

```ini
[Unit]
Description=Digital Signage Client
After=network.target graphical.target

[Service]
Type=simple
User=pi
Environment="DISPLAY=:0"
WorkingDirectory=/usr/local/lib/digitalsignage
ExecStart=/usr/bin/python3 /usr/local/lib/digitalsignage/client.py
Restart=always
RestartSec=10

[Install]
WantedBy=graphical.target
```

Enable and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable digitalsignage
sudo systemctl start digitalsignage
```

### Auto-start on Boot

Edit `/etc/xdg/lxsession/LXDE-pi/autostart`:

```bash
@xset s off
@xset -dpms
@xset s noblank
```

This disables screen blanking.

## Docker Deployment (Alternative)

### Server

```dockerfile
# Dockerfile for Server
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY publish/DigitalSignage.Server/ .
EXPOSE 8080
ENTRYPOINT ["dotnet", "DigitalSignage.Server.dll"]
```

Build and run:

```bash
docker build -t digitalsignage-server .
docker run -d -p 8080:8080 --name ds-server digitalsignage-server
```

### Client (Raspberry Pi)

```dockerfile
FROM balenalib/raspberry-pi-python:3.9
WORKDIR /app
COPY src/DigitalSignage.Client.RaspberryPi/ .
RUN pip install -r requirements.txt
CMD ["python3", "client.py"]
```

## Production Checklist

### Server
- [ ] .NET Runtime installed
- [ ] Firewall configured
- [ ] SQL Server accessible
- [ ] Backup strategy in place
- [ ] Logging configured
- [ ] SSL/TLS certificate (for HTTPS)
- [ ] Windows Service configured
- [ ] Auto-start enabled

### Client
- [ ] Python dependencies installed
- [ ] Server IP configured
- [ ] Network connectivity verified
- [ ] Display resolution set
- [ ] Systemd service enabled
- [ ] Auto-start configured
- [ ] Screen blanking disabled
- [ ] SSH access configured (for remote management)

## Monitoring

### Server Logs

Windows Event Viewer or log files in:
```
%APPDATA%\DigitalSignage\Logs\
```

### Client Logs

```bash
# View live logs
sudo journalctl -u digitalsignage -f

# View recent logs
sudo journalctl -u digitalsignage -n 100

# View logs from specific date
sudo journalctl -u digitalsignage --since "2024-01-01"
```

## Backup and Recovery

### Server Backup

```powershell
# Backup layouts
$BackupDir = "C:\Backups\DigitalSignage\$(Get-Date -Format 'yyyy-MM-dd')"
New-Item -ItemType Directory -Path $BackupDir -Force

Copy-Item "$env:APPDATA\DigitalSignage\Layouts\*" -Destination "$BackupDir\Layouts" -Recurse
Copy-Item "$env:APPDATA\DigitalSignage\Media\*" -Destination "$BackupDir\Media" -Recurse
```

### Client Backup

```bash
# Backup configuration
sudo tar -czf digitalsignage-backup.tar.gz /etc/digitalsignage /usr/local/lib/digitalsignage
```

## Troubleshooting

### Server won't start
1. Check if port 8080 is already in use: `netstat -ano | findstr :8080`
2. Check Windows Event Viewer for errors
3. Verify .NET Runtime is installed: `dotnet --list-runtimes`

### Client won't connect
1. Ping server: `ping <server-ip>`
2. Check firewall: `telnet <server-ip> 8080`
3. Verify configuration: `cat /etc/digitalsignage/config.json`
4. Check logs: `sudo journalctl -u digitalsignage -n 50`

### Display issues on Pi
1. Check X11 display: `echo $DISPLAY`
2. Verify PyQt5 installation: `python3 -c "from PyQt5 import QtWidgets"`
3. Test display: `DISPLAY=:0 xclock`

## Scaling

### Load Balancing
Deploy multiple server instances behind a load balancer (nginx, HAProxy):

```nginx
upstream digitalsignage {
    server 192.168.1.10:8080;
    server 192.168.1.11:8080;
}

server {
    listen 80;
    location / {
        proxy_pass http://digitalsignage;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

### Database Clustering
Use SQL Server Always On Availability Groups for high availability.

## Security Hardening

### Server
- Enable HTTPS with valid SSL certificate
- Implement API key authentication
- Use Windows Firewall rules
- Regular security updates
- Disable unnecessary Windows services

### Client
- Change default Pi password
- Configure SSH with key-based auth
- Disable unused services
- Configure UFW firewall
- Regular updates: `sudo apt-get update && sudo apt-get upgrade`
