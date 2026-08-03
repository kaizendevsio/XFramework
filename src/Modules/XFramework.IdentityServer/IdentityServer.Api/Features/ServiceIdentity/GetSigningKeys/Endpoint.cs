using FluentValidation;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.ServiceIdentity.GetSigningKeys;

public static class GetServiceSigningKeysEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
    [MapPost("/api/service-identity/signing-keys/query", Tags = ["Service Identity"],
        Summary = "Get service signing public keys",
        Description = "Returns public service signing keys for internal JWT validation.",
        ExcludeFromOpenApi = false)]
    public static async Task<Result<ServiceSigningKeysResponse>> Handle(
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
