using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Yap.Client.Services;

namespace Yap.Client.Tests;

/// <summary>
/// Only used by <c>dotnet ef dbcontext optimize</c>: the browser provider cannot run on a
/// developer machine, but it configures the same SQLite provider underneath, so the model the
/// tool builds here is the model the app uses.
/// </summary>
public sealed class OfflineDatabaseDesignTimeFactory : IDesignTimeDbContextFactory<OfflineDatabase>
{
    public OfflineDatabase CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<OfflineDatabase>().UseSqlite("Data Source=design-time.db").Options);
}
