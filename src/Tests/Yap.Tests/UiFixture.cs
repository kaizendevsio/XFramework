using System.Net;
using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Clients;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Storage.Domain.Shared.Contracts.Requests;
using Storage.Domain.Shared.Contracts.Responses;
using Storage.Integration.Drivers;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Responses;
using Yap.Services;

namespace Yap.Tests;

// Browser fixture only. The production project has no demo flag, fake login, or seeded chats.
internal static class UiFixture
{
    public static WebApplication Create(int port = 5189, Action<Mock<IIdentityServerServiceWrapper>>? configureIdentity = null)
    {
        var fixture = new ChatFixture();
        var registrationRole = Guid.NewGuid();
        var auth = YapSessionsTests.Session();
        auth.Credential!.Id = fixture.Credential;
        auth.Credential.TenantId = fixture.Tenant;
        var friend = Guid.NewGuid();
        var conversations = new List<ThreadListItemResponse>
        {
            new() { Id = fixture.Thread, Name = "Sarah Mensah", MemberCount = 2, LastMessagePreview = "Perfect, I'll push the new build tonight", LastMessageAt = DateTime.UtcNow, UnreadCount = 2 },
            new() { Id = Guid.NewGuid(), Name = "Design Team", MemberCount = 8, LastMessagePreview = "Marco: the thread on onboarding →", LastMessageAt = DateTime.UtcNow.AddMinutes(-5), UnreadCount = 4 },
            new() { Id = Guid.NewGuid(), Name = "Kwame Osei", MemberCount = 2, LastMessagePreview = "Sounds good, let's catch up", LastMessageAt = DateTime.UtcNow.AddHours(-1) },
            new() { Id = Guid.NewGuid(), Name = "Yap Core", MemberCount = 24, LastMessagePreview = "Amara: shipped 🚀", LastMessageAt = DateTime.UtcNow.AddHours(-2) },
            new() { Id = Guid.NewGuid(), Name = "Lena Novak", MemberCount = 2, LastMessagePreview = "Thanks for the review", LastMessageAt = DateTime.UtcNow.AddHours(-3) }
        };
        var features = Communications.Domain.Shared.Contracts.ConversationFeatures.All;
        var fixtureMembers = new List<ThreadMemberResponse> { new() { Id = Guid.NewGuid(), CredentialId = fixture.Credential, Alias = "Jamie Davis", Role = "Admin" }, new() { Id = Guid.NewGuid(), CredentialId = friend, Alias = "Sarah Mensah", Role = "Admin" } };
        var messages = new List<ThreadMessageItemResponse>
        {
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "Hey! Are you around? 👀", CreatedAt = DateTime.UtcNow.AddMinutes(-25) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "Can you take a look at the onboarding flow? I pushed the new screens last night.", CreatedAt = DateTime.UtcNow.AddMinutes(-24) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = fixture.Credential, Text = "Just opened it — the empty state is so much better now.", CreatedAt = DateTime.UtcNow.AddMinutes(-18) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "That's the one! I'll clean up the spacing today.", CreatedAt = DateTime.UtcNow.AddMinutes(-14) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = fixture.Credential, Text = "Perfect. Let's also sort the copy on step 3.", CreatedAt = DateTime.UtcNow.AddMinutes(-12) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "Perfect, I'll push the new build tonight", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        };
        messages[2].DeliveredCount = 1;
        messages[4].DeliveredCount = 1; messages[4].ReadCount = 1;
        var deletedThreads = new List<Guid>();
        fixture.Session.Setup(s => s.GetDeletedThreadsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, CancellationToken _) => ChatFixture.Ok(new GetDeletedThreadsResponse { Items = deletedThreads.Skip(page * 100).Take(100).ToList(), TotalCount = deletedThreads.Count }));
        fixture.Session.Setup(s => s.DeleteThreadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, CancellationToken _) =>
            { deletedThreads.Add(thread); conversations.RemoveAll(c => c.Id == thread); return Success(); });
        fixture.Session.Setup(s => s.ArchiveThreadAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, bool archived, CancellationToken _) =>
            { conversations.First(c => c.Id == thread).IsArchived = archived; return Success(); });
        fixture.Session.Setup(s => s.GetThreadsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ChatFixture.Ok(new GetThreadListResponse { Items = conversations.ToList(), TotalCount = conversations.Count }));
        fixture.Session.Setup(s => s.GetThreadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => ChatFixture.Ok(new GetThreadResponse
            {
                Id = id, Name = conversations.First(c => c.Id == id).Name, IsDirect = conversations.First(c => c.Id == id).IsDirect, CanManage = true, Features = features,
                Members = fixtureMembers.ToList()
            }));
        fixture.Session.Setup(s => s.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, int page, int size, CancellationToken _) => ChatFixture.Ok(new GetThreadMessagesResponse { Items = messages.OrderByDescending(m => m.CreatedAt).Skip(page * size).Take(size).ToList(), TotalCount = messages.Count }));
        foreach (var conversation in conversations) conversation.IsDirect = conversation.MemberCount == 2;
        fixture.Session.Setup(s => s.GetRepliesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid parent, int page, int size, CancellationToken _) =>
            {
                var replies = messages.Where(m => m.ParentMessageId == parent).OrderByDescending(m => m.CreatedAt).ToArray();
                return ChatFixture.Ok(new GetThreadMessagesResponse { Items = replies.Skip(page * size).Take(size).ToList(), TotalCount = replies.Length });
            });
        fixture.Session.Setup(s => s.ReactAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid message, Guid type, CancellationToken _) =>
            {
                messages.First(m => m.Id == message).Reactions = [new() { TypeId = type, Name = "Heart", Emoji = "❤️", Count = 1, MyReactionId = Guid.NewGuid() }];
                return Success();
            });
        fixture.Session.Setup(s => s.DeleteReactionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid message, Guid reaction, CancellationToken _) =>
            { messages.First(m => m.Id == message).Reactions.RemoveAll(r => r.MyReactionId == reaction); return Success(); });
        fixture.Session.Setup(s => s.SearchMessagesAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, Guid? thread, int page, int size, CancellationToken _) =>
            {
                var matches = messages.Where(m => m.Text.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
                return ChatFixture.Ok(new SearchMessagesResponse
                {
                    Items = matches.Skip(page * size).Take(size).Select(m => new SearchMessageItemResponse
                    { ThreadId = thread ?? fixture.Thread, MessageId = m.Id, Text = m.Text, SenderCredentialId = m.SenderCredentialId, CreatedAt = m.CreatedAt }).ToList(),
                    TotalCount = matches.Length, PageIndex = page, PageSize = size
                });
            });
        fixture.Session.Setup(s => s.SendMessageAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, string text, Guid? parent, IReadOnlyCollection<Guid>? mentions, CancellationToken _) =>
            {
                var message = new ThreadMessageItemResponse { Id = Guid.NewGuid(), SenderCredentialId = fixture.Credential, Text = text, ParentMessageId = parent, CreatedAt = DateTime.UtcNow };
                messages.Add(message);
                conversations.First(c => c.Id == id).LastMessagePreview = text;
                return ChatFixture.Ok(new CreateThreadMessageResponse { MessageId = message.Id });
            });
        fixture.Session.Setup(s => s.SendMessageAsync(It.IsAny<CreateThreadMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateThreadMessageRequest request, CancellationToken _) =>
            {
                var id = request.ClientMessageId ?? Guid.NewGuid();
                if (messages.All(m => m.Id != id)) messages.Add(new() { Id = id, Text = request.Text ?? "", ParentMessageId = request.ParentMessageId,
                    SenderCredentialId = fixture.Credential, CreatedAt = DateTime.UtcNow });
                conversations.First(c => c.Id == request.ThreadId).LastMessagePreview = request.Text;
                return ChatFixture.Ok(new CreateThreadMessageResponse { MessageId = id });
            });
        fixture.Session.Setup(s => s.CreateDirectThreadAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid other, Guid? type, string? name, CancellationToken _) =>
            {
                var item = new ThreadListItemResponse { Id = Guid.NewGuid(), Name = name ?? "New conversation", MemberCount = 2, IsDirect = true };
                conversations.Add(item); return ChatFixture.Ok(new CreateThreadResponse { ThreadId = item.Id });
            });
        fixture.Session.Setup(s => s.CreateThreadAsync(It.IsAny<CreateThreadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateThreadRequest request, CancellationToken _) =>
            {
                var item = new ThreadListItemResponse { Id = Guid.NewGuid(), Name = request.Name, MemberCount = request.InitialMemberCredentialIds.Count + 1 };
                conversations.Add(item); return ChatFixture.Ok(new CreateThreadResponse { ThreadId = item.Id });
            });
        fixture.Session.Setup(s => s.EditMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid id, string text, CancellationToken _) => { messages.First(m => m.Id == id).Text = text; return Success(); });
        fixture.Session.Setup(s => s.DeleteMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid id, CancellationToken _) => { messages.RemoveAll(m => m.Id == id); return Success(); });
        fixture.Session.Setup(s => s.PinMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid id, CancellationToken _) => { messages.First(m => m.Id == id).IsPinned = true; return Success(); });
        fixture.Session.Setup(s => s.SaveMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success());
        fixture.Session.Setup(s => s.PublishTypingAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Func<CommunicationsTypingState, Task>? onTyping = null;
        Guid typingThread = fixture.Thread;
        fixture.Session.Setup(s => s.SubscribeTypingAsync(It.IsAny<Guid>(), It.IsAny<Func<CommunicationsTypingState, Task>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Func<CommunicationsTypingState, Task>, CancellationToken>((thread, handler, _) => { typingThread = thread; onTyping = handler; })
            .Returns(Task.CompletedTask);

        var identity = new Mock<IIdentityServerServiceWrapper>();
        identity.Setup(i => i.RegisterIdentity(It.IsAny<RegisterIdentityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse<IdentityServer.Domain.Shared.Contracts.Responses.RegisterIdentityResponse>
            { HttpStatusCode = HttpStatusCode.OK, Response = new()
                { CredentialId = Guid.NewGuid(), TenantId = fixture.Tenant, RoleId = registrationRole } });
        identity.Setup(i => i.AuthenticateIdentity(It.IsAny<AuthenticateIdentityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticateIdentityRequest request, CancellationToken _) => request.UserName == "fixture"
                && request.Password == (Environment.GetEnvironmentVariable("YAP_FIXTURE_PASSWORD") ?? "fixture")
                ? ChatFixture.Ok(auth) : new() { HttpStatusCode = HttpStatusCode.Unauthorized });
        identity.Setup(i => i.Logout(It.IsAny<LogoutRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success());
        var directory = new Mock<IChatDirectory>();
        directory.Setup(d => d.ResolveAsync(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        directory.Setup(d => d.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ChatPerson(friend, "Sarah Mensah", "sarah"), new ChatPerson(Guid.NewGuid(), "Marco Bianchi", "marco")]);
        var storage = new Mock<IStorageServiceWrapper>();
        var fileId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        fixture.Session.Setup(s => s.CreateAttachmentUploadAsync(It.IsAny<Communications.Domain.Shared.Contracts.Requests.Attachments.CreateChatAttachmentUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadSessionResponse { Id = uploadId, StorageFileId = fileId, ChunkSizeBytes = 256 * 1024 }));
        storage.Setup(s => s.UploadChatStorageFilePart(It.IsAny<UploadChatStorageFilePartRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadPartResponse()));
        storage.Setup(s => s.CompleteChatStorageUploadSession(It.IsAny<CompleteChatStorageUploadSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageFileResponse { Id = fileId, Status = XFramework.Domain.Shared.Contracts.StorageFileStatus.Available }));
        storage.Setup(s => s.GetStorageFile(It.IsAny<GetStorageFileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageFileResponse { Id = fileId, Name = "offline-proof.txt", ContentType = "text/plain", ContentLengthBytes = 26,
                Status = XFramework.Domain.Shared.Contracts.StorageFileStatus.Available }));
        fixture.Session.Setup(s => s.GetAttachmentDownloadUrlAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageDownloadUrlResponse { StorageFileId = fileId, Url = $"http://127.0.0.1:{port}/test/file", ExpiresAt = DateTime.UtcNow.AddMinutes(5) }));
        storage.Setup(s => s.EnsureStorageUploadMetadata(It.IsAny<EnsureStorageUploadMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadMetadataResponse { TypeId = Guid.NewGuid(), StorageFileIdentifierId = Guid.NewGuid() }));
        storage.Setup(s => s.CreateStorageUploadSession(It.IsAny<CreateStorageUploadSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadSessionResponse { Id = uploadId, StorageFileId = fileId, ChunkSizeBytes = 256 * 1024 }));
        storage.Setup(s => s.UploadStorageFilePart(It.IsAny<UploadStorageFilePartRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadPartResponse()));
        storage.Setup(s => s.CompleteStorageUploadSession(It.IsAny<CompleteStorageUploadSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageFileResponse { Id = fileId }));
        var mediaLinks = new Dictionary<Guid, Guid>();
        var stored = new Dictionary<Guid, (string Name, string Type, MemoryStream Data)>();
        fixture.Session.Setup(s => s.CreateAttachmentUploadAsync(It.IsAny<Communications.Domain.Shared.Contracts.Requests.Attachments.CreateChatAttachmentUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Communications.Domain.Shared.Contracts.Requests.Attachments.CreateChatAttachmentUploadRequest request, CancellationToken _) => {
                var id = Guid.NewGuid(); stored[id] = (request.FileName, request.ContentType, new MemoryStream());
                return ChatFixture.Ok(new StorageUploadSessionResponse { Id = id, StorageFileId = id, ChunkSizeBytes = 256 * 1024 });
            });
        storage.Setup(s => s.UploadChatStorageFilePart(It.IsAny<UploadChatStorageFilePartRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UploadChatStorageFilePartRequest request, CancellationToken _) => { stored[request.UploadSessionId].Data.Write(request.ChunkBytes); return ChatFixture.Ok(new StorageUploadPartResponse()); });
        storage.Setup(s => s.CompleteChatStorageUploadSession(It.IsAny<CompleteChatStorageUploadSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompleteChatStorageUploadSessionRequest request, CancellationToken _) => ChatFixture.Ok(new StorageFileResponse { Id = request.UploadSessionId, Status = XFramework.Domain.Shared.Contracts.StorageFileStatus.Available }));
        storage.Setup(s => s.GetStorageFile(It.IsAny<GetStorageFileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetStorageFileRequest request, CancellationToken _) => {
                var data = stored.GetValueOrDefault(request.StorageFileId, ("offline-proof.txt", "text/plain", new MemoryStream()));
                return ChatFixture.Ok(new StorageFileResponse { Id = request.StorageFileId, Name = data.Item1, ContentType = data.Item2, ContentLengthBytes = data.Item3.Length, Status = XFramework.Domain.Shared.Contracts.StorageFileStatus.Available });
            });
        fixture.Session.Setup(s => s.GetAttachmentDownloadUrlAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid message, Guid attachment, CancellationToken _) => ChatFixture.Ok(new StorageDownloadUrlResponse { Url = $"http://127.0.0.1:{port}/test/media/{mediaLinks[attachment]}" }));
        var attachments = new List<MessageFileResponse>();
        fixture.Session.Setup(s => s.AttachFileAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid message, Guid storedFile, CancellationToken _) =>
            {
                var id = Guid.NewGuid(); mediaLinks[id] = storedFile;
                attachments.Add(new MessageFileResponse { Id = id, MessageId = message, StorageFileId = storedFile });
                messages.First(m => m.Id == message).HasAttachments = true;
                return Success();
            });
        fixture.Session.Setup(s => s.GetFilesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid message, int page, int size, CancellationToken _) =>
                ChatFixture.Ok(new PaginatedResult<MessageFileResponse>(attachments.Count, page, size, attachments.Where(f => f.MessageId == message).ToArray())));
        storage.Setup(s => s.GetStorageDownloadUrl(It.IsAny<GetStorageDownloadUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageDownloadUrlResponse { StorageFileId = fileId, Url = $"http://127.0.0.1:{port}/test/file", ExpiresAt = DateTime.UtcNow.AddMinutes(5) }));

        fixture.Session.Setup(s => s.UpdateThreadAsync(It.IsAny<UpdateThreadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateThreadRequest request, CancellationToken _) => { features = request.Features ?? features; if (request.NicknameMemberId.HasValue) fixtureMembers.First(m => m.Id == request.NicknameMemberId).Alias = request.Nickname ?? ""; return Success(); });
        fixture.Session.Setup(s => s.UpdateMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid member, string role, CancellationToken _) => { fixtureMembers.First(m => m.Id == member).Role = role; return Success(); });
        configureIdentity?.Invoke(identity);
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Presentation/XFramework.Yap"));
        var app = YapApplication.Build([
            "--contentRoot", root, "--applicationName", "XFramework.Yap", "--environment", "Development",
            "--webroot", Environment.GetEnvironmentVariable("YAP_FIXTURE_WEBROOT") ?? Path.Combine(root, "wwwroot"),
            "--staticWebAssets", Environment.GetEnvironmentVariable("YAP_FIXTURE_WEBROOT") is null
                ? Path.Combine(AppContext.BaseDirectory, "XFramework.Yap.staticwebassets.runtime.json") : Path.Combine(root, "fixture-published-no-runtime-manifest.json"),
            "--urls", $"http://127.0.0.1:{port}", "--Yap:TenantId", fixture.Tenant.ToString(),
            "--Yap:RoleId", registrationRole.ToString(),
            "--ServiceIdentity:GenerationId", "fixture-g1", "--ServiceIdentity:ClientSecret", "fixture-only-secret-not-for-any-real-service"
        ], builder =>
        {
            foreach (var service in builder.Services.Where(d => d.ServiceType == typeof(IHostedService) &&
                         d.ImplementationType?.Name.Contains("Bolt", StringComparison.Ordinal) == true).ToArray())
                builder.Services.Remove(service);
            builder.Services.Replace(ServiceDescriptor.Singleton(fixture.Client.Object));
            builder.Services.Replace(ServiceDescriptor.Singleton(identity.Object));
            builder.Services.Replace(ServiceDescriptor.Singleton(directory.Object));
            builder.Services.Replace(ServiceDescriptor.Singleton(storage.Object));
        });
        app.MapGet("/test/media/{id:guid}", (Guid id) => Results.Bytes(stored[id].Data.ToArray(), stored[id].Type));
        app.MapPost("/test/history/{count:int}", (int count) => {
            for (var i = 0; i < Math.Min(count, 2000); i++) messages.Add(new() { Id = Guid.NewGuid(), Text = $"History {i}: " + new string('a', i % 5 * 70), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", CreatedAt = DateTime.UtcNow.AddDays(-1).AddSeconds(i) });
            return Results.Ok();
        });
        app.MapPost("/test/incoming", async () =>
        {
            messages.Add(new() { Id = Guid.NewGuid(), Text = "A live update from Sarah", SenderCredentialId = friend, SenderAlias = "Sarah Mensah", CreatedAt = DateTime.UtcNow });
            if (fixture.OnEvent is not null) await fixture.OnEvent(new CommunicationsRealtimeEvent { TenantId = fixture.Tenant, ThreadId = fixture.Thread });
            return Results.Ok();
        });
        app.MapGet("/test/file", () => Results.Text("Fixture attachment download", "text/plain"));
        app.MapPost("/test/typing/{active:bool}", async (bool active) =>
        {
            if (onTyping is not null) await onTyping(new() { TenantId = fixture.Tenant, ThreadId = typingThread,
                CredentialId = friend, IsTyping = active, OccurredAt = DateTime.UtcNow });
            return Results.Ok();
        });
        return app;
    }

    private static CmdResponse Success() => new() { HttpStatusCode = HttpStatusCode.OK };
}
