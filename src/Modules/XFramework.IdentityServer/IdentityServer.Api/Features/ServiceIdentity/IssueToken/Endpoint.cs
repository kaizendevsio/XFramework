using FluentValidation;
using Microsoft.AspNetCore.Http;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Features.ServiceIdentity.IssueToken;

public static class IssueServiceTokenEndpoint
{
    [MapPost("/api/service-identity/token", Tags = ["Service Identity"],
        Summary = "Issue service token",
        Description = "Issues a short-lived internal service token for a target XFramework service.",
        RateLimitPolicy = "auth",
        ExcludeFromOpenApi = false,
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.Tenantless,
        AllowAnonymous = true)]
    public static async Task<Result<ServiceTokenResponse>> Handle(
        IssueServiceTokenRequest request,
        HttpRequest httpRequest,
        ServiceIdentityConfiguration configuration,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct)
    {
        if (!configuration.AllowInsecureHttp && !httpRequest.IsHttps)
            return Result<ServiceTokenResponse>.Failure("HTTPS is required", 400);

        return await serviceIdentityService.IssueTokenAsync(request, ct);
    }
}

public sealed class IssueServiceTokenRequestValidator : AbstractValidator<IssueServiceTokenRequest>
{
    public IssueServiceTokenRequestValidator()
    {
        RuleFor(request => request.ClientId)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(request => request.ClientSecret)
            .NotEmpty()
            .MaximumLength(1_024);
        RuleFor(request => request.Audience)
            .NotEmpty()
            .MaximumLength(256);
        RuleFor(request => request.Scopes)
            .NotNull()
            .Must(scopes => scopes is null || scopes.Count <= 64)
            .WithMessage("No more than 64 scopes may be requested");
        RuleForEach(request => request.Scopes)
            .NotEmpty()
            .MaximumLength(128)
            .When(request => request.Scopes is not null);
    }
}
