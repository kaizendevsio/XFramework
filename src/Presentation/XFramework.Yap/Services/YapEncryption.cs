using Communications.Integration.Drivers;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Yap.Contracts;
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
        api.MapGet("/encryption/people/{id:guid}", async (Guid id, Guid? senderDeviceId, IIdentityServerServiceWrapper identity,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            return YapApi.Require(await identity.GetEncryptionDirectory(new GetEncryptionDirectoryRequest
            { CredentialId = id, SenderDeviceId = senderDeviceId, Metadata = Metadata(actor.TenantId) }, ct));
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
        api.MapGet("/conversations/{id:guid}/encryption", async (Guid id, bool? allowPending,
            ICommunicationsChatClient chat, IIdentityServerServiceWrapper identity,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var session = await chat.ForCurrentActorAsync(ct: ct);
            var thread = YapApi.Require(await session.GetThreadAsync(id, ct));
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            var directories = new List<EncryptionDirectoryResponse>();
            // Messages may defer unconfigured members; calls keep the strict directory requirement.
            foreach (var member in thread.Members)
            {
                var result = await identity.GetEncryptionDirectory(new GetEncryptionDirectoryRequest
                { CredentialId = member.CredentialId, Metadata = Metadata(actor.TenantId) }, ct);
                if ((int)result.HttpStatusCode == 404)
                {
                    if (allowPending == true) continue;
                    throw new YapApiException(428, "Waiting for everyone to set up encrypted messages.");
                }
                var directory = YapApi.Require(result);
                if (allowPending == true && !directory.Devices.Any(d => d.Revocation is null)) continue;
                directories.Add(directory);
            }
            return directories;
        });
        api.MapGet("/encryption/pending", async (int? page, ICommunicationsServiceWrapper communications,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            var result = YapApi.Require(await communications.GetDeferredEncryptionAsync(new GetDeferredEncryptionRequest
            { PageIndex = Math.Max(0, page ?? 0), Metadata = Metadata(actor.TenantId) }, ct));
            return new DeferredDeliveryPage(result.Items.Select(x => new DeferredDelivery(new ChatMessage
            {
                Id = x.Message.Id, ThreadId = x.ThreadId, SenderId = x.Message.SenderCredentialId, Mine = true,
                Text = x.Message.Text, EncryptedEnvelope = x.Message.EncryptedEnvelope,
                ParentId = x.Message.ParentMessageId, IsThreadReply = x.Message.IsThreadReply, CreatedAt = x.Message.CreatedAt,
                EncryptionSenderDeviceId = x.Message.EncryptionSenderDeviceId,
                AcceptedSenderDirectoryRevision = x.Message.AcceptedSenderDirectoryRevision,
                PendingEncryptionCount = x.Message.PendingEncryptionCount,
                EncryptionAudienceCredentialIds = x.Message.EncryptionAudienceCredentialIds
            }, x.EnvelopeHash, x.PendingCredentialIds)).ToList(), result.TotalCount);
        });
        api.MapPost("/encryption/complete", async (CompleteDeferredEncryptionRequest request,
            ICommunicationsServiceWrapper communications, ICommunicationsChatActorProvider actors,
            IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            request.Metadata = Metadata(actor.TenantId);
            YapApi.Require(await communications.CompleteDeferredEncryptionAsync(request, ct));
            return Results.Ok();
        });
        api.MapPost("/encryption/reset", async (ResetEncryptionIdentityRequest request,
            IIdentityServerServiceWrapper identity, ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            request.Metadata = Metadata(actor.TenantId);
            return YapApi.Require(await identity.ResetEncryptionIdentity(request, ct));
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
