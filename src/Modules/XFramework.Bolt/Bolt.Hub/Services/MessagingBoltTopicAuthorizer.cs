using System.Security.Claims;
using Bolt.Server;
using IdentityServer.Domain.Shared.Contracts;
using Messaging.Domain.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Bolt.Hub.Services;

public sealed class MessagingBoltTopicAuthorizer(
    IServiceScopeFactory scopeFactory,
    ILogger<MessagingBoltTopicAuthorizer> logger) : IBoltTopicAuthorizer
{
    private const string Prefix = "messaging.tenant.";

    public async ValueTask<bool> AuthorizeAsync(BoltTopicAuthorizationContext context, CancellationToken ct = default)
    {
        if (context.Topic is null || !context.Topic.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return true;

        var credentialId = ResolveCredentialId(context.User);
        if (credentialId is null)
            return false;

        var segments = context.Topic.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 4 || !Guid.TryParse(segments[2], out var topicTenantId))
            return false;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        var credential = await db.Set<IdentityCredential>()
            .Where(c => c.Id == credentialId.Value)
            .Where(c => !c.IsDeleted && c.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (credential is null || credential.TenantId != topicTenantId)
            return false;

        var allowed = segments[3] switch
        {
            "user" => AuthorizeUserTopic(segments, credentialId.Value),
            "presence" => true,
            "thread" => await AuthorizeThreadTopicAsync(db, segments, topicTenantId, credentialId.Value, ct),
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

    private static bool AuthorizeUserTopic(IReadOnlyList<string> segments, Guid credentialId) =>
        segments.Count == 5 &&
        Guid.TryParse(segments[4], out var topicCredentialId) &&
        topicCredentialId == credentialId;

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
