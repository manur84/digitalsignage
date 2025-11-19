@echo off
echo ========================================
echo FORCE FIX BUILD - Hard Reset Solution
echo ========================================
echo.
echo This will:
echo 1. Hard reset your working directory
echo 2. Force pull from GitHub
echo 3. Clean all build artifacts
echo.
echo IMPORTANT: Any uncommitted changes will be LOST!
echo.
pause

cd /d "%~dp0"

echo.
echo [1/5] Resetting Git working directory...
git reset --hard HEAD
git clean -fd

echo.
echo [2/5] Fetching latest from GitHub...
git fetch origin

echo.
echo [3/5] Hard reset to remote branch...
git reset --hard origin/claude/fix-bugs-from-list-014T3FseE2sLEAjPMn8CQF7y

echo.
echo [4/5] Cleaning build artifacts...
if exist "src\DigitalSignage.Data\bin" rd /s /q "src\DigitalSignage.Data\bin"
if exist "src\DigitalSignage.Data\obj" rd /s /q "src\DigitalSignage.Data\obj"
if exist "src\DigitalSignage.Server\bin" rd /s /q "src\DigitalSignage.Server\bin"
if exist "src\DigitalSignage.Server\obj" rd /s /q "src\DigitalSignage.Server\obj"
if exist "src\DigitalSignage.Core\bin" rd /s /q "src\DigitalSignage.Core\bin"
if exist "src\DigitalSignage.Core\obj" rd /s /q "src\DigitalSignage.Core\obj"
if exist ".vs" rd /s /q ".vs"

echo.
echo [5/5] Verifying fix...
echo.
findstr /n "using DigitalSignage.Core.Utilities" "src\DigitalSignage.Data\Services\SqlDataService.cs"

echo.
echo ========================================
echo DONE!
echo ========================================
echo.
echo Now:
echo 1. Open Visual Studio
echo 2. Build -^> Rebuild Solution
echo.
echo The error should be GONE!
echo.
pause
