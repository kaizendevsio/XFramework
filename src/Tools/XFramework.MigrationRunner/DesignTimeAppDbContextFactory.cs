using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using XFramework.Domain.Contexts;

namespace XFramework.MigrationRunner;

public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private static readonly string[] DomainAssemblyNames =
    [
        "XFramework.Domain.Shared",
        "Bolt.Domain.Shared",
        "Attendance.Domain.Shared",
        "Community.Domain.Shared",
        "IdentityServer.Domain.Shared",
        "Inventario.Domain.Shared",
        "Messaging.Domain.Shared",
        "Notifications.Domain.Shared",
        "SmsGateway.Domain.Shared",
        "Storage.Domain.Shared",
        "Wallets.Domain.Shared"
    ];

    public AppDbContext CreateDbContext(string[] args)
    {
        LoadDomainAssemblies();

        var connectionString = Environment.GetEnvironmentVariable("DefaultDatabaseConnection")
            ?? "Host=localhost;Database=xframework_design;Username=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }

    private static void LoadDomainAssemblies()
    {
        foreach (var assemblyName in DomainAssemblyNames)
        {
            if (AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => assembly.GetName().Name == assemblyName))
            {
                continue;
            }

            Assembly.Load(new AssemblyName(assemblyName));
        }
    }
}
