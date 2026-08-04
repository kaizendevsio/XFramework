using FluentValidation;
using IdentityServer.Api.Features.PortalBootstrap;
using IdentityServer.Api.Infrastructure;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Features.PortalBootstrap.EnsureAdmin;

public static class EnsurePortalBootstrapAdminEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    public static Task<Result<PortalBootstrapAdminResponse>> Handle(
        EnsurePortalBootstrapAdminRequest request,
        AppDbContext db,
        ILogger<PortalBootstrapService> logger,
        CancellationToken ct) =>
        PortalBootstrapService.EnsureAdminAsync(request, db, logger, ct);
}

public sealed class EnsurePortalBootstrapAdminRequestValidator :
    AbstractValidator<EnsurePortalBootstrapAdminRequest>
{
    public EnsurePortalBootstrapAdminRequestValidator()
    {
        RuleFor(request => request.TenantName).NotEmpty().MaximumLength(200);
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.UserName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Must(IdentityPasswordPolicy.IsWithinBcryptByteLimit)
            .WithMessage("Password must not exceed 72 UTF-8 bytes");
        RuleFor(request => request.Metadata.RequestedTenantId)
            .Equal(PortalBootstrapConstants.AdminTenantId)
            .WithMessage("The Portal bootstrap tenant target is invalid");
    }
}
