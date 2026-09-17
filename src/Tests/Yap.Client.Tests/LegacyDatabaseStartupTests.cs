using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Yap.Client.Services;

namespace Yap.Client.Tests;

/// <summary>
/// Startup against a database an earlier release left on the device. Every client-storage change
/// has to pass this: the columns a release adds are only ever reachable through UpgradeAsync's
/// ALTER TABLE branch, which no fresh-origin test and no fresh browser profile ever executes.
/// </summary>
public sealed class LegacyDatabaseStartupTests
{
    // Exactly the columns UpgradeAsync knows how to add. A database written before they existed is
    // what an upgrading device actually hands the new build.
    private static readonly (string Table, string Column)[] AddedSince = [
        ("Outbox", "UploadId"), ("Messages", "IsThreadReply"), ("Messages", "Saved")];

    [Test]
    public async Task Startup_UpgradesADatabaseWrittenBeforeTheAddedColumnsExisted()
    {
        var file = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"legacy-{Guid.NewGuid():N}.db");
        var id = Guid.NewGuid();
        try
        {
            await WriteLegacyDatabaseAsync(file, id);

            // Reopen the way Program.cs configures the browser: the generated model, not a reflected one.
            await using (var db = Open(file))
            {
                await OfflineDatabase.PrepareAsync(db);
                await OfflineDatabase.PrepareAsync(db); // Second launch: the upgrade has to stay idempotent.
            }

            await using var reopened = Open(file);
            // The gate for every future client-storage change: a column added to the entities but not
            // to UpgradeAsync reads fine on a new device and throws "no such column" on every device
            // that already had Yap, which no fresh-origin test can see.
            foreach (var entity in reopened.Model.GetEntityTypes())
            {
                var table = entity.GetTableName()!;
                var columns = await reopened.Database.SqlQuery<string>($"SELECT name AS Value FROM pragma_table_info({table})").ToListAsync();
                Assert.That(columns, Is.SupersetOf(entity.GetProperties().Select(p => p.GetColumnName())), $"table {table}");
            }

            var message = await reopened.Messages.AsNoTracking().SingleAsync(x => x.Scope == "a" && x.Id == id);
            Assert.Multiple(() =>
            {
                // Backfilled from the serialized body, because the column did not exist when the row was written.
                Assert.That(message.IsThreadReply, Is.True);
                Assert.That(message.Saved, Is.True);
                Assert.That(reopened.Outbox.AsNoTracking().Single().UploadId, Is.Null);
                Assert.That(reopened.Settings.AsNoTracking().Single().Value, Is.EqualTo("account"), "settings survived the upgrade");
            });
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(file); }
    }

    private static OfflineDatabase Open(string file) => new(new DbContextOptionsBuilder<OfflineDatabase>()
        .UseModel(OfflineDatabaseModel.Instance).UseSqlite($"Data Source={file}").Options);

    // The previous release's file: rows written first, then the columns added since dropped back off,
    // so what survives is exactly what a device carrying older history hands the new build - the flags
    // recorded nowhere but the serialized body. The reflected model writes it, as the older build did.
    private static async Task WriteLegacyDatabaseAsync(string file, Guid id)
    {
        await using var db = new OfflineDatabase(new DbContextOptionsBuilder<OfflineDatabase>().UseSqlite($"Data Source={file}").Options);
        await db.Database.EnsureCreatedAsync();
        db.Messages.Add(new() { Scope = "a", Id = id, ThreadId = id, CreatedTicks = 7, Json = """{"isThreadReply":true,"saved":true}""" });
        db.Outbox.Add(new() { Scope = "a", Id = id, ThreadId = id, Text = "queued before the upgrade", CreatedTicks = 7 });
        db.Settings.Add(new() { Key = "user", Value = "account" });
        await db.SaveChangesAsync();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        foreach (var (table, column) in AddedSince)
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER TABLE \"{table}\" DROP COLUMN \"{column}\"";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
