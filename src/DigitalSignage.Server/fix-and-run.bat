@echo off
echo ============================================
echo Digital Signage Server - Quick Fix and Run
echo ============================================
echo.

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo WARNING: Not running as administrator
    echo Some fixes may not work without admin privileges
    echo.
    timeout /t 3 >nul
)

echo [1/5] Stopping any processes using port 8080...
for /f "tokens=5" %%a in ('netstat -aon ^| find ":8080" ^| find "LISTENING"') do (
    echo   Found process using port 8080: %%a
    taskkill /F /PID %%a 2>nul
    if errorlevel 1 (
        echo   WARNING: Could not stop process %%a
    ) else (
        echo   Process %%a stopped
    )
)
echo   Done

echo.
echo [2/5] Removing old error logs...
if exist startup-error.txt (
    del startup-error.txt
    echo   Removed startup-error.txt
)
if exist digitalsignage.db-wal (
    echo   Found SQLite WAL file, attempting to remove...
    del digitalsignage.db-wal 2>nul
)
if exist digitalsignage.db-shm (
    echo   Found SQLite SHM file, attempting to remove...
    del digitalsignage.db-shm 2>nul
)
echo   Done

echo.
echo [3/5] Cleaning build artifacts...
dotnet clean >nul 2>&1
if errorlevel 1 (
    echo   WARNING: Clean failed, continuing anyway...
) else (
    echo   Done
)

echo.
echo [4/5] Restoring packages and building...
echo   Restoring packages...
dotnet restore
if errorlevel 1 (
    echo.
    echo   ERROR: Package restore failed!
    echo   Please check your internet connection and NuGet sources
    pause
    exit /b 1
)

echo   Building project...
dotnet build
if errorlevel 1 (
    echo.
    echo   ERROR: Build failed!
    echo   Please review the build errors above
    pause
    exit /b 1
)
echo   Done

echo.
echo [5/5] Starting server...
echo.
echo ============================================
echo Starting Digital Signage Server...
echo ============================================
echo.
echo If the server fails to start, check:
echo - logs/digitalsignage-*.log for detailed logs
echo - startup-error.txt if it exists
echo - Run diagnose-server.ps1 for diagnostics
echo.

dotnet run

REM If we get here, the server exited
echo.
echo ============================================
echo Server has stopped
echo ============================================
echo.

REM Check for emergency error log
if exist startup-error.txt (
    echo STARTUP ERROR DETECTED!
    echo.
    type startup-error.txt
    echo.
    echo Run diagnose-server.ps1 for detailed diagnostics
)

pause
