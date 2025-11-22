# SSL/TLS Configuration - Simplified Approach

## Overview

The Digital Signage Server has been **simplified** to run in HTTP/WS mode by default. SSL/TLS is now handled via reverse proxy (nginx, caddy, IIS) instead of complex Windows netsh bindings.

## Why This Change?

**Previous approach (removed):**
- Complex Windows netsh SSL bindings
- Required Administrator privileges
- Platform-specific (Windows only)
- Error-prone certificate store management
- Difficult to debug

**New approach (current):**
- Industry-standard reverse proxy for SSL
- Works on all platforms (Windows, Linux, macOS)
- No Administrator privileges needed for server
- Easier certificate management
- Better security and performance
- Free SSL certificates with Let's Encrypt

---

## Development Setup (HTTP - No SSL)

**Server Configuration:**

`appsettings.json`:
```json
{
  "ServerSettings": {
    "Port": 8080,
    "EnableSsl": false
  }
}
```

**Client Configuration:**

`/opt/digitalsignage-client/config.json`:
```json
{
  "server_host": "192.168.0.10",
  "server_port": 8080,
  "use_ssl": false
}
```

**WebSocket URL:** `ws://192.168.0.10:8080/ws/`
**REST API URL:** `http://192.168.0.10:5001/api/`

---

## Production Setup (HTTPS - nginx SSL)

### Option 1: nginx on Linux (Recommended)

**1. Install nginx:**
```bash
# Ubuntu/Debian
sudo apt update
sudo apt install nginx certbot python3-certbot-nginx

# CentOS/RHEL
sudo yum install nginx certbot python3-certbot-nginx
```

**2. Copy nginx configuration:**
```bash
sudo cp nginx-ssl-example.conf /etc/nginx/sites-available/digitalsignage
sudo ln -s /etc/nginx/sites-available/digitalsignage /etc/nginx/sites-enabled/
```

**3. Edit configuration:**
```bash
sudo nano /etc/nginx/sites-available/digitalsignage

# Change:
# - server_name digitalsignage.example.com → your domain
# - upstream servers (127.0.0.1:8080 and :5001) → your server IPs
```

**4. Obtain Let's Encrypt SSL certificate (FREE):**
```bash
sudo certbot --nginx -d digitalsignage.example.com
```

**5. Test and reload:**
```bash
sudo nginx -t
sudo systemctl reload nginx
```

**6. Auto-renewal (certbot sets this up automatically):**
```bash
sudo certbot renew --dry-run
```

**Client Configuration:**
```json
{
  "server_host": "digitalsignage.example.com",
  "server_port": 443,
  "use_ssl": true,
  "verify_ssl": true
}
```

---

### Option 2: Caddy (Alternative - Automatic HTTPS)

Caddy automatically obtains and renews Let's Encrypt certificates!

**1. Install Caddy:**
```bash
# Ubuntu/Debian
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update
sudo apt install caddy
```

**2. Create Caddyfile:**
```bash
sudo nano /etc/caddy/Caddyfile
```

```caddy
digitalsignage.example.com {
    # WebSocket
    reverse_proxy /ws/* localhost:8080

    # REST API
    reverse_proxy /api/* localhost:5001

    # Swagger UI
    reverse_proxy /swagger/* localhost:5001
}
```

**3. Reload Caddy:**
```bash
sudo systemctl reload caddy
```

**That's it!** Caddy automatically handles SSL certificates, renewals, and HTTPS redirection.

---

### Option 3: IIS on Windows

