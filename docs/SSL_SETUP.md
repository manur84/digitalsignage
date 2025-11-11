# SSL/TLS Setup Guide for Digital Signage System

This guide explains how to enable SSL/TLS encryption for secure WebSocket (WSS) communication between the server and Raspberry Pi clients.

## Table of Contents

- [Overview](#overview)
- [Option 1: Reverse Proxy (Recommended for Production)](#option-1-reverse-proxy-recommended-for-production)
- [Option 2: Direct SSL with HttpListener (Windows)](#option-2-direct-ssl-with-httplistener-windows)
- [Option 3: Self-Signed Certificate (Development/Testing)](#option-3-self-signed-certificate-developmenttesting)
- [Client Configuration](#client-configuration)
- [Troubleshooting](#troubleshooting)

## Overview

The Digital Signage system supports both unencrypted (HTTP/WS) and encrypted (HTTPS/WSS) communication:

- **HTTP/WS** (default): Unencrypted communication on port 8080
- **HTTPS/WSS** (production): Encrypted communication on port 8443 (or custom port)

### Why Use SSL/TLS?

- **Security**: Encrypts all communication between server and clients
- **Data Protection**: Prevents eavesdropping and man-in-the-middle attacks
- **Integrity**: Ensures data isn't tampered with during transmission
- **Compliance**: Required for many enterprise and public deployments

---

## Option 1: Reverse Proxy (Recommended for Production)

Using a reverse proxy (nginx, IIS, Apache) is the **recommended approach** for production deployments. The proxy handles SSL termination, and the Digital Signage server continues to use HTTP internally.

### Advantages

- ✅ Easier certificate management
- ✅ Better performance (optimized SSL libraries)
- ✅ Additional features (load balancing, rate limiting)
- ✅ Works on all platforms (Windows, Linux)
- ✅ Automatic certificate renewal (Let's Encrypt)

### Setup with nginx (Linux)

#### 1. Install nginx

```bash
sudo apt-get update
sudo apt-get install nginx certbot python3-certbot-nginx
```

#### 2. Obtain SSL Certificate (Let's Encrypt)

```bash
sudo certbot --nginx -d yourdomain.com
```

#### 3. Configure nginx

Edit `/etc/nginx/sites-available/digitalsignage`:

```nginx
upstream digitalsignage {
    server 127.0.0.1:8080;
}

# Redirect HTTP to HTTPS
server {
    listen 80;
    server_name yourdomain.com;
    return 301 https://$server_name$request_uri;
}

# HTTPS Server
server {
    listen 443 ssl http2;
    server_name yourdomain.com;

    # SSL Configuration (managed by Certbot)
    ssl_certificate /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    # WebSocket upgrade headers
    location /ws/ {
        proxy_pass http://digitalsignage;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400;
    }
}
```

#### 4. Enable and Restart nginx

```bash
sudo ln -s /etc/nginx/sites-available/digitalsignage /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

#### 5. Configure Server

Keep `appsettings.json` with SSL disabled (nginx handles it):

```json
{
  "ServerSettings": {
    "Port": 8080,
    "EnableSsl": false
  }
}
```

#### 6. Configure Clients

Update client configuration to use HTTPS:

```json
{
  "server_host": "yourdomain.com",
  "server_port": 443,
  "use_ssl": true,
  "verify_ssl": true
}
```

---

### Setup with IIS (Windows)

#### 1. Install IIS with Application Request Routing (ARR)

- Open Server Manager → Add Roles and Features
- Install Web Server (IIS)
- Install URL Rewrite Module and ARR from [IIS Downloads](https://www.iis.net/downloads)

#### 2. Obtain SSL Certificate

- Use Let's Encrypt with [win-acme](https://www.win-acme.com/)
- Or use a commercial certificate from your CA
- Or generate self-signed for testing

#### 3. Configure ARR Reverse Proxy

1. Open IIS Manager
2. Select your site → URL Rewrite → Add Rule
3. Create Reverse Proxy rule:
   - Enable proxy: Yes
   - Inbound rules: `^ws/(.*)`
   - Rewrite URL: `http://localhost:8080/ws/{R:1}`
4. Configure WebSocket support:
   - Set `upgrade` header
   - Set `connection` header to "upgrade"

#### 4. Bind SSL Certificate

1. In IIS Manager, select your site
2. Bindings → Add → HTTPS → Port 443
3. Select your SSL certificate

#### 5. Configure Clients

Same as nginx setup above.

---

## Option 2: Direct SSL with HttpListener (Windows)

If you can't use a reverse proxy, you can configure SSL directly on the HttpListener. **Note:** This is more complex and requires administrative privileges.

### Prerequisites

- Windows Server or Windows 10/11 with Administrator access
- SSL certificate (PFX format)

### Steps

#### 1. Obtain Certificate

Generate self-signed (testing only):

```powershell
$cert = New-SelfSignedCertificate `
    -DnsName "digitalsignage.local" `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -NotAfter (Get-Date).AddYears(5)

$thumbprint = $cert.Thumbprint
Write-Host "Certificate Thumbprint: $thumbprint"
```

Or use a commercial/Let's Encrypt certificate.

#### 2. Bind Certificate to Port

```powershell
# Get certificate thumbprint
$thumbprint = "YOUR_CERTIFICATE_THUMBPRINT"

# Bind to port 8443
netsh http add sslcert ipport=0.0.0.0:8443 `
    certhash=$thumbprint `
    appid="{12345678-1234-1234-1234-123456789012}"

# Add URL ACL
netsh http add urlacl url=https://+:8443/ws/ user=Everyone
```

#### 3. Configure Server

Update `appsettings.Production.json`:

```json
{
  "ServerSettings": {
    "Port": 8443,
    "EnableSsl": true,
    "CertificateThumbprint": "YOUR_CERTIFICATE_THUMBPRINT"
  }
}
```

#### 4. Run Server

```powershell
# Set environment to Production
$env:ASPNETCORE_ENVIRONMENT="Production"

# Run as Administrator
.\DigitalSignage.Server.exe
```

#### 5. Configure Clients

```json
{
  "server_host": "server-hostname",
  "server_port": 8443,
  "use_ssl": true,
  "verify_ssl": true
}
```

---

## Option 3: Self-Signed Certificate (Development/Testing)

For development and testing, you can use a self-signed certificate.

### Windows: Generate Self-Signed Certificate

```powershell
$cert = New-SelfSignedCertificate `
    -Subject "CN=DigitalSignage" `
    -DnsName "localhost", "127.0.0.1", "digitalsignage.local" `
    -CertStoreLocation "Cert:\LocalMachine\My" `
    -NotAfter (Get-Date).AddYears(5) `
    -KeyUsage DigitalSignature, KeyEncipherment `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")

# Export for clients
$password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "digitalsignage.pfx" -Password $password
```

### Linux: Generate Self-Signed Certificate

```bash
# Generate private key and certificate
openssl req -x509 -newkey rsa:4096 -nodes \
    -keyout digitalsignage.key \
    -out digitalsignage.crt \
    -days 1825 \
    -subj "/CN=digitalsignage.local"

# Create PFX for Windows server (if needed)
openssl pkcs12 -export \
    -out digitalsignage.pfx \
    -inkey digitalsignage.key \
    -in digitalsignage.crt \
    -password pass:YourPassword
```

### Client Configuration for Self-Signed Certificates

**Important:** Self-signed certificates will fail verification by default.

**Option A**: Disable verification (NOT for production!):

```json
{
  "use_ssl": true,
  "verify_ssl": false
}
```

**Option B**: Install certificate on client (recommended):

```bash
# Copy certificate to Raspberry Pi
scp digitalsignage.crt pi@raspberrypi:/home/pi/

# Install as trusted certificate
sudo cp digitalsignage.crt /usr/local/share/ca-certificates/
sudo update-ca-certificates

# Client config
{
  "use_ssl": true,
  "verify_ssl": true
}
```

---

## Client Configuration

### Configuration File

Edit `/etc/digitalsignage/config.json` on Raspberry Pi:

```json
{
  "client_id": "unique-client-id",
  "server_host": "your-server.com",
  "server_port": 443,
  "use_ssl": true,
  "verify_ssl": true,
  "fullscreen": true,
  "log_level": "INFO"
}
```

### Environment Variables

Alternatively, use environment variables:

```bash
export DS_SERVER_HOST=your-server.com
export DS_SERVER_PORT=443
export DS_USE_SSL=true
export DS_VERIFY_SSL=true
```

---

## Troubleshooting

### Server Issues

#### "Access Denied" when starting server

**Solution:** Run as Administrator or configure URL ACL:

```powershell
netsh http add urlacl url=https://+:8443/ws/ user=Everyone
```

#### "SSL certificate not configured"

**Solution:** Ensure certificate is bound to port:

```powershell
netsh http show sslcert
```

#### HttpListener fails to start with SSL

**Solution:** Use reverse proxy (nginx/IIS) instead of direct SSL.

### Client Issues

#### "SSL: CERTIFICATE_VERIFY_FAILED"

**Solutions:**
1. Install server certificate on client
2. Use valid certificate from trusted CA
3. Disable verification (testing only): `"verify_ssl": false`

#### Connection timeout with SSL enabled

**Solutions:**
1. Check firewall allows port 443/8443
2. Verify server is listening: `netstat -an | findstr 8443`
3. Test with curl: `curl -v https://server:8443/ws/`

#### "SSL handshake failed"

**Solutions:**
1. Check certificate validity: `openssl x509 -in cert.crt -text -noout`
2. Ensure client and server use compatible TLS versions
3. Check logs on both client and server

### Port Forwarding

If server is behind NAT/firewall:

```bash
# Allow through firewall (Linux)
sudo ufw allow 443/tcp
sudo ufw allow 8443/tcp

# Windows Firewall
netsh advfirewall firewall add rule name="Digital Signage HTTPS" dir=in action=allow protocol=TCP localport=443
```

---

## Security Best Practices

1. **Always use SSL/TLS in production**
2. **Use certificates from trusted CAs** (not self-signed)
3. **Enable certificate verification** on clients
4. **Keep certificates up to date** (monitor expiration)
5. **Use strong cipher suites** (TLS 1.2+)
6. **Restrict access** with firewalls
7. **Monitor connections** in server logs
8. **Use reverse proxy** for additional security features

---

## Quick Reference

| Feature | Development | Production |
|---------|------------|------------|
| Method | Self-signed cert | Reverse proxy with Let's Encrypt |
| Port | 8443 | 443 (standard HTTPS) |
| Verification | Disabled | Enabled |
| Certificate | Self-signed | Valid CA certificate |
| Performance | Adequate | Optimized |
| Management | Manual | Automated renewal |

---

## Additional Resources

- [Let's Encrypt](https://letsencrypt.org/) - Free SSL certificates
- [nginx Documentation](https://nginx.org/en/docs/)
- [IIS URL Rewrite](https://www.iis.net/downloads/microsoft/url-rewrite)
- [HttpListener SSL Configuration](https://docs.microsoft.com/en-us/dotnet/api/system.net.httplistener)
- [python-socketio SSL](https://python-socketio.readthedocs.io/en/latest/client.html#ssl-security)

---

## Support

For issues with SSL configuration, check:
1. Server logs: `logs/digitalsignage-*.txt`
2. Client logs: `/var/log/digitalsignage-client.log`
3. System logs: `journalctl -u digitalsignage-client`
