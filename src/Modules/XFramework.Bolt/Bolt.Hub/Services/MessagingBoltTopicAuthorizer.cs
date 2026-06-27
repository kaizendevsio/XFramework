using System.Security.Claims;
using Bolt.Server;
using IdentityServer.Domain.Shared.Contracts;
using Messaging.Domain.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Integration.Abstractions;

namespace Bolt.Hub.Services;

public sealed class MessagingBoltTopicAuthorizer(
    IServiceScopeFactory scopeFactory,
    IJwtService jwtService,
    ILogger<MessagingBoltTopicAuthorizer> logger) : IBoltTopicAuthorizer
{
    private const string Prefix = "messaging.tenant.";
    private const string MessagingServiceClientId = "XFramework.Messaging";

    public async ValueTask<bool> AuthorizeAsync(BoltTopicAuthorizationContext context, CancellationToken ct = default)
    {
        if (context.Topic is null || !context.Topic.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return true;

        var segments = context.Topic.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 4 || !Guid.TryParse(segments[2], out var topicTenantId))
            return false;

        if (IsMessagingServiceIdentity(context))
            return AuthorizeMessagingServiceTopic(context, segments);

        var credentialId = await ResolveCredentialIdAsync(context, ct);
        if (credentialId is null)
            return false;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        var credential = await db.Set<IdentityCredential>()
            .Where(c => c.Id == credentialId.Value)
            .Where(c => !c.IsDeleted && c.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (credential is null || credential.TenantId != topicTenantId)
            return false;

        var allowed = context.Operation switch
        {
            BoltTopicOperation.Subscribe or BoltTopicOperation.Unsubscribe => segments[3] switch
            {
                "user" => context.Durable &&
                    AuthorizeUserTopic(segments, credentialId.Value) &&
                    AuthorizeUserSubscriberId(context.SubscriberId, topicTenantId, credentialId.Value),
                "presence" => !context.Durable,
                "thread" => !context.Durable && await AuthorizeThreadTopicAsync(db, segments, topicTenantId, credentialId.Value, ct),
                _ => false
            },
            BoltTopicOperation.Ack => segments[3] == "user" &&
                context.Durable &&
                AuthorizeUserTopic(segments, credentialId.Value) &&
                AuthorizeUserSubscriberId(context.SubscriberId, topicTenantId, credentialId.Value),
            BoltTopicOperation.Publish => false,
            _ => false
        };

        if (!allowed)
        {
            logger.LogWarning(
                "Rejected Messaging Bolt topic access. credential={CredentialId} topic={Topic} operation={Operation}",
                credentialId,
                context.Topic,
                context.Operation);
        }

        return allowed;
    }

    private static bool AuthorizeMessagingServiceTopic(
        BoltTopicAuthorizationContext context,
        IReadOnlyList<string> segments)
    {
        if (context.Operation != BoltTopicOperation.Publish)
            return false;

        if (context.Durable)
        {
            return segments.Count == 5 &&
                   segments[3] == "user" &&
                   Guid.TryParse(segments[4], out _);
        }

        return segments.Count == 4 && segments[3] == "presence" ||
               segments.Count == 6 && segments[3] == "thread" && segments[5] == "typing" && Guid.TryParse(segments[4], out _);
    }

    private static bool AuthorizeUserTopic(IReadOnlyList<string> segments, Guid credentialId) =>
        segments.Count == 5 &&
        Guid.TryParse(segments[4], out var topicCredentialId) &&
        topicCredentialId == credentialId;

    private static bool AuthorizeUserSubscriberId(string? subscriberId, Guid tenantId, Guid credentialId)
    {
        if (string.IsNullOrWhiteSpace(subscriberId))
            return false;

        var segments = subscriberId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length >= 3 &&
               segments[0] == "messaging" &&
               Guid.TryParse(segments[1], out var subscriberTenantId) &&
               subscriberTenantId == tenantId &&
               Guid.TryParse(segments[2], out var subscriberCredentialId) &&
               subscriberCredentialId == credentialId;
    }

    private static async Task<bool> AuthorizeThreadTopicAsync(
        DbContext db,
        IReadOnlyList<string> segments,
        Guid tenantId,
        Guid credentialId,
        CancellationToken ct)
    {
        if (segments.Count != 6 ||
            segments[5] != "typing" ||
            !Guid.TryParse(segments[4], out var threadId))
            return false;

        return await db.Set<MessageThreadMember>()
            .Where(m => m.TenantId == tenantId)
            .Where(m => m.MessageThreadId == threadId)
            .Where(m => m.CredentialId == credentialId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .AnyAsync(ct);
    }

    private static bool IsMessagingServiceIdentity(BoltTopicAuthorizationContext context)
    {
        if (!string.Equals(context.ClientName, MessagingServiceClientId, StringComparison.Ordinal))
            return false;

        var serviceClaim =
            context.User?.FindFirstValue("client_id") ??
            context.User?.FindFirstValue("service") ??
            context.User?.FindFirstValue("azp");

        return string.Equals(serviceClaim, MessagingServiceClientId, StringComparison.Ordinal);
    }

    private async Task<Guid?> ResolveCredentialIdAsync(BoltTopicAuthorizationContext context, CancellationToken ct)
    {
        var credentialId = ResolveCredentialId(context.User);
        if (credentialId is not null)
            return credentialId;

        if (string.IsNullOrWhiteSpace(context.ActorAccessToken))
            return null;

        try
        {
            var (principal, _) = await jwtService.DecodeJwtToken(context.ActorAccessToken);
            return ResolveCredentialId(principal);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rejected Messaging Bolt topic access because actor access token could not be validated");
            return null;
        }
    }

    private static Guid? ResolveCredentialId(ClaimsPrincipal? user)
    {
        var value =
            user?.FindFirstValue(ClaimTypes.Name) ??
            user?.FindFirstValue("credential_id") ??
            user?.FindFirstValue("CredentialId") ??
            user?.FindFirstValue("sub");

        return Guid.TryParse(value, out var credentialId)
            ? credentialId
            : null;
    }
}
