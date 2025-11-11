#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Configures Windows HTTP URL ACL for Digital Signage Server
.DESCRIPTION
    This script must be run as Administrator once to allow the Digital Signage Server
    to bind to HTTP URLs without requiring admin privileges.
.EXAMPLE
    .\setup-urlacl.ps1
#>

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Digital Signage Server - URL ACL Setup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    Write-Host "Then run this script again." -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

# Get current user
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
Write-Host "Current User: $currentUser" -ForegroundColor Gray
Write-Host ""

# Read port from appsettings.json
$port = 8080
if (Test-Path "appsettings.json") {
    try {
        $settings = Get-Content "appsettings.json" | ConvertFrom-Json
        if ($settings.ServerSettings.Port) {
            $port = $settings.ServerSettings.Port
        }
    } catch {
        Write-Host "Warning: Could not read port from appsettings.json, using default 8080" -ForegroundColor Yellow
    }
}

Write-Host "Configuring URL ACL for port: $port" -ForegroundColor Cyan
Write-Host ""

# URLs to register
$urls = @(
    "http://+:$port/ws/",
    "http://+:$port/"
)

foreach ($url in $urls) {
    Write-Host "Checking URL: $url" -ForegroundColor Yellow

    # Check if URL ACL already exists
    $existing = netsh http show urlacl url=$url 2>$null
    if ($LASTEXITCODE -eq 0 -and $existing -match "Reserved URL") {
        Write-Host "  Already configured, removing old entry..." -ForegroundColor Gray
        netsh http delete urlacl url=$url | Out-Null
    }

    # Add URL ACL
    Write-Host "  Registering URL ACL..." -ForegroundColor Gray
    # Use SID S-1-1-0 which is "Everyone" on all Windows language versions (Jeder, Tout le monde, etc.)
    $result = netsh http add urlacl url=$url sddl=D:(A;;GX;;;S-1-1-0)

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  OK: URL ACL registered successfully" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: Failed to register URL ACL" -ForegroundColor Red
        Write-Host "  $result" -ForegroundColor Red
    }
    Write-Host ""
}

# Verify configuration
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Verification" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Registered URL ACLs for port $port:" -ForegroundColor Yellow
netsh http show urlacl | Select-String -Pattern ":$port" -Context 0,3

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "You can now run Digital Signage Server without Administrator privileges." -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Close this PowerShell window" -ForegroundColor White
Write-Host "  2. Run the server normally (no admin needed)" -ForegroundColor White
Write-Host "  3. If you change the port in appsettings.json, run this script again" -ForegroundColor White
Write-Host ""

pause
