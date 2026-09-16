using FluentValidation;
using IdentityServer.Api.Infrastructure;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.Opaque;

public static class OpaqueAuthEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityRegister, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = ["XFramework.Yap"], ActorRequirement = ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant)]
    public static Task<Result<OpaqueAuthResponse>> Handle(OpaqueAuthRequest request, AuthService service,
        OpaqueExchanges exchanges, OpaqueSetup setup, Register.RegistrationService registration, CancellationToken ct) =>
        service.OpaqueAsync(request, exchanges, setup, registration, ct);
}

public sealed class OpaqueAuthValidator : AbstractValidator<OpaqueAuthRequest>
{
    public OpaqueAuthValidator()
    {
        RuleFor(x => x.Stage).Must(x => x is "status" or "options" or "login-start" or "login-finish" or
            "enroll-start" or "register-start" or "enroll-verify" or "enroll-finish" or "change-start");
        RuleFor(x => x.UserName).NotEmpty().Length(3, 100).Matches(@"\A[a-zA-Z0-9_.-]+\z").When(x => x.Stage != "status");
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.Message).MaximumLength(4096);
        RuleFor(x => x.Record).MaximumLength(2048);
        RuleFor(x => x.WrappedRecovery).MaximumLength(4096);
        RuleFor(x => x.RecoveryArchive).MaximumLength(2097152);
        RuleFor(x => x.DisplayName).MaximumLength(100);
        RuleFor(x => x.LegacyPassword).MaximumLength(256);
    }
}
