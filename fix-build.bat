@echo off
REM Fix Build - Replace all old namespace references
echo Fixing namespace references...

cd /d "%~dp0"

REM Find and replace in all CS files
powershell -Command "(Get-Content 'src\DigitalSignage.Data\Services\SqlDataService.cs' -Raw) -replace 'using DigitalSignage\.Server\.Utilities;', 'using DigitalSignage.Core.Utilities;' | Set-Content 'src\DigitalSignage.Data\Services\SqlDataService.cs'"

powershell -Command "(Get-Content 'src\DigitalSignage.Server\Services\DataSourceManager.cs' -Raw) -replace 'using DigitalSignage\.Server\.Utilities;', 'using DigitalSignage.Core.Utilities;' | Set-Content 'src\DigitalSignage.Server\Services\DataSourceManager.cs'"

powershell -Command "(Get-Content 'src\DigitalSignage.Server\Services\EnhancedMediaService.cs' -Raw) -replace 'using DigitalSignage\.Server\.Utilities;', 'using DigitalSignage.Core.Utilities;' | Set-Content 'src\DigitalSignage.Server\Services\EnhancedMediaService.cs'"

powershell -Command "(Get-Content 'src\DigitalSignage.Server\Services\MediaService.cs' -Raw) -replace 'using DigitalSignage\.Server\.Utilities;', 'using DigitalSignage.Core.Utilities;' | Set-Content 'src\DigitalSignage.Server\Services\MediaService.cs'"

powershell -Command "(Get-Content 'src\DigitalSignage.Server\Services\QueryCacheService.cs' -Raw) -replace 'using DigitalSignage\.Server\.Utilities;', 'using DigitalSignage.Core.Utilities;' | Set-Content 'src\DigitalSignage.Server\Services\QueryCacheService.cs'"

powershell -Command "(Get-Content 'src\DigitalSignage.Server\Services\SqlDataSourceService.cs' -Raw) -replace 'using DigitalSignage\.Server\.Utilities;', 'using DigitalSignage.Core.Utilities;' | Set-Content 'src\DigitalSignage.Server\Services\SqlDataSourceService.cs'"

echo.
echo Done! Now:
echo 1. Close Visual Studio
echo 2. Delete .vs folder
echo 3. Open Visual Studio again
echo 4. Build -^> Rebuild Solution
echo.
pause
