using FluentValidation;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Features.ServiceIdentity.GetSigningKeys;

public static class GetServiceSigningKeysEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode.Tenantless,
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
    public static async Task<Result<ServiceSigningKeysResponse>> Handle(
        GetServiceSigningKeysRequest request,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct)
    {
        return await serviceIdentityService.GetSigningKeysAsync(request, ct);
    }
}

public static class GetServiceSigningKeysHttpEndpoint
{
    [MapPost("/api/service-identity/signing-keys/query", Tags = ["Service Identity"],
        Summary = "Get service signing public keys",
        Description = "Returns public service signing keys for internal JWT validation.",
        ExcludeFromOpenApi = false,
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.Tenantless,
        AllowAnonymous = true)]
    public static async Task<Result<ServiceSigningKeysResponse>> HandleHttp(
        GetServiceSigningKeysRequest request,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct)
    {
        return await serviceIdentityService.GetSigningKeysAsync(request, ct);
    }
}

public sealed class GetServiceSigningKeysRequestValidator : AbstractValidator<GetServiceSigningKeysRequest>
{
    public GetServiceSigningKeysRequestValidator() =>
        RuleFor(request => request.KeyId).MaximumLength(128);
}
