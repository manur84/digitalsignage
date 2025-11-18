using Microsoft.Extensions.Logging;
using DigitalSignage.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;
using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for database backup and restore operations
/// </summary>
public class BackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly IDbContextFactory<DigitalSignageDbContext> _contextFactory;
    private const string DATABASE_FILE = "digitalsignage.db";

    public BackupService(
        ILogger<BackupService> logger,
        IDbContextFactory<DigitalSignageDbContext> contextFactory)
    {
        _logger = logger;
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Create a backup of the database to the specified target path
    /// </summary>
    /// <param name="targetPath">Full path where backup will be saved</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result> CreateBackupAsync(string targetPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return Result.Failure("Target path cannot be empty");
            }

            _logger.LogInformation("Starting database backup to: {TargetPath}", targetPath);

            // Get the source database path
            var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DATABASE_FILE);

            if (!File.Exists(sourcePath))
            {
                _logger.LogError("Source database file not found: {SourcePath}", sourcePath);
                return Result.Failure($"Database file not found: {sourcePath}");
            }

            // Close all database connections to ensure file is not locked
            await using (var context = await _contextFactory.CreateDbContextAsync())
            {
                _logger.LogInformation("Closing database connections...");
                await context.Database.CloseConnectionAsync();
            }

            // Wait a bit for connections to fully close
            await Task.Delay(500);

            // Ensure target directory exists
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
                _logger.LogInformation("Created target directory: {Directory}", targetDirectory);
            }

            // Copy main database file (avoid blocking thread)
            _logger.LogInformation("Copying database file from {Source} to {Target}", sourcePath, targetPath);
            await Task.Run(() => File.Copy(sourcePath, targetPath, overwrite: true));

            // Also copy WAL (Write-Ahead Log) file if it exists
            var sourceWalPath = $"{sourcePath}-wal";
            if (File.Exists(sourceWalPath))
            {
                var targetWalPath = $"{targetPath}-wal";
                await Task.Run(() => File.Copy(sourceWalPath, targetWalPath, overwrite: true));
                _logger.LogInformation("Copied WAL file");
            }

            // Also copy SHM (Shared Memory) file if it exists
            var sourceShmPath = $"{sourcePath}-shm";
            if (File.Exists(sourceShmPath))
            {
                var targetShmPath = $"{targetPath}-shm";
                await Task.Run(() => File.Copy(sourceShmPath, targetShmPath, overwrite: true));
                _logger.LogInformation("Copied SHM file");
            }

            // Verify backup file was created and has content
            var backupFileInfo = new FileInfo(targetPath);
            if (!backupFileInfo.Exists || backupFileInfo.Length == 0)
            {
                _logger.LogError("Backup file was not created or is empty");
                return Result.Failure("Backup file was not created properly");
            }

            _logger.LogInformation("Database backup completed successfully. Size: {Size} bytes", backupFileInfo.Length);
            return Result.Success($"Backup created successfully: {targetPath} ({backupFileInfo.Length / 1024} KB)");
        }
        catch (IOException ioEx)
        {
            _logger.LogError(ioEx, "I/O error during backup");
            return Result.Failure($"File access error: {ioEx.Message}");
        }
        catch (UnauthorizedAccessException uaEx)
        {
            _logger.LogError(uaEx, "Access denied during backup");
            return Result.Failure($"Access denied: {uaEx.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            return Result.Failure($"Backup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Restore database from a backup file
    /// </summary>
    /// <param name="backupPath">Full path to the backup file</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result> RestoreBackupAsync(string backupPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                return Result.Failure("Backup path cannot be empty");
            }

            if (!File.Exists(backupPath))
            {
                _logger.LogError("Backup file not found: {BackupPath}", backupPath);
                return Result.Failure($"Backup file not found: {backupPath}");
            }

            _logger.LogInformation("Starting database restore from: {BackupPath}", backupPath);

            var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DATABASE_FILE);

            // Close all database connections to ensure file is not locked
            await using (var context = await _contextFactory.CreateDbContextAsync())
            {
                _logger.LogInformation("Closing database connections...");
                await context.Database.CloseConnectionAsync();
            }

            // Wait a bit for connections to fully close
            await Task.Delay(500);

            // Create safety backup of current database before restoring
            var safetyBackupPath = $"{targetPath}.before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.db";
            if (File.Exists(targetPath))
            {
                _logger.LogInformation("Creating safety backup of current database: {SafetyBackup}", safetyBackupPath);
                await Task.Run(() => File.Copy(targetPath, safetyBackupPath, overwrite: true));
                _logger.LogInformation("Safety backup created successfully");
            }

            // Delete existing WAL and SHM files to prevent corruption
            var walPath = $"{targetPath}-wal";
            var shmPath = $"{targetPath}-shm";

            if (File.Exists(walPath))
            {
                await Task.Run(() => File.Delete(walPath));
                _logger.LogInformation("Deleted existing WAL file");
            }

            if (File.Exists(shmPath))
            {
                await Task.Run(() => File.Delete(shmPath));
                _logger.LogInformation("Deleted existing SHM file");
            }

            // Restore from backup
            _logger.LogInformation("Copying backup file to database location");
            await Task.Run(() => File.Copy(backupPath, targetPath, overwrite: true));

            // Copy WAL file if it exists
            var backupWalPath = $"{backupPath}-wal";
            if (File.Exists(backupWalPath))
            {
                await Task.Run(() => File.Copy(backupWalPath, walPath, overwrite: true));
                _logger.LogInformation("Restored WAL file");
            }

            // Copy SHM file if it exists
            var backupShmPath = $"{backupPath}-shm";
            if (File.Exists(backupShmPath))
            {
                await Task.Run(() => File.Copy(backupShmPath, shmPath, overwrite: true));
                _logger.LogInformation("Restored SHM file");
            }

            // Verify restored database
            var restoredFileInfo = new FileInfo(targetPath);
            if (!restoredFileInfo.Exists || restoredFileInfo.Length == 0)
            {
                _logger.LogError("Restored database file is empty or missing");

                // Attempt to rollback using safety backup
                if (File.Exists(safetyBackupPath))
                {
                    _logger.LogWarning("Attempting to rollback using safety backup");
                    await Task.Run(() => File.Copy(safetyBackupPath, targetPath, overwrite: true));
                }

                return Result.Failure("Database restore verification failed. Rolled back to previous state.");
            }

            // Test database connection
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var canConnect = await context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    _logger.LogError("Cannot connect to restored database");

                    // Attempt to rollback using safety backup
                    if (File.Exists(safetyBackupPath))
                    {
                        _logger.LogWarning("Attempting to rollback using safety backup");
                        await Task.Run(() => File.Copy(safetyBackupPath, targetPath, overwrite: true));
                    }

                    return Result.Failure("Restored database is not valid. Rolled back to previous state.");
                }

                _logger.LogInformation("Database connection test successful");
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Database connection test failed after restore");

                // Attempt to rollback using safety backup
                if (File.Exists(safetyBackupPath))
                {
                    _logger.LogWarning("Attempting to rollback using safety backup");
                    await Task.Run(() => File.Copy(safetyBackupPath, targetPath, overwrite: true));
                }

                return Result.Failure($"Restored database connection failed: {dbEx.Message}. Rolled back to previous state.");
            }

            _logger.LogInformation("Database restore completed successfully");
            _logger.LogInformation("Safety backup kept at: {SafetyBackup}", safetyBackupPath);

            return Result.Success($"Database restored successfully from: {backupPath}");
        }
        catch (IOException ioEx)
        {
            _logger.LogError(ioEx, "I/O error during restore");
            return Result.Failure($"File access error: {ioEx.Message}");
        }
        catch (UnauthorizedAccessException uaEx)
        {
            _logger.LogError(uaEx, "Access denied during restore");
            return Result.Failure($"Access denied: {uaEx.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database restore failed");
            return Result.Failure($"Restore failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get list of available backup files in the specified directory
    /// </summary>
    /// <param name="backupDirectory">Directory to search for backups</param>
    /// <returns>List of backup file information</returns>
    public async Task<List<BackupInfo>> GetAvailableBackupsAsync(string? backupDirectory = null)
    {
        try
        {
            var searchDirectory = backupDirectory ?? AppDomain.CurrentDomain.BaseDirectory;

            if (!Directory.Exists(searchDirectory))
            {
                _logger.LogWarning("Backup directory not found: {Directory}", searchDirectory);
                return new List<BackupInfo>();
            }

            var backupFiles = await Task.Run(() =>
            {
                return Directory.GetFiles(searchDirectory, "*.db")
                    .Where(f => f.Contains("backup", StringComparison.OrdinalIgnoreCase))
                    .Select(f => new BackupInfo
                    {
                        FilePath = f,
                        FileName = Path.GetFileName(f),
                        CreatedDate = File.GetCreationTime(f),
                        Size = new FileInfo(f).Length
                    })
                    .OrderByDescending(b => b.CreatedDate)
                    .ToList();
            });

            _logger.LogInformation("Found {Count} backup files in {Directory}", backupFiles.Count, searchDirectory);
            return backupFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available backups");
            return new List<BackupInfo>();
        }
    }
}

/// <summary>
/// Result class for backup/restore operations
/// </summary>
public class BackupInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public long Size { get; set; }

    public string SizeFormatted => Size < 1024 * 1024
        ? $"{Size / 1024} KB"
        : $"{Size / (1024 * 1024)} MB";
}
