using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using Serilog;
using System.Security.Cryptography;
using System.Text;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Background service for database initialization and migrations
/// </summary>
public class DatabaseInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DatabaseInitializationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = Log.ForContext<DatabaseInitializationService>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Starting database initialization...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

            // Apply pending migrations
            _logger.Information("Checking for pending database migrations...");
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);

            if (pendingMigrations.Any())
            {
                _logger.Information("Applying {Count} pending migrations...", pendingMigrations.Count());
                await dbContext.Database.MigrateAsync(cancellationToken);
                _logger.Information("Database migrations applied successfully");
            }
            else
            {
                _logger.Information("Database is up to date, no migrations needed");
            }

            // Seed default data
            await SeedDefaultDataAsync(dbContext, cancellationToken);

            _logger.Information("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize database");
            // Don't throw - allow application to start even if DB initialization fails
            // This allows manual fixes or configuration changes
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task SeedDefaultDataAsync(DigitalSignageDbContext dbContext, CancellationToken cancellationToken)
    {
        _logger.Information("Checking for default data seeding...");

        // Check if any users exist
        if (!await dbContext.Users.AnyAsync(cancellationToken))
        {
            _logger.Information("No users found. Creating default admin user...");

            var defaultPassword = GenerateSecurePassword();
            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@digitalsignage.local",
                FullName = "System Administrator",
                PasswordHash = HashPassword(defaultPassword),
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastPasswordChangedAt = DateTime.UtcNow
            };

            dbContext.Users.Add(adminUser);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Warning("==========================================================");
            _logger.Warning("DEFAULT ADMIN USER CREATED");
            _logger.Warning("Username: admin");
            _logger.Warning("Password: {Password}", defaultPassword);
            _logger.Warning("PLEASE CHANGE THIS PASSWORD IMMEDIATELY!");
            _logger.Warning("==========================================================");

            // Log to audit table
            var auditLog = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                Action = "SystemInitialization",
                EntityType = "User",
                EntityId = adminUser.Id.ToString(),
                Username = "System",
                Description = "Default admin user created during initial setup",
                IsSuccessful = true
            };
            dbContext.AuditLogs.Add(auditLog);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Information("Default admin user created successfully");
        }
        else
        {
            _logger.Information("Users already exist, skipping default user creation");
        }
    }

    /// <summary>
    /// Hash password using SHA256 (Note: In production, use BCrypt or Argon2)
    /// </summary>
    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    /// <summary>
    /// Generate a secure random password
    /// </summary>
    private static string GenerateSecurePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        var random = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(random);
        }

        var password = new StringBuilder(16);
        foreach (var b in random)
        {
            password.Append(chars[b % chars.Length]);
        }

        return password.ToString();
    }
}
