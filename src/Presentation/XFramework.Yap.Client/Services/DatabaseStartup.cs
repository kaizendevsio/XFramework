using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SqliteWasmBlazor;

namespace Yap.Client.Services;

// Run after the root component mounts so updates and recovery remain usable
// while another document owns SQLite. Never steal its lock or clear its data.
public sealed class DatabaseStartup(IServiceProvider services, IJSRuntime js)
{
    public string Phase { get; private set; } = "database-lock";

    public async Task<bool> TryInitializeAsync()
    {
        if (!await js.InvokeAsync<bool>("yap.device.acquireDatabase")) return false;
        await StageAsync("sqlite-runtime");
        await services.InitializeSqliteWasmAsync();
        await StageAsync("sqlite-open");
        await services.InitializeSqliteWasmDatabaseAsync<OfflineDatabase>();
        await using var db = await services.GetRequiredService<IDbContextFactory<OfflineDatabase>>().CreateDbContextAsync();
        await StageAsync("sqlite-schema");
        await db.Database.EnsureCreatedAsync();
        await OfflineDatabase.UpgradeAsync(db);
        await StageAsync("account-restore");
        return true;
    }

    private async Task StageAsync(string phase)
    {
        Phase = phase;
        await js.InvokeVoidAsync("yap.diagnostics.record", "startup.stage", new { phase });
    }
}
