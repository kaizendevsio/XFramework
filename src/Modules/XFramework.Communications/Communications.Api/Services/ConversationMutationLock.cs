using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace Communications.Api.Services;

// Session advisory locks serialize membership decisions across API replicas. Saves still
// use EF's normal transaction; the lock spans the read/check/write, including early exits.
internal sealed class ConversationMutationLock(DbConnection connection, long key, bool close) : IAsyncDisposable
{
    public static async Task<ConversationMutationLock?> AcquireAsync(DbContext? db, Guid tenant, Guid thread, CancellationToken ct)
        => await AcquireKeyAsync(db, $"communications:members:{tenant}:{thread}", ct);

    // Receipt writes from history, read and explicit delivery share a per-recipient lock.
    // They do not block message sends or other members' receipt processing.
    public static Task<ConversationMutationLock?> AcquireReceiptsAsync(DbContext? db, Guid tenant, Guid member, CancellationToken ct)
        => AcquireKeyAsync(db, $"communications:receipts:{tenant}:{member}", ct);

    private static async Task<ConversationMutationLock?> AcquireKeyAsync(DbContext? db, string identity, CancellationToken ct)
    {
        if (db is null || db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return null;
        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(ct);
        var key = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        try
        {
            await ExecuteAsync(connection, "SELECT pg_advisory_lock(@key)", key, ct);
            return new(connection, key, close);
        }
        catch { if (close) await connection.CloseAsync(); throw; }
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, long key, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.Value = key;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        try { await ExecuteAsync(connection, "SELECT pg_advisory_unlock(@key)", key, CancellationToken.None); }
        finally { if (close) await connection.CloseAsync(); }
    }
}
