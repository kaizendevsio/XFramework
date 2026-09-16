using System.Net;
using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Clients;
using Communications.Integration.Drivers;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
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
internal static partial class UiFixture
{
    public static WebApplication Create(int port = 5189, Action<Mock<IIdentityServerServiceWrapper>>? configureIdentity = null, bool? enableCalls = null, X509Certificate2? certificate = null,
        Action<ChatFixture>? configureChat = null, Action<IServiceCollection>? configureServices = null, bool? enableEncryption = null)
    {
        var fixture = new ChatFixture();
        var registrationRole = Guid.NewGuid();
        var auth = YapSessionsTests.Session();
        auth.Credential!.Id = fixture.Credential;
        auth.Credential.TenantId = fixture.Tenant;
        var friend = Guid.NewGuid();
        var encryptionFixture = enableEncryption ?? Environment.GetEnvironmentVariable("YAP_FIXTURE_ENCRYPTION") == "1";
        var voiceFixture = encryptionFixture || (enableCalls ?? Environment.GetEnvironmentVariable("YAP_FIXTURE_CALLS") == "1");
        var friendAuth = YapSessionsTests.Session("fixture-callee-token");
        friendAuth.Credential!.Id = friend;
        friendAuth.Credential.TenantId = fixture.Tenant;
        friendAuth.Credential.UserName = "Sarah Mensah";
        var third = Guid.NewGuid();
        var thirdAuth = YapSessionsTests.Session("fixture-third-token");
        thirdAuth.Credential!.Id = third;
        thirdAuth.Credential.TenantId = fixture.Tenant;
        thirdAuth.Credential.UserName = "Robin Chen";
        if (voiceFixture)
            fixture.Session.SetupGet(s => s.CredentialId).Returns(() =>
                Guid.TryParse(new HttpContextAccessor().HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var current) ? current : fixture.Credential);
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
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "Hey! Are you around? 👀", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "Can you take a look at the onboarding flow? I pushed the new screens last night.", CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = fixture.Credential, Text = "Just opened it — the empty state is so much better now.", CreatedAt = DateTime.UtcNow.AddMinutes(-18) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "That's the one! I'll clean up the spacing today.", CreatedAt = DateTime.UtcNow.AddMinutes(-14) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = fixture.Credential, Text = "Perfect. Let's also sort the copy on step 3.", CreatedAt = DateTime.UtcNow.AddMinutes(-12) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "Perfect, I'll push the new build tonight", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        };
        messages[2].DeliveredCount = 1;
        messages[4].DeliveredCount = 1; messages[4].ReadCount = 1; messages[4].ReadCredentialIds = [friend];
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
                Id = id, Name = conversations.First(c => c.Id == id).Name, IsDirect = conversations.First(c => c.Id == id).IsDirect, HasCustomName = conversations.First(c => c.Id == id).HasCustomName, PhotoStorageFileId = conversations.First(c => c.Id == id).PhotoStorageFileId, CanManage = true, Features = features,
                Members = fixtureMembers.ToList()
            }));
        fixture.Session.Setup(s => s.SetThreadActiveStatusAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, bool share, CancellationToken _) =>
            {
                fixtureMembers.Single(member => member.CredentialId == fixture.Session.Object.CredentialId).HideActiveStatus = !share;
                return new CmdResponse { HttpStatusCode = HttpStatusCode.OK };
            });
        fixture.Session.Setup(s => s.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .Returns(async (Guid thread, int page, int size, CancellationToken ct, bool suppressDeliveryAcknowledgement) =>
            {
                if (int.TryParse(Environment.GetEnvironmentVariable("YAP_FIXTURE_MESSAGE_DELAY_MS"), out var delay))
                    await Task.Delay(Math.Clamp(delay, 0, 10000), ct);
                return ChatFixture.Ok(new GetThreadMessagesResponse { Items = messages.OrderByDescending(m => m.CreatedAt).Skip(page * size).Take(size).ToList(), TotalCount = messages.Count });
            });
        foreach (var conversation in conversations) conversation.IsDirect = conversation.MemberCount == 2;
        fixture.Session.Setup(s => s.GetRepliesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync((Guid thread, Guid parent, int page, int size, CancellationToken _, bool suppressDeliveryAcknowledgement) =>
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
        // Saved messages are per-requester state, so the fixture keeps its own set and the
        // saved list is served from it exactly like the real per-member join.
        var saved = new Dictionary<Guid, DateTime> { [messages[1].Id] = DateTime.UtcNow.AddMinutes(-30), [messages[4].Id] = DateTime.UtcNow.AddMinutes(-2) };
        foreach (var message in messages) message.IsSaved = saved.ContainsKey(message.Id);
        fixture.Session.Setup(s => s.SaveMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid id, CancellationToken _) =>
            { saved[id] = DateTime.UtcNow; foreach (var m in messages.Where(m => m.Id == id)) m.IsSaved = true; return Success(); });
        fixture.Session.Setup(s => s.UnsaveMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid id, CancellationToken _) =>
            { saved.Remove(id); foreach (var m in messages.Where(m => m.Id == id)) m.IsSaved = false; return Success(); });
        fixture.Session.Setup(s => s.GetSavedMessagesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int size, CancellationToken _) =>
            {
                var rows = messages.Where(m => saved.ContainsKey(m.Id)).OrderByDescending(m => saved[m.Id]).ToArray();
                return ChatFixture.Ok(new GetSavedMessagesResponse
                {
                    Items = rows.Skip(page * size).Take(size).Select(m => new SavedMessageItemResponse
                    {
                        ThreadId = fixture.Thread, ThreadName = conversations.First(c => c.Id == fixture.Thread).Name,
                        IsDirect = true, MessageId = m.Id, SenderCredentialId = m.SenderCredentialId, SenderAlias = m.SenderAlias,
                        OtherCredentialId = m.SenderCredentialId == fixture.Credential ? friend : m.SenderCredentialId,
                        Text = m.Text, EncryptedEnvelope = m.EncryptedEnvelope, EncryptionSenderDeviceId = m.EncryptionSenderDeviceId,
                        AcceptedSenderDirectoryRevision = m.AcceptedSenderDirectoryRevision, EncryptionPending = m.EncryptionPending,
                        ParentMessageId = m.ParentMessageId, IsThreadReply = m.IsThreadReply,
                        CreatedAt = m.CreatedAt, SavedAt = saved[m.Id]
                    }).ToList(),
                    TotalCount = rows.Length, PageIndex = page, PageSize = size
                });
            });
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
            .ReturnsAsync((AuthenticateIdentityRequest request, CancellationToken _) => (request.UserName == "fixture" || voiceFixture && request.UserName == "callee" || encryptionFixture && request.UserName == "third")
                && request.Password == (Environment.GetEnvironmentVariable("YAP_FIXTURE_PASSWORD") ?? "fixture")
                ? ChatFixture.Ok(request.UserName == "third" ? thirdAuth : request.UserName == "callee" ? friendAuth : auth) : new() { HttpStatusCode = HttpStatusCode.Unauthorized });
        identity.Setup(i => i.Logout(It.IsAny<LogoutRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success());
        var directory = new Mock<IChatDirectory>();
        directory.Setup(d => d.ResolveAsync(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid[] ids, CancellationToken _) => ids.Contains(friend) ? new[] { new ChatPerson(friend, "Sarah Mensah", "sarah", "/yap-app-v2-192.png") } : Array.Empty<ChatPerson>());
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
        var upstreamReads = new System.Collections.Concurrent.ConcurrentDictionary<Guid, int>();
        Guid? profilePhoto = null;
        identity.Setup(i => i.UploadOwnAvatar(It.IsAny<UploadOwnAvatarRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UploadOwnAvatarRequest request, CancellationToken _) => {
                var id = Guid.NewGuid(); profilePhoto = id; stored[id] = (request.FileName!, request.ContentType!, new MemoryStream(request.FileBytes!));
                return ChatFixture.Ok(new IdentityServer.Domain.Shared.Contracts.Responses.CredentialAvatarResponse { CredentialId = fixture.Credential, StorageFileId = id });
            });
        // A peer photo has to come from the account-scoped media route, exactly like production.
        // Pointing peers at a static PNG hid every caching defect on that route behind the
        // static-file pipeline, which is the one pipeline the route does not use.
        var friendPhoto = Guid.NewGuid();
        stored[friendPhoto] = ("sarah.jpg", "image/jpeg", new MemoryStream(FixtureJpeg));
        directory.Setup(d => d.ResolveAsync(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid[] ids, CancellationToken _) =>
            new[] { new ChatPerson(friend, "Sarah Mensah", "sarah", $"/api/chat/people/{friend}/photo?account={fixture.Tenant:N}:{fixture.Credential:N}&v={friendPhoto:N}", friendPhoto),
                new ChatPerson(fixture.Credential, "Jamie Davis", "fixture", profilePhoto is { } id ? $"/api/chat/people/{fixture.Credential}/photo?account={fixture.Tenant:N}:{fixture.Credential:N}&v={id:N}" : null, profilePhoto) }.Where(p => ids.Contains(p.Id)).ToArray());
        fixture.Session.Setup(s => s.CreateAttachmentUploadAsync(It.IsAny<Communications.Domain.Shared.Contracts.Requests.Attachments.CreateChatAttachmentUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Communications.Domain.Shared.Contracts.Requests.Attachments.CreateChatAttachmentUploadRequest request, CancellationToken _) => {
                var id = Guid.NewGuid(); stored[id] = (request.FileName, request.ContentType, new MemoryStream());
                return ChatFixture.Ok(new StorageUploadSessionResponse { Id = id, StorageFileId = id, ChunkSizeBytes = request.ChunkSizeBytes ?? 256 * 1024 });
            });
        storage.Setup(s => s.UploadChatStorageFilePart(It.IsAny<UploadChatStorageFilePartRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UploadChatStorageFilePartRequest request, CancellationToken _) => {
                var data = stored[request.UploadSessionId].Data;
                lock (data) { data.Position = request.OffsetBytes; data.Write(request.ChunkBytes); }
                return ChatFixture.Ok(new StorageUploadPartResponse()); });
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
        if (Environment.GetEnvironmentVariable("YAP_FIXTURE_VIDEO") is { } videoPath)
        {
            var name = Path.GetFileName(videoPath);
            var id = Guid.NewGuid(); stored[id] = (name, "application/octet-stream", new MemoryStream(File.ReadAllBytes(videoPath)));
            var message = new ThreadMessageItemResponse { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = name, HasAttachments = true, CreatedAt = DateTime.UtcNow.AddSeconds(-10) };
            messages.Add(message); var attachment = Guid.NewGuid(); mediaLinks[attachment] = id;
            attachments.Add(new MessageFileResponse { Id = attachment, MessageId = message.Id, StorageFileId = id });
        }
        // One received and one sent clip: the voice player must appear on both sides.
        if (Environment.GetEnvironmentVariable("YAP_FIXTURE_VOICE") is { } voicePath)
        {
            var id = Guid.NewGuid(); stored[id] = ("Voice message.m4a", "audio/mp4", new MemoryStream(File.ReadAllBytes(voicePath)));
            foreach (var sender in new[] { friend, fixture.Credential })
            {
                var message = new ThreadMessageItemResponse { Id = Guid.NewGuid(), SenderCredentialId = sender, SenderAlias = sender == friend ? "Sarah Mensah" : "Jamie Davis", Text = "Voice message.m4a", HasAttachments = true, CreatedAt = DateTime.UtcNow.AddSeconds(-20) };
                messages.Add(message); var attachment = Guid.NewGuid(); mediaLinks[attachment] = id;
                attachments.Add(new MessageFileResponse { Id = attachment, MessageId = message.Id, StorageFileId = id });
            }
        }
        if (int.TryParse(Environment.GetEnvironmentVariable("YAP_FIXTURE_HISTORY"), out var history))
        {
            var small = Environment.GetEnvironmentVariable("YAP_FIXTURE_SMALL_MESSAGES") == "1";
            // Optional browser stress fixture: paginated variable-height text and real PNG previews.
            var image = Guid.NewGuid();
            stored[image] = ("scroll-fixture.png", "image/png", new MemoryStream(File.ReadAllBytes(Path.GetFullPath("src/Presentation/XFramework.Yap.Client/wwwroot/yap-app-v2-512.png"))));
            for (var i = 0; i < Math.Clamp(history, 0, 1000); i++)
            {
                var item = new ThreadMessageItemResponse { Id = Guid.NewGuid(), SenderCredentialId = i % 2 == 0 ? friend : fixture.Credential,
                    SenderAlias = "Scroll fixture", Text = small ? $"Message {i:D4}" : $"History {i:D4}: " + string.Join(' ', Enumerable.Repeat("Variable height message for rapid scrolling.", i % 6 + 1)),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-2000 + i), HasAttachments = !small && i % 5 == 0 };
                messages.Add(item);
                if (item.HasAttachments)
                {
                    var attachment = Guid.NewGuid(); mediaLinks[attachment] = image;
                    attachments.Add(new MessageFileResponse { Id = attachment, MessageId = item.Id, StorageFileId = image });
                }
            }
        }
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
            .ReturnsAsync((GetStorageDownloadUrlRequest request, CancellationToken _) => ChatFixture.Ok(new StorageDownloadUrlResponse { StorageFileId = request.StorageFileId, Url = stored.ContainsKey(request.StorageFileId) ? $"http://127.0.0.1:{new HttpContextAccessor().HttpContext!.Request.Host.Port}/test/media/{request.StorageFileId}" : $"http://127.0.0.1:{new HttpContextAccessor().HttpContext!.Request.Host.Port}/test/file", ExpiresAt = DateTime.UtcNow.AddMinutes(5) }));
        fixture.Session.Setup(s => s.GetThreadPhotoDownloadUrlAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            // The live port, not the requested one: tests ask for 0 and are given a free port.
            .ReturnsAsync((Guid thread, CancellationToken _) => ChatFixture.Ok(new StorageDownloadUrlResponse { Url = $"http://127.0.0.1:{new HttpContextAccessor().HttpContext!.Request.Host.Port}/test/media/{conversations.First(c => c.Id == thread).PhotoStorageFileId}" }));

        fixture.Session.Setup(s => s.UpdateThreadAsync(It.IsAny<UpdateThreadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateThreadRequest request, CancellationToken _) => { var chat = conversations.First(c => c.Id == request.ThreadId); if(request.Name is not null) { chat.Name = request.Name; chat.HasCustomName = true; } if (request.PhotoStorageFileId is { } photo) chat.PhotoStorageFileId = photo; features = request.Features ?? features; if (request.NicknameMemberId.HasValue) fixtureMembers.First(m => m.Id == request.NicknameMemberId).Alias = request.Nickname ?? ""; return Success(); });
        fixture.Session.Setup(s => s.UpdateMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid member, string role, CancellationToken _) => { fixtureMembers.First(m => m.Id == member).Role = role; return Success(); });
        var callMembership = new Mock<ICommunicationsServiceWrapper>();
        callMembership.Setup(x => x.GetDeferredEncryptionAsync(It.IsAny<GetDeferredEncryptionRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(ChatFixture.Ok(new DeferredEncryptionResponse()));
        var encryption = encryptionFixture ? new EncryptionFixture(fixture, identity, directory, fixtureMembers, conversations, messages, attachments, mediaLinks, stored, third, callMembership) : null;
        identity.Setup(x => x.OpaqueAuth(It.IsAny<OpaqueAuthRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OpaqueAuthRequest request, CancellationToken _) => ChatFixture.Ok(new IdentityServer.Domain.Shared.Contracts.Responses.OpaqueAuthResponse
            { Mode = "legacy", UserName = "fixture", Client = $"{fixture.Tenant:D}:{fixture.Credential:D}" }));
        configureIdentity?.Invoke(identity);
        callMembership.Setup(c => c.GetThreadAsync(It.IsAny<GetThreadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetThreadRequest request, CancellationToken _) => request.Id == fixture.Thread
                ? ChatFixture.Ok(new GetThreadResponse { Id = fixture.Thread, Name = "Fixture voice conversation", IsDirect = !encryptionFixture, Members = fixtureMembers })
                : new() { HttpStatusCode = HttpStatusCode.Forbidden });
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Presentation/XFramework.Yap"));
        var app = YapApplication.Build([
            "--contentRoot", root, "--applicationName", "XFramework.Yap", "--environment", "Development",
            "--webroot", Environment.GetEnvironmentVariable("YAP_FIXTURE_WEBROOT") ?? Path.Combine(root, "wwwroot"),
            "--staticWebAssets", Environment.GetEnvironmentVariable("YAP_FIXTURE_WEBROOT") is null
                ? Path.Combine(AppContext.BaseDirectory, "XFramework.Yap.staticwebassets.runtime.json") : Path.Combine(root, "fixture-published-no-runtime-manifest.json"),
            "--urls", $"{(voiceFixture ? "https" : "http")}://127.0.0.1:{port}", "--Yap:TenantId", fixture.Tenant.ToString(),
            "--AllowedHosts", "localhost;127.0.0.1;*.dev.localhost",
            "--Yap:Calls:Enabled", voiceFixture.ToString(), "--Yap:Calls:SecurityMode", encryptionFixture ? "EndToEndEncrypted" : "TrustedServerTls",
            "--Yap:Encryption:Enabled", encryptionFixture.ToString(),
            "--Yap:RoleId", registrationRole.ToString(),
            "--ServiceIdentity:GenerationId", "fixture-g1", "--ServiceIdentity:ClientSecret", "fixture-only-secret-not-for-any-real-service"
        ], builder =>
        {
            if (certificate is not null) builder.WebHost.ConfigureKestrel(options =>
                options.ConfigureHttpsDefaults(https => https.ServerCertificate = certificate));
            foreach (var service in builder.Services.Where(d => d.ServiceType == typeof(IHostedService) &&
                         d.ImplementationType?.Name.Contains("Bolt", StringComparison.Ordinal) == true).ToArray())
                builder.Services.Remove(service);
            builder.Services.Replace(ServiceDescriptor.Singleton(fixture.Client.Object));
            builder.Services.Replace(ServiceDescriptor.Singleton(identity.Object));
            builder.Services.Replace(ServiceDescriptor.Singleton(directory.Object));
            builder.Services.Replace(ServiceDescriptor.Singleton(storage.Object));
            if (voiceFixture) builder.Services.Replace(ServiceDescriptor.Singleton(callMembership.Object));
            encryption?.ConfigureServices(builder.Services);
            configureChat?.Invoke(fixture);
            configureServices?.Invoke(builder.Services);
        });
        // Upstream reads are proxied one-for-one, so this count is exactly how many browser
        // requests reached the server - the number a caching claim has to be measured against.
        app.MapGet("/test/media/{id:guid}", (Guid id) =>
        {
            upstreamReads.AddOrUpdate(id, 1, (_, count) => count + 1);
            return Results.Bytes(stored[id].Data.ToArray(), stored[id].Type, enableRangeProcessing: true);
        });
        app.MapGet("/test/upstream-reads", () => Results.Ok(upstreamReads.ToDictionary(x => x.Key.ToString("N"), x => x.Value)));
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

    // A 1x1 baseline JPEG: the smallest payload an <img> will actually decode, so a photo that
    // fails to render cannot be mistaken for a photo that failed to cache.
    internal static byte[] FixtureJpeg { get; } = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/" +
        "wAALCAABAAEBAREA/8QAFAABAAAAAAAAAAAAAAAAAAAACf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAD8AKp//2Q==");
}
