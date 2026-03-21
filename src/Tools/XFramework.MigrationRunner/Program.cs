using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Contexts;

var connectionString = Environment.GetEnvironmentVariable("DefaultDatabaseConnection")
    ?? throw new InvalidOperationException(
        "DefaultDatabaseConnection environment variable is not set. " +
        "Set it to a PostgreSQL connection string, e.g.: Host=localhost;Database=XFramework;Username=dbAdmin;Password=secret");

Console.WriteLine("[MigrationRunner] Connecting to database...");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString, npgsql => npgsql
        .EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null))
    .Options;

try
{
    using var context = new AppDbContext(options);

    var pending = context.Database.GetPendingMigrations().ToList();

    if (pending.Count == 0)
    {
        Console.WriteLine("[MigrationRunner] Database is up to date. No migrations to apply.");
        return 0;
    }

    Console.WriteLine($"[MigrationRunner] Applying {pending.Count} pending migration(s)...");
    foreach (var migration in pending)
    {
        Console.WriteLine($"  - {migration}");
    }

    context.Database.Migrate();

    Console.WriteLine("[MigrationRunner] All migrations applied successfully.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[MigrationRunner] Migration failed: {ex.Message}");
    Console.Error.WriteLine(ex.ToString());
    return 1;
}
