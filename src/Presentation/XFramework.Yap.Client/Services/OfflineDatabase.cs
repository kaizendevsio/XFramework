using Microsoft.EntityFrameworkCore;

namespace Yap.Client.Services;

// This database is a device-local cache/outbox, not an XFramework module database.
public sealed class OfflineDatabase(DbContextOptions<OfflineDatabase> options) : DbContext(options)
{
    public DbSet<CachedConversation> Conversations => Set<CachedConversation>();
    public DbSet<CachedMessage> Messages => Set<CachedMessage>();
    public DbSet<QueuedMessage> Outbox => Set<QueuedMessage>();
    public DbSet<SavedDraft> Drafts => Set<SavedDraft>();
    public DbSet<LocalSetting> Settings => Set<LocalSetting>();
    public DbSet<CachedAttachment> Attachments => Set<CachedAttachment>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<CachedConversation>().HasKey(x => new { x.Scope, x.Id });
        model.Entity<CachedMessage>().HasKey(x => new { x.Scope, x.Id });
        model.Entity<CachedMessage>().HasIndex(x => new { x.Scope, x.ThreadId, x.CreatedTicks });
        model.Entity<QueuedMessage>().HasKey(x => new { x.Scope, x.Id });
        model.Entity<QueuedMessage>().HasIndex(x => new { x.Scope, x.CreatedTicks });
        model.Entity<SavedDraft>().HasKey(x => new { x.Scope, x.Key });
        model.Entity<LocalSetting>().HasKey(x => x.Key);
        model.Entity<CachedAttachment>().HasKey(x => new { x.Scope, x.Id });
    }

    /// <summary>
    /// EnsureCreated never alters a database an earlier build already created, so additive
    /// columns are applied here instead. Each statement is skipped when the column exists.
    /// </summary>
    public static async Task UpgradeAsync(OfflineDatabase db, CancellationToken ct = default)
    {
        await AddColumnAsync(db, "Outbox", "UploadId", "TEXT NULL", ct);
    }

    private static async Task AddColumnAsync(OfflineDatabase db, string table, string column, string definition, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);
        await using (var probe = connection.CreateCommand())
        {
            probe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
            if (Convert.ToInt64(await probe.ExecuteScalarAsync(ct)) > 0) return;
        }
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
        await alter.ExecuteNonQueryAsync(ct);
    }
}

public sealed class CachedConversation
{
    public string Scope { get; set; } = "";
    public Guid Id { get; set; }
    public string Json { get; set; } = "";
}
public sealed class CachedMessage
{
    public string Scope { get; set; } = "";
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public Guid? ParentId { get; set; }
    public long CreatedTicks { get; set; }
    public string Json { get; set; } = "";
}
public sealed class QueuedMessage
{
    public string Scope { get; set; } = "";
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public string Text { get; set; } = "";
    public Guid? ParentId { get; set; }
    public long CreatedTicks { get; set; }
    public bool MessageConfirmed { get; set; }
    public string? FileKey { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public Guid? StorageId { get; set; }
    /// <summary>Resumable session for an attachment too large to stage. Completed at send time.</summary>
    public Guid? UploadId { get; set; }
    public string? Error { get; set; }
    public bool Paused { get; set; }
}
public sealed class SavedDraft
{
    public string Scope { get; set; } = "";
    public string Key { get; set; } = "";
    public string Text { get; set; } = "";
}
public sealed class LocalSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
public sealed class CachedAttachment
{
    public string Scope { get; set; } = "";
    public Guid Id { get; set; }
    public string FileKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
}
