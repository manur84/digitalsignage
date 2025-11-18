using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using DigitalSignage.Core.Models;
using DigitalSignage.Data.Entities;
using System.Text.Json;

namespace DigitalSignage.Data;

/// <summary>
/// Database context for Digital Signage application
/// </summary>
public class DigitalSignageDbContext : DbContext
{
    public DigitalSignageDbContext(DbContextOptions<DigitalSignageDbContext> options)
        : base(options)
    {
    }

    // Core entities
    public DbSet<RaspberryPiClient> Clients => Set<RaspberryPiClient>();
    public DbSet<DisplayLayout> DisplayLayouts => Set<DisplayLayout>();
    public DbSet<DataSource> DataSources => Set<DataSource>();

    // Authentication entities
    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ClientRegistrationToken> ClientRegistrationTokens => Set<ClientRegistrationToken>();

    // Media entities
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    // Scheduling entities
    public DbSet<LayoutSchedule> LayoutSchedules => Set<LayoutSchedule>();

    // Audit entities
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Alert entities
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Temporarily suppress pending model changes warning
        // This is being removed as we're migrating to file-based storage
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Value comparers for JSON collections/dictionaries
        var scheduleListComparer = new ValueComparer<List<Schedule>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        var displayElementListComparer = new ValueComparer<List<DisplayElement>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        var dataSourceListComparer = new ValueComparer<List<DataSource>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        var stringObjectDictComparer = new ValueComparer<Dictionary<string, object>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.Count == c2.Count && !c1.Except(c2).Any()),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value != null ? v.Value.GetHashCode() : 0)),
            c => new Dictionary<string, object>(c));

        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        var guidListComparer = new ValueComparer<List<Guid>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        // Configure RaspberryPiClient
        modelBuilder.Entity<RaspberryPiClient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.IpAddress).HasMaxLength(45); // IPv6 max length
            entity.Property(e => e.MacAddress).HasMaxLength(17);
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.Group).HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>();

            // Store DeviceInfo as JSON
            entity.OwnsOne(e => e.DeviceInfo, di =>
            {
                di.ToJson();
                di.Property(d => d.Hostname).IsRequired();
            });

            // Store Schedules as JSON
            entity.Property(e => e.Schedules)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<Schedule>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Schedule>()
                )
                .Metadata.SetValueComparer(scheduleListComparer);

            // Store Metadata as JSON
            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
                )
                .Metadata.SetValueComparer(stringObjectDictComparer);

            // Indexes
            entity.HasIndex(e => e.MacAddress).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.LastSeen);
        });

        // Configure DisplayLayout
        modelBuilder.Entity<DisplayLayout>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Version).HasMaxLength(20);
            entity.Property(e => e.BackgroundImage).HasMaxLength(500);
            entity.Property(e => e.BackgroundColor).HasMaxLength(20);

            // Store Resolution as JSON
            entity.OwnsOne(e => e.Resolution, r =>
            {
                r.ToJson();
            });

            // Store Elements as JSON
            entity.Property(e => e.Elements)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<DisplayElement>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<DisplayElement>()
                )
                .Metadata.SetValueComparer(displayElementListComparer);

            // Store DataSources as JSON
            entity.Property(e => e.DataSources)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<DataSource>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<DataSource>()
                )
                .Metadata.SetValueComparer(dataSourceListComparer);

            // Store Metadata as JSON
            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
                )
                .Metadata.SetValueComparer(stringObjectDictComparer);

            // Store Tags as JSON
            entity.Property(e => e.Tags)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
                )
                .Metadata.SetValueComparer(stringListComparer);

            // Store LinkedDataSourceIds as JSON
            entity.Property(e => e.LinkedDataSourceIds)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Guid>()
                )
                .Metadata.SetValueComparer(guidListComparer);

            entity.Property(e => e.Category).HasMaxLength(100);

            // Indexes
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Created);
            entity.HasIndex(e => e.Category);
        });

        // Configure DataSource (standalone)
        modelBuilder.Entity<DataSource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.ConnectionString).IsRequired();
            entity.Property(e => e.Query).IsRequired();

            // Store Parameters as JSON
            entity.Property(e => e.Parameters)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
                )
                .Metadata.SetValueComparer(stringObjectDictComparer);

            // Store Metadata as JSON
            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
                )
                .Metadata.SetValueComparer(stringObjectDictComparer);

            // Indexes
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Enabled);
        });

        // Configure User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).HasConversion<string>();

            // Indexes
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configure ApiKey
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.KeyHash).IsRequired();

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.KeyHash);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.IsActive);
        });

        // Configure ClientRegistrationToken
        modelBuilder.Entity<ClientRegistrationToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(500);

            // Relationships
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.IsUsed);
        });

        // Configure AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EntityType).HasMaxLength(100);
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(45);

            // Store Changes as JSON
            entity.Property(e => e.Changes)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
                )
                .Metadata.SetValueComparer(stringObjectDictComparer);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        // Configure MediaFile
        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.MimeType).HasMaxLength(100);
            entity.Property(e => e.ThumbnailPath).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Tags).HasMaxLength(500);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Hash).HasMaxLength(64); // SHA256 hash

            // Relationships
            entity.HasOne(e => e.UploadedByUser)
                .WithMany()
                .HasForeignKey(e => e.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.FileName);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.UploadedAt);
            entity.HasIndex(e => e.Hash).IsUnique();
            entity.HasIndex(e => e.Tags);
        });


        // Configure LayoutSchedule
        modelBuilder.Entity<LayoutSchedule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.LayoutId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ClientId).HasMaxLength(100);
            entity.Property(e => e.ClientGroup).HasMaxLength(100);
            entity.Property(e => e.StartTime).IsRequired().HasMaxLength(5);
            entity.Property(e => e.EndTime).IsRequired().HasMaxLength(5);
            entity.Property(e => e.DaysOfWeek).IsRequired().HasMaxLength(100);

            // Indexes
            entity.HasIndex(e => e.LayoutId);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.ClientGroup);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.ValidFrom);
            entity.HasIndex(e => e.ValidUntil);
        });

        // Configure AlertRule
        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.RuleType).HasConversion<string>();
            entity.Property(e => e.Severity).HasConversion<string>();

            // Indexes
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.RuleType);
            entity.HasIndex(e => e.Severity);
        });

        // Configure Alert
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.Severity).HasConversion<string>();
            entity.Property(e => e.EntityType).HasMaxLength(100);

            // Relationships
            entity.HasOne(e => e.AlertRule)
                .WithMany(r => r.Alerts)
                .HasForeignKey(e => e.AlertRuleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.TriggeredAt);
            entity.HasIndex(e => e.IsAcknowledged);
            entity.HasIndex(e => e.IsResolved);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });
    }
}
