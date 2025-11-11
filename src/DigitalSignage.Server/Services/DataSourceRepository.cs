using DigitalSignage.Core.Models;
using DigitalSignage.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Repository for managing DataSource entities
/// </summary>
public class DataSourceRepository
{
    private readonly IDbContextFactory<DigitalSignageDbContext> _contextFactory;
    private readonly ILogger _logger;

    public DataSourceRepository(IDbContextFactory<DigitalSignageDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
        _logger = Log.ForContext<DataSourceRepository>();
    }

    public async Task<List<DataSource>> GetAllAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.DataSources
            .OrderBy(ds => ds.Name)
            .ToListAsync();
    }

    public async Task<DataSource?> GetByIdAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.DataSources
            .FirstOrDefaultAsync(ds => ds.Id == id);
    }

    public async Task<DataSource> AddAsync(DataSource dataSource)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        // Ensure Id is set
        if (string.IsNullOrEmpty(dataSource.Id))
        {
            dataSource.Id = Guid.NewGuid().ToString();
        }

        context.DataSources.Add(dataSource);
        await context.SaveChangesAsync();

        _logger.Information("Added new data source: {Name} (ID: {Id})", dataSource.Name, dataSource.Id);
        return dataSource;
    }

    public async Task<DataSource> UpdateAsync(DataSource dataSource)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var existing = await context.DataSources.FirstOrDefaultAsync(ds => ds.Id == dataSource.Id);
        if (existing == null)
        {
            throw new InvalidOperationException($"DataSource with ID {dataSource.Id} not found");
        }

        context.Entry(existing).CurrentValues.SetValues(dataSource);
        await context.SaveChangesAsync();

        _logger.Information("Updated data source: {Name} (ID: {Id})", dataSource.Name, dataSource.Id);
        return dataSource;
    }

    public async Task DeleteAsync(string id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var dataSource = await context.DataSources.FirstOrDefaultAsync(ds => ds.Id == id);
        if (dataSource != null)
        {
            context.DataSources.Remove(dataSource);
            await context.SaveChangesAsync();
            _logger.Information("Deleted data source: {Name} (ID: {Id})", dataSource.Name, id);
        }
    }
}
