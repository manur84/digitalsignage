# PowerShell Script to Create/Reset Digital Signage Database
# Run this from the repository root directory

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Digital Signage - Database Setup" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Check if we're in the right directory
if (!(Test-Path "DigitalSignage.sln")) {
    Write-Host "ERROR: Please run this script from the repository root directory!" -ForegroundColor Red
    Write-Host "Current directory: $(Get-Location)" -ForegroundColor Yellow
    pause
    exit 1
}

# Navigate to Data project
Set-Location "src\DigitalSignage.Data"

Write-Host "Installing/Updating dotnet-ef tool..." -ForegroundColor Yellow
dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    dotnet tool install --global dotnet-ef
}

Write-Host ""
Write-Host "Checking for existing database..." -ForegroundColor Yellow

# Check if database exists in Server directory
$serverDbPath = "..\DigitalSignage.Server\digitalsignage.db"
$rootDbPath = "..\..\digitalsignage.db"

if (Test-Path $serverDbPath) {
    Write-Host "Found existing database at: $serverDbPath" -ForegroundColor Green
    $response = Read-Host "Do you want to DELETE and recreate the database? (yes/no)"
    if ($response -eq "yes") {
        Write-Host "Deleting existing database files..." -ForegroundColor Red
        Remove-Item $serverDbPath -Force
        Remove-Item "$serverDbPath-shm" -Force -ErrorAction SilentlyContinue
        Remove-Item "$serverDbPath-wal" -Force -ErrorAction SilentlyContinue
        Write-Host "Database deleted." -ForegroundColor Green
    } else {
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        Set-Location ..\..
        pause
        exit 0
    }
}

if (Test-Path $rootDbPath) {
    Write-Host "Found existing database at: $rootDbPath" -ForegroundColor Green
    $response = Read-Host "Do you want to DELETE and recreate the database? (yes/no)"
    if ($response -eq "yes") {
        Write-Host "Deleting existing database files..." -ForegroundColor Red
        Remove-Item $rootDbPath -Force
        Remove-Item "$rootDbPath-shm" -Force -ErrorAction SilentlyContinue
        Remove-Item "$rootDbPath-wal" -Force -ErrorAction SilentlyContinue
        Write-Host "Database deleted." -ForegroundColor Green
    } else {
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        Set-Location ..\..
        pause
        exit 0
    }
}

Write-Host ""
Write-Host "Creating database and applying migrations..." -ForegroundColor Yellow
Write-Host "This may take a few moments..." -ForegroundColor Gray

# Apply migrations
dotnet ef database update --startup-project ..\DigitalSignage.Server\DigitalSignage.Server.csproj

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Green
    Write-Host "SUCCESS! Database created successfully" -ForegroundColor Green
    Write-Host "======================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Database location:" -ForegroundColor Cyan

    # Check where the database was created
    if (Test-Path $serverDbPath) {
        Write-Host "  $serverDbPath" -ForegroundColor White
    } elseif (Test-Path $rootDbPath) {
        Write-Host "  $rootDbPath" -ForegroundColor White
    } else {
        Write-Host "  Database created, but location unknown. Check DigitalSignage.Server directory." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Start the application: dotnet run --project src\DigitalSignage.Server\DigitalSignage.Server.csproj" -ForegroundColor White
    Write-Host "  2. The database will be seeded with default data on first startup" -ForegroundColor White
    Write-Host "  3. Default admin credentials will be logged in the console" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Red
    Write-Host "ERROR: Database creation failed!" -ForegroundColor Red
    Write-Host "======================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common solutions:" -ForegroundColor Yellow
    Write-Host "  1. Make sure no application is using the database file" -ForegroundColor White
    Write-Host "  2. Check that you have write permissions to the directory" -ForegroundColor White
    Write-Host "  3. Close Visual Studio if it's open" -ForegroundColor White
    Write-Host "  4. Check the error message above for specific details" -ForegroundColor White
}

Write-Host ""
Set-Location ..\..
pause
