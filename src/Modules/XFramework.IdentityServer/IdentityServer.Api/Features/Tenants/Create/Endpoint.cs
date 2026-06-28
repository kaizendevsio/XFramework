using FluentValidation;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Tenants.Create;

public static class CreateTenantEndpoint
{
    [BoltHandler]
    [MapPost("/api/tenants", Tags = ["Tenants"],
        Summary = "Create a tenant",
        Description = "Creates a tenant through the IdentityServer admin workflow.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<Tenant>> Handle(
        CreateTenantRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.CreateTenantAsync(request, ct);
    }
}

public class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant name is required");

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("Version must be greater than zero");

        RuleFor(x => x.Status)
            .Must(status => status is null or >= 0 and <= 3)
            .WithMessage("Tenant status is invalid");

        RuleFor(x => x.ParentTenantId)
            .Must(parentTenantId => parentTenantId is null || parentTenantId != Guid.Empty)
            .WithMessage("Parent tenant is invalid");
    }
}
