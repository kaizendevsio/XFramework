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
        var messages = new List<ThreadMessageItemResponse>
        {
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "Hey! Are you around? 👀", CreatedAt = DateTime.UtcNow.AddMinutes(-25) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "Can you take a look at the onboarding flow? I pushed the new screens last night.", CreatedAt = DateTime.UtcNow.AddMinutes(-24) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = fixture.Credential, Text = "Just opened it — the empty state is so much better now.", CreatedAt = DateTime.UtcNow.AddMinutes(-18) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "That's the one! I'll clean up the spacing today.", CreatedAt = DateTime.UtcNow.AddMinutes(-14) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = fixture.Credential, Text = "Perfect. Let's also sort the copy on step 3.", CreatedAt = DateTime.UtcNow.AddMinutes(-12) },
            new() { Id = Guid.NewGuid(), SenderCredentialId = friend, SenderAlias = "Sarah Mensah", Text = "Perfect, I'll push the new build tonight", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        };
        fixture.Session.Setup(s => s.GetThreadsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ChatFixture.Ok(new GetThreadListResponse { Items = conversations.ToList(), TotalCount = conversations.Count }));
        fixture.Session.Setup(s => s.GetThreadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => ChatFixture.Ok(new GetThreadResponse
            {
                Id = id, Name = conversations.First(c => c.Id == id).Name,
                Members = [new() { CredentialId = fixture.Credential, Alias = "Jamie Davis" }, new() { CredentialId = friend, Alias = "Sarah Mensah" }]
            }));
        fixture.Session.Setup(s => s.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ChatFixture.Ok(new GetThreadMessagesResponse { Items = messages.ToList(), TotalCount = messages.Count }));
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
        fixture.Session.Setup(s => s.SubscribeTypingAsync(It.IsAny<Guid>(), It.IsAny<Func<CommunicationsTypingState, Task>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var identity = new Mock<IIdentityServerServiceWrapper>();
        identity.Setup(i => i.RegisterIdentity(It.IsAny<RegisterIdentityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse<IdentityServer.Domain.Shared.Contracts.Responses.RegisterIdentityResponse>
            { HttpStatusCode = HttpStatusCode.OK, Response = new()
                { CredentialId = Guid.NewGuid(), TenantId = fixture.Tenant, RoleId = registrationRole } });
        identity.Setup(i => i.AuthenticateIdentity(It.IsAny<AuthenticateIdentityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticateIdentityRequest request, CancellationToken _) => request.UserName == "fixture" && request.Password == "fixture"
                ? ChatFixture.Ok(auth) : new() { HttpStatusCode = HttpStatusCode.Unauthorized });
        identity.Setup(i => i.Logout(It.IsAny<LogoutRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success());
        var directory = new Mock<IChatDirectory>();
        directory.Setup(d => d.ResolveAsync(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        directory.Setup(d => d.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ChatPerson(friend, "Sarah Mensah", "sarah"), new ChatPerson(Guid.NewGuid(), "Marco Bianchi", "marco")]);
        var storage = new Mock<IStorageServiceWrapper>();
        var fileId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        storage.Setup(s => s.EnsureStorageUploadMetadata(It.IsAny<EnsureStorageUploadMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadMetadataResponse { TypeId = Guid.NewGuid(), StorageFileIdentifierId = Guid.NewGuid() }));
        storage.Setup(s => s.CreateStorageUploadSession(It.IsAny<CreateStorageUploadSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadSessionResponse { Id = uploadId, StorageFileId = fileId, ChunkSizeBytes = 256 * 1024 }));
        storage.Setup(s => s.UploadStorageFilePart(It.IsAny<UploadStorageFilePartRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageUploadPartResponse()));
        storage.Setup(s => s.CompleteStorageUploadSession(It.IsAny<CompleteStorageUploadSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageFileResponse { Id = fileId }));
        var attachments = new List<MessageFileResponse>();
        fixture.Session.Setup(s => s.AttachFileAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid message, Guid storedFile, CancellationToken _) =>
            {
                attachments.Add(new MessageFileResponse { Id = Guid.NewGuid(), MessageId = message, StorageFileId = storedFile });
                return Success();
            });
        fixture.Session.Setup(s => s.GetFilesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid thread, Guid message, int page, int size, CancellationToken _) =>
                ChatFixture.Ok(new PaginatedResult<MessageFileResponse>(attachments.Count, page, size, attachments.Where(f => f.MessageId == message).ToArray())));
        storage.Setup(s => s.GetStorageDownloadUrl(It.IsAny<GetStorageDownloadUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatFixture.Ok(new StorageDownloadUrlResponse { StorageFileId = fileId, Url = $"http://127.0.0.1:{port}/test/file", ExpiresAt = DateTime.UtcNow.AddMinutes(5) }));

        configureIdentity?.Invoke(identity);
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Presentation/XFramework.Yap"));
        var app = YapApplication.Build([
            "--contentRoot", root, "--applicationName", "XFramework.Yap", "--environment", "Development",
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
        app.MapPost("/test/incoming", async () =>
        {
            messages.Add(new() { Id = Guid.NewGuid(), Text = "A live update from Sarah", SenderCredentialId = friend, SenderAlias = "Sarah Mensah", CreatedAt = DateTime.UtcNow });
            if (fixture.OnEvent is not null) await fixture.OnEvent(new CommunicationsRealtimeEvent { TenantId = fixture.Tenant, ThreadId = fixture.Thread });
            return Results.Ok();
        });
        app.MapGet("/test/file", () => Results.Text("Fixture attachment download", "text/plain"));
        return app;
    }

    private static CmdResponse Success() => new() { HttpStatusCode = HttpStatusCode.OK };
}
