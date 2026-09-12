using Communications.Integration.Clients;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.DataContext;
using XFramework.Integration.Security;

namespace Yap.Services;

public sealed record ChatPerson(Guid Id, string Name, string UserName, string? AvatarUrl = null);

public interface IChatDirectory
{
    Task<IReadOnlyList<ChatPerson>> SearchAsync(string search, CancellationToken ct);
    Task<IReadOnlyList<ChatPerson>> ResolveAsync(Guid[] credentialIds, CancellationToken ct);
}

// Read-only remote query through IdentityServer's existing authorized entity endpoint.
// Chat creation and membership changes always go through Communications.
public sealed class ChatDirectory(IServiceProvider services, ICommunicationsChatActorProvider actors,
    IActorAccessTokenScope tokenScope) : IChatDirectory
{
    public async Task<IReadOnlyList<ChatPerson>> ResolveAsync(Guid[] credentialIds, CancellationToken ct)
    {
        if (credentialIds.Length == 0) return [];
        var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
        using var token = tokenScope.Push(actor.AccessToken!);
        var data = new RemoteDataContext(services, new RequestMetadata
        {
            RequestedTenantId = actor.TenantId, RequestId = Guid.NewGuid(), OperationName = "Yap member names"
        });
        var people = new List<ChatPerson>();
        foreach (var ids in credentialIds.Distinct().Chunk(50))
        {
            var members = await data.Query<IdentityCredential>()
                .Where(p => p.TenantId == actor.TenantId && ids.Contains(p.Id) && p.IsEnabled && !p.IsDeleted)
                .Take(50).ToListAsync(ct);
            people.AddRange(members.Select(p => new ChatPerson(p.Id,
                string.IsNullOrWhiteSpace(p.UserAlias) ? p.UserName ?? "Workspace member" : p.UserAlias,
                p.UserName ?? "", p.AvatarUrl)));
        }
        return people;
    }

    public async Task<IReadOnlyList<ChatPerson>> SearchAsync(string search, CancellationToken ct)
    {
        search = search.Trim();
        if (search.Length < 2) return [];
        var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
        using var token = tokenScope.Push(actor.AccessToken!);
        var data = new RemoteDataContext(services, new RequestMetadata
        {
            RequestedTenantId = actor.TenantId, RequestId = Guid.NewGuid(), OperationName = "Yap people search"
        });
        var people = await data.Query<IdentityCredential>()
            .Where(p => p.TenantId == actor.TenantId && p.Id != actor.CredentialId && p.IsEnabled && !p.IsDeleted)
            .Where(p => (p.UserName != null && p.UserName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                        (p.UserAlias != null && p.UserAlias.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.UserName).Take(20).ToListAsync(ct);
        return people.Select(p => new ChatPerson(p.Id,
            string.IsNullOrWhiteSpace(p.UserAlias) ? p.UserName ?? "Workspace member" : p.UserAlias,
            p.UserName ?? "", p.AvatarUrl)).ToArray();
    }
}
