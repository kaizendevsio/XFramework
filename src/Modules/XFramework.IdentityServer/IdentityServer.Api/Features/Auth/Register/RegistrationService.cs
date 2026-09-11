using FluentValidation;
using IdentityServer.Api.Infrastructure;
using Npgsql;
using XFramework.Core.RateLimiting;

namespace IdentityServer.Api.Features.Auth.Register;

public sealed class RegistrationClient
{
    public string ClientId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
}

public sealed class RegisterIdentityValidator : AbstractValidator<RegisterIdentityRequest>
{
    public RegisterIdentityValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UserName).NotEmpty().Length(3, 100).Matches(@"\A[a-zA-Z0-9_.-]+\z");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Must(IdentityPasswordPolicy.IsWithinBcryptByteLimit);
    }
}

public sealed class RegistrationService(DbContext db, IConfiguration configuration,
    ITrustedInvocationContextAccessor invocation, IDistributedSecurityRateLimiter limiter,
    ILogger<RegistrationService> logger)
{
    public async Task<Result<RegisterIdentityResponse>> RegisterAsync(RegisterIdentityRequest request, CancellationToken ct)
    {
        var caller = invocation.Current?.Service;
        if (caller is null || !caller.Scopes.Contains(XFrameworkServiceScopes.IdentityRegister) ||
            !caller.Scopes.Contains(XFrameworkServiceScopes.TenantTarget))
            return Result<RegisterIdentityResponse>.Forbidden("Registration is not available for this application.");

        var profile = configuration.GetSection("SelfRegistration:Clients").Get<RegistrationClient[]>()?
            .FirstOrDefault(x => string.Equals(x.ClientId, caller.ClientId, StringComparison.OrdinalIgnoreCase));
        if (profile is null || profile.TenantId == Guid.Empty || profile.RoleId == Guid.Empty ||
            invocation.Current?.EffectiveTenantId != profile.TenantId || request.Metadata.RequestedTenantId != profile.TenantId)
            return Result<RegisterIdentityResponse>.Forbidden("Registration is not enabled for this workspace.");

        request.DisplayName = request.DisplayName?.Trim() ?? string.Empty;
        request.UserName = request.UserName?.Trim() ?? string.Empty;
        if (!(await new RegisterIdentityValidator().ValidateAsync(request, ct)).IsValid)
            return Result<RegisterIdentityResponse>.Failure("Check your name, username, and password.", 400);

        try
        {
            // A distributed application-wide bound also applies to direct Bolt callers.
            var limit = await limiter.AcquireAsync(new("identity-register", 10, TimeSpan.FromMinutes(1)), caller.ClientId, ct);
            if (!limit.IsAllowed)
                return Result<RegisterIdentityResponse>.Failure("Too many registration attempts. Please try again shortly.", 429);

            var now = DateTime.UtcNow;
            // This pre-login workflow has no actor tenant. Every lookup is bounded by the
            // administrator-configured application policy, never request metadata.
            var activeTenant = await db.Set<Tenant>().IgnoreQueryFilters().AnyAsync(x =>
                x.Id == profile.TenantId && x.IsEnabled && !x.IsDeleted &&
                (x.AvailabilityDate == null || x.AvailabilityDate <= now) &&
                (x.Expiration == null || x.Expiration > now), ct);
            var activeMemberRole = await db.Set<IdentityRoleType>().IgnoreQueryFilters().AnyAsync(x =>
                x.Id == profile.RoleId && x.TenantId == profile.TenantId && x.IsEnabled && !x.IsDeleted &&
                x.RoleLevel == 0 && x.Group != null && x.Group.TenantId == profile.TenantId &&
                x.Group.IsEnabled && !x.Group.IsDeleted, ct);
            if (!activeTenant || !activeMemberRole)
                return Result<RegisterIdentityResponse>.Forbidden("Registration is not enabled for this workspace.");

            if (await db.Set<IdentityCredential>().IgnoreQueryFilters().AnyAsync(x =>
                x.TenantId == profile.TenantId && x.UserName == request.UserName, ct))
                return Result<RegisterIdentityResponse>.Conflict("That username is already taken.");

            var identity = new IdentityInformation
            {
                Id = Guid.NewGuid(), TenantId = profile.TenantId, IdentityName = request.DisplayName,
                IsEnabled = true, IsVerified = false, CreatedAt = now, ConcurrencyStamp = Guid.NewGuid()
            };
            var credential = new IdentityCredential
            {
                Id = Guid.NewGuid(), TenantId = profile.TenantId, IdentityInfoId = identity.Id,
                UserName = request.UserName, UserAlias = request.DisplayName,
                PasswordByte = Encoding.ASCII.GetBytes(BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 11)),
                IsEnabled = true, CreatedAt = now, ConcurrencyStamp = Guid.NewGuid()
            };
            var role = new IdentityRole
            {
                Id = Guid.NewGuid(), TenantId = profile.TenantId, CredentialId = credential.Id,
                TypeId = profile.RoleId, RoleExpiration = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc),
                IsEnabled = true, CreatedAt = now, ConcurrencyStamp = Guid.NewGuid()
            };
            // One EF save is transactional: failure cannot leave a profile without its credential/role.
            db.AddRange(identity, credential, role);
            await db.SaveChangesAsync(ct);
            return Result<RegisterIdentityResponse>.Success(new()
            { CredentialId = credential.Id, TenantId = profile.TenantId, RoleId = profile.RoleId });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_IdentityCredential_TenantId_UserName" })
        {
            return Result<RegisterIdentityResponse>.Conflict("That username is already taken.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning("Registration could not complete ({ErrorType}).", ex.GetType().Name);
            return Result<RegisterIdentityResponse>.Failure("Registration is temporarily unavailable.", 503);
        }
    }
}
