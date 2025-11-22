# SSL/WSS Sicherheitsanalyse - DigitalSignage System

**Datum:** 2025-11-22
**Autor:** Claude Code Agent
**Status:** Analyse abgeschlossen

---

## Executive Summary

Das DigitalSignage System ist **teilweise** auf HTTPS/WSS vorbereitet, aber **NICHT vollständig aktiviert**. Die Infrastruktur ist vorhanden, aber SSL ist standardmäßig deaktiviert. Es fehlt ein CertificateService zur automatischen Zertifikatsgenerierung.

### Bewertung nach Komponente

| Komponente | HTTP/WS Status | HTTPS/WSS Ready | SSL Aktiviert | Bemerkung |
|------------|---------------|-----------------|---------------|-----------|
| **REST API (ApiHost.cs)** | ✅ | ✅ | ✅ | **HTTPS Port 5001** - Production Ready |
| **WebSocket Server** | ✅ | ⚠️ | ❌ | Infrastruktur vorhanden, SSL=false |
| **Python Client (Pi)** | ✅ | ✅ | ⚠️ | Unterstützt WSS, verify_ssl=False |
| **iOS App (Mobile)** | ✅ | ✅ | ⚠️ | Unterstützt WSS, akzeptiert self-signed |
| **Zertifikatsverwaltung** | N/A | ❌ | ❌ | **CertificateService fehlt** |

**Gesamtbewertung:** ⚠️ **60% Ready** - Funktioniert, aber nicht sicher konfiguriert

---

## 1. Server-Analyse (Windows .NET 8)

### 1.1 REST API (ApiHost.cs) - ✅ VOLLSTÄNDIG HTTPS

**Status:** **Production Ready - HTTPS aktiviert**

```csharp
// Zeile 96-104: ApiHost.cs
options.ListenAnyIP(port, listenOptions =>
{
    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    // Use development certificate for HTTPS
    // In production, configure proper SSL certificate
    listenOptions.UseHttps();  // ✅ SSL AKTIVIERT
});
```

**Konfiguration:**
- **Port:** 5001 (Fallbacks: 5002-5006)
- **Protokoll:** HTTPS (erzwungen)
- **Zertifikat:** ASP.NET Core Development Certificate
- **Produktionshinweis:** "In production, configure proper SSL certificate"

**Beurteilung:**
- ✅ Funktioniert bereits mit HTTPS
- ✅ Verwendet ASP.NET Core Development Cert
- ⚠️ Für Produktion echtes Zertifikat benötigt
- ✅ iOS App Transport Security (ATS) konform

---

### 1.2 WebSocket Server (WebSocketCommunicationService.cs) - ⚠️ SSL DEAKTIVIERT

**Status:** **Infrastruktur vorhanden, aber SSL standardmäßig AUS**

#### Aktuelle Konfiguration (appsettings.json):

```json
"ServerSettings": {
  "Port": 8080,
  "EnableSsl": false,  // ❌ SSL DEAKTIVIERT
  "CertificateThumbprint": null,
  "CertificatePath": null,
  "CertificatePassword": null,
  "EndpointPath": "/ws/"
}
```

#### Code-Analyse:

**SSL-Unterstützung im Code vorhanden:**

```csharp
// Zeile 86-92: WebSocketCommunicationService.cs
var ipProtocol = _settings.EnableSsl ? "https" : "http";
bindingAttempts.Add(($"{ipProtocol}://{ip}:{_settings.Port}{_settings.EndpointPath}", $"IP {ip}"));

// Zeile 160-163: GetWebSocketProtocol()
public string GetWebSocketProtocol()
{
    return EnableSsl ? "wss" : "ws";  // ✅ WSS-Support im Code
}
```

**Problem:** HttpListener in .NET unterstützt SSL nur eingeschränkt:

```csharp
// Zeile 28-29: WebSocketCommunicationService.cs
private HttpListener? _httpListener;  // ⚠️ HttpListener hat begrenzte SSL-Unterstützung
```

