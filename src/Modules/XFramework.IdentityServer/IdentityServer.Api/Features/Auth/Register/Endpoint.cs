using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.Register;

public static class RegisterIdentityEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityRegister, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = ["XFramework.Yap"],
        ActorRequirement = ActorRequirement.Optional, TenantAccessMode = TenantAccessMode.ServiceTargetTenant)]
    public static Task<Result<RegisterIdentityResponse>> Handle(RegisterIdentityRequest request,
        RegistrationService service, CancellationToken ct) => service.RegisterAsync(request, ct);
}
