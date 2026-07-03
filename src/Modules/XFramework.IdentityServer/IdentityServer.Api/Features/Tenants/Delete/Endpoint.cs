using FluentValidation;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Tenants.Delete;

public static class DeleteTenantEndpoint
{
    [BoltHandler]
    [MapPost("/api/tenants/delete", Tags = ["Tenants"],
        Summary = "Delete a tenant",
        Description = "Soft-deletes a tenant through the IdentityServer admin workflow.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        DeleteTenantRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.DeleteTenantAsync(request, ct);
    }
}

public class DeleteTenantRequestValidator : AbstractValidator<DeleteTenantRequest>
{
    public DeleteTenantRequestValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");
    }
}
