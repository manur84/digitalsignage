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
                // Blank Templates - Various Resolutions
                new LayoutTemplate
                {
                    Name = "Blank 1920x1080 (Full HD)",
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
                    Name = "Blank 1080x1920 Portrait (Full HD)",
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
                    Name = "Blank 1280x720 (HD)",
                    Description = "Empty landscape template at HD resolution",
                    Category = LayoutTemplateCategory.Blank,
                    Resolution = new Core.Models.Resolution { Width = 1280, Height = 720, Orientation = "landscape" },
                    BackgroundColor = "#FFFFFF",
                    ElementsJson = "[]",
                    IsBuiltIn = true,
                    IsPublic = true
                },
                new LayoutTemplate
                {
                    Name = "Blank 3840x2160 (4K UHD)",
                    Description = "Empty landscape template at 4K Ultra HD resolution",
                    Category = LayoutTemplateCategory.Blank,
                    Resolution = new Core.Models.Resolution { Width = 3840, Height = 2160, Orientation = "landscape" },
                    BackgroundColor = "#FFFFFF",
                    ElementsJson = "[]",
                    IsBuiltIn = true,
                    IsPublic = true
                },
                new LayoutTemplate
                {
                    Name = "Blank 2160x3840 Portrait (4K UHD)",
                    Description = "Empty portrait template at 4K Ultra HD resolution",
                    Category = LayoutTemplateCategory.Blank,
                    Resolution = new Core.Models.Resolution { Width = 2160, Height = 3840, Orientation = "portrait" },
                    BackgroundColor = "#FFFFFF",
                    ElementsJson = "[]",
                    IsBuiltIn = true,
                    IsPublic = true
                },

                // Information Boards
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

                // Room Occupancy
                new LayoutTemplate
                {
                    Name = "Room Occupancy Display",
                    Description = "Room status and occupancy information with dynamic data",
                    Category = LayoutTemplateCategory.RoomOccupancy,
                    Resolution = new Core.Models.Resolution { Width = 1920, Height = 1080, Orientation = "landscape" },
                    BackgroundColor = "#34495E",
                    ElementsJson = "[{\"Id\":\"roomname\",\"Type\":\"text\",\"X\":100,\"Y\":100,\"Width\":1720,\"Height\":200,\"Content\":\"{{RoomName}}\",\"Style\":{\"FontSize\":\"86\",\"FontWeight\":\"bold\",\"Color\":\"#FFFFFF\",\"TextAlign\":\"center\"}},{\"Id\":\"status\",\"Type\":\"text\",\"X\":100,\"Y\":400,\"Width\":1720,\"Height\":300,\"Content\":\"{{Status}}\",\"Style\":{\"FontSize\":\"64\",\"Color\":\"#2ECC71\",\"TextAlign\":\"center\"}},{\"Id\":\"time\",\"Type\":\"text\",\"X\":100,\"Y\":800,\"Width\":1720,\"Height\":150,\"Content\":\"{{CurrentTime}}\",\"Style\":{\"FontSize\":\"48\",\"Color\":\"#BDC3C7\",\"TextAlign\":\"center\"}}]",
                    IsBuiltIn = true,
                    IsPublic = true
                },

                // Welcome Screen
                new LayoutTemplate
                {
                    Name = "Corporate Welcome Screen",
                    Description = "Professional welcome display for visitors",
                    Category = LayoutTemplateCategory.WelcomeScreen,
                    Resolution = new Core.Models.Resolution { Width = 1920, Height = 1080, Orientation = "landscape" },
                    BackgroundColor = "#1A1A2E",
                    ElementsJson = "[{\"Id\":\"header\",\"Type\":\"text\",\"X\":100,\"Y\":150,\"Width\":1720,\"Height\":120,\"Content\":\"Welcome to\",\"Style\":{\"FontSize\":\"48\",\"Color\":\"#BDC3C7\",\"TextAlign\":\"center\"}},{\"Id\":\"company\",\"Type\":\"text\",\"X\":100,\"Y\":300,\"Width\":1720,\"Height\":180,\"Content\":\"{{CompanyName}}\",\"Style\":{\"FontSize\":\"96\",\"FontWeight\":\"bold\",\"Color\":\"#FFFFFF\",\"TextAlign\":\"center\"}},{\"Id\":\"datetime\",\"Type\":\"text\",\"X\":100,\"Y\":850,\"Width\":1720,\"Height\":80,\"Content\":\"{{date_format DateTime \\\"dddd, MMMM d, yyyy - HH:mm\\\"}}\",\"Style\":{\"FontSize\":\"36\",\"Color\":\"#95A5A6\",\"TextAlign\":\"center\"}}]",
                    IsBuiltIn = true,
                    IsPublic = true
                },

                // Menu Board
                new LayoutTemplate
                {
                    Name = "Digital Menu Board",
                    Description = "Menu display template with header and item list",
                    Category = LayoutTemplateCategory.MenuBoard,
                    Resolution = new Core.Models.Resolution { Width = 1920, Height = 1080, Orientation = "landscape" },
                    BackgroundColor = "#ECEFF1",
                    ElementsJson = "[{\"Id\":\"menutitle\",\"Type\":\"text\",\"X\":50,\"Y\":30,\"Width\":1820,\"Height\":100,\"Content\":\"Today's Menu\",\"Style\":{\"FontSize\":\"64\",\"FontWeight\":\"bold\",\"Color\":\"#263238\",\"TextAlign\":\"center\"}},{\"Id\":\"menuitems\",\"Type\":\"text\",\"X\":100,\"Y\":200,\"Width\":1720,\"Height\":800,\"Content\":\"Menu items will be displayed here\",\"Style\":{\"FontSize\":\"32\",\"Color\":\"#37474F\",\"TextAlign\":\"left\"}}]",
                    IsBuiltIn = true,
                    IsPublic = true
                },

                // Wayfinding
                new LayoutTemplate
                {
                    Name = "Directory Wayfinding",
                    Description = "Building directory and wayfinding display",
                    Category = LayoutTemplateCategory.Wayfinding,
                    Resolution = new Core.Models.Resolution { Width = 1920, Height = 1080, Orientation = "landscape" },
                    BackgroundColor = "#FAFAFA",
                    ElementsJson = "[{\"Id\":\"dirheader\",\"Type\":\"text\",\"X\":50,\"Y\":40,\"Width\":1820,\"Height\":100,\"Content\":\"Building Directory\",\"Style\":{\"FontSize\":\"58\",\"FontWeight\":\"bold\",\"Color\":\"#212121\",\"TextAlign\":\"center\"}},{\"Id\":\"directions\",\"Type\":\"text\",\"X\":100,\"Y\":180,\"Width\":1720,\"Height\":820,\"Content\":\"Floor information and directions\",\"Style\":{\"FontSize\":\"36\",\"Color\":\"#424242\",\"TextAlign\":\"left\"}}]",
                    IsBuiltIn = true,
                    IsPublic = true
                },

                // Emergency
                new LayoutTemplate
                {
                    Name = "Emergency Information",
                    Description = "Emergency alerts and safety information",
                    Category = LayoutTemplateCategory.Emergency,
                    Resolution = new Core.Models.Resolution { Width = 1920, Height = 1080, Orientation = "landscape" },
                    BackgroundColor = "#B71C1C",
                    ElementsJson = "[{\"Id\":\"emergtitle\",\"Type\":\"text\",\"X\":100,\"Y\":200,\"Width\":1720,\"Height\":150,\"Content\":\"EMERGENCY INFORMATION\",\"Style\":{\"FontSize\":\"72\",\"FontWeight\":\"bold\",\"Color\":\"#FFFFFF\",\"TextAlign\":\"center\"}},{\"Id\":\"emergmsg\",\"Type\":\"text\",\"X\":150,\"Y\":450,\"Width\":1620,\"Height\":400,\"Content\":\"{{EmergencyMessage}}\",\"Style\":{\"FontSize\":\"48\",\"Color\":\"#FFEBEE\",\"TextAlign\":\"center\"}}]",
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
