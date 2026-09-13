using Communications.Integration.Clients;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Microsoft.AspNetCore.Components.Forms;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Yap.Services;

public static class YapProfile
{
    public static void MapYapProfile(this RouteGroupBuilder api)
    {
        api.MapPost("/profile/photo", async (ProfilePhoto request, IIdentityServerServiceWrapper identity,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, CancellationToken ct) =>
        {
            Validate(request);
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            YapApi.Require(await identity.UploadOwnAvatar(new UploadOwnAvatarRequest
            {
                FileName = "profile.jpg", ContentType = "image/jpeg", FileBytes = request.Bytes,
                Metadata = new RequestMetadata { RequestedTenantId = actor.TenantId, RequestId = Guid.NewGuid() }
            }, ct));
            return Results.NoContent();
        });
        api.MapGet("/people/{id:guid}/photo", async (Guid id, IChatDirectory directory,
            ICommunicationsChatActorProvider actors, IActorAccessTokenScope tokens, IStorageServiceWrapper storage,
            IHttpClientFactory http, IConfiguration configuration, HttpContext context, CancellationToken ct) =>
        {
            var person = (await directory.ResolveAsync([id], ct)).SingleOrDefault();
            if (person?.AvatarStorageFileId is not { } storageId) return Results.NotFound();
            var actor = await actors.GetCurrentActorAsync(ct) ?? throw new UnauthorizedAccessException();
            using var token = tokens.Push(actor.AccessToken!);
            var download = YapApi.Require(await storage.GetStorageDownloadUrl(new GetStorageDownloadUrlRequest
            {
                StorageFileId = storageId, ExpirationMinutes = 1,
                Metadata = new RequestMetadata { RequestedTenantId = actor.TenantId, RequestId = Guid.NewGuid() }
            }, ct));
            using var request = YapApi.CreateAttachmentDownloadRequest(download.Url, configuration);
            using var response = await http.CreateClient("attachments").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            context.Response.ContentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            await response.Content.CopyToAsync(context.Response.Body, ct);
            return Results.Empty;
        }).WithMetadata(new MediaAccountQuery());

        api.MapPost("/conversations/{id:guid}/photo", async (Guid id, ProfilePhoto request,
            ChatFiles files, ICommunicationsChatClient client, CancellationToken ct) =>
        {
            Validate(request);
            var session = await client.ForCurrentActorAsync(ct: ct);
            var thread = YapApi.Require(await session.GetThreadAsync(id, ct));
            if (thread.IsDirect || !thread.CanManage) throw new YapApiException(403, "Only group admins can change this photo.");
            var storageId = await files.UploadAsync(new PhotoFile(request.Bytes!), id, null, ct);
            YapApi.Require(await session.UpdateThreadAsync(new UpdateThreadRequest { ThreadId = id, PhotoStorageFileId = storageId }, ct));
            return Results.NoContent();
        });
        api.MapGet("/conversations/{id:guid}/photo", async (Guid id, ICommunicationsChatClient client,
            IHttpClientFactory http, IConfiguration configuration, HttpContext context, CancellationToken ct) =>
        {
            var session = await client.ForCurrentActorAsync(ct: ct);
            var download = YapApi.Require(await session.GetThreadPhotoDownloadUrlAsync(id, ct));
            using var request = YapApi.CreateAttachmentDownloadRequest(download.Url, configuration);
            using var response = await http.CreateClient("attachments").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            context.Response.ContentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            await response.Content.CopyToAsync(context.Response.Body, ct);
            return Results.Empty;
        }).WithMetadata(new MediaAccountQuery());
    }
    private static void Validate(ProfilePhoto request)
    {
        if (request.Bytes is not { Length: > 0 and <= 5 * 1024 * 1024 }) throw new YapApiException(413, "Choose a smaller photo.");
        if (request.Bytes.Length < 3 || request.Bytes[0] != 0xff || request.Bytes[1] != 0xd8 || request.Bytes[2] != 0xff)
            throw new YapApiException(400, "Choose a valid photo.");
    }
    public static string? GroupPhoto(Guid thread, Guid? photo, Guid tenant, Guid actor) => photo is { } file
        ? $"/api/chat/conversations/{thread}/photo?account={tenant:N}:{actor:N}&v={file:N}" : null;
    public sealed record ProfilePhoto(byte[]? Bytes);
    private sealed class PhotoFile(byte[] bytes) : IBrowserFile
    {
        public string Name => "conversation.jpg";
        public string ContentType => "image/jpeg";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => bytes.Length;
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream(bytes, writable: false);
    }
}
