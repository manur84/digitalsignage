# Deploy using basic PowerShell commands (no plink/WSL required)
# This creates a batch script that can be run manually

$RASPI_IP = "192.168.0.178"
$RASPI_USER = "pro"
$CLIENT_DIR = "/opt/digitalsignage-client"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "RASPBERRY PI DEPLOYMENT INSTRUCTIONS" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Since SSH tools are not installed, please follow these steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "OPTION 1: Use PuTTY" -ForegroundColor Green
Write-Host "-----------------" -ForegroundColor Green
Write-Host "1. Open PuTTY" -ForegroundColor White
Write-Host "2. Connect to: $RASPI_IP" -ForegroundColor White
Write-Host "3. Login as: $RASPI_USER" -ForegroundColor White
Write-Host "4. Run these commands:" -ForegroundColor White
Write-Host ""
Write-Host "   cd /opt/digitalsignage-client" -ForegroundColor Cyan
Write-Host "   git pull origin main" -ForegroundColor Cyan
Write-Host "   sudo systemctl restart digitalsignage-client" -ForegroundColor Cyan
Write-Host "   sudo journalctl -u digitalsignage-client -f" -ForegroundColor Cyan
Write-Host ""
Write-Host ""
Write-Host "OPTION 2: Install OpenSSH Client" -ForegroundColor Green
Write-Host "--------------------------------" -ForegroundColor Green
Write-Host "1. Open PowerShell as Administrator" -ForegroundColor White
Write-Host "2. Run: Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0" -ForegroundColor Cyan
Write-Host "3. Then run this script again" -ForegroundColor White
Write-Host ""
Write-Host ""
Write-Host "OPTION 3: Use This Script (copy/paste)" -ForegroundColor Green
Write-Host "---------------------------------------" -ForegroundColor Green
Write-Host ""

# Check if ssh is available
$sshPath = Get-Command ssh -ErrorAction SilentlyContinue
if ($sshPath) {
    Write-Host "? OpenSSH Client is installed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Running deployment now..." -ForegroundColor Cyan
    Write-Host ""
    
    Write-Host "[1/4] Pulling latest code..." -ForegroundColor Cyan
    ssh -o StrictHostKeyChecking=no $RASPI_USER@$RASPI_IP "cd $CLIENT_DIR && git pull origin main"
    
    Write-Host ""
    Write-Host "[2/4] Checking files..." -ForegroundColor Cyan
    ssh $RASPI_USER@$RASPI_IP "cd $CLIENT_DIR && ls -la *.py | head -5"
    
    Write-Host ""
    Write-Host "[3/4] Restarting service..." -ForegroundColor Cyan
    ssh $RASPI_USER@$RASPI_IP "sudo systemctl restart digitalsignage-client"
    
    Write-Host ""
    Write-Host "[4/4] Checking status..." -ForegroundColor Cyan
    ssh $RASPI_USER@$RASPI_IP "systemctl status digitalsignage-client --no-pager | head -15"
    
    Write-Host ""
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host "? DEPLOYMENT COMPLETE!" -ForegroundColor Green
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "View live logs:" -ForegroundColor Yellow
    Write-Host "  ssh $RASPI_USER@$RASPI_IP 'sudo journalctl -u digitalsignage-client -f'" -ForegroundColor Cyan
} else {
    Write-Host "? OpenSSH Client not found" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please use Option 1 (PuTTY) or install OpenSSH Client (Option 2)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Copy these commands to run on the Raspberry Pi:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "cd /opt/digitalsignage-client" -ForegroundColor Cyan
    Write-Host "git pull origin main" -ForegroundColor Cyan
    Write-Host "sudo systemctl restart digitalsignage-client" -ForegroundColor Cyan
    Write-Host "sudo journalctl -u digitalsignage-client -f" -ForegroundColor Cyan
}

Write-Host ""
