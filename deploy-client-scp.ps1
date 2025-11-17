# Deploy Python client files to Raspberry Pi via SCP
# This copies updated Python files directly to /opt/digitalsignage-client

$RASPI_IP = "192.168.0.178"
$RASPI_USER = "pro"
$RASPI_DIR = "/opt/digitalsignage-client"
$LOCAL_DIR = "src\DigitalSignage.Client.RaspberryPi"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "DEPLOYING PYTHON CLIENT TO RASPBERRY PI" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# Check if scp is available
$scpPath = Get-Command scp -ErrorAction SilentlyContinue
if (-not $scpPath) {
    Write-Host "ERROR: scp not found. OpenSSH Client required." -ForegroundColor Red
    Write-Host ""
    Write-Host "Install with: Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0" -ForegroundColor Yellow
    exit 1
}

# Files to deploy
$filesToDeploy = @(
    "client.py",
    "display_renderer.py",
    "status_screen.py",
    "config.py",
    "cache_manager.py",
    "burn_in_protection.py",
    "config_txt_manager.py"
)

Write-Host "[1/3] Copying Python files to Raspberry Pi..." -ForegroundColor Cyan
Write-Host ""

foreach ($file in $filesToDeploy) {
    $localFile = Join-Path $LOCAL_DIR $file
    
    if (Test-Path $localFile) {
        Write-Host "  ? Copying: $file" -ForegroundColor Yellow
        scp -o StrictHostKeyChecking=no "$localFile" "${RASPI_USER}@${RASPI_IP}:${RASPI_DIR}/"
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "    ? Success" -ForegroundColor Green
        } else {
            Write-Host "    ? Failed" -ForegroundColor Red
        }
    } else {
        Write-Host "  ? File not found: $localFile" -ForegroundColor Red
    }
    Write-Host ""
}

Write-Host "[2/3] Restarting Digital Signage Client service..." -ForegroundColor Cyan
ssh $RASPI_USER@$RASPI_IP "sudo systemctl restart digitalsignage-client"
Start-Sleep -Seconds 2

Write-Host ""
Write-Host "[3/3] Checking service status..." -ForegroundColor Cyan
ssh $RASPI_USER@$RASPI_IP "systemctl status digitalsignage-client --no-pager | head -20"

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "? DEPLOYMENT COMPLETE!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Changes deployed:" -ForegroundColor Yellow
Write-Host "  • Fixed PNG scaling (now uses KeepAspectRatioByExpanding)" -ForegroundColor Cyan
Write-Host "  • Fixed 'wrapped C/C++ object deleted' error" -ForegroundColor Cyan
Write-Host "  • Optimized status screen scaling" -ForegroundColor Cyan
Write-Host "  • Fixed DbUpdateConcurrencyException on server" -ForegroundColor Cyan
Write-Host ""
Write-Host "View live logs:" -ForegroundColor Yellow
Write-Host "  ssh $RASPI_USER@$RASPI_IP 'sudo journalctl -u digitalsignage-client -f'" -ForegroundColor Cyan
Write-Host ""
