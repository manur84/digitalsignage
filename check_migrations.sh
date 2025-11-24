#!/bin/bash
cd /mnt/c/Users/reinert/digitalsignage/src/DigitalSignage.Data

echo "=== Checking EF Core Migrations Status ==="
echo ""

# Check if dotnet is available
if ! command -v /mnt/c/Program\ Files/dotnet/dotnet.exe &> /dev/null; then
    echo "ERROR: dotnet.exe not found"
    exit 1
fi

# List migrations
echo "=== All Migrations ==="
/mnt/c/Program\ Files/dotnet/dotnet.exe ef migrations list --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj --no-build

echo ""
echo "=== Applying Pending Migrations ==="
/mnt/c/Program\ Files/dotnet/dotnet.exe ef database update --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj --no-build

echo ""
echo "=== Migration Status After Update ==="
/mnt/c/Program\ Files/dotnet/dotnet.exe ef migrations list --startup-project ../DigitalSignage.Server/DigitalSignage.Server.csproj --no-build
