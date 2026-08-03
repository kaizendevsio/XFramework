using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Contexts;

public class XDbContext : DbContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly IConfiguration? _configuration;

    /// <summary>
    /// Parameterless constructor for EF Core design-time tooling only.
    /// Do not use in production — tenant resolution will not work.
    /// </summary>
    public XDbContext()
    {
    }

    public XDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public XDbContext(DbContextOptions options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public XDbContext(DbContextOptions options, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    /// <summary>
    /// Property used by EF Core parameterized query filters.
    /// EF Core evaluates this per-query (not at model-build time) because the filter
    /// expression references this property via Expression.Property on the DbContext instance.
    /// </summary>
    private Guid CurrentTenantId => GetCurrentTenantId();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ApplyGlobalFilters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Applies global query filters for soft delete (ISoftDeletable) and multi-tenancy (IHasTenantId).
    /// These filters automatically exclude soft-deleted records and filter by current tenant ID.
    /// Use .IgnoreQueryFilters() in queries to bypass these filters when needed (e.g., admin operations).
    /// </summary>
    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            var implementsSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);
            var implementsHasTenantId = typeof(IHasTenantId).IsAssignableFrom(clrType);
            var allowsGlobalTenantRows = typeof(IAllowsGlobalTenantRows).IsAssignableFrom(clrType);

            if (!implementsSoftDeletable && !implementsHasTenantId)
                continue;

            var parameter = Expression.Parameter(clrType, "e");
            Expression? filterExpression = null;

            // Build soft delete filter: e.IsDeleted == false
            if (implementsSoftDeletable)
            {
                var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var falseConstant = Expression.Constant(false);
                filterExpression = Expression.Equal(isDeletedProperty, falseConstant);
            }

            // Build multi-tenancy filter: e.TenantId == this.CurrentTenantId
            // Uses Expression.Property on the DbContext instance so EF Core evaluates per-query,
            // not at model-build time. This is the standard EF Core parameterized filter pattern.
            if (implementsHasTenantId)
            {
                var tenantIdProperty = Expression.Property(parameter, nameof(IHasTenantId.TenantId));
                var contextRef = Expression.Constant(this, typeof(XDbContext));
                var currentTenantProperty = Expression.Property(contextRef, nameof(CurrentTenantId));
                var tenantFilter = Expression.Equal(tenantIdProperty, currentTenantProperty);
                if (allowsGlobalTenantRows)
                {
                    tenantFilter = Expression.OrElse(
                        tenantFilter,
                        Expression.Equal(tenantIdProperty, Expression.Constant(Guid.Empty)));
                }

                filterExpression = filterExpression is null
                    ? tenantFilter
                    : Expression.AndAlso(filterExpression, tenantFilter);
            }

            if (filterExpression is not null)
            {
                var lambda = Expression.Lambda(filterExpression, parameter);
                modelBuilder.Entity(clrType).HasQueryFilter(lambda);
            }
        }
    }

    /// <summary>
    /// Retrieves the current tenant ID from (in priority order):
    ///   1. HttpContext claims (tenant_id / tenantId / TenantId / tid / tenant)
    ///   2. Configuration fallback (Tenant:DefaultId) — for unauthenticated endpoints like auth/verify
    ///   3. Guid.Empty — design-time / migration-only (global filter excludes everything)
    /// </summary>
    private Guid GetCurrentTenantId()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = httpContext.User.FindFirst("tenant_id")?.Value
                ?? httpContext.User.FindFirst("tenantId")?.Value
                ?? httpContext.User.FindFirst("TenantId")?.Value
                ?? httpContext.User.FindFirst("tid")?.Value
                ?? httpContext.User.FindFirst("tenant")?.Value;

            if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tenantId))
            {
                return tenantId;
            }

            throw new InvalidOperationException(
                "Authenticated user does not have a tenant ID claim. " +
                "Ensure the authentication provider includes a supported tenant claim.");
        }

        // Unauthenticated requests (auth, verify, register) — use configured default tenant
        var defaultTenantId = _configuration?["Tenant:DefaultId"];
        if (!string.IsNullOrEmpty(defaultTenantId) && Guid.TryParse(defaultTenantId, out var defaultId))
        {
            return defaultId;
        }

        return Guid.Empty;
    }

    public override int SaveChanges()
    {
        OnBeforeSaveChanges();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        OnBeforeSaveChanges();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Handles soft-delete conversion, default values, and tenant validation before saving.
    /// Audit timestamps (CreatedAt, ModifiedAt) are handled by <see cref="Interceptors.AuditInterceptor"/>.
    /// </summary>
    private void OnBeforeSaveChanges()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            foreach (var property in entry.Properties)
            {
                switch (property.Metadata.Name)
                {
                    case nameof(BaseModel.IsEnabled):
                        property.CurrentValue ??= true;
                        break;

                    case nameof(BaseModel.IsDeleted):
                        if (entry.State == EntityState.Deleted)
                        {
                            // Convert DELETE to soft-delete UPDATE
                            entry.State = EntityState.Modified;
                            property.CurrentValue = true;

                            var deletedAtProp = entry.Properties
                                .FirstOrDefault(x => x.Metadata.Name == nameof(BaseModel.DeletedAt));
                            if (deletedAtProp is not null)
                            {
                                deletedAtProp.CurrentValue = DateTime.UtcNow;
                            }
                        }
                        else
                        {
                            property.CurrentValue ??= false;
                        }
                        break;

                    case nameof(BaseModel.TenantId):
                        if (property.CurrentValue is null || (Guid)property.CurrentValue == Guid.Empty)
                        {
                            throw new InvalidOperationException(
                                $"Cannot save entity of type '{entry.Entity.GetType().Name}' without a valid TenantId. " +
                                "Ensure TenantId is set before saving.");
                        }
                        break;
                }
            }
        }
    }
}
