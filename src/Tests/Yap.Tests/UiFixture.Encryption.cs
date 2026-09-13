using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Bolt.Server;
using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Storage.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Security;
using Yap.Services;

namespace Yap.Tests;

internal static partial class UiFixture
{
    // Disposable backing services for the browser fixture, never registered by the production host.
    // OpenPGP generation, approval, recovery, outbox, uploads and SFrame all run in the real browser code.
    // This models actor ownership and compare-and-swap, not the production service's crypto validation.
    private sealed class EncryptionFixture
    {
        private readonly object gate = new();
        private readonly ChatFixture fixture;
        private readonly Dictionary<Guid, EncryptionDirectoryResponse> directories = [];
        private readonly Dictionary<Guid, EncryptionRecoveryResponse> recovery = [];
        private readonly Dictionary<Guid, CreateThreadMessageRequest> acceptedRequests = [];
        private readonly List<(Func<CommunicationsRealtimeEvent, Task> Handler, CancellationToken Token)> listeners = [];
        private readonly Dictionary<Guid, (string Name, string Type, MemoryStream Data)> stored;
        private readonly HashSet<Guid> accounts;
        private readonly IIdentityServerServiceWrapper identity;
        private readonly Dictionary<string, Guid> tokens;
        private IActorAccessTokenProvider? actorTokens;

