# Digital Signage Server Diagnostic Tool
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Digital Signage Server Diagnostics" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$hasErrors = $false
$hasWarnings = $false

# Check 1: .NET Runtime
Write-Host "[1/8] Checking .NET Runtime..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  OK: .NET Version $dotnetVersion" -ForegroundColor Green

        # Check if it's .NET 8 or higher
        $versionNumber = [version]$dotnetVersion.Split('-')[0]
        if ($versionNumber.Major -lt 8) {
            Write-Host "  WARNING: .NET 8.0 or higher recommended (found $dotnetVersion)" -ForegroundColor Yellow
            $hasWarnings = $true
        }
    } else {
        throw "dotnet command failed"
    }
} catch {
    Write-Host "  ERROR: .NET not found or not in PATH" -ForegroundColor Red
    Write-Host "  Install from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    $hasErrors = $true
}
Write-Host ""

# Check 2: Port 8080 availability
Write-Host "[2/8] Checking Port 8080..." -ForegroundColor Yellow
try {
    $port8080InUse = Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue
    if ($port8080InUse) {
        Write-Host "  ERROR: Port 8080 is already in use!" -ForegroundColor Red
        $hasErrors = $true

        foreach ($conn in $port8080InUse) {
            $process = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
            if ($process) {
                Write-Host "  Process using port 8080:" -ForegroundColor Red
                Write-Host "    PID: $($process.Id)" -ForegroundColor Red
                Write-Host "    Name: $($process.ProcessName)" -ForegroundColor Red
                Write-Host "    Path: $($process.Path)" -ForegroundColor Red
            }
        }

        Write-Host ""
        Write-Host "  Solutions:" -ForegroundColor Yellow
        Write-Host "    1. Stop the process above:" -ForegroundColor Yellow
        Write-Host "       Stop-Process -Id $($port8080InUse[0].OwningProcess) -Force" -ForegroundColor Cyan
        Write-Host "    2. Change port in appsettings.json (ServerSettings.Port)" -ForegroundColor Yellow
        Write-Host "    3. Run fix-and-run.bat to automatically fix" -ForegroundColor Yellow
    } else {
        Write-Host "  OK: Port 8080 is available" -ForegroundColor Green
    }
} catch {
    Write-Host "  WARNING: Could not check port status (requires admin privileges)" -ForegroundColor Yellow
    $hasWarnings = $true
}
Write-Host ""

# Check 3: appsettings.json
Write-Host "[3/8] Checking appsettings.json..." -ForegroundColor Yellow
$appSettingsPath = "appsettings.json"
if (Test-Path $appSettingsPath) {
    Write-Host "  OK: appsettings.json found" -ForegroundColor Green
    try {
        $settings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
        Write-Host "  OK: Valid JSON format" -ForegroundColor Green

        # Check connection string
        if ($settings.ConnectionStrings.DefaultConnection) {
            Write-Host "  OK: Connection string present: $($settings.ConnectionStrings.DefaultConnection)" -ForegroundColor Green
        } else {
            Write-Host "  ERROR: Missing ConnectionStrings.DefaultConnection" -ForegroundColor Red
            $hasErrors = $true
        }

        # Check ServerSettings
        if ($settings.ServerSettings) {
            Write-Host "  OK: ServerSettings section found (Port: $($settings.ServerSettings.Port))" -ForegroundColor Green
        } else {
            Write-Host "  WARNING: Missing ServerSettings section" -ForegroundColor Yellow
            $hasWarnings = $true
        }

        # Check Serilog
        if ($settings.Serilog) {
            Write-Host "  OK: Serilog configuration found" -ForegroundColor Green
        } else {
            Write-Host "  WARNING: Missing Serilog configuration" -ForegroundColor Yellow
            $hasWarnings = $true
        }
    } catch {
        Write-Host "  ERROR: Invalid JSON format" -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
        $hasErrors = $true
    }
} else {
    Write-Host "  ERROR: appsettings.json not found!" -ForegroundColor Red
    Write-Host "  Expected location: $((Get-Location).Path)\$appSettingsPath" -ForegroundColor Red
    Write-Host "  Solution: Copy from src/DigitalSignage.Server/appsettings.json" -ForegroundColor Yellow
    $hasErrors = $true
}
Write-Host ""

