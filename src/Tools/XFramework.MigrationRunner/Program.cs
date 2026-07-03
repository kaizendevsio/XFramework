using Microsoft.EntityFrameworkCore;
using System.Reflection;
using XFramework.Domain.Contexts;

string[] domainAssemblyNames =
[
    "XFramework.Domain.Shared",
    "Bolt.Domain.Shared",
    "Attendance.Domain.Shared",
    "Community.Domain.Shared",
    "IdentityServer.Domain.Shared",
    "Inventario.Domain.Shared",
    "Communications.Domain.Shared",
    "Notifications.Domain.Shared",
    "SmsGateway.Domain.Shared",
    "Storage.Domain.Shared",
    "POS.Domain.Shared",
    "Wallets.Domain.Shared"
];

foreach (var assemblyName in domainAssemblyNames)
{
    if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == assemblyName))
        continue;

    Assembly.Load(new AssemblyName(assemblyName));
}

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
    .ConfigureWarnings(w => w.Ignore(
        Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
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
