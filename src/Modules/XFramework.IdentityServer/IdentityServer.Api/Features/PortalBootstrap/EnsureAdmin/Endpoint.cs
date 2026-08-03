using FluentValidation;
using IdentityServer.Api.Features.PortalBootstrap;
using IdentityServer.Api.Infrastructure;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.PortalBootstrap.EnsureAdmin;

public static class EnsurePortalBootstrapAdminEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
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
    }
}