# Check 4: SQLite database
Write-Host "[4/8] Checking SQLite database..." -ForegroundColor Yellow
$dbPath = "digitalsignage.db"
if (Test-Path $dbPath) {
    Write-Host "  OK: Database file exists" -ForegroundColor Green
    $dbInfo = Get-Item $dbPath
    Write-Host "  Size: $([math]::Round($dbInfo.Length/1KB, 2)) KB" -ForegroundColor Gray
    Write-Host "  Modified: $($dbInfo.LastWriteTime)" -ForegroundColor Gray

    # Check permissions
    try {
        $acl = Get-Acl $dbPath -ErrorAction Stop
        $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        $hasWriteAccess = $false

        foreach ($access in $acl.Access) {
            if ($access.FileSystemRights -match "Write|FullControl|Modify") {
                $hasWriteAccess = $true
                break
            }
        }

        if ($hasWriteAccess) {
            Write-Host "  OK: Current user has write access" -ForegroundColor Green
        } else {
            Write-Host "  WARNING: Current user may not have write access" -ForegroundColor Yellow
            Write-Host "  Solution: Right-click database file > Properties > Security > Edit" -ForegroundColor Yellow
            $hasWarnings = $true
        }
    } catch {
        Write-Host "  WARNING: Could not check file permissions" -ForegroundColor Yellow
        $hasWarnings = $true
    }

    # Check for database locks
    try {
        $lockFiles = Get-ChildItem -Path "." -Filter "digitalsignage.db-*" -ErrorAction SilentlyContinue
        if ($lockFiles) {
            Write-Host "  WARNING: Database lock files detected (may indicate locked database):" -ForegroundColor Yellow
            foreach ($lock in $lockFiles) {
                Write-Host "    - $($lock.Name)" -ForegroundColor Yellow
            }
            $hasWarnings = $true
        }
    } catch {}
} else {
    Write-Host "  INFO: Database file doesn't exist (will be created on first run)" -ForegroundColor Cyan
}
Write-Host ""

# Check 5: Build status
Write-Host "[5/8] Checking build status..." -ForegroundColor Yellow
$debugBuild = "bin/Debug/net8.0-windows/DigitalSignage.Server.dll"
$releaseBuild = "bin/Release/net8.0-windows/DigitalSignage.Server.dll"

if (Test-Path $debugBuild) {
    Write-Host "  OK: Debug build exists" -ForegroundColor Green
    $buildInfo = Get-Item $debugBuild
    Write-Host "  Built: $($buildInfo.LastWriteTime)" -ForegroundColor Gray
} elseif (Test-Path $releaseBuild) {
    Write-Host "  OK: Release build exists" -ForegroundColor Green
    $buildInfo = Get-Item $releaseBuild
    Write-Host "  Built: $($buildInfo.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "  ERROR: No build found!" -ForegroundColor Red
    Write-Host "  Solution: Run 'dotnet build'" -ForegroundColor Yellow
    $hasErrors = $true
}

# Check for project file
if (Test-Path "DigitalSignage.Server.csproj") {
    Write-Host "  OK: Project file found" -ForegroundColor Green
} else {
    Write-Host "  ERROR: DigitalSignage.Server.csproj not found!" -ForegroundColor Red
    Write-Host "  Current directory: $((Get-Location).Path)" -ForegroundColor Red
    Write-Host "  Solution: cd to src/DigitalSignage.Server directory" -ForegroundColor Yellow
    $hasErrors = $true
}
Write-Host ""

# Check 6: Firewall
Write-Host "[6/8] Checking Windows Firewall..." -ForegroundColor Yellow
try {
    $firewallRules = Get-NetFirewallRule -DisplayName "*Digital*Signage*" -ErrorAction SilentlyContinue
    if ($firewallRules) {
        Write-Host "  OK: Firewall rule(s) found:" -ForegroundColor Green
        foreach ($rule in $firewallRules) {
            Write-Host "    - $($rule.DisplayName) ($($rule.Direction), $($rule.Action))" -ForegroundColor Gray
        }
    } else {
        Write-Host "  WARNING: No firewall rule found for Digital Signage" -ForegroundColor Yellow
        Write-Host "  Clients may not be able to connect from other machines" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  Solution (Run PowerShell as Administrator):" -ForegroundColor Yellow
        Write-Host "    New-NetFirewallRule -DisplayName 'Digital Signage Server' ``" -ForegroundColor Cyan
        Write-Host "      -Direction Inbound -LocalPort 8080 -Protocol TCP -Action Allow" -ForegroundColor Cyan
        $hasWarnings = $true
    }
} catch {
    Write-Host "  WARNING: Could not check firewall (requires admin privileges)" -ForegroundColor Yellow
    $hasWarnings = $true
}
Write-Host ""

