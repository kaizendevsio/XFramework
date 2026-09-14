using Communications.Integration.Clients;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Yap.Services;

public static class YapEncryption
{
    public static void MapYapEncryption(this RouteGroupBuilder api)
    {
        // Public keys remain readable for historical senders who have since left a conversation.
        // Identity enforces same-tenant directory access; this endpoint never returns private keys.
        api.MapGet("/encryption/people/{id:guid}", async (Guid id, IIdentityServerServiceWrapper identity,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            return YapApi.Require(await identity.GetEncryptionDirectory(new GetEncryptionDirectoryRequest
            { CredentialId = id, Metadata = Metadata(actor.TenantId) }, ct));
        });
        api.MapGet("/encryption/directory", async (IIdentityServerServiceWrapper identity,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            var result = await identity.GetEncryptionDirectory(new GetEncryptionDirectoryRequest
            { CredentialId = actor.CredentialId, Metadata = Metadata(actor.TenantId) }, ct);
            return YapApi.Require(result);
        });
        api.MapPost("/encryption/directory", async (PutEncryptionDirectoryRequest request,
            IIdentityServerServiceWrapper identity, ICommunicationsChatActorProvider actors,
            IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            request.Metadata = Metadata(actor.TenantId);
            return YapApi.Require(await identity.PutEncryptionDirectory(request, ct));
        });
        api.MapGet("/conversations/{id:guid}/encryption", async (Guid id,
            ICommunicationsChatClient chat, IIdentityServerServiceWrapper identity,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var session = await chat.ForCurrentActorAsync(ct: ct);
            var thread = YapApi.Require(await session.GetThreadAsync(id, ct));
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            var directories = new List<EncryptionDirectoryResponse>();
            // An absent device directory fails the whole operation; never drop a recipient or downgrade.
            foreach (var member in thread.Members)
            {
                var result = await identity.GetEncryptionDirectory(new GetEncryptionDirectoryRequest
                { CredentialId = member.CredentialId, Metadata = Metadata(actor.TenantId) }, ct);
                if ((int)result.HttpStatusCode == 404)
                    throw new YapApiException(428, "Waiting for everyone to set up encrypted messages.");
                directories.Add(YapApi.Require(result));
            }
            return directories;
        });
        api.MapGet("/encryption/recovery", async (IIdentityServerServiceWrapper identity,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            return YapApi.Require(await identity.GetEncryptionRecovery(new GetEncryptionRecoveryRequest
            { Metadata = Metadata(actor.TenantId) }, ct));
        });
        api.MapPost("/encryption/recovery", async (PutEncryptionRecoveryRequest request,
            IIdentityServerServiceWrapper identity, ICommunicationsChatActorProvider actors,
            IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            request.Metadata = Metadata(actor.TenantId);
            return YapApi.Require(await identity.PutEncryptionRecovery(request, ct));
        });
    }

    private static RequestMetadata Metadata(Guid tenant) => new() { RequestedTenantId = tenant, RequestId = Guid.NewGuid() };
}
