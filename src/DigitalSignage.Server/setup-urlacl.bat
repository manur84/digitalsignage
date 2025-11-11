@echo off
echo ============================================
echo Digital Signage Server - URL ACL Setup
echo ============================================
echo.
echo This script will configure Windows to allow the
echo Digital Signage Server to run without Administrator
echo privileges.
echo.
echo This requires Administrator privileges.
echo.
pause

:: Check for admin privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo.
    echo Right-click this file and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

:: Run PowerShell script
PowerShell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-urlacl.ps1"

pause