# Check 7: Recent logs
Write-Host "[7/8] Checking recent logs..." -ForegroundColor Yellow
$logPath = "logs"
if (Test-Path $logPath) {
    $recentLogs = Get-ChildItem $logPath -Filter "*.log" -Recurse -ErrorAction SilentlyContinue |
                  Sort-Object LastWriteTime -Descending |
                  Select-Object -First 1

    if ($recentLogs) {
        Write-Host "  OK: Log files found" -ForegroundColor Green
        Write-Host "  Latest: $($recentLogs.Name) ($($recentLogs.LastWriteTime))" -ForegroundColor Gray
        Write-Host ""
        Write-Host "  Last 15 lines:" -ForegroundColor Cyan

        try {
            $logContent = Get-Content $recentLogs.FullName -Tail 15 -ErrorAction Stop
            foreach ($line in $logContent) {
                if ($line -match "\[ERR\]|\[Error\]|ERROR|Exception|Failed") {
                    Write-Host "    $line" -ForegroundColor Red
                } elseif ($line -match "\[WRN\]|\[Warning\]|WARNING|WARN") {
                    Write-Host "    $line" -ForegroundColor Yellow
                } else {
                    Write-Host "    $line" -ForegroundColor Gray
                }
            }
        } catch {
            Write-Host "  WARNING: Could not read log file (may be locked)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  INFO: No log files found yet" -ForegroundColor Cyan
    }

    # Check for error logs specifically
    $errorLogPath = "logs/errors"
    if (Test-Path $errorLogPath) {
        $errorLogs = Get-ChildItem $errorLogPath -Filter "*.log" -ErrorAction SilentlyContinue |
                     Sort-Object LastWriteTime -Descending |
                     Select-Object -First 1

        if ($errorLogs -and $errorLogs.Length -gt 0) {
            Write-Host ""
            Write-Host "  WARNING: Error log detected!" -ForegroundColor Yellow
            Write-Host "  Latest error log: $($errorLogs.Name)" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "  Last 10 errors:" -ForegroundColor Red

            try {
                $errorContent = Get-Content $errorLogs.FullName -Tail 10 -ErrorAction Stop
                foreach ($line in $errorContent) {
                    Write-Host "    $line" -ForegroundColor Red
                }
            } catch {
                Write-Host "  Could not read error log" -ForegroundColor Yellow
            }
            $hasWarnings = $true
        }
    }
} else {
    Write-Host "  INFO: No logs directory found yet (will be created on first run)" -ForegroundColor Cyan
}
Write-Host ""

# Check 8: Dependencies
Write-Host "[8/8] Checking NuGet packages..." -ForegroundColor Yellow
if (Test-Path "DigitalSignage.Server.csproj") {
    Write-Host "  Restoring packages..." -ForegroundColor Gray
    $restoreOutput = dotnet restore --verbosity quiet 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  OK: All packages restored successfully" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: Package restore failed" -ForegroundColor Red
        Write-Host "  Output: $restoreOutput" -ForegroundColor Red
        Write-Host "  Solution: Run 'dotnet restore' with verbose output" -ForegroundColor Yellow
        $hasErrors = $true
    }
} else {
    Write-Host "  WARNING: Project file not found, skipping package check" -ForegroundColor Yellow
    $hasWarnings = $true
}
Write-Host ""

# Check for startup-error.txt (emergency log)
if (Test-Path "startup-error.txt") {
    Write-Host "============================================" -ForegroundColor Red
    Write-Host "EMERGENCY STARTUP ERROR DETECTED!" -ForegroundColor Red
    Write-Host "============================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Content of startup-error.txt:" -ForegroundColor Red
    Get-Content "startup-error.txt" | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    Write-Host ""
    $hasErrors = $true
}

# Summary
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Diagnostic Complete" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

if ($hasErrors) {
    Write-Host "STATUS: ERRORS FOUND (fix required)" -ForegroundColor Red
} elseif ($hasWarnings) {
    Write-Host "STATUS: WARNINGS (may work, but issues detected)" -ForegroundColor Yellow
} else {
    Write-Host "STATUS: ALL CHECKS PASSED" -ForegroundColor Green
}
Write-Host ""

Write-Host "Next steps:" -ForegroundColor Yellow
if ($hasErrors) {
    Write-Host "  1. Fix the RED errors above" -ForegroundColor Yellow
    Write-Host "  2. Re-run this diagnostic script" -ForegroundColor Yellow
} else {
    Write-Host "  1. Try building: dotnet build" -ForegroundColor Yellow
    Write-Host "  2. Try running: dotnet run" -ForegroundColor Yellow
    Write-Host "  3. If issues persist, check logs in logs/ directory" -ForegroundColor Yellow
    Write-Host "  4. Use fix-and-run.bat for automated fix" -ForegroundColor Yellow
}
Write-Host ""

# Exit with error code if errors found
if ($hasErrors) {
    exit 1
} else {
    exit 0
}
