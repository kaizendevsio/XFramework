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
    .Options;

try
{
    using var context = new AppDbContext(options);
    var connection = context.Database.GetDbConnection();
    context.Database.OpenConnection();

    using (var auditContextCommand = connection.CreateCommand())
    {
        auditContextCommand.CommandText =
            """
            SELECT
                set_config('xframework.audit.actor_kind', 'System', false),
                set_config('xframework.audit.service_name', 'XFramework.MigrationRunner', false),
                set_config('xframework.audit.environment', @environment, false),
                set_config('xframework.audit.instance_id', @instance_id, false),
                set_config('xframework.audit.operation_name', 'Database migration', false),
                set_config('xframework.audit.transaction_ordinal', '', false);
            """;
        AddParameter(
            auditContextCommand,
            "environment",
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown");
        AddParameter(auditContextCommand, "instance_id", Environment.MachineName);
        auditContextCommand.ExecuteNonQuery();
    }

    var pending = context.Database.GetPendingMigrations().ToList();

    if (pending.Count == 0)
    {
        Console.WriteLine("[MigrationRunner] Database is up to date. No migrations to apply.");
    }
    else
    {
        Console.WriteLine($"[MigrationRunner] Applying {pending.Count} pending migration(s)...");
        foreach (var migration in pending)
        {
            Console.WriteLine($"  - {migration}");
        }

        context.Database.Migrate();
        Console.WriteLine("[MigrationRunner] All migrations applied successfully.");
    }

    using var ensureAuditCommand = connection.CreateCommand();
    ensureAuditCommand.CommandText = "SELECT audit.ensure_table_triggers();";
    var installedTriggerCount = Convert.ToInt32(ensureAuditCommand.ExecuteScalar());
    Console.WriteLine($"[MigrationRunner] Audit coverage installed on {installedTriggerCount} new table(s).");

    using var verifyAuditCommand = connection.CreateCommand();
    verifyAuditCommand.CommandText =
        "SELECT string_agg(format('%I.%I', schema_name, table_name), ', ') FROM audit.missing_table_triggers();";
    var missingTables = verifyAuditCommand.ExecuteScalar() as string;
    if (!string.IsNullOrWhiteSpace(missingTables))
    {
        throw new InvalidOperationException($"Audit trigger coverage is incomplete: {missingTables}");
    }

    Console.WriteLine("[MigrationRunner] Audit trigger coverage verified.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[MigrationRunner] Migration failed: {ex.Message}");
    Console.Error.WriteLine(ex.ToString());
    return 1;
}

static void AddParameter(System.Data.Common.DbCommand command, string name, string value)
{
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value;
    command.Parameters.Add(parameter);
}
