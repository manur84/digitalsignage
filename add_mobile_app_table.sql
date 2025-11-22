-- Manual SQL script to create MobileAppRegistrations table
-- Run this on the Windows Server if automatic migration doesn't work

CREATE TABLE IF NOT EXISTS "MobileAppRegistrations" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "DeviceName" TEXT NOT NULL,
    "DeviceIdentifier" TEXT NOT NULL,
    "AppVersion" TEXT NOT NULL,
    "Platform" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Token" TEXT NULL,
    "Permissions" TEXT NULL,
    "AuthorizedBy" TEXT NULL,
    "Notes" TEXT NULL,
    "RegisteredAt" TEXT NOT NULL,
    "LastSeenAt" TEXT NULL,
    "AuthorizedAt" TEXT NULL
);

-- Create unique index on DeviceIdentifier
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MobileAppRegistrations_DeviceIdentifier"
ON "MobileAppRegistrations" ("DeviceIdentifier");

-- Create unique index on Token
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MobileAppRegistrations_Token"
ON "MobileAppRegistrations" ("Token");

-- Create index on Status
CREATE INDEX IF NOT EXISTS "IX_MobileAppRegistrations_Status"
ON "MobileAppRegistrations" ("Status");

-- Create index on RegisteredAt
CREATE INDEX IF NOT EXISTS "IX_MobileAppRegistrations_RegisteredAt"
ON "MobileAppRegistrations" ("RegisteredAt");

-- Create index on LastSeenAt
CREATE INDEX IF NOT EXISTS "IX_MobileAppRegistrations_LastSeenAt"
ON "MobileAppRegistrations" ("LastSeenAt");

-- Add entry to migrations history
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251121000000_AddMobileAppRegistrations', '8.0.0')
ON CONFLICT DO NOTHING;
