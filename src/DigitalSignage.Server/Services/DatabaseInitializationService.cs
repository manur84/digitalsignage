using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using DigitalSignage.Data;
using DigitalSignage.Data.Entities;
using DigitalSignage.Core.Models;
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

            // Verify database is ready
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                _logger.Error("CRITICAL: Cannot connect to database. Database initialization must have failed.");
                return;
            }

            _logger.Information("Database connection verified. Proceeding with seed data...");

            // Seed default data (users, templates, example layouts)
            await SeedDefaultDataAsync(dbContext, cancellationToken);

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

        // Seed example layout if no layouts exist
        await SeedExampleLayoutAsync(dbContext, cancellationToken);

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
    /// Hash password using BCrypt with workFactor 12 (recommended for production)
    /// </summary>
    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    /// <summary>
    /// Verify password against BCrypt hash
    /// </summary>
    private static bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
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
