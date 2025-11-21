@echo off
setlocal EnableDelayedExpansion

REM ============================================
REM Digital Signage Server - URL ACL Setup for Specific IP
REM Allows binding to a specific network interface
REM ============================================

echo.
echo ============================================
echo Digital Signage Server - URL ACL Setup
echo SPECIFIC IP BINDING
echo ============================================
echo.
echo Dieses Skript konfiguriert URL ACL fuer eine
echo SPEZIFISCHE IP-Adresse (z.B. 192.168.1.100).
echo.
echo HINWEIS: Dies ist OPTIONAL!
echo Der Server funktioniert auch mit der Standard-
echo Wildcard-Konfiguration (http://+:8080/).
echo.
echo Diese Konfiguration ist nur noetig, wenn Sie:
echo   - Nur auf einem bestimmten Interface binden wollen
echo   - PreferredNetworkInterface in appsettings.json nutzen
echo.

REM Step 1: Check for admin privileges
echo [Schritt 1/4] Administrator-Rechte pruefen...
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo [FEHLER] Keine Administrator-Rechte!
    echo.
    echo ANLEITUNG:
    echo   1. Rechtsklick auf diese Datei
    echo   2. Waehlen Sie "Als Administrator ausfuehren"
    echo   3. Bestaetigen Sie die UAC-Abfrage
    echo.
    pause
    exit /b 1
)
echo   [OK] Administrator-Rechte vorhanden
echo.

REM Step 2: Get port from appsettings.json or use default
set PORT=8080
echo [Schritt 2/4] Port-Konfiguration ermitteln...
if exist "appsettings.json" (
    echo   appsettings.json gefunden, lese Port...
    for /f "tokens=2 delims=:, " %%a in ('findstr /i "\"Port\"" appsettings.json 2^>nul') do (
        set PORT=%%a
    )
    echo   [OK] Port aus appsettings.json: !PORT!
) else (
    echo   [INFO] appsettings.json nicht gefunden, verwende Standard-Port: 8080
)
echo.

REM Step 3: Get IP address from user
echo [Schritt 3/4] IP-Adresse eingeben...
echo.
echo Verfuegbare Netzwerkinterfaces:
echo ----------------------------------------
ipconfig | findstr /i "IPv4"
echo ----------------------------------------
echo.
set /p IP_ADDRESS="Geben Sie die IP-Adresse ein (z.B. 192.168.1.100): "

if "!IP_ADDRESS!"=="" (
    echo [FEHLER] Keine IP-Adresse eingegeben!
    pause
    exit /b 1
)

echo   Gewaehlte IP: !IP_ADDRESS!
echo.

REM Validate IP format (basic check)
echo !IP_ADDRESS! | findstr /r "^[0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo [WARNUNG] IP-Adresse hat ungültiges Format!
    echo Fortfahren? (J/N)
    set /p CONTINUE=
    if /i not "!CONTINUE!"=="J" (
        echo Abgebrochen.
        pause
        exit /b 1
    )
)

REM Step 4: Configure URL ACLs
echo [Schritt 4/4] URL ACL konfigurieren...
echo.

set ERROR_OCCURRED=0

REM URL 1: WebSocket endpoint on specific IP
echo   [1/2] Konfiguriere: http://!IP_ADDRESS!:!PORT!/ws/
echo   ----------------------------------------

echo   - Entferne alte Konfiguration (falls vorhanden)...
netsh http delete urlacl url=http://!IP_ADDRESS!:!PORT!/ws/ >nul 2>&1

echo   - Fuege neue URL ACL hinzu...
echo   - Befehl: netsh http add urlacl url=http://!IP_ADDRESS!:!PORT!/ws/ sddl=D:(A;;GX;;;S-1-1-0)
netsh http add urlacl url=http://!IP_ADDRESS!:!PORT!/ws/ sddl=D:(A;;GX;;;S-1-1-0)
if errorlevel 1 (
    echo   [FEHLER] Konnte URL ACL nicht hinzufuegen!
    set ERROR_OCCURRED=1
) else (
    echo   [OK] Erfolgreich konfiguriert
)
echo.

REM URL 2: Main endpoint on specific IP
echo   [2/2] Konfiguriere: http://!IP_ADDRESS!:!PORT!/
echo   ----------------------------------------

echo   - Entferne alte Konfiguration (falls vorhanden)...
netsh http delete urlacl url=http://!IP_ADDRESS!:!PORT!/ >nul 2>&1

echo   - Fuege neue URL ACL hinzu...
echo   - Befehl: netsh http add urlacl url=http://!IP_ADDRESS!:!PORT!/ sddl=D:(A;;GX;;;S-1-1-0)
netsh http add urlacl url=http://!IP_ADDRESS!:!PORT!/ sddl=D:(A;;GX;;;S-1-1-0)
if errorlevel 1 (
    echo   [FEHLER] Konnte URL ACL nicht hinzufuegen!
    set ERROR_OCCURRED=1
) else (
    echo   [OK] Erfolgreich konfiguriert
)
echo.

REM Verification
echo ============================================
echo Verifikation
echo ============================================
echo.

if !ERROR_OCCURRED! equ 0 (
    echo Aktuelle URL ACL Konfiguration fuer !IP_ADDRESS!:
    echo ----------------------------------------
    netsh http show urlacl | findstr "!IP_ADDRESS!"
    if errorlevel 1 (
        echo [WARNUNG] Keine URL ACLs fuer !IP_ADDRESS! gefunden!
        set ERROR_OCCURRED=1
    ) else (
        echo.
        echo ============================================
        echo [ERFOLG] URL ACL erfolgreich konfiguriert!
        echo ============================================
        echo.
        echo Naechste Schritte:
        echo   1. Oeffnen Sie appsettings.json
        echo   2. Setzen Sie "PreferredNetworkInterface": "!IP_ADDRESS!"
        echo   3. Starten Sie den Server neu
        echo.
        echo Der Server wird dann NUR auf !IP_ADDRESS! binden!
        echo.
        echo HINWEIS: Entfernen Sie PreferredNetworkInterface
        echo wieder aus appsettings.json, wenn Sie zurueck
        echo zum Wildcard-Binding wollen.
        echo.
        pause
        exit /b 0
    )
)

REM Error handling
echo.
echo ============================================
echo [FEHLER] Konfiguration fehlgeschlagen!
echo ============================================
echo.
echo Manuelle Loesung:
echo.
echo Oeffnen Sie PowerShell als Administrator:
echo.
echo   netsh http add urlacl url=http://!IP_ADDRESS!:!PORT!/ws/ user=Everyone
echo   netsh http add urlacl url=http://!IP_ADDRESS!:!PORT!/ user=Everyone
echo.
echo Zum Pruefen:
echo   netsh http show urlacl
echo.
echo Zum Entfernen:
echo   netsh http delete urlacl url=http://!IP_ADDRESS!:!PORT!/ws/
echo   netsh http delete urlacl url=http://!IP_ADDRESS!:!PORT!/
echo.
pause
exit /b 1
