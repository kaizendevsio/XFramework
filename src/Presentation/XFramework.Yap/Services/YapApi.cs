using System.Net;
using System.Security.Claims;
using System.Threading.Channels;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Communications.Integration.Clients;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Microsoft.AspNetCore.Antiforgery;
using XFramework.Domain.Shared.BusinessObjects;
using Yap.Contracts;
using ApiMessage = Yap.Contracts.ChatMessage;

namespace Yap.Services;

public static class YapApi
{
    public static void MapYapApi(this WebApplication app)
    {
        app.MapGet("/api/session", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var user = context.User.Identity?.IsAuthenticated == true
                ? new UserSession(Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!),
                    Guid.Parse(context.User.FindFirstValue(YapAuth.TenantClaim)!), context.User.Identity.Name ?? "You")
                : null;
            return Results.Ok(new SessionResponse(user, antiforgery.GetAndStoreTokens(context).RequestToken!));
        });

        var api = app.MapGroup("/api/chat").RequireAuthorization().AddEndpointFilter<YapApiFilter>();
        api.MapPost("/initialize", async (ICommunicationsChatClient client, CancellationToken ct) =>
        {
            var session = await client.ForCurrentActorAsync(ct: ct);
            var data = Require(await session.EnsureChatDefaultsAsync(ct));
            return new ChatDefaults(data.ThreadTypeId,
                data.ReactionTypes.Select(x => new ReactionType(x.Id, x.Name, x.Emoji)).ToList());
        });
        api.MapGet("/conversations/deleted", async (int? page, ICommunicationsChatClient client, CancellationToken ct) =>
        {
            var session = await client.ForCurrentActorAsync(ct: ct);
            var data = Require(await session.GetDeletedThreadsAsync(Page(page), ct));
            return new ChatPage<Guid>(data.Items, data.TotalCount);
        });
        api.MapGet("/conversations", async (int? page, ICommunicationsChatClient client, IChatDirectory directory, CancellationToken ct) =>
        {
            var session = await client.ForCurrentActorAsync(ct: ct);
            var data = Require(await session.GetThreadsAsync(Page(page), 30, ct));
            var people = await directory.ResolveAsync(data.Items.Where(x => x.OtherCredentialId.HasValue)
                .Select(x => x.OtherCredentialId!.Value).Distinct().ToArray(), ct);
            return new ChatPage<Conversation>(data.Items.Select(x => new Conversation
            {
                Id = x.Id, Name = x.IsDirect ? people.FirstOrDefault(p => p.Id == x.OtherCredentialId)?.Name ?? "Direct message" : x.Name,
                Group = !x.IsDirect, Members = x.MemberCount, Unread = x.UnreadCount,
                Muted = x.IsMuted, Removed = x.IsArchived,
                Preview = x.LastMessagePreview ?? "Start a conversation", LastMessageAt = x.LastMessageAt
            }).ToList(), data.TotalCount);
        });
        api.MapGet("/conversations/{id:guid}", async (Guid id, ICommunicationsChatClient client, IChatDirectory directory, CancellationToken ct) =>
        {
            var session = await client.ForCurrentActorAsync(ct: ct);
            var data = Require(await session.GetThreadAsync(id, ct));
            var people = await directory.ResolveAsync(data.Members.Select(x => x.CredentialId).ToArray(), ct);
            var members = data.Members.Select(x => {
                var person = people.FirstOrDefault(p => p.Id == x.CredentialId);
                return new Person(x.CredentialId, string.IsNullOrWhiteSpace(x.Alias) ? person?.Name ?? "Workspace member" : x.Alias,
                    person?.UserName ?? "", person?.AvatarUrl, x.Id, x.Role, x.Alias);
            }).ToList();
            return new Conversation { Id = id, Name = data.IsDirect ? members.FirstOrDefault(x => x.Id != session.CredentialId)?.Name ?? "Direct message" : data.Name,
                Group = !data.IsDirect, Members = members.Count, People = members, Features = (int)data.Features, CanManage = data.CanManage };
        });
        api.MapGet("/conversations/{id:guid}/messages", async (Guid id, int? page, Guid? parent,
            ICommunicationsChatClient client, IChatDirectory directory, CancellationToken ct) =>
        {
            var session = await client.ForCurrentActorAsync(ct: ct);
            var data = Require(parent.HasValue ? await session.GetRepliesAsync(id, parent.Value, Page(page), 50, ct)
                : await session.GetMessagesAsync(id, Page(page), 50, ct));
            var people = await directory.ResolveAsync(data.Items
                .Select(x => x.SenderCredentialId).Distinct().ToArray(), ct);
            return new ChatPage<ApiMessage>(data.Items.Select(x => new ApiMessage
            {
                Id = x.Id, ThreadId = id, SenderId = x.SenderCredentialId,
                Sender = x.SenderCredentialId == session.CredentialId ? "You" : string.IsNullOrWhiteSpace(x.SenderAlias)
                    ? people.FirstOrDefault(p => p.Id == x.SenderCredentialId)?.Name ?? "Workspace member" : x.SenderAlias,
                Text = x.Text, CreatedAt = x.CreatedAt, Mine = x.SenderCredentialId == session.CredentialId,
                HasAttachments = x.HasAttachments, IsThreadReply = x.IsThreadReply,
                DeliveredCount = x.DeliveredCount, ReadCount = x.ReadCount,
                AvatarUrl = people.FirstOrDefault(p => p.Id == x.SenderCredentialId)?.AvatarUrl,
                ParentId = x.ParentMessageId, Pinned = x.IsPinned, Saved = x.IsSaved, ReplyTotal = x.ReplyCount,
                Reactions = x.Reactions.ToDictionary(r => r.Emoji, r => r.Count),
                MyReactionIds = x.Reactions.Where(r => r.MyReactionId.HasValue).ToDictionary(r => r.Emoji, r => r.MyReactionId!.Value)
            }).ToList(), data.TotalCount);
        });
        api.MapGet("/people", async (string search, IChatDirectory directory, CancellationToken ct) =>
            (await directory.SearchAsync(search, ct)).Select(x => new Person(x.Id, x.Name, x.UserName, x.AvatarUrl)));
        api.MapGet("/search", async (string query, Guid? thread, int? page, ICommunicationsChatClient client, CancellationToken ct) =>
        {
            var session = await client.ForCurrentActorAsync(ct: ct);
            var data = Require(await session.SearchMessagesAsync(query, thread, Page(page), 30, ct));
            return new ChatPage<SearchHit>(data.Items.Select(x => new SearchHit(x.ThreadId, x.MessageId, x.Text, x.CreatedAt)).ToList(), data.TotalCount);
        });
        api.MapPost("/conversations", async (CreateConversation request, ICommunicationsChatClient client, CancellationToken ct) =>
        {
            if (request.Members.Count is < 1 or > 100 || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
                throw new YapApiException(400, "Choose members and a conversation name.");
            var session = await client.ForCurrentActorAsync(ct: ct);
            var defaults = Require(await session.EnsureChatDefaultsAsync(ct));
            var data = request.Group || request.Members.Count > 1
                ? Require(await session.CreateThreadAsync(new CreateThreadRequest { Name = request.Name, TypeId = defaults.ThreadTypeId, InitialMemberCredentialIds = request.Members }, ct))
                : Require(await session.CreateDirectThreadAsync(request.Members[0], name: request.Name, ct: ct));
            Require(await session.ArchiveThreadAsync(data.ThreadId, false, ct));
            return new { Id = data.ThreadId };
        });
        api.MapPost("/messages", async (SendMessage request, ICommunicationsChatClient client, CancellationToken ct) =>
        {
            if (request.Id == Guid.Empty || request.ThreadId == Guid.Empty || string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 4000)
                throw new YapApiException(400, "Write a message up to 4,000 characters.");
            var session = await client.ForCurrentActorAsync(ct: ct);
            var data = Require(await session.SendMessageAsync(new CreateThreadMessageRequest
            { ThreadId = request.ThreadId, Text = request.Text, ParentMessageId = request.ParentId, ClientMessageId = request.Id, IsThreadReply = request.IsThreadReply }, ct));
            return new MessageReceipt(data.MessageId);
        });
        api.MapPost("/conversation-settings", async (ConversationUpdate request, ICommunicationsChatClient client, CancellationToken ct) =>
        {
            var session = await client.ForCurrentActorAsync(ct: ct);
            Require(await session.UpdateThreadAsync(new UpdateThreadRequest { ThreadId = request.ThreadId,
                Features = request.Features.HasValue ? (Communications.Domain.Shared.Contracts.ConversationFeatures)request.Features.Value : null,
                NicknameMemberId = request.NicknameMemberId, Nickname = request.Nickname }, ct));
            return Results.NoContent();
        });
        api.MapPost("/conversation-members", async (ConversationMemberAction request, ICommunicationsChatClient client, CancellationToken ct) =>
        {
            var session = await client.ForCurrentActorAsync(ct: ct);
            Require(request.Action switch {
                "add" => await session.AddThreadMemberAsync(request.ThreadId, request.CredentialId, ct),
                "remove" => await session.RemoveThreadMemberAsync(request.ThreadId, request.CredentialId, ct),
                "role" when request.Role is "Admin" or "Member" => await session.UpdateMemberRoleAsync(request.ThreadId, request.MemberId, request.Role, ct),
                _ => throw new YapApiException(400, "Choose a supported member action.")
            });
            return Results.NoContent();
        });
        api.MapPost("/message-actions", async (MessageAction request, ICommunicationsChatClient client, CancellationToken ct) =>
        {
            var session = await client.ForCurrentActorAsync(ct: ct);
            var response = request.Action switch
            {
                "edit" => await session.EditMessageAsync(request.ThreadId, request.MessageId, request.Text ?? "", ct),
                "delete" => await session.DeleteMessageAsync(request.ThreadId, request.MessageId, ct),
                "pin" => await session.PinMessageAsync(request.ThreadId, request.MessageId, ct),
                "unpin" => await session.UnpinMessageAsync(request.ThreadId, request.MessageId, ct),
                "save" => await session.SaveMessageAsync(request.ThreadId, request.MessageId, ct),
                "unsave" => await session.UnsaveMessageAsync(request.ThreadId, request.MessageId, ct),
                "react" when request.ReactionTypeId.HasValue => await session.ReactAsync(request.ThreadId, request.MessageId, request.ReactionTypeId.Value, ct),
                "unreact" when request.ReactionId.HasValue => await session.DeleteReactionAsync(request.ThreadId, request.MessageId, request.ReactionId.Value, ct),
                _ => throw new YapApiException(400, "Choose a supported message action.")
            };
            Require(response);
            return Results.NoContent();
        });
        api.MapPost("/read", async (ReadMessages request, ICommunicationsChatClient client, CancellationToken ct) =>
        {
            if (request.MessageIds.Count > 100) throw new YapApiException(400, "Too many messages.");
            var session = await client.ForCurrentActorAsync(ct: ct);
            Require(await session.MarkReadAsync(request.ThreadId, request.MessageIds, ct));
            return Results.NoContent();
        });
        api.MapPost("/thread-actions", async (ThreadAction request, ICommunicationsChatClient client, CancellationToken ct) =>
        {
            var session = await client.ForCurrentActorAsync(ct: ct);
            switch (request.Action)
            {
                case "mute": Require(await session.MuteThreadAsync(request.ThreadId, request.Value, ct)); break;
                case "delete-for-me": Require(await session.ArchiveThreadAsync(request.ThreadId, true, ct)); break;
                case "delete-for-everyone": Require(await session.DeleteThreadAsync(request.ThreadId, ct)); break;
                case "typing": await session.PublishTypingAsync(request.ThreadId, request.Value, ct); break;
                default: throw new YapApiException(400, "Choose a supported conversation action.");
            }
            return Results.NoContent();
        });
        api.MapGet("/events", StreamEventsAsync);
        api.MapPost("/uploads/{thread:guid}", async (Guid thread, HttpContext context, ChatFiles files, CancellationToken ct) =>
        {
            var form = await context.Request.ReadFormAsync(ct);
            if (form.Files.Count != 1) throw new YapApiException(400, "Choose one attachment.");
            var id = await files.UploadAsync(new UploadedFile(form.Files[0]), thread, null, ct);
            return Results.Ok(new { Id = id });
        }).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(ChatFiles.StagedFileBytes + 65536));

        // Resumable path for attachments too large to buffer in one request. The browser
        // slices the file and posts parts; only one part is ever held in this process.
        api.MapPost("/uploads/{thread:guid}/session", async (Guid thread, BeginUpload request, ChatFiles files, CancellationToken ct) =>
            Results.Ok(await files.BeginAsync(thread, request.FileName, request.ContentType, request.TotalBytes, ct)));
        api.MapPost("/uploads/session/{upload:guid}/parts/{part:int}", async (Guid upload, int part, long offset, HttpContext context, ChatFiles files, CancellationToken ct) =>
        {
            if (part < 1) throw new YapApiException(400, "Part numbers start at one.");
            if (offset < 0) throw new YapApiException(400, "Part offset cannot be negative.");
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, ct);
            if (buffer.Length == 0) throw new YapApiException(400, "The attachment part is empty.");
            await files.UploadPartAsync(upload, part, offset, buffer.ToArray(), ct);
            return Results.NoContent();
        }).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(ChatFiles.PartRequestBytes));
        api.MapPost("/uploads/session/{upload:guid}/complete", async (Guid upload, ChatFiles files, CancellationToken ct) =>
            Results.Ok(new { Id = await files.CompleteAsync(upload, ct) }));
        api.MapPost("/uploads/session/{upload:guid}/abort", async (Guid upload, ChatFiles files, CancellationToken ct) =>
        {
            await files.AbortAsync(upload, ct);
            return Results.NoContent();
        });
        api.MapPost("/attachments", async (AttachMessageFile request, ChatFiles files, CancellationToken ct) =>
        {
            await files.AttachAsync(request.ThreadId, request.MessageId, request.StorageId, ct);
            return Results.NoContent();
        });
        api.MapGet("/conversations/{thread:guid}/messages/{message:guid}/attachments",
            (Guid thread, Guid message, ChatFiles files, CancellationToken ct) => files.GetDetailsAsync(thread, message, ct));
        api.MapGet("/conversations/{thread:guid}/messages/{message:guid}/attachments/{file:guid}",
            async (Guid thread, Guid message, Guid file, HttpContext context, ICommunicationsChatClient client, IHttpClientFactory http, IConfiguration configuration, CancellationToken ct) =>
            {
                var session = await client.ForCurrentActorAsync(ct: ct);
                // The URL is minted by the authorized SDK; the browser cannot supply a proxy target.
                var download = Require(await session.GetAttachmentDownloadUrlAsync(thread, message, file, ct));
                using var request = CreateAttachmentDownloadRequest(download.Url, configuration);
                using var response = await http.CreateClient("attachments").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                context.Response.Headers.XContentTypeOptions = "nosniff";
                await response.Content.CopyToAsync(context.Response.Body, ct);
            });
    }

    private sealed class UploadedFile(IFormFile file) : Microsoft.AspNetCore.Components.Forms.IBrowserFile
    {
        public string Name => Path.GetFileName(file.FileName);
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => file.Length;
        public string ContentType => file.ContentType;
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            file.Length <= maxAllowedSize ? file.OpenReadStream()
                : throw new YapApiException(413, $"Send attachments over {ChatFiles.StagedFileBytes / (1024 * 1024)} MB with a resumable upload.");
    }

    private static async Task StreamEventsAsync(HttpContext context, ICommunicationsChatClient client, Guid? thread, CancellationToken ct)
    {
        var session = await client.ForCurrentActorAsync(ct: ct);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lifetime.CancelAfter(TimeSpan.FromMinutes(5)); // Reconnect revalidates the cookie and actor session.
        try
        {
            var hints = Channel.CreateBounded<string>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });
            if (thread.HasValue)
            {
                Require(await session.GetThreadAsync(thread.Value, lifetime.Token));
                await session.SubscribeTypingAsync(thread.Value, state =>
                {
                    hints.Writer.TryWrite($"event: typing\ndata: {JsonSerializer.Serialize(new TypingUpdate(state.ThreadId, state.CredentialId, state.IsTyping))}\n\n");
                    return Task.CompletedTask;
                }, lifetime.Token);
            }
            await session.SubscribeUserEventsAsync(_ => { hints.Writer.TryWrite("data: refresh\n\n"); return Task.CompletedTask; }, lifetime.Token);
            context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            await context.Response.WriteAsync(": connected\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    heartbeat.CancelAfter(TimeSpan.FromSeconds(15));
                    var frame = ": heartbeat\n\n";
                    try { frame = await hints.Reader.ReadAsync(heartbeat.Token); }
                    catch (OperationCanceledException) when (!lifetime.IsCancellationRequested) { }
                    await context.Response.WriteAsync(frame, lifetime.Token);
                    await context.Response.Body.FlushAsync(lifetime.Token);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        }
        finally { await lifetime.CancelAsync(); }
    }

    public static HttpRequestMessage CreateAttachmentDownloadRequest(string url, IConfiguration configuration)
    {
        var signed = new Uri(url, UriKind.Absolute);
        if (signed.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(signed.UserInfo))
            throw new YapApiException(503, "This attachment cannot be opened.");
        var target = signed;
        var publicEndpoint = configuration["Yap:AttachmentPublicEndpoint"];
        var internalEndpoint = configuration["Yap:AttachmentInternalEndpoint"];
        if (!string.IsNullOrWhiteSpace(publicEndpoint) && !string.IsNullOrWhiteSpace(internalEndpoint))
        {
            var external = new Uri(publicEndpoint, UriKind.Absolute);
            var local = new Uri(internalEndpoint, UriKind.Absolute);
            if (local.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(local.UserInfo) || local.AbsolutePath != "/")
                throw new InvalidOperationException("The attachment internal endpoint must be an HTTP origin.");
            if (string.Equals(signed.GetLeftPart(UriPartial.Authority), external.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                target = new UriBuilder(signed) { Scheme = local.Scheme, Host = local.Host, Port = local.Port }.Uri;
        }
        var request = new HttpRequestMessage(HttpMethod.Get, target);
        // S3 signs the public Host header. Change only where the server connects;
        // retain the signed authority, object path and query on the wire.
        if (target != signed) request.Headers.Host = signed.Authority;
        return request;
    }

    private static int Page(int? page) => Math.Clamp(page ?? 0, 0, 10000);
    internal static T Require<T>(QueryResponse<T> result) => result.IsSuccess && result.Response is not null
        ? result.Response : throw new YapApiException((int)result.HttpStatusCode, "The chat service could not complete this request.");
    internal static void Require(CmdResponse result)
    { if (!result.IsSuccess) throw new YapApiException((int)result.HttpStatusCode, "The chat service could not complete this request."); }
}

public sealed class YapApiException(int status, string message) : Exception(message)
{ public int Status { get; } = status is >= 400 and <= 599 ? status : 503; }

public sealed class YapApiFilter(IAntiforgery antiforgery, ILogger<YapApiFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var context = invocation.HttpContext;
        context.Response.Headers.CacheControl = "no-store";
        var expected = $"{Guid.Parse(context.User.FindFirstValue(YapAuth.TenantClaim)!):N}:{Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!):N}";
        var account = context.Request.Headers["X-Yap-Account"].ToString();
        if (string.IsNullOrEmpty(account) && context.Request.Path == "/api/chat/events") account = context.Request.Query["account"].ToString();
        if (account != expected) return Results.Problem("The signed-in account changed. Sign in again.", statusCode: 401);
        try
        {
            if (!HttpMethods.IsGet(context.Request.Method)) await antiforgery.ValidateRequestAsync(context);
            return await next(invocation);
        }
        catch (AntiforgeryValidationException) { return Results.Problem("Refresh your session and try again.", statusCode: 400); }
        catch (YapApiException ex) { return Results.Problem(ex.Message, statusCode: ex.Status); }
        catch (UnauthorizedAccessException) { return Results.Problem("Your session ended. Sign in again.", statusCode: 401); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return Results.Empty; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Yap browser API request failed");
            return context.Response.HasStarted ? Results.Empty : Results.Problem("Cannot reach chat right now. Your queued messages remain on this device.", statusCode: 503);
        }
    }
}
