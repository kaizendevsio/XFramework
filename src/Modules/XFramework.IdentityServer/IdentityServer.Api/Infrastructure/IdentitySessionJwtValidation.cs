using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Infrastructure;

public static class IdentitySessionJwtValidation
{
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddIdentitySessionJwtValidation(this IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            var priorValidation = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                if (priorValidation is not null)
                    await priorValidation(context);

                if (context.Result?.Failure is not null || context.Principal is null)
                    return;

                var credentialClaim = context.Principal.FindFirst("credential_id")?.Value;
                var sessionClaim = context.Principal.FindFirst("session_id")?.Value;
                var tenantClaim = context.Principal.FindFirst("tenant_id")?.Value
                    ?? context.Principal.FindFirst("tenantId")?.Value;
                var generationClaim = context.Principal
                    .FindFirst(JwtCredentialSet.GenerationClaim)?.Value;

                if (!Guid.TryParse(credentialClaim, out var credentialId) ||
                    !Guid.TryParse(sessionClaim, out var sessionId) ||
                    !Guid.TryParse(tenantClaim, out var tenantId) ||
                    string.IsNullOrWhiteSpace(generationClaim))
                {
                    context.Fail("Identity session is no longer valid");
                    return;
                }

                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                        context.HttpContext.RequestAborted);
                    timeout.CancelAfter(ValidationTimeout);

                    var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                    var result = await authService.ValidateIdentitySessionAsync(
                        new ValidateIdentitySessionRequest
                        {
                            TenantId = tenantId,
                            CredentialId = credentialId,
                            SessionId = sessionId,
                            RoleTypeIds = ParseRoleTypeIds(context.Principal)
                        },
                        timeout.Token);

                    if (!result.IsSuccess || result.Data?.IsValid != true)
                        context.Fail("Identity session is no longer valid");
                }
                catch (Exception exception)
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("IdentitySessionJwtValidation");
                    logger.LogWarning(
                        exception,
                        "Identity session validation failed closed for tenant {TenantId}, credential {CredentialId}, session {SessionId}",
                        tenantId,
                        credentialId,
                        sessionId);
                    context.Fail("Identity session validation is unavailable");
                }
            };
        });

        return services;
    }

    private static List<Guid> ParseRoleTypeIds(ClaimsPrincipal principal)
    {
        var result = new HashSet<Guid>();
        foreach (var claim in principal.FindAll(ClaimTypes.Role))
        {
            if (Guid.TryParse(claim.Value, out var roleTypeId))
            {
                result.Add(roleTypeId);
                continue;
            }

            try
            {
                foreach (var value in JsonSerializer.Deserialize<List<Guid>>(claim.Value) ?? [])
                    result.Add(value);
            }
            catch (JsonException)
            {
                // An invalid role claim cannot grant a role and is rejected by the lifecycle comparison.
                result.Add(Guid.Empty);
            }
        }

        return result.ToList();
    }
}
