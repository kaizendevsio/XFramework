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
            // Answered before storage is touched: a revalidating client costs nothing upstream.
            if (WritePhotoHeaders(context, storageId)) return Results.Empty;
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
            // The thread read is the same membership check the download already performs, and it
            // is what names the current photo, so the version is known before any storage call.
            var thread = YapApi.Require(await session.GetThreadAsync(id, ct));
            if (thread.PhotoStorageFileId is not { } storageId) return Results.NotFound();
            if (WritePhotoHeaders(context, storageId)) return Results.Empty;
            var download = YapApi.Require(await session.GetThreadPhotoDownloadUrlAsync(id, ct));
            using var request = YapApi.CreateAttachmentDownloadRequest(download.Url, configuration);
            using var response = await http.CreateClient("attachments").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            context.Response.ContentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            await response.Content.CopyToAsync(context.Response.Body, ct);
            return Results.Empty;
        }).WithMetadata(new MediaAccountQuery());
    }

    // A photo URL names the exact stored file it serves, so its bytes can never change under it:
    // replacing the photo mints a new storage ID and therefore a new URL. That is what makes
    // immutable safe, and immutable is what stops a reload or a cold app start from asking again
    // for a photo the device already has - max-age alone leaves the client free to revalidate,
    // and a revalidation with no validator can only come back as the whole image all over again.
    // private, never public: the account is in the URL and membership is checked per request, so
    // a shared proxy holding these would hand one member's face to another.
    // Returns true when the response is complete and the caller must not write a body.
    private static bool WritePhotoHeaders(HttpContext context, Guid version)
    {
        var tag = $"\"{version:N}\"";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        context.Response.Headers.ETag = tag;
        // Clients send a list, and a cache that stored the body but not the strength sends W/.
        if (!context.Request.Headers.IfNoneMatch.ToString().Split(',')
            .Any(x => x.Trim() is var candidate && (candidate == "*" || candidate.TrimStart('W', '/') == tag))) return false;
        context.Response.StatusCode = StatusCodes.Status304NotModified;
        return true;
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
