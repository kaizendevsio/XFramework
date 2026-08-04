using FluentValidation;
using Microsoft.AspNetCore.Http;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Features.ServiceIdentity.IssueBoltTransportToken;

public static class IssueBoltTransportTokenEndpoint
{
    [MapPost("/api/service-identity/bolt-transport-token", Tags = ["Service Identity"],
        Summary = "Issue Bolt transport token",
        Description = "Issues a short-lived Bolt transport token for the authenticated service client.",
        RateLimitPolicy = "auth",
        ExcludeFromOpenApi = false,
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.Tenantless,
        AllowAnonymous = true)]
    public static async Task<Result<ServiceTokenResponse>> Handle(
        IssueBoltTransportTokenRequest request,
        HttpRequest httpRequest,
        ServiceIdentityConfiguration configuration,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct)
    {
        if (!configuration.AllowInsecureHttp && !httpRequest.IsHttps)
            return Result<ServiceTokenResponse>.Failure("HTTPS is required", 400);

        return await serviceIdentityService.IssueBoltTransportTokenAsync(
            request.ClientId,
            request.ClientSecret,
            ct);
    }
}

public sealed record IssueBoltTransportTokenRequest
{
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
}

public sealed class IssueBoltTransportTokenRequestValidator : AbstractValidator<IssueBoltTransportTokenRequest>
{
    public IssueBoltTransportTokenRequestValidator()
    {
        RuleFor(request => request.ClientId)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(request => request.ClientSecret)
            .NotEmpty()
            .MaximumLength(1_024);
    }
}