        public EncryptionFixture(ChatFixture fixture, Mock<IIdentityServerServiceWrapper> identity,
            Mock<IChatDirectory> directory, List<ThreadMemberResponse> members,
            List<ThreadListItemResponse> conversations, List<ThreadMessageItemResponse> messages,
            List<MessageFileResponse> attachments, Dictionary<Guid, Guid> mediaLinks,
            Dictionary<Guid, (string Name, string Type, MemoryStream Data)> stored, Guid third)
        {
            this.fixture = fixture;
            this.stored = stored;
            this.identity = identity.Object;
            tokens = new() { ["fixture-access-token"] = fixture.Credential,
                ["fixture-callee-token"] = members.Single(x => x.CredentialId != fixture.Credential).CredentialId,
                ["fixture-third-token"] = third };
            members.Add(new() { Id = Guid.NewGuid(), CredentialId = third, Alias = "Robin Chen", Role = "Admin" });
            accounts = members.Select(x => x.CredentialId).ToHashSet();
            conversations.RemoveAll(x => x.Id != fixture.Thread);
            conversations[0].Name = "Encrypted browser fixture";
            conversations[0].IsDirect = false;
            conversations[0].MemberCount = 3;
            conversations[0].LastMessagePreview = null;
            conversations[0].UnreadCount = 0;
            messages.Clear();
            attachments.Clear();
            var people = members.Select(x => new ChatPerson(x.CredentialId, x.Alias,
                x.CredentialId == fixture.Credential ? "fixture" : x.CredentialId == third ? "third" : "callee")).ToArray();
            directory.Setup(x => x.ResolveAsync(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid[] ids, CancellationToken _) => people.Where(x => ids.Contains(x.Id)).ToArray());
            directory.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string term, CancellationToken _) => people.Where(x => x.Id != Actor()
                    && (x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || x.UserName.Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray());

            identity.Setup(x => x.GetEncryptionDirectory(It.IsAny<GetEncryptionDirectoryRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GetEncryptionDirectoryRequest request, CancellationToken _) =>
                {
                    Actor();
                    lock (gate) return directories.TryGetValue(request.CredentialId, out var value)
                        ? ChatFixture.Ok(Clone(value)) : Missing<EncryptionDirectoryResponse>();
                });
            identity.Setup(x => x.PutEncryptionDirectory(It.IsAny<PutEncryptionDirectoryRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PutEncryptionDirectoryRequest request, CancellationToken _) =>
                {
                    var actor = Actor();
                    lock (gate)
                    {
                        var old = directories.GetValueOrDefault(actor);
                        if ((old?.Revision ?? 0) != request.ExpectedRevision || old is not null && old.RootPublicKey != request.RootPublicKey)
                            return Conflict<EncryptionDirectoryResponse>();
                        var value = new EncryptionDirectoryResponse { TenantId = fixture.Tenant, CredentialId = actor,
                            Revision = request.ExpectedRevision + 1, RootPublicKey = request.RootPublicKey,
                            Roster = request.Roster, Devices = Clone(request.Devices) };
                        directories[actor] = value;
                        return ChatFixture.Ok(Clone(value));
                    }
                });
            identity.Setup(x => x.GetEncryptionRecovery(It.IsAny<GetEncryptionRecoveryRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GetEncryptionRecoveryRequest _, CancellationToken _) =>
                {
                    var actor = Actor();
                    lock (gate) return !directories.ContainsKey(actor) ? Missing<EncryptionRecoveryResponse>()
                        : ChatFixture.Ok(Clone(recovery.GetValueOrDefault(actor) ?? new()));
                });
            identity.Setup(x => x.PutEncryptionRecovery(It.IsAny<PutEncryptionRecoveryRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PutEncryptionRecoveryRequest request, CancellationToken _) =>
                {
                    var actor = Actor();
                    lock (gate)
                    {
                        if (!directories.ContainsKey(actor)) return Missing<EncryptionRecoveryResponse>();
                        if ((recovery.GetValueOrDefault(actor)?.Revision ?? 0) != request.ExpectedRevision) return Conflict<EncryptionRecoveryResponse>();
                        var value = new EncryptionRecoveryResponse { Revision = request.ExpectedRevision + 1, Archive = request.Archive };
                        recovery[actor] = value;
                        return ChatFixture.Ok(Clone(value));
                    }
                });
            fixture.Session.Setup(x => x.SubscribeUserEventsAsync(It.IsAny<Func<CommunicationsRealtimeEvent, Task>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<CommunicationsRealtimeEvent, Task> handler, CancellationToken token) =>
                {
                    Actor();
                    lock (gate) { listeners.RemoveAll(x => x.Token.IsCancellationRequested); listeners.Add((handler, token)); }
                    return Task.CompletedTask;
                });
            fixture.Session.Setup(x => x.GetMessagesAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid thread, int page, int size, CancellationToken _) =>
                {
                    Actor();
                    lock (gate) return ChatFixture.Ok(new GetThreadMessagesResponse
                    { Items = Clone(messages.OrderByDescending(x => x.CreatedAt).Skip(page * size).Take(size).ToList()), TotalCount = messages.Count });
                });
            fixture.Session.Setup(x => x.SendMessageAsync(It.IsAny<CreateThreadMessageRequest>(), It.IsAny<CancellationToken>()))
                .Returns(async (CreateThreadMessageRequest request, CancellationToken _) =>
                {
                    var actor = Actor();
                    var id = request.ClientMessageId ?? Guid.NewGuid();
                    lock (gate)
                    {
                        if (acceptedRequests.TryGetValue(id, out var accepted))
                            return accepted.SenderCredentialId == actor && accepted.EncryptedEnvelope == request.EncryptedEnvelope
                                && accepted.EncryptionSenderDeviceId == request.EncryptionSenderDeviceId
                                && accepted.SenderDirectoryRevision == request.SenderDirectoryRevision
                                ? ChatFixture.Ok(new CreateThreadMessageResponse { MessageId = id }) : Conflict<CreateThreadMessageResponse>();
                        if (!directories.TryGetValue(actor, out var sender) || request.SenderDirectoryRevision != sender.Revision
                            || request.EncryptionSenderDeviceId is not { } device || !sender.Devices.Any(x => x.DeviceId == device && x.Revocation is null)
                            || !accounts.SetEquals(request.RecipientCredentialIds)
                            || accounts.Any(x => !directories.TryGetValue(x, out var recipient)
                                || request.RecipientDirectoryRevisions.GetValueOrDefault(x) != recipient.Revision))
                            return Conflict<CreateThreadMessageResponse>();
                        var saved = Clone(request); saved.SenderCredentialId = actor; acceptedRequests[id] = saved;
                        messages.Add(new() { Id = id, Text = request.Text ?? "", ParentMessageId = request.ParentMessageId,
                            SenderCredentialId = actor, SenderAlias = members.Single(x => x.CredentialId == actor).Alias,
                            EncryptedEnvelope = request.EncryptedEnvelope, AcceptedSenderDirectoryRevision = sender.Revision,
                            EncryptionSenderDeviceId = request.EncryptionSenderDeviceId, IsThreadReply = request.IsThreadReply, CreatedAt = DateTime.UtcNow });
                        conversations[0].LastMessagePreview = "Encrypted message";
                        conversations[0].LastMessageAt = DateTime.UtcNow;
                    }
                    await PublishAsync(actor);
                    return ChatFixture.Ok(new CreateThreadMessageResponse { MessageId = id });
                });
            fixture.Session.Setup(x => x.AttachFileAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(async (Guid thread, Guid message, Guid storageId, CancellationToken _) =>
                {
                    var actor = Actor();
                    lock (gate)
                    {
                        if (messages.Single(x => x.Id == message).SenderCredentialId != actor) return new CmdResponse { HttpStatusCode = HttpStatusCode.Forbidden };
                        if (!attachments.Any(x => x.MessageId == message && x.StorageFileId == storageId))
                        {
                            var id = Guid.NewGuid(); mediaLinks[id] = storageId;
                            attachments.Add(new() { Id = id, MessageId = message, StorageFileId = storageId });
                            messages.Single(x => x.Id == message).HasAttachments = true;
                        }
                    }
                    await PublishAsync(actor);
                    return Success();
                });
            fixture.Session.Setup(x => x.GetAttachmentDownloadUrlAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid thread, Guid message, Guid attachment, CancellationToken _) =>
                {
                    Actor();
                    lock (gate) return ChatFixture.Ok(new StorageDownloadUrlResponse
                    { Url = $"https://fixture-storage.invalid/media/{mediaLinks[attachment]}" });
                });
        }

