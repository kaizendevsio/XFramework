using System.Net;
using System.Reflection;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Requests.ReferenceData;
using Communications.Domain.Shared.Contracts.Requests.Reactions;
using Communications.Domain.Shared.Contracts.Requests.Attachments;
using Storage.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Contracts.Responses;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Clients;
using Communications.Integration.Drivers;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Communications.Tests.Integration;

public sealed class CommunicationsChatClientTests
{
    [Test]
    public async Task CurrentActorSession_PropagatesActorTokenTenantAndCancellation()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        const string actorToken = "actor-access-token";
        using var cancellation = new CancellationTokenSource();
        var actorProvider = new StubActorProvider(new CommunicationsChatActor(
            tenantId,
            credentialId,
            "device-1",
            actorToken));
        var tokenScope = new RecordingActorAccessTokenScope();
        var proxy = DispatchProxy.Create<ICommunicationsServiceWrapper, RecordingWrapperProxy>();
        var recordingProxy = (RecordingWrapperProxy)(object)proxy;
        recordingProxy.OnInvoke = (method, arguments) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(method.Name, Is.EqualTo(nameof(ICommunicationsServiceWrapper.GetUnreadCountsAsync)));
                Assert.That(tokenScope.CurrentToken, Is.EqualTo(actorToken));
                Assert.That(arguments[0], Is.TypeOf<GetUnreadCountsRequest>());
                Assert.That(((GetUnreadCountsRequest)arguments[0]!).Metadata!.RequestedTenantId, Is.EqualTo(tenantId));
                Assert.That((CancellationToken)arguments[1]!, Is.EqualTo(cancellation.Token));
            });

            return Task.FromResult(new QueryResponse<GetUnreadCountsResponse>
            {
                HttpStatusCode = HttpStatusCode.OK
            });
        };
        var client = new CommunicationsChatClient(
            proxy,
            new ConfigurationBuilder().Build(),
            actorProvider,
            tokenScope);

        var session = await client.ForCurrentActorAsync(ct: cancellation.Token);
        var response = await session.GetUnreadCountsAsync(cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(response.HttpStatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(session.TenantId, Is.EqualTo(tenantId));
            Assert.That(session.CredentialId, Is.EqualTo(credentialId));
            Assert.That(session.DeviceId, Is.EqualTo("device-1"));
            Assert.That(tokenScope.CurrentToken, Is.Null);
            Assert.That(actorProvider.CallCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task NewChatReadsAndDefaults_PropagateActorTenantParametersAndCancellation()
    {
        var tenant = Guid.NewGuid();
        var thread = Guid.NewGuid();
        var message = Guid.NewGuid();
        var scope = new RecordingActorAccessTokenScope();
        var proxy = DispatchProxy.Create<ICommunicationsServiceWrapper, RecordingWrapperProxy>();
        var requests = new List<RequestBase>();
        using var cancellation = new CancellationTokenSource();
        ((RecordingWrapperProxy)(object)proxy).OnInvoke = (method, args) =>
        {
            var request = (RequestBase)args[0]!;
            requests.Add(request);
            Assert.That(request.Metadata!.RequestedTenantId, Is.EqualTo(tenant));
            Assert.That(scope.CurrentToken, Is.EqualTo("actor-token"));
            Assert.That(args[1], Is.EqualTo(cancellation.Token));
            return request switch
            {
                EnsureChatDefaultsRequest or GetChatReferenceDataRequest => Task.FromResult(new QueryResponse<ChatReferenceDataResponse>()),
                GetMessageReactionsRequest => Task.FromResult(new QueryResponse<PaginatedResult<MessageReactionResponse>>()),
                GetThreadMessagesRequest => Task.FromResult(new QueryResponse<GetThreadMessagesResponse>()),
                CreateChatAttachmentUploadRequest => Task.FromResult(new QueryResponse<StorageUploadSessionResponse>()),
                GetChatAttachmentDownloadUrlRequest => Task.FromResult(new QueryResponse<StorageDownloadUrlResponse>()),
                _ => throw new InvalidOperationException(method.Name)
            };
        };
        var client = new CommunicationsChatClient(proxy, new ConfigurationBuilder().Build(),
            new StubActorProvider(new(tenant, Guid.NewGuid(), AccessToken: "actor-token")), scope);
        var session = await client.ForCurrentActorAsync();
        await session.EnsureChatDefaultsAsync(cancellation.Token);
        await session.GetChatReferenceDataAsync(cancellation.Token);
        await session.GetReactionsAsync(thread, message, 2, 10, cancellation.Token);
        await session.GetRepliesAsync(thread, message, 3, 20, cancellation.Token);
        await session.CreateAttachmentUploadAsync(new CreateChatAttachmentUploadRequest
            { ThreadId = thread, FileName = "report.txt", ContentType = "text/plain", TotalSizeBytes = 50 }, cancellation.Token);
        var file = Guid.NewGuid();
        await session.GetAttachmentDownloadUrlAsync(thread, message, file, cancellation.Token);
        var upload = requests.OfType<CreateChatAttachmentUploadRequest>().Single();
        Assert.That((upload.ThreadId, upload.FileName, upload.ContentType, upload.TotalSizeBytes),
            Is.EqualTo((thread, "report.txt", "text/plain", 50L)));
        var download = requests.OfType<GetChatAttachmentDownloadUrlRequest>().Single();
        Assert.That((download.ThreadId, download.MessageId, download.FileId), Is.EqualTo((thread, message, file)));
        var reactions = requests.OfType<GetMessageReactionsRequest>().Single();
        Assert.That((reactions.ThreadId, reactions.MessageId, reactions.PageIndex, reactions.PageSize),
            Is.EqualTo((thread, message, 2, 10)));
        var replies = requests.OfType<GetThreadMessagesRequest>().Single();
        Assert.That(replies.ParentMessageId, Is.EqualTo(message));
        Assert.That(replies.ThreadId, Is.EqualTo(thread));
        Assert.That(scope.CurrentToken, Is.Null);
    }

    private sealed class StubActorProvider(CommunicationsChatActor actor) : ICommunicationsChatActorProvider
    {
        public int CallCount { get; private set; }

        public ValueTask<CommunicationsChatActor?> GetCurrentActorAsync(CancellationToken ct = default)
        {
            CallCount++;
            return ValueTask.FromResult<CommunicationsChatActor?>(actor);
        }
    }

    private sealed class RecordingActorAccessTokenScope : IActorAccessTokenScope
    {
        public string? CurrentToken { get; private set; }

        public IDisposable Push(string actorAccessToken)
        {
            var previous = CurrentToken;
            CurrentToken = actorAccessToken;
            return new CallbackDisposable(() => CurrentToken = previous);
        }
    }

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        public void Dispose() => callback();
    }

    private class RecordingWrapperProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> OnInvoke { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            OnInvoke(targetMethod!, args ?? []);
    }
}
