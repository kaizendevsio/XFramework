using Communications.Integration.Clients;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.DataContext;
using XFramework.Integration.Security;

namespace Yap.Services;

public sealed record ChatPerson(Guid Id, string Name, string UserName, string? AvatarUrl = null, Guid? AvatarStorageFileId = null);

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
            var members = await Refusable(data.Query<IdentityCredential>().NoCache()
                .Where(p => p.TenantId == actor.TenantId && ids.Contains(p.Id) && p.IsEnabled && !p.IsDeleted)
                .Take(50).ToListAsync(ct));
            people.AddRange(members.Select(p => new ChatPerson(p.Id,
                string.IsNullOrWhiteSpace(p.UserAlias) ? p.UserName ?? "Workspace member" : p.UserAlias,
                p.UserName ?? "", AvatarUrl(p, actor), p.AvatarStorageFileId)));
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
        var people = await Refusable(data.Query<IdentityCredential>()
            .Where(p => p.TenantId == actor.TenantId && p.Id != actor.CredentialId && p.IsEnabled && !p.IsDeleted)
            .Where(p => (p.UserName != null && p.UserName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                        (p.UserAlias != null && p.UserAlias.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.UserName).Take(20).ToListAsync(ct));
        return people.Select(p => new ChatPerson(p.Id,
            string.IsNullOrWhiteSpace(p.UserAlias) ? p.UserName ?? "Workspace member" : p.UserAlias,
            p.UserName ?? "", AvatarUrl(p, actor), p.AvatarStorageFileId)).ToArray();
    }

    // The remote query driver reports a refused actor token only as an InvalidOperationException
    // naming the status. Surface it as the 401 it is, so a dead sign-in is settled like any
    // other refusal instead of reading as the chat service being unreachable.
    internal static async Task<T> Refusable<T>(Task<T> query)
    {
        try { return await query; }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("DataContext query request failed with status 401", StringComparison.Ordinal))
        { throw new YapApiException(401, "The directory refused this sign-in."); }
    }

    private static string? AvatarUrl(IdentityCredential person, CommunicationsChatActor actor) =>
        person.AvatarStorageFileId is { } file
            ? $"/api/chat/people/{person.Id}/photo?account={actor.TenantId:N}:{actor.CredentialId:N}&v={file:N}"
            : person.AvatarUrl;
}
