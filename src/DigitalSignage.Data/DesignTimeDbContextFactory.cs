using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DigitalSignage.Data;

/// <summary>
/// Design-time factory for creating DbContext instances during migrations
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DigitalSignageDbContext>
{
    public DigitalSignageDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DigitalSignageDbContext>();

        // Use SQLite for design-time operations
        // The connection string doesn't matter much since we're just generating migrations
        optionsBuilder.UseSqlite("Data Source=digitalsignage.db");

        return new DigitalSignageDbContext(optionsBuilder.Options);
    }
}