        private Guid Actor()
        {
            // Gateway lease revalidation runs outside HTTP, with an explicit authenticated actor token scope.
            var token = actorTokens?.GetTokenAsync().GetAwaiter().GetResult();
            if (token is not null && tokens.TryGetValue(token, out var scopedActor)) return scopedActor;
            var user = new HttpContextAccessor().HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true || user.FindFirstValue(YapAuth.TenantClaim) != fixture.Tenant.ToString()
                || !Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) || !accounts.Contains(actor))
                throw new UnauthorizedAccessException("A fixture account is required.");
            return actor;
        }

        private async Task PublishAsync(Guid actor)
        {
            (Func<CommunicationsRealtimeEvent, Task> Handler, CancellationToken Token)[] snapshot;
            lock (gate) { listeners.RemoveAll(x => x.Token.IsCancellationRequested); snapshot = listeners.ToArray(); }
            foreach (var listener in snapshot)
                if (!listener.Token.IsCancellationRequested)
                    await listener.Handler(new() { EventId = Guid.NewGuid(), TenantId = fixture.Tenant, ThreadId = fixture.Thread,
                        ActorCredentialId = actor, EventType = "MessageChanged", OccurredAt = DateTime.UtcNow });
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Singleton<IIdentityServerServiceWrapper>(sp =>
            { actorTokens = sp.GetRequiredService<IActorAccessTokenProvider>(); return identity; }));
            services.Replace(ServiceDescriptor.Singleton<YapCallGateway>(sp => new(sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<ILogger<BoltServer>>(), enableGroupLifecycle: true)));
            // Only the mocked storage origin is intercepted. Browser HTTP, cookies, account binding,
            // antiforgery, attachment proxy and websocket upgrade still traverse the real host.
            services.AddHttpClient("attachments").ConfigurePrimaryHttpMessageHandler(() => new FixtureStorageHandler(stored));
        }

        private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
        private static QueryResponse<T> Missing<T>() => new() { HttpStatusCode = HttpStatusCode.NotFound, Message = "Encryption directory not found" };
        private static QueryResponse<T> Conflict<T>() => new() { HttpStatusCode = HttpStatusCode.Conflict, Message = "Encryption state changed; refresh before retrying" };
    }

    private sealed class FixtureStorageHandler(Dictionary<Guid, (string Name, string Type, MemoryStream Data)> stored) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not { Host: "fixture-storage.invalid", Scheme: "https" } uri
                || !Guid.TryParse(uri.Segments.Last(), out var id) || !stored.TryGetValue(id, out var file))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var data = file.Data.GetBuffer();
            var length = checked((int)file.Data.Length);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data, 0, length) };
            response.Content.Headers.ContentType = new(file.Type);
            return Task.FromResult(response);
        }
    }
}
