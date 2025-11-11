# Build script for Digital Signage System
# PowerShell script to build all projects

param(
    [string]$Configuration = "Release",
    [switch]$SkipTests,
    [switch]$Publish
)

Write-Host "Digital Signage Build Script" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"
$SolutionDir = Split-Path -Parent $PSScriptRoot

# Navigate to solution directory
Set-Location $SolutionDir

# Restore NuGet packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to restore packages" -ForegroundColor Red
    exit 1
}

# Build solution
Write-Host ""
Write-Host "Building solution ($Configuration)..." -ForegroundColor Yellow
dotnet build --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed" -ForegroundColor Red
    exit 1
}

# Run tests
if (-not $SkipTests) {
    Write-Host ""
    Write-Host "Running tests..." -ForegroundColor Yellow
    dotnet test --configuration $Configuration --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Tests failed" -ForegroundColor Red
        exit 1
    }
}

# Publish
if ($Publish) {
    Write-Host ""
    Write-Host "Publishing application..." -ForegroundColor Yellow

    $PublishDir = Join-Path $SolutionDir "publish"

    # Publish Windows Server
    Write-Host "Publishing Windows Server..." -ForegroundColor Yellow
    dotnet publish src/DigitalSignage.Server/DigitalSignage.Server.csproj `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained true `
        --output "$PublishDir/DigitalSignage.Server" `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Publish failed" -ForegroundColor Red
        exit 1
    }

    # Copy Python client
    Write-Host "Copying Python client..." -ForegroundColor Yellow
    $ClientSrc = Join-Path $SolutionDir "src/DigitalSignage.Client.RaspberryPi"
    $ClientDest = Join-Path $PublishDir "DigitalSignage.Client.RaspberryPi"
    Copy-Item -Path $ClientSrc -Destination $ClientDest -Recurse -Force

    Write-Host ""
    Write-Host "Published to: $PublishDir" -ForegroundColor Green
}

Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