**SSL-Warnings vorhanden:**

```csharp
// Zeile 172-183: WebSocketCommunicationService.cs
if (_settings.EnableSsl)
{
    _logger.LogWarning("SSL/TLS is enabled. Ensure SSL certificate is properly configured.");
    _logger.LogWarning("For Windows: Use 'netsh http add sslcert' to bind certificate to port {Port}", _settings.Port);
    _logger.LogWarning("For production: Consider using a reverse proxy (nginx/IIS) for SSL termination");

    if (string.IsNullOrWhiteSpace(_settings.CertificateThumbprint) &&
        string.IsNullOrWhiteSpace(_settings.CertificatePath))
    {
        _logger.LogError("SSL enabled but no certificate configured. Server may fail to accept connections.");
    }
}
```

#### Beurteilung:
- ✅ Code ist WSS-ready (EnableSsl-Flag vorhanden)
- ❌ SSL ist standardmäßig deaktiviert (EnableSsl=false)
- ❌ **Kein CertificateService** zur automatischen Zertifikatsgenerierung
- ⚠️ HttpListener SSL-Support eingeschränkt (netsh http add sslcert erforderlich)
- ⚠️ Empfehlung: Reverse Proxy (nginx/IIS) für SSL Termination

---

### 1.3 Fehlende Komponente: CertificateService - ❌ NICHT VORHANDEN

**Suche nach CertificateService:**
```bash
$ find . -name "*CertificateService.cs"
# ERGEBNIS: Keine Datei gefunden
```

**Erwartete Funktionen (laut CLAUDE.md):**
- SSL certificate generation (self-signed für Development)
- Certificate binding für HttpListener
- Certificate renewal logic

**Status:** ❌ **SERVICE FEHLT KOMPLETT**

**Auswirkung:**
- Keine automatische Zertifikatsgenerierung
- Manuelle netsh-Konfiguration erforderlich
- Keine self-signed Zertifikate für Entwicklung
- Produktionsreife SSL-Implementierung fehlt

---

## 2. Client-Analyse

### 2.1 Python Client (Raspberry Pi) - ✅ WSS READY

**Status:** **WSS-fähig, akzeptiert self-signed Zertifikate**

#### Code-Analyse:

```python
# Zeile 394: client.py
ws_url = server_url.replace('http://', 'ws://').replace('https://', 'wss://')
```

**SSL-Konfiguration:**

```python
# Zeile 413-419: client.py
sslopt = None
if ws_url.startswith('wss://'):
    if not self.config.verify_ssl:
        import ssl
        sslopt = {"cert_reqs": ssl.CERT_NONE}  # ✅ Self-signed akzeptiert
        logger.warning("SSL certificate verification disabled (self-signed certificates accepted)")
```

**WebSocket-Verbindung:**

```python
# Zeile 422-428: client.py
self.ws_app = websocket.WebSocketApp(
    ws_url,
    on_open=self.on_open,
    on_message=self.on_message,
    on_error=self.on_error,
    on_close=self.on_close
)
```

**SSL Ping/Timeout:**

```python
# Zeile 432-436: client.py
target=lambda: self.ws_app.run_forever(
    sslopt=sslopt,  # ✅ SSL-Optionen übergeben
    ping_interval=30,
    ping_timeout=10
)
```

#### Beurteilung:
- ✅ Unterstützt WSS-Protokoll
- ✅ Akzeptiert self-signed Zertifikate (verify_ssl=False)
- ✅ Korrekte URL-Konvertierung (http→ws, https→wss)
- ✅ SSL-Error-Handling vorhanden
- ⚠️ Produktionsreife: SSL-Verifikation sollte aktivierbar sein

---

### 2.2 iOS App (Mobile) - ✅ WSS READY

**Status:** **WSS-fähig, akzeptiert self-signed Zertifikate**