**1. Install URL Rewrite and ARR:**
- Download and install [URL Rewrite Module](https://www.iis.net/downloads/microsoft/url-rewrite)
- Download and install [Application Request Routing (ARR)](https://www.iis.net/downloads/microsoft/application-request-routing)

**2. Enable proxy in ARR:**
- Open IIS Manager
- Click on server name
- Double-click "Application Request Routing"
- Click "Server Proxy Settings"
- Check "Enable proxy"

**3. Create SSL binding:**
- In IIS Manager, add HTTPS binding with SSL certificate
- Obtain certificate from Let's Encrypt or use commercial CA

**4. Add web.config:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
    <system.webServer>
        <rewrite>
            <rules>
                <!-- WebSocket Proxy -->
                <rule name="WebSocket" stopProcessing="true">
                    <match url="ws/(.*)" />
                    <action type="Rewrite" url="http://localhost:8080/ws/{R:1}" />
                </rule>

                <!-- REST API Proxy -->
                <rule name="API" stopProcessing="true">
                    <match url="api/(.*)" />
                    <action type="Rewrite" url="http://localhost:5001/api/{R:1}" />
                </rule>
            </rules>
        </rewrite>
    </system.webServer>
</configuration>
```

---

## Self-Signed Certificate (Development/Testing Only)

**Generate self-signed certificate:**
```bash
# Linux/macOS
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout server.key \
  -out server.crt \
  -subj "/CN=digitalsignage.local"

# Windows (PowerShell)
New-SelfSignedCertificate -DnsName "digitalsignage.local" -CertStoreLocation "cert:\LocalMachine\My"
```

**Configure nginx with self-signed cert:**
```nginx
ssl_certificate /path/to/server.crt;
ssl_certificate_key /path/to/server.key;
```

**Client Configuration:**
```json
{
  "server_host": "digitalsignage.local",
  "server_port": 443,
  "use_ssl": true,
  "verify_ssl": false  // WARNING: Only for development!
}
```

**WARNING:** Self-signed certificates will show browser warnings and are NOT suitable for production!

---

## Firewall Configuration

**Allow inbound connections:**

**Linux (ufw):**
```bash
sudo ufw allow 80/tcp    # HTTP (for Let's Encrypt ACME challenge)
sudo ufw allow 443/tcp   # HTTPS/WSS
sudo ufw reload
```

**Linux (iptables):**
```bash
sudo iptables -A INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -A INPUT -p tcp --dport 443 -j ACCEPT
sudo iptables-save | sudo tee /etc/iptables/rules.v4
```

**Windows Firewall:**
```powershell
New-NetFirewallRule -DisplayName "Digital Signage HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow
New-NetFirewallRule -DisplayName "Digital Signage HTTP" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow
```

---

## Monitoring and Troubleshooting

### nginx Logs

**Access logs:**
```bash
sudo tail -f /var/log/nginx/digitalsignage-ws-access.log
sudo tail -f /var/log/nginx/digitalsignage-api-access.log
```

**Error logs:**
```bash
sudo tail -f /var/log/nginx/digitalsignage-ws-error.log
sudo tail -f /var/log/nginx/digitalsignage-api-error.log
```

### Test SSL Configuration

**Check SSL certificate:**
```bash
openssl s_client -connect digitalsignage.example.com:443 -servername digitalsignage.example.com
```

**Test SSL configuration quality:**
- Visit: https://www.ssllabs.com/ssltest/
- Enter your domain
- Check for A+ rating

### Test WebSocket Connection

**Using wscat:**
```bash
npm install -g wscat
wscat -c wss://digitalsignage.example.com/ws/
```

**Expected output:**
```
Connected (press CTRL+C to quit)
```

### Test REST API

**Health check:**
```bash
curl https://digitalsignage.example.com/api/health
```

**Expected response:**
```json
{"status":"healthy","version":"1.0.0"}
```

---

## Security Best Practices

1. **Use Let's Encrypt for free SSL certificates** (auto-renewal)
2. **Enable HSTS** (HTTP Strict Transport Security) in nginx config
3. **Use strong SSL ciphers** (TLSv1.2+ only)
4. **Disable weak protocols** (SSLv3, TLSv1.0, TLSv1.1)
5. **Enable OCSP stapling** for faster SSL handshakes
6. **Implement rate limiting** in nginx to prevent abuse
7. **Use firewall** to restrict access to trusted IPs (if possible)
8. **Monitor SSL certificate expiration** (Let's Encrypt: 90 days)

---

## Migration from Old SSL Setup

If you were using the old Windows netsh SSL binding approach:

**1. Remove old SSL bindings:**
```powershell
# List bindings
netsh http show sslcert

# Remove binding (replace PORT with your port)
netsh http delete sslcert ipport=0.0.0.0:8080
```

**2. Update server configuration:**
```json
{
  "ServerSettings": {
    "EnableSsl": false,
    "AutoConfigureSslBinding": false
  }
}
```

**3. Set up nginx reverse proxy** (see above)

**4. Update client configurations** to point to nginx (port 443)

---

## Architecture Diagram

```
┌──────────────────┐
│  Raspberry Pi    │
│    Clients       │
│                  │
│ ws://192.168.x.x │ ← Development (no SSL)
│ wss://domain.com │ ← Production (nginx SSL)
└────────┬─────────┘
         │
         │ HTTPS/WSS (443)
         │
┌────────▼─────────┐
│     nginx        │ ← SSL Termination
│  Reverse Proxy   │    (Let's Encrypt)
└────────┬─────────┘
         │
         ├─ HTTP (8080) ──→ WebSocket Server
         │
         └─ HTTP (5001) ──→ REST API Server
                │
           ┌────▼─────────┐
           │   Digital    │
           │   Signage    │
           │   Server     │
           │  (Windows)   │
           └──────────────┘
```

---

## Summary

**Development:**
- Server: HTTP on port 8080 (WebSocket) and 5001 (REST API)
- Client: `use_ssl: false`
- No SSL complexity

**Production:**
- nginx reverse proxy with Let's Encrypt SSL
- Server: Still HTTP (nginx handles SSL termination)
- Client: `use_ssl: true`, connects to nginx on port 443
- Industry-standard approach
- Easy certificate management

**Benefits:**
- Simpler development workflow
- Production-ready SSL with zero cost (Let's Encrypt)
- Platform-independent
- Better security and performance
- Easier troubleshooting

For detailed nginx configuration, see: `nginx-ssl-example.conf`
