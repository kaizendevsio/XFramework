using System.Security.Claims;
using Bolt.Server;
using Communications.Domain.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Integration.Security;

namespace Bolt.Hub.Services;

public sealed class CommunicationsBoltTopicAuthorizer(
    IServiceScopeFactory scopeFactory,
    ILogger<CommunicationsBoltTopicAuthorizer> logger) : IBoltTopicAuthorizer
{
    private const string Prefix = "communications.tenant.";
    private const string CommunicationsServiceClientId = "XFramework.Communications";
    private const int MaxTopicLength = 128;
    private const int MaxSubscriberIdLength = 256;
    private const int MaxTransientSubscriberIdLength = 128;
    private const int MaxDeviceSegmentLength = 64;
    private const int MaxActorAccessTokenLength = 16 * 1024;

    public async ValueTask<bool> AuthorizeAsync(BoltTopicAuthorizationContext context, CancellationToken ct = default)
    {
        try
        {
            return await AuthorizeCoreAsync(context, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Rejected Bolt topic access because authorization failed. topic={Topic} operation={Operation} client={ClientId}",
                context.Topic,
                context.Operation,
                context.ClientId);
            return false;
        }
    }

    private async ValueTask<bool> AuthorizeCoreAsync(BoltTopicAuthorizationContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.Topic) ||
            context.Topic.Length > MaxTopicLength ||
            !context.Topic.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var segments = context.Topic.Split('.');
        if (!TryValidateTopicGrammar(context, segments, out var topicTenantId))
            return false;

        if (IsCommunicationsServiceIdentity(context))
            return AuthorizeCommunicationsServiceTopic(context, segments);

        if (string.IsNullOrWhiteSpace(context.ActorAccessToken) ||
            context.ActorAccessToken.Length > MaxActorAccessTokenLength)
            return false;

        await using var scope = scopeFactory.CreateAsyncScope();
        var actorValidation = await scope.ServiceProvider
            .GetRequiredService<IActorIdentityProvider>()
            .ValidateAsync(context.ActorAccessToken, ct);
        if (!actorValidation.IsValid || actorValidation.Identity is not { } actor || actor.TenantId != topicTenantId)
            return false;

        var credentialId = actor.CredentialId;
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        var allowed = context.Operation switch
        {
            BoltTopicOperation.Subscribe or BoltTopicOperation.Unsubscribe => segments[3] switch
            {
                "user" => context.Durable &&
                    AuthorizeUserTopic(segments, credentialId) &&
                    AuthorizeUserSubscriberId(context.SubscriberId, topicTenantId, credentialId),
                "presence" => !context.Durable,
                "thread" => !context.Durable && await AuthorizeThreadTopicAsync(db, segments, topicTenantId, credentialId, ct),
                _ => false
            },
            BoltTopicOperation.Ack => segments[3] == "user" &&
                context.Durable &&
                AuthorizeUserTopic(segments, credentialId) &&
                AuthorizeUserSubscriberId(context.SubscriberId, topicTenantId, credentialId),
            BoltTopicOperation.Publish => false,
            _ => false
        };

        if (!allowed)
        {
            logger.LogWarning(
                "Rejected Communications Bolt topic access. credential={CredentialId} topic={Topic} operation={Operation}",
                credentialId,
                context.Topic,
                context.Operation);
        }

        return allowed;
    }

    private static bool AuthorizeCommunicationsServiceTopic(
        BoltTopicAuthorizationContext context,
        IReadOnlyList<string> segments)
    {
        if (context.Operation != BoltTopicOperation.Publish)
            return false;

        if (context.Durable)
        {
            return segments.Count == 5 &&
                   segments[3] == "user" &&
                   IsCanonicalGuid(segments[4]);
        }

        return segments.Count == 4 && segments[3] == "presence" ||
               segments.Count == 6 && segments[3] == "thread" && segments[5] == "typing" && IsCanonicalGuid(segments[4]);
    }

    private static bool AuthorizeUserTopic(IReadOnlyList<string> segments, Guid credentialId) =>
        segments.Count == 5 &&
        TryParseCanonicalGuid(segments[4], out var topicCredentialId) &&
        topicCredentialId == credentialId;

    private static bool AuthorizeUserSubscriberId(string? subscriberId, Guid tenantId, Guid credentialId)
    {
        if (string.IsNullOrWhiteSpace(subscriberId) || subscriberId.Length > MaxSubscriberIdLength)
            return false;

        var segments = subscriberId.Split(':');
        if (segments.Length is not (6 or 7) ||
            segments[0] != "communications" ||
            !TryParseCanonicalGuid(segments[1], out var subscriberTenantId) ||
            subscriberTenantId != tenantId ||
            !TryParseCanonicalGuid(segments[2], out var subscriberCredentialId) ||
            subscriberCredentialId != credentialId ||
            segments[3] != "device" ||
            !IsValidDeviceSegment(segments[4]))
            return false;

        return segments.Length == 6
            ? segments[5] == "user"
            : segments[5] == "thread" && IsCanonicalGuid(segments[6]);
    }

    private static bool HasValidSubscriberGrammar(string? subscriberId)
    {
        if (string.IsNullOrWhiteSpace(subscriberId) || subscriberId.Length > MaxSubscriberIdLength)
            return false;

        var segments = subscriberId.Split(':');
        return segments.Length is 6 or 7 &&
               segments[0] == "communications" &&
               IsCanonicalGuid(segments[1]) &&
               IsCanonicalGuid(segments[2]) &&
               segments[3] == "device" &&
               IsValidDeviceSegment(segments[4]) &&
               (segments.Length == 6
                   ? segments[5] == "user"
                   : segments[5] == "thread" && IsCanonicalGuid(segments[6]));
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
            !TryParseCanonicalGuid(segments[4], out var threadId))
            return false;

        // Thread authorization uses the same explicit system-read boundary.
        return await db.Set<MessageThreadMember>()
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId)
            .Where(m => m.MessageThreadId == threadId)
            .Where(m => m.CredentialId == credentialId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .AnyAsync(ct);
    }

    private static bool IsCommunicationsServiceIdentity(BoltTopicAuthorizationContext context)
    {
        if (!string.Equals(context.ClientName, CommunicationsServiceClientId, StringComparison.Ordinal))
            return false;

        var serviceClaim =
            context.User?.FindFirstValue("client_id") ??
            context.User?.FindFirstValue("service") ??
            context.User?.FindFirstValue("azp");

        return string.Equals(serviceClaim, CommunicationsServiceClientId, StringComparison.Ordinal);
    }

    private static bool TryValidateTopicGrammar(
        BoltTopicAuthorizationContext context,
        IReadOnlyList<string> segments,
        out Guid tenantId)
    {
        tenantId = default;
        if (segments.Count < 4 ||
            segments[0] != "communications" ||
            segments[1] != "tenant" ||
            !TryParseCanonicalGuid(segments[2], out tenantId))
            return false;

        var validTopic = segments[3] switch
        {
            "user" => segments.Count == 5 && IsCanonicalGuid(segments[4]),
            "presence" => segments.Count == 4,
            "thread" => segments.Count == 6 && IsCanonicalGuid(segments[4]) && segments[5] == "typing",
            _ => false
        };

        if (!validTopic)
            return false;

        return context.Operation switch
        {
            BoltTopicOperation.Publish => context.SubscriberId is null,
            BoltTopicOperation.Subscribe or BoltTopicOperation.Unsubscribe =>
                context.Durable == (segments[3] == "user") &&
                (context.Durable ? HasValidSubscriberGrammar(context.SubscriberId) : HasValidTransientSubscriber(context)),
            BoltTopicOperation.Ack =>
                context.Durable && segments[3] == "user" && HasValidSubscriberGrammar(context.SubscriberId),
            _ => false
        };
    }

    private static bool IsCanonicalGuid(string value) =>
        Guid.TryParseExact(value, "N", out _);

    private static bool TryParseCanonicalGuid(string value, out Guid result) =>
        Guid.TryParseExact(value, "N", out result);

    private static bool IsValidDeviceSegment(string value) =>
        value.Length is > 0 and <= MaxDeviceSegmentLength &&
        value.All(static ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');

    private static bool HasValidTransientSubscriber(BoltTopicAuthorizationContext context) =>
        !string.IsNullOrWhiteSpace(context.SubscriberId) &&
        context.SubscriberId.Length <= MaxTransientSubscriberIdLength &&
        string.Equals(context.SubscriberId, context.ClientId, StringComparison.Ordinal);
}
