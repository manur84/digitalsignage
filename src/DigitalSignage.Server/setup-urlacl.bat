@echo off
setlocal EnableDelayedExpansion

REM ============================================
REM Digital Signage Server - URL ACL Setup
REM FOOLPROOF VERSION WITH COMPREHENSIVE DIAGNOSTICS
REM ============================================

echo.
echo ============================================
echo Digital Signage Server - URL ACL Setup
echo ============================================
echo.
echo Dieses Skript konfiguriert Windows, damit der
echo Digital Signage Server ohne Administrator-Rechte
echo laufen kann (URL ACL Konfiguration).
echo.

REM Step 1: Check for admin privileges
echo [Schritt 1/5] Administrator-Rechte pruefen...
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo [FEHLER] Keine Administrator-Rechte!
    echo.
    echo ANLEITUNG:
    echo   1. Rechtsklick auf diese Datei: setup-urlacl.bat
    echo   2. Waehlen Sie "Als Administrator ausfuehren"
    echo   3. Bestaetigen Sie die UAC-Abfrage
    echo.
    pause
    exit /b 1
)
echo   [OK] Administrator-Rechte vorhanden
echo.

REM Step 2: System Information
echo [Schritt 2/5] System-Informationen...
echo   Benutzer: %USERNAME%
echo   Computer: %COMPUTERNAME%
echo   Windows: %OS%
for /f "tokens=4-5 delims=. " %%i in ('ver') do set VERSION=%%i.%%j
echo   Version: %VERSION%
echo.

REM Step 3: Get port from appsettings.json or use default
set PORT=8080
echo [Schritt 3/5] Port-Konfiguration ermitteln...
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

REM Step 4: Check HTTP.sys service
echo [Schritt 4/5] HTTP.sys Service pruefen...
sc query HTTP >nul 2>&1
if %errorlevel% neq 0 (
    echo   [WARNUNG] HTTP.sys Service nicht gefunden!
    echo   Dies kann zu Problemen fuehren.
) else (
    for /f "tokens=3" %%a in ('sc query HTTP ^| findstr "STATE"') do set HTTPSYS_STATE=%%a
    if "!HTTPSYS_STATE!"=="RUNNING" (
        echo   [OK] HTTP.sys Service laeuft
    ) else (
        echo   [WARNUNG] HTTP.sys Service Status: !HTTPSYS_STATE!
        echo   [INFO] Versuche HTTP.sys Service zu starten...
        net start HTTP >nul 2>&1
        if !errorlevel! equ 0 (
            echo   [OK] HTTP.sys Service gestartet
        ) else (
            echo   [WARNUNG] HTTP.sys konnte nicht gestartet werden
            echo   Dies kann die URL ACL Konfiguration verhindern
        )
    )
)
echo.

REM Step 5: Configure URL ACLs
echo [Schritt 5/5] URL ACL konfigurieren...
echo.

set ERROR_OCCURRED=0

REM URL 1: WebSocket endpoint
echo   [1/2] Konfiguriere: http://+:!PORT!/ws/
echo   ----------------------------------------

echo   - Entferne alte Konfiguration (falls vorhanden)...
netsh http delete urlacl url=http://+:!PORT!/ws/ >nul 2>&1

echo   - Fuege neue URL ACL hinzu...
echo   - Befehl: netsh http add urlacl url=http://+:!PORT!/ws/ sddl=D:(A;;GX;;;S-1-1-0)
echo   - Info: SID S-1-1-0 = Everyone/Jeder (funktioniert auf allen Windows-Sprachversionen)
netsh http add urlacl url=http://+:!PORT!/ws/ sddl=D:(A;;GX;;;S-1-1-0)
if errorlevel 1 (
    echo   [FEHLER] Konnte URL ACL nicht hinzufuegen!
    echo   Fehlercode: %errorlevel%
    set ERROR_OCCURRED=1
) else (
    echo   [OK] Erfolgreich konfiguriert
)
echo.

REM URL 2: Main endpoint
echo   [2/2] Konfiguriere: http://+:!PORT!/
echo   ----------------------------------------

echo   - Entferne alte Konfiguration (falls vorhanden)...
netsh http delete urlacl url=http://+:!PORT!/ >nul 2>&1

echo   - Fuege neue URL ACL hinzu...
echo   - Befehl: netsh http add urlacl url=http://+:!PORT!/ sddl=D:(A;;GX;;;S-1-1-0)
echo   - Info: SID S-1-1-0 = Everyone/Jeder (funktioniert auf allen Windows-Sprachversionen)
netsh http add urlacl url=http://+:!PORT!/ sddl=D:(A;;GX;;;S-1-1-0)
if errorlevel 1 (
    echo   [FEHLER] Konnte URL ACL nicht hinzufuegen!
    echo   Fehlercode: %errorlevel%
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
    echo Aktuelle URL ACL Konfiguration:
    echo ----------------------------------------
    netsh http show urlacl | findstr ":!PORT!"
    if errorlevel 1 (
        echo [WARNUNG] Keine URL ACLs fuer Port !PORT! gefunden!
        echo Die Konfiguration wurde moeglicherweise nicht gespeichert.
        set ERROR_OCCURRED=1
    ) else (
        echo.
        echo ============================================
        echo [ERFOLG] URL ACL erfolgreich konfiguriert!
        echo ============================================
        echo.
        echo Sie koennen jetzt:
        echo   1. Dieses Fenster schliessen
        echo   2. Digital Signage Server NORMAL starten
        echo      (OHNE Administrator-Rechte)
        echo.
        echo Diese Konfiguration muss nur EINMAL durchgefuehrt werden!
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
echo Moegliche Ursachen:
echo.
echo 1. Windows Firewall oder Antivirus blockiert
echo    - Deaktivieren Sie kurzzeitig Antivirus
echo    - Fuegen Sie eine Firewall-Ausnahme hinzu
echo.
echo 2. HTTP.sys Service nicht gestartet
echo    - Pruefen: sc query HTTP
echo    - Starten: net start HTTP
echo.
echo 3. Port !PORT! wird bereits verwendet
echo    - Pruefen: netstat -ano ^| findstr :!PORT!
echo    - Anderen Port in appsettings.json konfigurieren
echo.
echo 4. Windows Update erforderlich
echo    - Installieren Sie alle Windows Updates
echo    - Starten Sie Windows neu
echo.
echo 5. Beschaedigte HTTP.sys Installation
echo    - Fuehren Sie aus: sfc /scannow
echo    - Dann Windows neu starten
echo.
echo ============================================
echo Manuelle Loesung
echo ============================================
echo.
echo Oeffnen Sie PowerShell als Administrator und fuehren Sie aus:
echo.
echo   netsh http add urlacl url=http://+:!PORT!/ws/ sddl=D:(A;;GX;;;S-1-1-0)
echo   netsh http add urlacl url=http://+:!PORT!/ sddl=D:(A;;GX;;;S-1-1-0)
echo.
echo Alternative (deutsch): Verwenden Sie "user=Jeder" statt der SID:
echo   netsh http add urlacl url=http://+:!PORT!/ws/ user=Jeder
echo   netsh http add urlacl url=http://+:!PORT!/ user=Jeder
echo.
echo Zum Pruefen der Konfiguration:
echo   netsh http show urlacl
echo.
echo ============================================
echo Support
echo ============================================
echo.
echo Falls nichts funktioniert:
echo   1. Erstellen Sie einen Screenshot dieser Meldung
echo   2. Fuehren Sie aus: netsh http show urlacl ^> urlacl-config.txt
echo   3. Senden Sie beide an den Support
echo.
pause
exit /b 1
