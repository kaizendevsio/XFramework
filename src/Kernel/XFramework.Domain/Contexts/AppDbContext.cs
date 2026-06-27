using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Shared.Contracts;

namespace XFramework.Domain.Contexts;

/// <summary>
/// Application DbContext that auto-discovers entity configurations from all loaded assemblies.
/// Each module's Domain.Shared project contains IEntityTypeConfiguration&lt;T&gt; classes that are
/// picked up via ApplyConfigurationsFromAssembly. Services control which modules are loaded
/// by their ProjectReference graph — only referenced assemblies contribute entity configurations.
///
/// DbSet properties for module entities are declared in each module's Domain.Shared via
/// partial class extensions or accessed via context.Set&lt;T&gt;().
/// </summary>
public partial class AppDbContext : XDbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public AppDbContext(DbContextOptions options, IHttpContextAccessor httpContextAccessor)
        : base(options, httpContextAccessor)
    {
    }

    public AppDbContext(DbContextOptions options, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        : base(options, httpContextAccessor, configuration)
    {
    }

    // Framework-level entities (XFramework.Domain.Shared)
    public virtual DbSet<MetaData> MetaData { get; set; }
    public virtual DbSet<MetaDataType> MetaDataTypes { get; set; }
    public virtual DbSet<MetaDataTypeGroup> MetaDataTypeGroups { get; set; }
    public virtual DbSet<PaymentGateway> PaymentGateways { get; set; }
    public virtual DbSet<PaymentGatewayCategory> PaymentGatewayCategories { get; set; }
    public virtual DbSet<PaymentGatewayEndpoint> PaymentGatewayEndpoints { get; set; }
    public virtual DbSet<PaymentGatewayType> PaymentGatewayTypes { get; set; }
    public virtual DbSet<PaymentGatewayInstruction> PaymentGatewayInstructions { get; set; }
    public virtual DbSet<PaymentGatewayResponse> PaymentGatewayResponses { get; set; }
    public virtual DbSet<PaymentGatewayResponseStatusType> PaymentGatewayResponseStatusTypes { get; set; }
    public virtual DbSet<PaymentGatewayResponseType> PaymentGatewayResponseTypes { get; set; }

    // Module-specific DbSets (Identity, Wallets, Messaging, Community) are discovered
    // automatically via IEntityTypeConfiguration<T> in each module's Domain.Shared assembly.
    // Access them via context.Set<T>() in service code.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        // Auto-discover IEntityTypeConfiguration<T> from all referenced assemblies.
        // Each module's Domain.Shared contains its own entity configurations.
        var configAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName is not null
                && (a.FullName.Contains("Domain.Shared") || a.FullName.Contains("XFramework.Domain")));

        foreach (var assembly in configAssemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        base.OnModelCreating(modelBuilder);
    }
}
