#!/usr/bin/env pwsh
# Digital Signage Server Discovery Test
# Tests if the server is discoverable via UDP and can be reached

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Digital Signage Discovery Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check if UDP port 5555 is listening
Write-Host "[Test 1] Checking UDP Discovery Port 5555..." -ForegroundColor Yellow
$udpConnections = Get-NetUDPEndpoint -LocalPort 5555 -ErrorAction SilentlyContinue
if ($udpConnections) {
    Write-Host "✓ UDP Port 5555 is LISTENING" -ForegroundColor Green
    Write-Host "  Process: $($udpConnections[0].OwningProcess)" -ForegroundColor Gray
} else {
    Write-Host "✗ UDP Port 5555 is NOT listening" -ForegroundColor Red
    Write-Host "  Server discovery service not running!" -ForegroundColor Red
}
Write-Host ""

# Test 2: Check if TCP port 8080 is listening
Write-Host "[Test 2] Checking WebSocket Port 8080..." -ForegroundColor Yellow
$tcpConnections = Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue
if ($tcpConnections) {
    Write-Host "✓ TCP Port 8080 is LISTENING" -ForegroundColor Green
    Write-Host "  Process: $($tcpConnections[0].OwningProcess)" -ForegroundColor Gray
} else {
    Write-Host "✗ TCP Port 8080 is NOT listening" -ForegroundColor Red
    Write-Host "  Server not running!" -ForegroundColor Red
}
Write-Host ""

# Test 3: Check Firewall Rules
Write-Host "[Test 3] Checking Firewall Rules..." -ForegroundColor Yellow
$firewallRules = @(
    "Digital Signage - UDP Discovery",
    "Digital Signage - mDNS",
    "Digital Signage - WebSocket"
)

foreach ($ruleName in $firewallRules) {
    $rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    if ($rule -and $rule.Enabled -eq $true) {
        Write-Host "  ✓ $ruleName - ENABLED" -ForegroundColor Green
    } elseif ($rule -and $rule.Enabled -eq $false) {
        Write-Host "  ⚠ $ruleName - EXISTS but DISABLED" -ForegroundColor Yellow
    } else {
        Write-Host "  ✗ $ruleName - NOT FOUND" -ForegroundColor Red
    }
}
Write-Host ""

# Test 4: Get Local IP Addresses
Write-Host "[Test 4] Local IP Addresses..." -ForegroundColor Yellow
$adapters = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -ne "127.0.0.1" }
foreach ($adapter in $adapters) {
    Write-Host "  - $($adapter.IPAddress) (Interface: $($adapter.InterfaceAlias))" -ForegroundColor Cyan
}
Write-Host ""

# Test 5: Send UDP Discovery Request
Write-Host "[Test 5] Sending UDP Discovery Request..." -ForegroundColor Yellow
try {
    $udpClient = New-Object System.Net.Sockets.UdpClient
    $udpClient.Client.ReceiveTimeout = 3000

    # Send broadcast
    $endpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Broadcast, 5555)
    $message = [System.Text.Encoding]::UTF8.GetBytes("DIGITALSIGNAGE_DISCOVER")

    Write-Host "  Sending broadcast to 255.255.255.255:5555..." -ForegroundColor Gray
    $udpClient.Send($message, $message.Length, $endpoint) | Out-Null

    # Wait for response
    Write-Host "  Waiting for response (3 seconds)..." -ForegroundColor Gray
    $remoteEP = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)

    try {
        $response = $udpClient.Receive([ref]$remoteEP)
        $responseText = [System.Text.Encoding]::UTF8.GetString($response)

        Write-Host "✓ RESPONSE RECEIVED from $($remoteEP.Address):$($remoteEP.Port)" -ForegroundColor Green
        Write-Host ""
        Write-Host "Response Data:" -ForegroundColor Cyan
        Write-Host $responseText -ForegroundColor White

        # Try to parse as JSON
        try {
            $json = $responseText | ConvertFrom-Json
            Write-Host ""
            Write-Host "Parsed Server Info:" -ForegroundColor Cyan
            Write-Host "  Server Name: $($json.ServerName)" -ForegroundColor White
            Write-Host "  Port: $($json.Port)" -ForegroundColor White
            Write-Host "  Protocol: $($json.Protocol)" -ForegroundColor White
            Write-Host "  SSL Enabled: $($json.SslEnabled)" -ForegroundColor White
            Write-Host "  IPs: $($json.LocalIPs -join ', ')" -ForegroundColor White
        } catch {
            Write-Host "  (Could not parse as JSON)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "✗ NO RESPONSE (timeout after 3 seconds)" -ForegroundColor Red
        Write-Host "  Server discovery service is not responding!" -ForegroundColor Red
    }

    $udpClient.Close()
} catch {
    Write-Host "✗ FAILED to send discovery request" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
}
Write-Host ""

# Test 6: Check Server Process
Write-Host "[Test 6] Checking Server Process..." -ForegroundColor Yellow
$serverProcess = Get-Process | Where-Object { $_.ProcessName -like "*DigitalSignage*" -or $_.ProcessName -like "*dotnet*" }
if ($serverProcess) {
    Write-Host "✓ Found server process(es):" -ForegroundColor Green
    foreach ($proc in $serverProcess) {
        Write-Host "  - $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor Gray
    }
} else {
    Write-Host "✗ No server process found" -ForegroundColor Red
    Write-Host "  Is the Digital Signage Server running?" -ForegroundColor Red
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$allGood = $true

if (-not $udpConnections) {
    Write-Host "✗ UDP Discovery Port NOT listening" -ForegroundColor Red
    $allGood = $false
} else {
    Write-Host "✓ UDP Discovery Port listening" -ForegroundColor Green
}

if (-not $tcpConnections) {
    Write-Host "✗ WebSocket Port NOT listening" -ForegroundColor Red
    $allGood = $false
} else {
    Write-Host "✓ WebSocket Port listening" -ForegroundColor Green
}

if ($allGood) {
    Write-Host ""
    Write-Host "SERVER IS DISCOVERABLE!" -ForegroundColor Green -BackgroundColor Black
    Write-Host "Clients should be able to find this server." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "SERVER IS NOT DISCOVERABLE!" -ForegroundColor Red -BackgroundColor Black
    Write-Host "Fix the issues above and try again." -ForegroundColor Red
}
Write-Host ""