#### Code-Analyse:

```csharp
// Zeile 162-165: WebSocketService.cs
_webSocket = new ClientWebSocket();

// Configure SSL options (allow self-signed certificates for development)
_webSocket.Options.RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true;
```

**Verbindung:**

```csharp
// Zeile 167-168: WebSocketService.cs
Console.WriteLine($"Connecting to WebSocket: {_webSocketUrl}");
await _webSocket.ConnectAsync(new Uri(_webSocketUrl), cancellationToken);
```

#### Beurteilung:
- ✅ Unterstützt WSS-Protokoll (ClientWebSocket)
- ✅ Akzeptiert self-signed Zertifikate (RemoteCertificateValidationCallback)
- ✅ Fehlerbehandlung vorhanden
- ⚠️ **Sicherheitswarnung:** RemoteCertificateValidationCallback = true akzeptiert ALLE Zertifikate
- ⚠️ Für Produktion: Certificate Pinning empfohlen

---

## 3. Kritische Sicherheitslücken

### 3.1 WebSocket Server SSL-Unterstützung - KRITISCH

**Problem:**
- HttpListener hat begrenzte SSL-Unterstützung
- Erfordert manuelle netsh-Konfiguration unter Windows
- Kein automatisches Certificate Binding

**Empfohlene Lösungen:**

**Option 1: Reverse Proxy (EMPFOHLEN für Produktion)**
```
[Clients] --HTTPS/WSS--> [nginx/IIS] --HTTP/WS--> [DigitalSignage Server]
```

Vorteile:
- ✅ Bewährte SSL-Implementierung
- ✅ Automatische Certificate Renewal (Let's Encrypt)
- ✅ Load Balancing möglich
- ✅ Keine Code-Änderungen erforderlich

**Option 2: CertificateService implementieren**
- Automatische self-signed Zertifikatsgenerierung
- netsh-Integration für Windows
- Certificate Store Management

**Option 3: ASP.NET Core Migration**
- WebSocket-Server auf ASP.NET Core Kestrel umstellen
- Verwendet gleiche SSL-Infrastruktur wie REST API
- Bessere SSL-Unterstützung als HttpListener

---

### 3.2 Self-Signed Certificate Acceptance - WARNUNG

**Alle Clients akzeptieren JEDES Zertifikat:**

**Python Client:**
```python
sslopt = {"cert_reqs": ssl.CERT_NONE}  # ⚠️ Keine Zertifikatsprüfung
```

**iOS App:**
```csharp
RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true;  // ⚠️ Akzeptiert alles
```

**Sicherheitsrisiko:**
- Man-in-the-Middle (MITM) Angriffe möglich
- Keine Authentifizierung des Servers
- Produktionsumgebung unsicher

**Empfehlung:**
1. **Development:** Self-signed OK mit pinning
2. **Production:** Echte Zertifikate (Let's Encrypt)
3. **Certificate Pinning:** SHA256-Hash des Server-Zertifikats in Clients hinterlegen

---

### 3.3 Fehlende Zertifikatsverwaltung - KRITISCH

**Aktueller Stand:**
- ❌ Kein CertificateService
- ❌ Keine automatische Zertifikatsgenerierung
- ❌ Keine Certificate Renewal Logic
- ❌ Keine zentrale Zertifikatsverwaltung

**Erforderlich:**
```csharp
public interface ICertificateService
{
    Task<X509Certificate2> GetOrCreateCertificateAsync();
    Task<bool> BindCertificateToPortAsync(int port);
    Task RenewCertificateAsync();
    Task<bool> ValidateCertificateAsync();
}
```

---

## 4. Implementierungsplan: Vollständige WSS-Aktivierung

### Phase 1: Basis-SSL-Unterstützung (SCHNELL)

**Ziel:** WebSocket-Server mit self-signed Zertifikaten

**Schritte:**
1. **CertificateService implementieren**
   - Self-signed Zertifikat generieren
   - netsh http add sslcert Integration
   - Certificate Store Management

2. **appsettings.json anpassen**
   ```json
   "ServerSettings": {
     "EnableSsl": true,  // ✅ AKTIVIEREN
     "CertificatePath": "./certs/server.pfx",
     "CertificatePassword": "ChangeMe123!"
   }
   ```

3. **HttpListener SSL-Binding**
   - Automatische netsh-Konfiguration beim Server-Start
   - Certificate-to-Port Binding

4. **Testing**
   - Python Client mit wss://
   - iOS App mit wss://
   - Zertifikatsakzeptanz prüfen

**Zeitaufwand:** 2-3 Tage

---

### Phase 2: Production-Ready SSL (MITTEL)

**Ziel:** Echte Zertifikate, sichere Konfiguration

**Schritte:**
1. **Let's Encrypt Integration**
   - ACME-Client implementieren
   - Automatische Certificate Renewal
   - DNS/HTTP Challenge Support

2. **Certificate Pinning in Clients**
   - SHA256-Hash des Server-Zertifikats
   - Pinning-Logik in Python Client
   - Pinning-Logik in iOS App

3. **Reverse Proxy Setup**
   - nginx/IIS Konfiguration
   - SSL Termination
   - Load Balancing

4. **Security Hardening**
   - TLS 1.2+ erzwingen
   - Cipher Suite Konfiguration
   - HSTS Headers

**Zeitaufwand:** 5-7 Tage

---

### Phase 3: Enterprise-Level Security (LANGFRISTIG)

**Ziel:** Vollständige Enterprise-Sicherheit

**Schritte:**
1. **Certificate Authority (CA) Integration**
   - Windows AD Certificate Services
   - Linux CA Integration
   - Zentrale Zertifikatsverwaltung

2. **Mutual TLS (mTLS)**
   - Client-Zertifikate für Pi-Clients
   - iOS App Client Certificates
   - Gegenseitige Authentifizierung

3. **Security Monitoring**
   - Certificate Expiry Monitoring
   - SSL/TLS Handshake Logging
   - Security Audit Logs

4. **Compliance**
   - GDPR-konforme Verschlüsselung
   - Audit-Trail
   - Security Documentation

**Zeitaufwand:** 10-15 Tage

---

## 5. Sofort umsetzbare Verbesserungen

### 5.1 CertificateService Implementierung (PRIORITÄT 1)

**Neue Datei:** `src/DigitalSignage.Server/Services/CertificateService.cs`

```csharp
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

public interface ICertificateService
{
    Task<X509Certificate2> GetOrCreateSelfSignedCertificateAsync();
    Task<bool> BindCertificateToPortAsync(int port, string certificateHash);
    bool IsCertificateBoundToPort(int port);
}

public class CertificateService : ICertificateService
{
    private readonly ILogger<CertificateService> _logger;
    private readonly string _certPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "certs");
    private readonly string _certFile = "server.pfx";
    private readonly string _certPassword = "DigitalSignage2024!";

    public CertificateService(ILogger<CertificateService> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(_certPath);
    }

    public async Task<X509Certificate2> GetOrCreateSelfSignedCertificateAsync()
    {
        var certFullPath = Path.Combine(_certPath, _certFile);

        // Check if certificate already exists
        if (File.Exists(certFullPath))
        {
            try
            {
                var existingCert = new X509Certificate2(certFullPath, _certPassword);

                // Check if certificate is still valid
                if (existingCert.NotAfter > DateTime.Now.AddDays(30))
                {
                    _logger.LogInformation("Using existing SSL certificate (expires: {Expiry})",
                        existingCert.NotAfter);
                    return existingCert;
                }
                else
                {
                    _logger.LogWarning("Existing certificate expires soon ({Expiry}), regenerating",
                        existingCert.NotAfter);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load existing certificate, will regenerate");
            }
        }

        // Generate new self-signed certificate
        _logger.LogInformation("Generating new self-signed SSL certificate...");

        var distinguishedName = new X500DistinguishedName($"CN=DigitalSignage Server");

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Add Subject Alternative Names
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName(Environment.MachineName);
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);

        // Add local IP addresses
        var localIPs = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName())
            .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        foreach (var ip in localIPs)
        {
            sanBuilder.AddIpAddress(ip);
        }

        request.CertificateExtensions.Add(sanBuilder.Build());

        // Set certificate as server authentication
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                critical: false));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: false));

        // Create self-signed certificate (valid for 1 year)
        var certificate = request.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddYears(1));

        // Export to PFX with private key
        var pfxBytes = certificate.Export(X509ContentType.Pfx, _certPassword);
        await File.WriteAllBytesAsync(certFullPath, pfxBytes);

        _logger.LogInformation("SSL certificate generated successfully");
        _logger.LogInformation("  Subject: {Subject}", certificate.Subject);
        _logger.LogInformation("  Thumbprint: {Thumbprint}", certificate.Thumbprint);
        _logger.LogInformation("  Valid from: {From} to {To}", certificate.NotBefore, certificate.NotAfter);
        _logger.LogInformation("  Saved to: {Path}", certFullPath);

        return new X509Certificate2(certFullPath, _certPassword);
    }

    public async Task<bool> BindCertificateToPortAsync(int port, string certificateHash)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("Certificate binding is only supported on Windows");
            return false;
        }

        try
        {
            // Check if already bound
            if (IsCertificateBoundToPort(port))
            {
                _logger.LogInformation("Certificate already bound to port {Port}", port);
                return true;
            }

            // netsh http add sslcert ipport=0.0.0.0:PORT certhash=HASH appid={GUID}
            var appId = "{12345678-1234-1234-1234-123456789012}"; // Fixed GUID for this app
            var command = $"http add sslcert ipport=0.0.0.0:{port} certhash={certificateHash} appid={appId}";

            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = command,
                UseShellExecute = true,
                Verb = "runas", // Run as administrator
                CreateNoWindow = true
            };

            _logger.LogInformation("Binding SSL certificate to port {Port}...", port);
            _logger.LogDebug("Running: netsh {Command}", command);

            var process = System.Diagnostics.Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("Successfully bound SSL certificate to port {Port}", port);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to bind certificate (exit code: {ExitCode})", process.ExitCode);
                    return false;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error binding certificate to port {Port}", port);
            return false;
        }
    }

    public bool IsCertificateBoundToPort(int port)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"http show sslcert ipport=0.0.0.0:{port}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = System.Diagnostics.Process.Start(processInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Contains("IP:port") && !output.Contains("not found");
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
```

---

### 5.2 appsettings.json Update (PRIORITÄT 2)

```json
{
  "ServerSettings": {
    "Port": 8080,
    "AutoSelectPort": true,
    "AlternativePorts": [ 8081, 8082, 8083, 8888, 9000 ],
    "EnableSsl": true,  // ✅ AKTIVIERT
    "CertificateThumbprint": null,  // Auto-generiert via CertificateService
    "CertificatePath": "./certs/server.pfx",  // ✅ Pfad gesetzt
    "CertificatePassword": "DigitalSignage2024!",  // ✅ Passwort gesetzt
    "EndpointPath": "/ws/",
    "MaxMessageSize": 1048576,
    "ClientHeartbeatTimeout": 90
  }
}
```

---

### 5.3 Dependency Injection Setup (PRIORITÄT 3)

**Datei:** `src/DigitalSignage.Server/App.xaml.cs`

```csharp
// In ConfigureServices():
services.AddSingleton<ICertificateService, CertificateService>();
```

**Datei:** `src/DigitalSignage.Server/Services/WebSocketCommunicationService.cs`

```csharp
// Constructor Injection:
private readonly ICertificateService _certificateService;

public WebSocketCommunicationService(
    ILogger<WebSocketCommunicationService> logger,
    ILogger<WebSocketMessageSerializer> serializerLogger,
    ServerSettings settings,
    IServiceProvider serviceProvider,
    ICertificateService certificateService)  // ✅ Neu
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    _certificateService = certificateService ?? throw new ArgumentNullException(nameof(certificateService));
    _messageSerializer = new WebSocketMessageSerializer(serializerLogger, enableCompression: true);
}

// In StartAsync():
if (_settings.EnableSsl)
{
    var cert = await _certificateService.GetOrCreateSelfSignedCertificateAsync();
    await _certificateService.BindCertificateToPortAsync(_settings.Port, cert.Thumbprint);
}
```

---

## 6. Zusammenfassung und Empfehlungen

### Aktueller Stand

| Komponente | Status | Bewertung |
|------------|--------|-----------|
| REST API | ✅ HTTPS aktiviert | Production Ready |
| WebSocket Server | ⚠️ SSL deaktiviert | Infrastruktur vorhanden |
| Python Client | ✅ WSS-fähig | Akzeptiert self-signed |
| iOS App | ✅ WSS-fähig | Akzeptiert self-signed |
| Zertifikatsverwaltung | ❌ Fehlt | Kritische Lücke |

### Sofortige Maßnahmen (KRITISCH)

1. **CertificateService implementieren** (siehe 5.1)
2. **EnableSsl=true setzen** in appsettings.json
3. **Testing mit WSS-URLs** durchführen

### Mittelfristige Maßnahmen (WICHTIG)

1. **Reverse Proxy Setup** (nginx/IIS) für Production
2. **Certificate Pinning** in Clients implementieren
3. **Let's Encrypt Integration** für echte Zertifikate

### Langfristige Maßnahmen (EMPFOHLEN)

1. **Mutual TLS (mTLS)** für Client-Authentifizierung
2. **ASP.NET Core Migration** des WebSocket-Servers
3. **Enterprise CA Integration**

---

## 7. Risikobewertung

### Hohe Risiken (SOFORT BEHEBEN)

1. ❌ **Keine Verschlüsselung** - WebSocket-Traffic unverschlüsselt
2. ❌ **Fehlende Zertifikatsverwaltung** - Manuelle Konfiguration erforderlich
3. ❌ **Self-signed ohne Pinning** - MITM-Angriffe möglich

### Mittlere Risiken (BALD BEHEBEN)

1. ⚠️ **Development Certificates** - Nicht für Produktion geeignet
2. ⚠️ **HttpListener SSL-Limitierungen** - Eingeschränkte Unterstützung
3. ⚠️ **Keine Certificate Renewal** - Manuelle Erneuerung nötig

### Niedrige Risiken (LANGFRISTIG)

1. ℹ️ **Fehlende mTLS** - Client-Authentifizierung nur via Token
2. ℹ️ **Keine CA-Integration** - Keine zentrale Verwaltung
3. ℹ️ **Monitoring fehlt** - Keine Zertifikatsüberwachung

---

## Anhang: Hilfreiche Kommandos

### Windows (netsh)

```powershell
# Zertifikat an Port binden
netsh http add sslcert ipport=0.0.0.0:8080 certhash=THUMBPRINT appid={GUID}

# Binding anzeigen
netsh http show sslcert

# Binding entfernen
netsh http delete sslcert ipport=0.0.0.0:8080
```

### OpenSSL (Certificate Generation)

```bash
# Self-signed Zertifikat generieren
openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 365 -nodes

# PFX erstellen
openssl pkcs12 -export -out server.pfx -inkey key.pem -in cert.pem
```

### Testing

```bash
# Python Client Test
python3 client.py --server wss://192.168.0.100:8080/ws/

# OpenSSL WebSocket Test
openssl s_client -connect localhost:8080 -servername localhost
```

---

**Ende der Analyse**
