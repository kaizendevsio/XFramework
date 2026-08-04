using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Services;

public sealed class IdentityAdministrationService(
    IDataContext dataContext,
    DbContext dbContext,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor) : IIdentityAdministrationService
{
    public async Task<Result<IdentityAdministrationResponse>> CreateAsync(
        CreateIdentityRequest request,
        CancellationToken ct)
    {
        var tenantIdResult = ResolveTenantId(request.Metadata);
        if (!tenantIdResult.IsSuccess)
            return Result<IdentityAdministrationResponse>.Forbidden(tenantIdResult.Message!);

        var tenantId = tenantIdResult.Data;
        var now = DateTime.UtcNow;
        var tenantIsActive = await dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(tenant => tenant.Id == tenantId)
            .Where(tenant => tenant.IsEnabled && !tenant.IsDeleted)
            .Where(tenant => tenant.AvailabilityDate == null || tenant.AvailabilityDate <= now)
            .Where(tenant => tenant.Expiration == null || tenant.Expiration > now)
            .AnyAsync(ct);
        if (!tenantIsActive)
            return Result<IdentityAdministrationResponse>.NotFound("Tenant not found");

        var identity = new IdentityInformation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = Normalize(request.FirstName),
            MiddleName = Normalize(request.MiddleName),
            LastName = Normalize(request.LastName),
            Suffix = Normalize(request.Suffix),
            IdentityName = Normalize(request.IdentityName),
            IdentityDescription = Normalize(request.IdentityDescription),
            BirthDate = request.BirthDate,
            Gender = request.Gender,
            CivilStatus = request.CivilStatus,
            IsVerified = false,
            IsEnabled = true,
            IsDeleted = false,
            ConcurrencyStamp = Guid.NewGuid(),
            CreatedAt = now
        };

        dataContext.Add(identity);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        return saveResult.IsSuccess
            ? Result<IdentityAdministrationResponse>.Success(ToResponse(identity))
            : Result<IdentityAdministrationResponse>.Failure("Identity could not be created", saveResult.StatusCode);
    }

    public async Task<Result<IdentityAdministrationResponse>> UpdateProfileAsync(
        UpdateIdentityProfileRequest request,
        CancellationToken ct)
    {
        var identityResult = await FindIdentityAsync(
            request.IdentityId,
            request.ExpectedConcurrencyStamp,
            request.Metadata,
            ct);
        if (!identityResult.IsSuccess)
            return Result<IdentityAdministrationResponse>.Failure(
                identityResult.Message!,
                identityResult.StatusCode);

        var identity = identityResult.Data!;
        dataContext.Update(identity);
        identity.FirstName = Normalize(request.FirstName);
        identity.MiddleName = Normalize(request.MiddleName);
        identity.LastName = Normalize(request.LastName);
        identity.Suffix = Normalize(request.Suffix);
        identity.IdentityName = Normalize(request.IdentityName);
        identity.IdentityDescription = Normalize(request.IdentityDescription);
        identity.BirthDate = request.BirthDate;
        identity.Gender = request.Gender;
        identity.CivilStatus = request.CivilStatus;
        identity.ModifiedAt = DateTime.UtcNow;
        identity.ConcurrencyStamp = Guid.NewGuid();

        var saveResult = await dataContext.SaveChangesAsync(ct);
        return saveResult.IsSuccess
            ? Result<IdentityAdministrationResponse>.Success(ToResponse(identity))
            : Result<IdentityAdministrationResponse>.Failure("Identity could not be updated", saveResult.StatusCode);
    }

    public async Task<Result<IdentityAdministrationResponse>> SetEnabledAsync(
        SetIdentityEnabledRequest request,
        CancellationToken ct)
    {
        var identityResult = await FindIdentityAsync(
            request.IdentityId,
            request.ExpectedConcurrencyStamp,
            request.Metadata,
            ct);
        if (!identityResult.IsSuccess)
            return Result<IdentityAdministrationResponse>.Failure(
                identityResult.Message!,
                identityResult.StatusCode);

        var identity = identityResult.Data!;
        dataContext.Update(identity);
        identity.IsEnabled = request.IsEnabled;
        identity.ModifiedAt = DateTime.UtcNow;
        identity.ConcurrencyStamp = Guid.NewGuid();

        await using var transaction = !request.IsEnabled && dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(ct)
            : null;
        if (!request.IsEnabled)
            await RevokeActiveSessionsAsync(identity.Id, identity.TenantId, ct);

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<IdentityAdministrationResponse>.Failure(
                "Identity status could not be updated",
                saveResult.StatusCode);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        return Result<IdentityAdministrationResponse>.Success(ToResponse(identity));
    }

    public async Task<Result> SoftDeleteAsync(SoftDeleteIdentityRequest request, CancellationToken ct)
    {
        var identityResult = await FindIdentityAsync(
            request.IdentityId,
            request.ExpectedConcurrencyStamp,
            request.Metadata,
            ct);
        if (!identityResult.IsSuccess)
            return Result.Failure(identityResult.Message!, identityResult.StatusCode);

        var identity = identityResult.Data!;
        identity.IsEnabled = false;
        dataContext.Remove(identity);
        identity.ConcurrencyStamp = Guid.NewGuid();

        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(ct)
            : null;
        await RevokeActiveSessionsAsync(identity.Id, identity.TenantId, ct);

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result.Failure("Identity could not be deleted", saveResult.StatusCode);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        return Result.Success("Identity deleted");
    }

    private async Task<Result<IdentityInformation>> FindIdentityAsync(
        Guid identityId,
        Guid expectedConcurrencyStamp,
        RequestMetadata metadata,
        CancellationToken ct)
    {
        var tenantIdResult = ResolveTenantId(metadata);
        if (!tenantIdResult.IsSuccess)
            return Result<IdentityInformation>.Forbidden(tenantIdResult.Message!);

        var identity = await dataContext.Query<IdentityInformation>()
            .IgnoreQueryFilters()
            .NoCache()
            .Where(item => item.Id == identityId && item.TenantId == tenantIdResult.Data)
            .FirstOrDefaultAsync(ct);
        if (identity is null || identity.IsDeleted)
            return Result<IdentityInformation>.NotFound("Identity not found");
        if (identity.ConcurrencyStamp != expectedConcurrencyStamp)
            return Result<IdentityInformation>.Failure(
                "Identity was modified by another operation",
                StatusCodes.Status409Conflict);

        return Result<IdentityInformation>.Success(identity);
    }

    private async Task RevokeActiveSessionsAsync(Guid identityId, Guid tenantId, CancellationToken ct)
    {
        var credentialIds = dbContext.Set<IdentityCredential>()
            .IgnoreQueryFilters()
            .Where(credential => credential.TenantId == tenantId)
            .Where(credential => credential.IdentityInfoId == identityId && !credential.IsDeleted)
            .Select(credential => credential.Id);

        var now = DateTime.UtcNow;
        var concurrencyStamp = Guid.NewGuid();
        await dbContext.Set<Session>()
            .IgnoreQueryFilters()
            .Where(session => session.TenantId == tenantId)
            .Where(session => credentialIds.Contains(session.CredentialId))
            .Where(session => session.Status == CurrentSessionState.Active && !session.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.Status, CurrentSessionState.Inactive)
                .SetProperty(session => session.ModifiedAt, now)
                .SetProperty(session => session.ConcurrencyStamp, concurrencyStamp), ct);
    }

    private Result<Guid> ResolveTenantId(RequestMetadata metadata) =>
        trustedInvocationContextAccessor.Current?.EffectiveTenantId is { } tenantId && tenantId != Guid.Empty
            ? Result<Guid>.Success(tenantId)
            : Result<Guid>.Forbidden("An authorized tenant context is required");

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IdentityAdministrationResponse ToResponse(IdentityInformation identity) => new()
    {
        Id = identity.Id,
        TenantId = identity.TenantId,
        FirstName = identity.FirstName,
        MiddleName = identity.MiddleName,
        LastName = identity.LastName,
        Suffix = identity.Suffix,
        IdentityName = identity.IdentityName,
        IdentityDescription = identity.IdentityDescription,
        BirthDate = identity.BirthDate,
        Gender = identity.Gender,
        CivilStatus = identity.CivilStatus,
        IsVerified = identity.IsVerified,
        IsEnabled = identity.IsEnabled,
        ConcurrencyStamp = identity.ConcurrencyStamp,
        CreatedAt = identity.CreatedAt,
        ModifiedAt = identity.ModifiedAt
    };
}
