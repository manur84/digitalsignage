# SSL/TLS Certificate Directory

This directory contains SSL/TLS certificates for secure WebSocket (WSS) connections.

## Development Mode (Self-Signed Certificate)

When `EnableSsl=true` in `appsettings.json`, the server will automatically generate a self-signed certificate on first startup:

- **File:** `server.pfx`
- **Password:** `DigitalSignage2024!` (configurable in `appsettings.json`)
- **Validity:** 1 year from generation date
- **Key Size:** 2048-bit RSA
- **Signature:** SHA256

### Important Notes for Development

⚠️ **Self-signed certificates are NOT suitable for production use!**

- Clients will show security warnings
- Browsers will display "Not Secure" warnings
- Raspberry Pi clients must be configured to accept self-signed certificates

## Production Mode (CA-Signed Certificate)

For production deployments, replace the self-signed certificate with a proper certificate from a trusted Certificate Authority (CA):

1. Obtain a certificate from a CA (e.g., Let's Encrypt, DigiCert, Sectigo)
2. Export the certificate with private key in PFX format
3. Place the PFX file in this directory
4. Update `appsettings.json`:
   ```json
   "ServerSettings": {
     "EnableSsl": true,
     "CertificatePath": "./certs/your-certificate.pfx",
     "CertificatePassword": "your-secure-password"
   }
   ```

## Windows HttpListener HTTPS Configuration

**IMPORTANT:** `HttpListener` on Windows requires additional configuration for HTTPS:

### 1. Install Certificate in Windows Certificate Store

```powershell
# Import PFX to LocalMachine\My store
$cert = Import-PfxCertificate -FilePath "server.pfx" -CertStoreLocation Cert:\LocalMachine\My -Password (ConvertTo-SecureString -String "DigitalSignage2024!" -AsPlainText -Force)
```

### 2. Bind Certificate to Port using netsh

```powershell
# Get certificate thumbprint (remove spaces)
$thumbprint = $cert.Thumbprint

# Bind certificate to port 8080
netsh http add sslcert ipport=0.0.0.0:8080 certhash=$thumbprint appid="{00000000-0000-0000-0000-000000000000}"
```

### 3. Configure URL ACL for HTTPS

```powershell
# Add URL reservation for HTTPS
netsh http add urlacl url=https://+:8080/ws/ user=Everyone
```

### Remove SSL Binding (if needed)

```powershell
# Remove SSL certificate binding
netsh http delete sslcert ipport=0.0.0.0:8080

# Remove URL ACL
netsh http delete urlacl url=https://+:8080/ws/
```

## Alternative: Reverse Proxy for SSL Termination

For easier SSL management, use a reverse proxy (recommended for production):

### Option 1: Nginx

```nginx
server {
    listen 443 ssl;
    server_name digitalsignage.yourdomain.com;

    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;

    location /ws/ {
        proxy_pass http://localhost:8080/ws/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

Then configure DigitalSignage Server:
```json
"ServerSettings": {
  "EnableSsl": false,  // SSL handled by nginx
  "Port": 8080
}
```

### Option 2: IIS with URL Rewrite + Application Request Routing

1. Install IIS with WebSocket Protocol support
2. Install URL Rewrite and ARR modules
3. Configure reverse proxy rules for WebSocket
4. Bind SSL certificate in IIS

## Client Configuration

### Raspberry Pi Python Client

If using self-signed certificates, configure the client to accept them:

**Option 1: Disable SSL verification (development only)**
```python
# In client.py
ssl_context = ssl.create_default_context()
ssl_context.check_hostname = False
ssl_context.verify_mode = ssl.CERT_NONE
```

**Option 2: Trust self-signed certificate**
```python
# Export server certificate (without private key)
# On server:
openssl pkcs12 -in server.pfx -clcerts -nokeys -out server.crt

# On Pi client:
ssl_context = ssl.create_default_context()
ssl_context.load_verify_locations('server.crt')
```

### Web Browser Testing

For browser testing with self-signed certificates:

1. Navigate to `https://server-ip:8080/ws/`
2. Accept the security warning
3. Add exception for the certificate

**Chrome/Edge:** Type `thisisunsafe` when security warning is shown

**Firefox:** Click "Advanced" → "Accept the Risk and Continue"

## Security Best Practices

1. **Never commit certificates to git** - Already in `.gitignore`
2. **Use strong passwords** - Minimum 12 characters
3. **Rotate certificates** - Replace before expiry
4. **Secure file permissions** - Restrict access to certificate files
5. **Use production certificates** - Never use self-signed in production
6. **Monitor expiry** - Set up alerts for certificate expiration

## Troubleshooting

### Server fails to start with HTTPS enabled

**Check:**
1. Certificate file exists at configured path
2. Password is correct
3. Certificate is not expired
4. netsh SSL binding is configured (Windows)
5. URL ACL is configured for HTTPS

**View logs:**
```
logs/digitalsignage-{date}.log
```

### Clients cannot connect via WSS

**Check:**
1. Firewall allows HTTPS traffic on port
2. Client trusts certificate (or SSL verification disabled)
3. Server is listening on correct IP address
4. WebSocket upgrade headers are allowed

**Test with curl:**
```bash
# Test HTTPS endpoint
curl -k https://server-ip:8080/ws/

# Test with certificate verification
curl --cacert server.crt https://server-ip:8080/ws/
```

**Test with wscat:**
```bash
# Install wscat
npm install -g wscat

# Test WSS connection
wscat -c wss://server-ip:8080/ws/ --no-check
```

## Certificate Information

After server startup, check logs for certificate details:

```
[INFO] SSL certificate loaded successfully
[INFO] Certificate Subject: CN=DigitalSignage Server
[INFO] Certificate Thumbprint: {thumbprint}
[INFO] Certificate Valid Until: {date}
```

## Support

For issues with SSL configuration, see:
- **Server Logs:** `logs/digitalsignage-{date}.log`
- **Project Docs:** `CLAUDE.md`
- **Windows SSL:** https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener
