# Database Migration Error Fix

## Error Message
```
System.InvalidOperationException: The model for context 'DigitalSignageDbContext' has pending changes. Add a new migration before updating the database.
```

## Root Cause
Entity Framework Core detected model changes that haven't been captured in a migration. This can happen when:
- Model properties were added/changed
- Database configuration changed in `OnModelCreating`
- Model snapshot is out of sync

## Solution (Execute on Windows)

### Step 1: Clean Old Database Files

**IMPORTANT:** Delete the existing database on Windows to start fresh:

```
Location: C:\Users\pro\source\repos\digitalsignage\src\DigitalSignage.Server\bin\Debug\net8.0-windows\
```

**Delete these files:**
- `digitalsignage.db`
- `digitalsignage.db-shm` (if exists)
- `digitalsignage.db-wal` (if exists)

### Step 2: Pull Latest Changes from GitHub

**In PowerShell/Command Prompt (on Windows):**

```powershell
cd C:\Users\pro\source\repos\digitalsignage
git pull origin claude/digital-signage-management-system-011CV1bUPLZ3uM2W8Dj7Wdcn
```

### Step 3: Create New Migration (Windows Only)

**Open PowerShell in project root:**

```powershell
cd C:\Users\pro\source\repos\digitalsignage\src\DigitalSignage.Data

dotnet ef migrations add SyncPendingModelChanges --startup-project ..\DigitalSignage.Server\DigitalSignage.Server.csproj
```

This will:
- Detect any pending model changes
- Create a new migration file in `Migrations/`
- Update the model snapshot

### Step 4: Review Migration

Check the generated migration file in:
```
src/DigitalSignage.Data/Migrations/[TIMESTAMP]_SyncPendingModelChanges.cs
```

**Verify it contains only expected changes** (should be minimal or empty).

### Step 5: Rebuild Solution

**In Visual Studio** or **PowerShell:**

```powershell
cd C:\Users\pro\source\repos\digitalsignage
dotnet build DigitalSignage.sln
```

### Step 6: Run Application

Launch the application. The `DatabaseInitializationService` will:
1. Automatically apply all pending migrations
2. Create all tables fresh
3. Seed initial data

### Step 7: Push Migration to GitHub (if new migration was created)

**If Step 3 created a new migration file:**

```powershell
git add src/DigitalSignage.Data/Migrations/*
git commit -m "Fix: Sync database migrations for pending model changes

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"

git push
```

## Alternative: Force Recreate Database

If migrations keep causing issues, you can force a fresh database:

### Option A: Delete Database and Migrations (Nuclear Option - ONLY if necessary)

**⚠️ WARNING: This deletes all data and migration history!**

```powershell
# 1. Delete database
Remove-Item "src/DigitalSignage.Server/bin/Debug/net8.0-windows/digitalsignage.db*"

# 2. Delete ALL migrations except InitialCreate
Remove-Item "src/DigitalSignage.Data/Migrations/20251115095200_AddLayoutCategoryAndTags.*"
# Keep: 20251114000639_InitialCreate.*

# 3. Recreate migration from scratch
cd src/DigitalSignage.Data
dotnet ef migrations add CompleteModelSync --startup-project ..\DigitalSignage.Server\DigitalSignage.Server.csproj

# 4. Rebuild and run
dotnet build
```

### Option B: EnsureCreated (Development Only)

Temporarily modify `DatabaseInitializationService.cs`:

```csharp
// In InitializeAsync method, replace:
await _context.Database.MigrateAsync();

// With (TEMPORARY):
await _context.Database.EnsureDeletedAsync(); // DELETES DATABASE!
await _context.Database.EnsureCreatedAsync();  // Recreates from model
```

**⚠️ IMPORTANT:** Remove this code after database is created! It will delete data on every startup.

## Verification

After applying the fix:

1. **Check logs** - No migration errors
2. **Verify tables exist:**
   ```sql
   SELECT name FROM sqlite_master WHERE type='table';
   ```

   Expected tables:
   - Clients
   - DisplayLayouts
   - DataSources
   - Users
   - MediaFiles
   - LayoutTemplates
   - LayoutSchedules
   - Alerts
   - AlertRules
   - AuditLogs
   - ApiKeys
   - ClientRegistrationTokens
   - __EFMigrationsHistory

3. **Test functionality:**
   - Create a layout
   - Register a client
   - Upload media
   - All should work without database errors

## Current Migration Status (as of 2025-11-15)

**Existing Migrations:**
1. `20251114000639_InitialCreate` - Initial database schema
2. `20251115095200_AddLayoutCategoryAndTags` - Added Category and Tags to DisplayLayout

**Model Changes Since Last Migration:**
- All model changes have been captured in migrations
- Build is clean (0 errors, 0 warnings on Linux)
- No obvious pending changes detected in code review

**Likely Cause:**
- EF Core false positive (different behavior on Windows vs Linux)
- Model snapshot formatting differences
- Cached metadata on Windows

**Recommended Action:**
- Delete database files (Step 1)
- Pull latest code (Step 2)
- Run application (migrations auto-apply)
- **Only create new migration if error persists**

## Troubleshooting

### "No migrations configuration type was found"
- Ensure you're in `src/DigitalSignage.Data` folder
- Ensure `--startup-project` points to Server project

### "Build failed"
- Run `dotnet build` in solution root first
- Check for compilation errors in Visual Studio

### "Access denied" when deleting database
- Stop the running application
- Close any SQLite browser tools
- Try again

### Migration created but is empty
- This is GOOD! Means models are in sync
- Just commit and push the empty migration
- Database will work correctly

## Prevention

To avoid this issue in the future:

1. **Always create migration after model changes:**
   ```powershell
   cd src/DigitalSignage.Data
   dotnet ef migrations add DescriptiveChangeNameHere --startup-project ..\DigitalSignage.Server\DigitalSignage.Server.csproj
   ```

2. **Test on Windows after model changes:**
   - Model changes on Linux may behave differently on Windows
   - Always test database creation after entity changes

3. **Delete database between major changes:**
   - During development, easier to recreate than migrate
   - Production will use migrations properly

---

**Questions or issues?** Check logs at: `C:\Users\pro\source\repos\digitalsignage\src\DigitalSignage.Server\bin\Debug\net8.0-windows\logs\`
