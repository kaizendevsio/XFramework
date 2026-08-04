using IdentityServer.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using XFramework.Core.Services;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Security;
using XFramework.Integration.Security;
using FeatureGateService = XFramework.Core.Services.FeatureGates.ITenantModuleFeatureService;

namespace IdentityServer.Api.Features.Tenants;

public interface ITenantAdministrationService
{
    Task<Result<TenantAdministrationResponse>> CreateAsync(CreateTenantRequest request, CancellationToken ct);
    Task<Result<TenantAdministrationResponse>> UpdateAsync(UpdateTenantRequest request, CancellationToken ct);
    Task<Result> DeleteAsync(DeleteTenantRequest request, CancellationToken ct);
}

public sealed class TenantAdministrationService(
    IDataContext dataContext,
    DbContext dbContext,
    ITenantResolver tenantResolver,
    FeatureGateService featureService,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor,
    ICrossTenantWriteAuthorizationScopeFactory crossTenantWriteAuthorizationScopeFactory) : ITenantAdministrationService
{
    private static readonly (string Name, Guid SystemReferenceId)[] RequiredSessionTypes =
    [
        ("User", IdentityConstants.SessionType.User),
        ("Service", IdentityConstants.SessionType.Service),
        ("Rpc", IdentityConstants.SessionType.Rpc)
    ];

    public async Task<Result<TenantAdministrationResponse>> CreateAsync(
        CreateTenantRequest request,
        CancellationToken ct)
    {
        if (trustedInvocationContextAccessor.Current?.EffectiveTenantId is not { } activeTenantId || activeTenantId == Guid.Empty)
            return Result<TenantAdministrationResponse>.Forbidden("An authorized tenant context is required");

        if (request.ParentTenantId is { } parentTenantId)
        {
            var parentExists = await dataContext.Query<Tenant>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(tenant => tenant.Id == parentTenantId)
                .Where(tenant => !tenant.IsDeleted)
                .AnyAsync(ct);
            if (!parentExists)
                return Result<TenantAdministrationResponse>.NotFound("Parent tenant not found");
        }

        var now = DateTime.UtcNow;
        var newTenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = newTenantId,
            TenantId = newTenantId,
            Name = request.Name!.Trim(),
            Description = request.Description,
            Version = request.Version,
            Status = request.Status ?? 1,
            Expiration = request.Expiration,
            AvailabilityDate = request.AvailabilityDate,
            ParentTenantId = request.ParentTenantId,
            CreatedAt = now,
            IsEnabled = true,
            ConcurrencyStamp = Guid.NewGuid()
        };

        using (crossTenantWriteAuthorizationScopeFactory.BeginTenantAdministrationScope())
        {
            dataContext.Add(tenant);
            dataContext.Add(new TenantAuthorizationPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = newTenantId,
                MissingPermissionBehavior = MissingPermissionBehavior.Deny,
                CreatedAt = now,
                IsEnabled = true,
                ConcurrencyStamp = Guid.NewGuid()
            });

            foreach (var feature in TenantModuleFeatureKeys.All.Where(feature =>
                         string.Equals(feature.ModuleKey, TenantModuleFeatureKeys.Identity, StringComparison.OrdinalIgnoreCase)))
            {
                dataContext.Add(new TenantModuleFeature
                {
                    Id = Guid.NewGuid(),
                    TenantId = newTenantId,
                    ModuleKey = feature.ModuleKey,
                    SubFeatureKey = feature.SubFeatureKey,
                    DisplayName = feature.DisplayName,
                    Description = feature.Description,
                    CreatedAt = now,
                    IsEnabled = true,
                    ConcurrencyStamp = Guid.NewGuid()
                });
            }

            foreach (var (name, systemReferenceId) in RequiredSessionTypes)
            {
                dataContext.Add(new SessionType
                {
                    Id = Guid.NewGuid(),
                    TenantId = newTenantId,
                    Name = name,
                    SystemReferenceId = systemReferenceId,
                    CreatedAt = now,
                    IsEnabled = true,
                    ConcurrencyStamp = Guid.NewGuid()
                });
            }

            var saveResult = await dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<TenantAdministrationResponse>.Failure("Tenant could not be created", saveResult.StatusCode);
        }

        TenantLifecycleOperations.Invalidate(newTenantId, tenantResolver, featureService);
        return Result<TenantAdministrationResponse>.Success(ToResponse(tenant));
    }

    public async Task<Result<TenantAdministrationResponse>> UpdateAsync(
        UpdateTenantRequest request,
        CancellationToken ct)
    {
        if (trustedInvocationContextAccessor.Current?.EffectiveTenantId is not { } activeTenantId || activeTenantId == Guid.Empty)
            return Result<TenantAdministrationResponse>.Forbidden("An authorized tenant context is required");
        if (!request.IsEnabled && activeTenantId == request.TenantId)
            return Result<TenantAdministrationResponse>.Forbidden("The active tenant cannot be disabled");
        if (request.ParentTenantId == request.TenantId)
            return Result<TenantAdministrationResponse>.Failure("A tenant cannot be its own parent", 400);

        var tenant = await dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(item => item.Id == request.TenantId)
            .FirstOrDefaultAsync(ct);
        if (tenant is null || tenant.IsDeleted)
            return Result<TenantAdministrationResponse>.NotFound("Tenant not found");
        if (tenant.ConcurrencyStamp != request.ConcurrencyStamp)
            return Result<TenantAdministrationResponse>.Failure("Tenant was modified by another operation", 409);

        if (request.ParentTenantId is { } parentTenantId)
        {
            var parentExists = await dataContext.Query<Tenant>()
                .IgnoreQueryFilters()
                .NoCache()
                .Where(item => item.Id == parentTenantId)
                .Where(item => !item.IsDeleted)
                .AnyAsync(ct);
            if (!parentExists)
                return Result<TenantAdministrationResponse>.NotFound("Parent tenant not found");
        }

        var disabling = tenant.IsEnabled && !request.IsEnabled;
        dataContext.Update(tenant);
        tenant.Name = request.Name.Trim();
        tenant.Description = request.Description;
        tenant.Version = request.Version;
        tenant.Status = request.Status;
        tenant.Expiration = request.Expiration;
        tenant.AvailabilityDate = request.AvailabilityDate;
        tenant.ParentTenantId = request.ParentTenantId;
        tenant.IsEnabled = request.IsEnabled;
        tenant.ModifiedAt = DateTime.UtcNow;
        tenant.ConcurrencyStamp = Guid.NewGuid();

        await using var transaction = disabling
            ? await dbContext.Database.BeginTransactionAsync(ct)
            : null;
        if (disabling)
            await TenantLifecycleOperations.RevokeActiveSessionsAsync(dbContext, tenant.Id, ct);

        using var crossTenantWriteScope = tenant.TenantId == activeTenantId
            ? null
            : crossTenantWriteAuthorizationScopeFactory.BeginTenantAdministrationScope();
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<TenantAdministrationResponse>.Failure("Tenant could not be updated", saveResult.StatusCode);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        TenantLifecycleOperations.Invalidate(tenant.Id, tenantResolver, featureService);
        return Result<TenantAdministrationResponse>.Success(ToResponse(tenant));
    }

    public async Task<Result> DeleteAsync(DeleteTenantRequest request, CancellationToken ct)
    {
        if (trustedInvocationContextAccessor.Current?.EffectiveTenantId is not { } activeTenantId || activeTenantId == Guid.Empty)
            return Result.Forbidden("An authorized tenant context is required");
        if (activeTenantId == request.TenantId)
            return Result.Forbidden("The active tenant cannot be deleted");

        var tenant = await dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(item => item.Id == request.TenantId)
            .FirstOrDefaultAsync(ct);
        if (tenant is null || tenant.IsDeleted)
            return Result.NotFound("Tenant not found");
        if (tenant.ConcurrencyStamp != request.ExpectedConcurrencyStamp)
            return Result.Failure("Tenant was modified by another operation", 409);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        await TenantLifecycleOperations.RevokeActiveSessionsAsync(dbContext, tenant.Id, ct);
        tenant.IsEnabled = false;
        dataContext.Remove(tenant);

        using var crossTenantWriteScope = crossTenantWriteAuthorizationScopeFactory.BeginTenantAdministrationScope();
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result.Failure("Tenant could not be deleted", saveResult.StatusCode);
        await transaction.CommitAsync(ct);

        TenantLifecycleOperations.Invalidate(tenant.Id, tenantResolver, featureService);
        return Result.Success("Tenant deleted");
    }

    private static TenantAdministrationResponse ToResponse(Tenant tenant) => new()
    {
        Id = tenant.Id,
        TenantId = tenant.TenantId,
        Name = tenant.Name,
        Description = tenant.Description,
        Status = tenant.Status,
        Expiration = tenant.Expiration,
        AvailabilityDate = tenant.AvailabilityDate,
        ParentTenantId = tenant.ParentTenantId,
        Version = tenant.Version,
        IsEnabled = tenant.IsEnabled,
        ConcurrencyStamp = tenant.ConcurrencyStamp,
        CreatedAt = tenant.CreatedAt,
        ModifiedAt = tenant.ModifiedAt
    };
}
