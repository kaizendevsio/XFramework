using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NUnit.Framework;
using Yap.Client.Services;

namespace Yap.Client.Tests;

/// <summary>
/// The browser builds <see cref="OfflineDatabase"/>'s model from the generated
/// <c>OfflineDatabaseModel</c> instead of reflecting over the entity classes at every launch.
/// A generated model that no longer matches those classes would quietly read and write the
/// wrong columns, so these tests fail the build the moment the two disagree.
/// Regenerate with the command in <c>docs/solutions/developer-experience/yap-ef-compiled-model.md</c>.
/// </summary>
public sealed class CompiledModelTests
{
    [Test]
    public void CompiledModel_MatchesTheEntityClasses()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        Assert.That(Shape(Build(connection, compiled: true)), Is.EqualTo(Shape(Build(connection, compiled: false))),
            "The generated compiled model is stale. Regenerate it (see docs/solutions/developer-experience/yap-ef-compiled-model.md).");
    }

    [Test]
    public async Task CompiledModel_QueriesTheSchemaTheAppActuallyCreates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Build(connection, compiled: true);
        // EnsureCreated and UpgradeAsync both work off the conventional model, exactly as
        // DatabaseStartup does, so every column the compiled model queries has to appear there.
        await db.Database.EnsureCreatedAsync();
        await OfflineDatabase.UpgradeAsync(db);
        await OfflineDatabase.UpgradeAsync(db);
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName()!;
            var columns = await db.Database.SqlQuery<string>($"SELECT name AS Value FROM pragma_table_info({table})").ToListAsync();
            Assert.That(columns, Is.SupersetOf(entity.GetProperties().Select(p => p.GetColumnName())), $"table {table}");
        }

        var id = Guid.NewGuid();
        db.Messages.Add(new() { Scope = "a", Id = id, ThreadId = id, CreatedTicks = 7, IsThreadReply = true, Saved = true, Json = "{}" });
        db.Settings.Add(new() { Key = "user", Value = "account" });
        await db.SaveChangesAsync();

        var saved = await db.Messages.AsNoTracking().SingleAsync(x => x.Scope == "a" && x.Saved);
        Assert.Multiple(() =>
        {
            Assert.That(saved.Id, Is.EqualTo(id));
            Assert.That(saved.IsThreadReply, Is.True);
            Assert.That(db.Settings.Single().Value, Is.EqualTo("account"));
        });
    }

    private static OfflineDatabase Build(SqliteConnection connection, bool compiled)
    {
        var options = new DbContextOptionsBuilder<OfflineDatabase>().UseSqlite(connection);
        if (compiled) options.UseModel(OfflineDatabaseModel.Instance);
        return new OfflineDatabase(options.Options);
    }

    // Column-level projection: the generated model and the reflected one are different IModel
    // implementations, so compare what the database actually depends on rather than the objects.
    private static string Shape(OfflineDatabase db) => string.Join('\n', db.Model.GetEntityTypes()
        .OrderBy(e => e.Name, StringComparer.Ordinal)
        .Select(e => $"{e.Name} table={e.GetTableName()}\n"
            + string.Join('\n', e.GetProperties().OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => $"  {p.Name} {p.ClrType} column={p.GetColumnName()} type={p.GetColumnType()} null={p.IsNullable} generated={p.ValueGenerated}"))
            + "\n" + string.Join('\n', e.GetKeys().Select(k => $"  key {Names(k.Properties)}"))
            + "\n" + string.Join('\n', e.GetIndexes().Select(i => $"  index {Names(i.Properties)} unique={i.IsUnique}"))
            + "\n" + string.Join('\n', e.GetForeignKeys().Select(f => $"  fk {Names(f.Properties)} -> {f.PrincipalEntityType.Name}"))));

    private static string Names(IEnumerable<IProperty> properties) => string.Join(',', properties.Select(p => p.Name));
}
