using System.IO;
using System.Data;
using DigitalSignage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DigitalSignage.Server.Configuration;

/// <summary>
/// Handles synchronous database initialization during application startup
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Runs database initialization synchronously (BLOCKS startup until complete)
    /// </summary>
    public static void InitializeDatabase(IServiceCollection services)
    {
        Log.Information("==========================================================");
        Log.Information("INITIALIZING DATABASE (SYNCHRONOUS - BLOCKING STARTUP)");
        Log.Information("==========================================================");

        try
        {
            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            Log.Information("Working directory: {WorkingDirectory}", Directory.GetCurrentDirectory());
            var dbConnectionString = dbContext.Database.GetConnectionString();
            Log.Information("Database connection string: {ConnectionString}", dbConnectionString);

            // Extract database file path for logging
            string? dbPath = ExtractDatabasePath(dbConnectionString);
            if (dbPath != null)
            {
                Log.Information("Database file path: {DatabasePath}", dbPath);
                Log.Information("Database file exists before migration: {Exists}", File.Exists(dbPath));
            }

            // Check for pending migrations
            LogMigrationStatus(dbContext);

            // Apply migrations
            ApplyPendingMigrations(dbContext);

            // Ensure critical tables exist (fallback if migrations fail)
            EnsureCriticalTablesExist(dbContext);

            // Verify database connection
            VerifyDatabaseConnection(dbContext);

            // Log database size
            if (dbPath != null && File.Exists(dbPath))
            {
                Log.Information("Database file exists after migration: {Exists}", File.Exists(dbPath));
                Log.Information("Database size: {Size} bytes", new FileInfo(dbPath).Length);
            }

            Log.Information("==========================================================");
            Log.Information("DATABASE INITIALIZATION COMPLETED SUCCESSFULLY");
            Log.Information("==========================================================");
        }
        catch (Exception ex)
        {
            LogDatabaseInitializationFailure(ex);
            throw; // Re-throw to prevent app startup
        }
    }

    private static string? ExtractDatabasePath(string? connectionString)
    {
        if (connectionString?.Contains("Data Source=") != true)
            return null;

        try
        {
            var startIndex = connectionString.IndexOf("Data Source=") + "Data Source=".Length;
            var remainingString = connectionString.Substring(startIndex);
            var dbFile = remainingString.Split(';')[0].Trim();
            return Path.IsPathRooted(dbFile) ? dbFile : Path.Combine(Directory.GetCurrentDirectory(), dbFile);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not parse database path from connection string");
            return null;
        }
    }

    private static void LogMigrationStatus(DigitalSignageDbContext dbContext)
    {
        Log.Information("Checking for pending database migrations...");
        var allMigrations = dbContext.Database.GetMigrations();
        var appliedMigrations = dbContext.Database.GetAppliedMigrations();
        var pendingMigrations = dbContext.Database.GetPendingMigrations();

        Log.Information("Total migrations: {Total}", allMigrations.Count());
        Log.Information("Applied migrations: {Applied}", appliedMigrations.Count());
        Log.Information("Pending migrations: {Pending}", pendingMigrations.Count());

        if (!appliedMigrations.Any())
        {
            Log.Information("No migrations applied yet - database will be created from scratch");
        }
    }

    private static void ApplyPendingMigrations(DigitalSignageDbContext dbContext)
    {
        var pendingMigrations = dbContext.Database.GetPendingMigrations();

        if (pendingMigrations.Any())
        {
            Log.Information("Applying {Count} pending migrations:", pendingMigrations.Count());
            foreach (var migration in pendingMigrations)
            {
                Log.Information("  - {Migration}", migration);
            }

            // SYNCHRONOUS migration - blocks until complete
            dbContext.Database.Migrate();
            Log.Information("Database migrations applied successfully");
        }
        else
        {
            Log.Information("Database is up to date - no pending migrations");
        }
    }

    private static void EnsureCriticalTablesExist(DigitalSignageDbContext dbContext)
    {
        Log.Information("Verifying critical database tables exist...");

        try
        {
            // Check if MobileAppRegistrations table exists by trying to query it
            bool tableExists = false;
            try
            {
                // This will throw if table doesn't exist
                _ = dbContext.MobileAppRegistrations.Any();
                tableExists = true;
                Log.Information("MobileAppRegistrations table exists");
            }
            catch (Exception)
            {
                // Table doesn't exist
                tableExists = false;
                Log.Information("MobileAppRegistrations table does not exist");
            }

            // If table doesn't exist, create it manually
            if (!tableExists)
            {
                Log.Warning("MobileAppRegistrations table not found - creating manually");
                CreateMobileAppRegistrationsTable(dbContext);
                Log.Information("MobileAppRegistrations table created successfully");
            }
            else
            {
                Log.Information("All critical tables verified and present");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error checking critical tables - will attempt to create");

            try
            {
                CreateMobileAppRegistrationsTable(dbContext);
                Log.Information("MobileAppRegistrations table created as fallback");
            }
            catch (Exception createEx)
            {
                Log.Error(createEx, "Failed to create MobileAppRegistrations table");
                // Don't throw - let app continue, the error will be caught when accessing the table
            }
        }
    }

    private static void CreateMobileAppRegistrationsTable(DigitalSignageDbContext dbContext)
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS ""MobileAppRegistrations"" (
                ""Id"" TEXT NOT NULL PRIMARY KEY,
                ""DeviceName"" TEXT NOT NULL,
                ""DeviceIdentifier"" TEXT NOT NULL,
                ""AppVersion"" TEXT NOT NULL,
                ""Platform"" TEXT NOT NULL,
                ""Status"" TEXT NOT NULL,
                ""Token"" TEXT NULL,
                ""Permissions"" TEXT NULL,
                ""AuthorizedBy"" TEXT NULL,
                ""Notes"" TEXT NULL,
                ""RegisteredAt"" TEXT NOT NULL,
                ""LastSeenAt"" TEXT NULL,
                ""AuthorizedAt"" TEXT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MobileAppRegistrations_DeviceIdentifier""
            ON ""MobileAppRegistrations"" (""DeviceIdentifier"");

            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MobileAppRegistrations_Token""
            ON ""MobileAppRegistrations"" (""Token"");

            CREATE INDEX IF NOT EXISTS ""IX_MobileAppRegistrations_Status""
            ON ""MobileAppRegistrations"" (""Status"");

            CREATE INDEX IF NOT EXISTS ""IX_MobileAppRegistrations_RegisteredAt""
            ON ""MobileAppRegistrations"" (""RegisteredAt"");

            CREATE INDEX IF NOT EXISTS ""IX_MobileAppRegistrations_LastSeenAt""
            ON ""MobileAppRegistrations"" (""LastSeenAt"");
        ";

        dbContext.Database.ExecuteSqlRaw(sql);

        // Add migration history entry if it doesn't exist
        try
        {
            // Check if migration history entry already exists
            var checkSql = @"SELECT COUNT(*) FROM __EFMigrationsHistory
                           WHERE MigrationId = '20251121000000_AddMobileAppRegistrations'";

            using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = checkSql;

            if (command.Connection?.State != System.Data.ConnectionState.Open)
            {
                command.Connection?.Open();
            }

            var count = Convert.ToInt32(command.ExecuteScalar() ?? 0);

            if (count == 0)
            {
                // Entry doesn't exist, so add it
                dbContext.Database.ExecuteSqlRaw(
                    @"INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                      VALUES ('20251121000000_AddMobileAppRegistrations', '8.0.0')"
                );
                Log.Information("Added migration history entry for MobileAppRegistrations");
            }
            else
            {
                Log.Information("Migration history entry already exists for MobileAppRegistrations");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not check/add migration history entry - may already exist");
        }
    }

    private static void VerifyDatabaseConnection(DigitalSignageDbContext dbContext)
    {
        var canConnect = dbContext.Database.CanConnect();
        Log.Information("Database connection verification: {Status}", canConnect ? "SUCCESS" : "FAILED");

        if (!canConnect)
        {
            var errorMsg = "CRITICAL: Database exists but cannot connect! Check connection string and permissions.";
            Log.Error(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }
    }

    private static void LogDatabaseInitializationFailure(Exception ex)
    {
        Log.Fatal(ex, "CRITICAL: Database initialization FAILED during app startup!");
        Log.Fatal("Error type: {ErrorType}", ex.GetType().FullName);
        Log.Fatal("Error message: {Message}", ex.Message);

        if (ex.InnerException != null)
        {
            Log.Fatal("Inner exception: {InnerMessage}", ex.InnerException.Message);
        }

        Log.Fatal("==========================================================");
        Log.Fatal("DATABASE INITIALIZATION FAILED - APPLICATION CANNOT START");
        Log.Fatal("Common solutions:");
        Log.Fatal("1. Check database file permissions");
        Log.Fatal("2. Ensure no other process has the database file locked");
        Log.Fatal("3. Verify the connection string in appsettings.json");
        Log.Fatal("4. Run: dotnet ef database update");
        Log.Fatal("==========================================================");
    }
}
