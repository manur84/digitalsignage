using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using DigitalSignage.Core.Models;
using DigitalSignage.Core.Interfaces;
using Serilog;
using System.Security.Cryptography;
using System.Text;
using System.IO;

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
        // NOTE: Database migrations are now applied SYNCHRONOUSLY in App.xaml.cs constructor
        // This service only handles SEED DATA after the application has started

        _logger.Information("==========================================================");
        _logger.Information("SEEDING DEFAULT DATA (Asynchronous Background Service)");
        _logger.Information("==========================================================");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

            // Verify database is ready
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                _logger.Error("CRITICAL: Cannot connect to database. Database initialization must have failed.");
                return;
            }

            _logger.Information("Database connection verified. Proceeding with seed data...");

            // Seed default data (users, example layouts)
            await SeedDefaultDataAsync(dbContext, authService, cancellationToken);

            _logger.Information("==========================================================");
            _logger.Information("SEED DATA COMPLETED SUCCESSFULLY");
            _logger.Information("==========================================================");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to seed default data");
            _logger.Warning("Application will continue, but default data may be missing");
            // Don't throw - seed data failure should not crash the app
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task SeedDefaultDataAsync(DigitalSignageDbContext dbContext, IAuthenticationService authService, CancellationToken cancellationToken)
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
                PasswordHash = authService.HashPassword(defaultPassword),
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

        // Seed example layout if no layouts exist
        await SeedExampleLayoutAsync(dbContext, cancellationToken);
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

    /// <summary>
    /// Seed example layout with comprehensive sample data demonstrating all element types
    /// </summary>
    private async Task SeedExampleLayoutAsync(DigitalSignageDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            // Check if any display layouts already exist
            if (await dbContext.DisplayLayouts.AnyAsync(cancellationToken))
            {
                _logger.Information("Display layouts already exist, skipping example layout creation");
                return;
            }

            _logger.Information("No display layouts found. Creating example layout...");

            var exampleLayout = new DisplayLayout
            {
                Name = "Company Information Board - Example",
                Description = "Comprehensive example layout demonstrating all element types with sample data",
                Resolution = new Core.Models.Resolution
                {
                    Width = 1920,
                    Height = 1080,
                    Orientation = "landscape"
                },
                BackgroundColor = "#1a1a2e",
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                Elements = new List<Core.Models.DisplayElement>
                {
                    // 1. Header Text Element
                    new Core.Models.DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "text",
                        Name = "Header - Welcome",
                        Position = new Core.Models.Position { X = 100, Y = 50 },
                        Size = new Core.Models.Size { Width = 1720, Height = 120 },
                        ZIndex = 10,
                        Properties = new Dictionary<string, object>
                        {
                            ["Content"] = "Welcome to Digital Signage Demo",
                            ["FontFamily"] = "Arial",
                            ["FontSize"] = 64,
                            ["FontWeight"] = "Bold",
                            ["Color"] = "#FFFFFF",
                            ["TextAlign"] = "Center"
                        }
                    },

                    // 2. Date/Time Element with Template Variable
                    new Core.Models.DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "text",
                        Name = "Current Date and Time",
                        Position = new Core.Models.Position { X = 1400, Y = 30 },
                        Size = new Core.Models.Size { Width = 450, Height = 60 },
                        ZIndex = 11,
                        Properties = new Dictionary<string, object>
                        {
                            ["Content"] = "{{date_format DateTime \"dddd, MMMM d, yyyy - HH:mm\"}}",
                            ["FontFamily"] = "Arial",
                            ["FontSize"] = 28,
                            ["Color"] = "#FFFFFF",
                            ["TextAlign"] = "Right"
                        }
                    },

                    // 3. Logo/Image Placeholder Element
                    new Core.Models.DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "image",
                        Name = "Company Logo",
                        Position = new Core.Models.Position { X = 50, Y = 30 },
                        Size = new Core.Models.Size { Width = 200, Height = 100 },
                        ZIndex = 12,
                        Properties = new Dictionary<string, object>
                        {
                            ["Source"] = "",
                            ["Stretch"] = "Uniform",
                            ["Description"] = "Upload your company logo here"
                        }
                    },

                    // 4. Company Information Text Block
                    new Core.Models.DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "text",
                        Name = "Company Info",
                        Position = new Core.Models.Position { X = 100, Y = 250 },
                        Size = new Core.Models.Size { Width = 500, Height = 300 },
                        ZIndex = 5,
                        Properties = new Dictionary<string, object>
                        {
                            ["Content"] = "Your Company Name\nAddress Line 1\nAddress Line 2\nCity, State ZIP\nPhone: +1 234 567 890\nEmail: info@company.com",
                            ["FontFamily"] = "Arial",
                            ["FontSize"] = 24,
                            ["Color"] = "#FFFFFF",
                            ["TextAlign"] = "Left"
                        }
                    },

                    // 5. Decorative Rectangle Background for Footer
                    new Core.Models.DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "rectangle",
                        Name = "Footer Background",
                        Position = new Core.Models.Position { X = 100, Y = 700 },
                        Size = new Core.Models.Size { Width = 1400, Height = 300 },
                        ZIndex = 1,
                        Properties = new Dictionary<string, object>
                        {
                            ["FillColor"] = "#2A2A2A80",
                            ["BorderColor"] = "#4A4A4A",
                            ["BorderThickness"] = 2
                        }
                    },

                    // 6. Data Display Table/Text Area
                    new Core.Models.DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "text",
                        Name = "Department Data",
                        Position = new Core.Models.Position { X = 650, Y = 250 },
                        Size = new Core.Models.Size { Width = 1100, Height = 400 },
                        ZIndex = 6,
                        Properties = new Dictionary<string, object>
                        {
                            ["Content"] = "DEPARTMENT STATUS\n\n" +
                                        "IT Department      Status: Active      Count: 12\n" +
                                        "Sales              Status: Active      Count: 8\n" +
                                        "Marketing          Status: Active      Count: 5\n" +
                                        "HR                 Status: Active      Count: 3\n" +
                                        "Finance            Status: Active      Count: 4\n\n" +
                                        "Total Employees: 32",
                            ["FontFamily"] = "Consolas",
                            ["FontSize"] = 28,
                            ["Color"] = "#E0E0E0",
                            ["TextAlign"] = "Left"
                        }
                    },

                    // 7. QR Code Element
                    new Core.Models.DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "text",
                        Name = "QR Code Placeholder",
                        Position = new Core.Models.Position { X = 1600, Y = 750 },
                        Size = new Core.Models.Size { Width = 250, Height = 250 },
                        ZIndex = 8,
                        Properties = new Dictionary<string, object>
                        {
                            ["Content"] = "[QR]\nhttps://example.com/info",
                            ["FontFamily"] = "Arial",
                            ["FontSize"] = 16,
                            ["Color"] = "#FFFFFF",
                            ["TextAlign"] = "Center",
                            ["Description"] = "QR code will be generated from URL"
                        }
                    },

                    // 8. Footer Text with Announcements
                    new Core.Models.DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "text",
                        Name = "Announcements",
                        Position = new Core.Models.Position { X = 150, Y = 750 },
                        Size = new Core.Models.Size { Width = 1300, Height = 200 },
                        ZIndex = 7,
                        Properties = new Dictionary<string, object>
                        {
                            ["Content"] = "Important Announcements:\n" +
                                        "• Team meeting in Conference Room A at 2:00 PM\n" +
                                        "• New parking regulations effective next Monday\n" +
                                        "• Holiday schedule posted on company intranet",
                            ["FontFamily"] = "Arial",
                            ["FontSize"] = 20,
                            ["Color"] = "#B0B0B0",
                            ["TextAlign"] = "Left"
                        }
                    },

                    // 9. Status Indicator Rectangle
                    new Core.Models.DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "rectangle",
                        Name = "Status Indicator",
                        Position = new Core.Models.Position { X = 50, Y = 150 },
                        Size = new Core.Models.Size { Width = 150, Height = 50 },
                        ZIndex = 9,
                        Properties = new Dictionary<string, object>
                        {
                            ["FillColor"] = "#2ECC71",
                            ["BorderColor"] = "#27AE60",
                            ["BorderThickness"] = 2
                        }
                    },

                    // 10. Status Text
                    new Core.Models.DisplayElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = "text",
                        Name = "Status Text",
                        Position = new Core.Models.Position { X = 50, Y = 155 },
                        Size = new Core.Models.Size { Width = 150, Height = 40 },
                        ZIndex = 10,
                        Properties = new Dictionary<string, object>
                        {
                            ["Content"] = "ONLINE",
                            ["FontFamily"] = "Arial",
                            ["FontSize"] = 20,
                            ["FontWeight"] = "Bold",
                            ["Color"] = "#FFFFFF",
                            ["TextAlign"] = "Center"
                        }
                    }
                }
            };

            dbContext.DisplayLayouts.Add(exampleLayout);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Information("==========================================================");
            _logger.Information("EXAMPLE LAYOUT CREATED");
            _logger.Information("Layout Name: {LayoutName}", exampleLayout.Name);
            _logger.Information("Layout ID: {LayoutId}", exampleLayout.Id);
            _logger.Information("Resolution: {Width}x{Height}",
                exampleLayout.Resolution.Width, exampleLayout.Resolution.Height);
            _logger.Information("Elements Count: {Count}", exampleLayout.Elements.Count);
            _logger.Information("Open the Designer tab to view and edit this example layout");
            _logger.Information("==========================================================");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create example layout");
        }
    }
}
