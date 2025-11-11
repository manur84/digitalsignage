# Entity Framework Core Migrations Guide

## Prerequisites

Ensure you have the .NET EF Core tools installed:

```bash
dotnet tool install --global dotnet-ef
# or update existing
dotnet tool update --global dotnet-ef
```

## Creating Migrations

### Initial Migration

From the solution root directory:

```bash
# Create initial migration
dotnet ef migrations add InitialCreate --project src/DigitalSignage.Data --startup-project src/DigitalSignage.Server

# Apply migration to database
dotnet ef database update --project src/DigitalSignage.Data --startup-project src/DigitalSignage.Server
```

### Subsequent Migrations

After making changes to entity models:

```bash
# Create a new migration (replace MigrationName with descriptive name)
dotnet ef migrations add <MigrationName> --project src/DigitalSignage.Data --startup-project src/DigitalSignage.Server

# Apply to database
dotnet ef database update --project src/DigitalSignage.Data --startup-project src/DigitalSignage.Server
```

## Common Migration Commands

### List Migrations

```bash
dotnet ef migrations list --project src/DigitalSignage.Data --startup-project src/DigitalSignage.Server
```

### Remove Last Migration (if not applied)

```bash
dotnet ef migrations remove --project src/DigitalSignage.Data --startup-project src/DigitalSignage.Server
```

### Rollback to Specific Migration

```bash
dotnet ef database update <MigrationName> --project src/DigitalSignage.Data --startup-project src/DigitalSignage.Server
```

### Generate SQL Script

```bash
# Generate SQL for all migrations
dotnet ef migrations script --project src/DigitalSignage.Data --startup-project src/DigitalSignage.Server --output migration.sql

# Generate SQL from specific migration to latest
dotnet ef migrations script <FromMigration> --project src/DigitalSignage.Data --startup-project src/DigitalSignage.Server --output migration.sql
```

## Automatic Migrations

The `DatabaseInitializationService` automatically applies pending migrations on application startup. This is configured in `App.xaml.cs`.

To disable automatic migrations:
1. Remove the `DatabaseInitializationService` registration from `App.xaml.cs`
2. Apply migrations manually using the commands above

## Database Connection String

The connection string is configured in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(local);Database=DigitalSignage;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Update this connection string to match your SQL Server instance.

## Troubleshooting

### Error: "Build failed"

Ensure your solution builds successfully before creating migrations:

```bash
dotnet build
```

### Error: "No DbContext was found"

Ensure you're running the command from the solution root and specifying both projects:
- `--project src/DigitalSignage.Data` (where DbContext is located)
- `--startup-project src/DigitalSignage.Server` (where configuration is located)

### Error: "A connection was not established"

Check your connection string in `appsettings.json` and ensure SQL Server is running.

## Production Deployment

For production environments:

1. **Generate SQL scripts** instead of automatic migrations:
   ```bash
   dotnet ef migrations script --idempotent --output migration.sql
   ```

2. **Review and test** the SQL script in a staging environment

3. **Apply manually** to production database using SQL Server Management Studio or sqlcmd

4. **Consider disabling** automatic migrations in production by removing `DatabaseInitializationService` or adding a configuration flag

## Entity Model Changes

When modifying entity models:

1. Make changes to entity classes in `DigitalSignage.Data/Entities/`
2. Update `DigitalSignageDbContext.cs` if needed (Fluent API configuration)
3. Create a new migration
4. Review generated migration code
5. Apply to database
6. Test thoroughly

## Default Admin User

On first run, the `DatabaseInitializationService` creates a default admin user:

- **Username**: admin
- **Password**: [Randomly generated, shown in logs]
- **Email**: admin@digitalsignage.local
- **Role**: Admin

**Important**: Change this password immediately after first login!

The password is logged to:
- Console output
- `logs/digitalsignage-*.txt` log file

Search for: "DEFAULT ADMIN USER CREATED"

## Database Schema

### Tables Created

1. **Clients** - Raspberry Pi client devices
2. **Layouts** - Display layout configurations
3. **DataSources** - SQL data source configurations
4. **Users** - System users with roles
5. **ApiKeys** - API keys for programmatic access
6. **ClientRegistrationTokens** - One-time registration tokens
7. **AuditLogs** - Audit trail of all system changes

### Relationships

- **ApiKeys** → **Users** (Many-to-One)
- **ClientRegistrationTokens** → **Users** (Many-to-One)
- **AuditLogs** → **Users** (Many-to-One, nullable)

## Security Notes

### Password Hashing

Current implementation uses SHA256 for password hashing. **This is not recommended for production!**

For production, implement proper password hashing using:
- **BCrypt.Net**: `Install-Package BCrypt.Net-Next`
- **Argon2**: `Install-Package Konscious.Security.Cryptography.Argon2`

Example with BCrypt:

```csharp
// Replace in DatabaseInitializationService.cs
using BCrypt.Net;

private static string HashPassword(string password)
{
    return BCrypt.HashPassword(password, BCrypt.GenerateSalt(12));
}

private static bool VerifyPassword(string password, string hash)
{
    return BCrypt.Verify(password, hash);
}
```

### Connection String Security

- Store production connection strings in **User Secrets** or **Azure Key Vault**
- Never commit connection strings with passwords to source control
- Use **Windows Authentication** (Integrated Security) where possible

Example with User Secrets:

```bash
dotnet user-secrets init --project src/DigitalSignage.Server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=prod;Database=DigitalSignage;User Id=app;Password=SecurePass123!" --project src/DigitalSignage.Server
```

## Additional Resources

- [EF Core Migrations Documentation](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [EF Core Fluent API](https://docs.microsoft.com/en-us/ef/core/modeling/)
- [Connection Strings](https://www.connectionstrings.com/sql-server/)
