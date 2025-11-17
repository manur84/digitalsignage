# Deploy to Raspberry Pi Digital Signage Client
# Usage: .\deploy-to-raspi.ps1

$RASPI_IP = "192.168.0.178"
$RASPI_USER = "pro"
$RASPI_PASS = "mr412393"
$CLIENT_DIR = "/opt/digitalsignage-client"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "DEPLOYING TO RASPBERRY PI DIGITAL SIGNAGE CLIENT" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "Target: $RASPI_USER@$RASPI_IP" -ForegroundColor Yellow
Write-Host "Directory: $CLIENT_DIR" -ForegroundColor Yellow
Write-Host ""

# Check if plink is available (PuTTY SSH client)
$plinkPath = Get-Command plink -ErrorAction SilentlyContinue
if (-not $plinkPath) {
    Write-Host "ERROR: plink not found. Please install PuTTY or use WSL." -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternative: Use WSL (Windows Subsystem for Linux):" -ForegroundColor Yellow
    Write-Host "  wsl ssh $RASPI_USER@$RASPI_IP" -ForegroundColor Green
    exit 1
}

Write-Host "[1/5] Connecting to Raspberry Pi..." -ForegroundColor Cyan

# Pull latest code from Git
Write-Host "[2/5] Pulling latest code from GitHub..." -ForegroundColor Cyan
$gitPullCmd = "cd $CLIENT_DIR && git pull origin main"
$output = echo y | plink -ssh -pw $RASPI_PASS $RASPI_USER@$RASPI_IP $gitPullCmd 2>&1
Write-Host $output

# Check Python client files
Write-Host "[3/5] Checking Python client files..." -ForegroundColor Cyan
$checkCmd = "cd $CLIENT_DIR && ls -la *.py | head -10"
$output = echo y | plink -ssh -pw $RASPI_PASS $RASPI_USER@$RASPI_IP $checkCmd 2>&1
Write-Host $output

# Restart client service
Write-Host "[4/5] Restarting Digital Signage Client service..." -ForegroundColor Cyan
$restartCmd = "sudo systemctl restart digitalsignage-client"
$output = echo y | plink -ssh -pw $RASPI_PASS $RASPI_USER@$RASPI_IP $restartCmd 2>&1
Write-Host $output

# Check service status
Write-Host "[5/5] Checking service status..." -ForegroundColor Cyan
$statusCmd = "systemctl status digitalsignage-client --no-pager | head -20"
$output = echo y | plink -ssh -pw $RASPI_PASS $RASPI_USER@$RASPI_IP $statusCmd 2>&1
Write-Host $output

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "DEPLOYMENT COMPLETE" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "View logs with:" -ForegroundColor Yellow
Write-Host "  ssh $RASPI_USER@$RASPI_IP" -ForegroundColor Green
Write-Host "  sudo journalctl -u digitalsignage-client -f" -ForegroundColor Green
Write-Host ""
