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

        // Seed default layout templates
        if (!await dbContext.LayoutTemplates.AnyAsync(cancellationToken))
        {
            _logger.Information("No layout templates found. Creating built-in templates...");

            var templates = new[]
            {
                new LayoutTemplate
                {
                    Name = "Blank 1920x1080",
                    Description = "Empty landscape template at Full HD resolution",
                    Category = LayoutTemplateCategory.Blank,
                    Resolution = new Core.Models.Resolution { Width = 1920, Height = 1080, Orientation = "landscape" },
                    BackgroundColor = "#FFFFFF",
                    ElementsJson = "[]",
                    IsBuiltIn = true,
                    IsPublic = true
                },
                new LayoutTemplate
                {
                    Name = "Blank 1080x1920 Portrait",
                    Description = "Empty portrait template at Full HD resolution",
                    Category = LayoutTemplateCategory.Blank,
                    Resolution = new Core.Models.Resolution { Width = 1080, Height = 1920, Orientation = "portrait" },
                    BackgroundColor = "#FFFFFF",
                    ElementsJson = "[]",
                    IsBuiltIn = true,
                    IsPublic = true
                },
                new LayoutTemplate
                {
                    Name = "Simple Information Board",
                    Description = "Basic information display with title and content area",
                    Category = LayoutTemplateCategory.InformationBoard,
                    Resolution = new Core.Models.Resolution { Width = 1920, Height = 1080, Orientation = "landscape" },
                    BackgroundColor = "#2C3E50",
                    ElementsJson = "[{\"Id\":\"title\",\"Type\":\"text\",\"X\":50,\"Y\":50,\"Width\":1820,\"Height\":150,\"Content\":\"Welcome\",\"Style\":{\"FontSize\":\"72\",\"FontWeight\":\"bold\",\"Color\":\"#FFFFFF\",\"TextAlign\":\"center\"}},{\"Id\":\"content\",\"Type\":\"text\",\"X\":100,\"Y\":250,\"Width\":1720,\"Height\":700,\"Content\":\"Your content here\",\"Style\":{\"FontSize\":\"36\",\"Color\":\"#FFFFFF\",\"TextAlign\":\"left\"}}]",
                    IsBuiltIn = true,
                    IsPublic = true
                },
                new LayoutTemplate
                {
                    Name = "Room Occupancy Display",
                    Description = "Room status and occupancy information",
                    Category = LayoutTemplateCategory.RoomOccupancy,
                    Resolution = new Core.Models.Resolution { Width = 1920, Height = 1080, Orientation = "landscape" },
                    BackgroundColor = "#34495E",
                    ElementsJson = "[{\"Id\":\"roomname\",\"Type\":\"text\",\"X\":100,\"Y\":100,\"Width\":1720,\"Height\":200,\"Content\":\"{{RoomName}}\",\"Style\":{\"FontSize\":\"86\",\"FontWeight\":\"bold\",\"Color\":\"#FFFFFF\",\"TextAlign\":\"center\"}},{\"Id\":\"status\",\"Type\":\"text\",\"X\":100,\"Y\":400,\"Width\":1720,\"Height\":300,\"Content\":\"{{Status}}\",\"Style\":{\"FontSize\":\"64\",\"Color\":\"#2ECC71\",\"TextAlign\":\"center\"}},{\"Id\":\"time\",\"Type\":\"text\",\"X\":100,\"Y\":800,\"Width\":1720,\"Height\":150,\"Content\":\"{{CurrentTime}}\",\"Style\":{\"FontSize\":\"48\",\"Color\":\"#BDC3C7\",\"TextAlign\":\"center\"}}]",
                    IsBuiltIn = true,
                    IsPublic = true
                }
            };

            dbContext.LayoutTemplates.AddRange(templates);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Information("Created {Count} built-in layout templates", templates.Length);
        }
        else
        {
            _logger.Information("Layout templates already exist, skipping template creation");
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
