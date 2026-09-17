using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SqliteWasmBlazor;

namespace Yap.Client.Services;

// Run after the root component mounts so updates and recovery remain usable
// while another document owns SQLite. Never steal its lock or clear its data.
public sealed class DatabaseStartup(IServiceProvider services, IJSRuntime js)
{
    // Waiting for another tab's lock is unbounded on purpose - that tab may be in use - but once
    // this document owns the database nothing below may wait forever: a stalled worker or a stalled
    // upgrade has to reach the recovery screen instead of spinning. Nothing here writes until the
    // ALTER TABLE statements, and each of those is guarded, so the retry button is always safe.
    private static readonly TimeSpan StorageDeadline = TimeSpan.FromSeconds(90);

    public string Phase { get; private set; } = "database-lock";

    public async Task<bool> TryInitializeAsync()
    {
        if (!await js.InvokeAsync<bool>("yap.device.acquireDatabase")) return false;
        using var deadline = new CancellationTokenSource(StorageDeadline);
        await StageAsync("sqlite-runtime");
        await services.InitializeSqliteWasmAsync(deadline.Token);
        await StageAsync("sqlite-open");
        await services.InitializeSqliteWasmDatabaseAsync<OfflineDatabase>().WaitAsync(deadline.Token);
        await using var db = await services.GetRequiredService<IDbContextFactory<OfflineDatabase>>().CreateDbContextAsync(deadline.Token);
        await StageAsync("sqlite-schema");
        await OfflineDatabase.PrepareAsync(db, deadline.Token);
        await StageAsync("account-restore");
        return true;
    }

    private async Task StageAsync(string phase)
    {
        Phase = phase;
        await js.InvokeVoidAsync("yap.diagnostics.record", "startup.stage", new { phase });
    }
}
