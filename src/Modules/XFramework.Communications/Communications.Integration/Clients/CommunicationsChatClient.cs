using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Attachments;
using Communications.Domain.Shared.Contracts.Requests.Create;
using Communications.Domain.Shared.Contracts.Requests.Delete;
using Communications.Domain.Shared.Contracts.Requests.Edit;
using Communications.Domain.Shared.Contracts.Requests.Reactions;
using Communications.Domain.Shared.Contracts.Requests.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Drivers;
using Microsoft.Extensions.Configuration;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Security;

namespace Communications.Integration.Clients;

public sealed record CommunicationsChatActor(
    Guid TenantId,
    Guid CredentialId,
    string? DeviceId = null,
    string? AccessToken = null);

public interface ICommunicationsChatActorProvider
{
    ValueTask<CommunicationsChatActor?> GetCurrentActorAsync(CancellationToken ct = default);
}

internal sealed class EmptyCommunicationsChatActorProvider : ICommunicationsChatActorProvider
{
    public ValueTask<CommunicationsChatActor?> GetCurrentActorAsync(CancellationToken ct = default) =>
        ValueTask.FromResult<CommunicationsChatActor?>(null);
}

public interface ICommunicationsChatClient
{
    ICommunicationsChatSession For(Guid tenantId, Guid credentialId, string? deviceId = null);
    ValueTask<ICommunicationsChatSession> ForCurrentActorAsync(string? deviceId = null, CancellationToken ct = default);
}

public interface ICommunicationsChatSession
{
    Guid TenantId { get; }
    Guid CredentialId { get; }
    string DeviceId { get; }

    Task<QueryResponse<CreateThreadResponse>> CreateThreadAsync(
        CreateThreadRequest request,
        CancellationToken ct = default);

    Task<QueryResponse<CreateThreadResponse>> CreateDirectThreadAsync(
        Guid otherCredentialId,
        Guid? typeId = null,
        string? name = null,
        CancellationToken ct = default);

    Task<QueryResponse<GetThreadListResponse>> GetThreadsAsync(
        int pageIndex = 0,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<QueryResponse<GetThreadResponse>> GetThreadAsync(
        Guid threadId,
        CancellationToken ct = default);

    Task<QueryResponse<GetUnreadCountsResponse>> GetUnreadCountsAsync(CancellationToken ct = default);
    Task<CmdResponse> UpdateThreadAsync(UpdateThreadRequest request, CancellationToken ct = default);
    Task<CmdResponse> LeaveThreadAsync(Guid threadId, CancellationToken ct = default);
    Task<CmdResponse> MuteThreadAsync(Guid threadId, bool isMuted, CancellationToken ct = default);
    Task<CmdResponse> ArchiveThreadAsync(Guid threadId, bool isArchived, CancellationToken ct = default);
    Task<CmdResponse> AddThreadMemberAsync(Guid threadId, Guid credentialId, CancellationToken ct = default);
    Task<CmdResponse> RemoveThreadMemberAsync(Guid threadId, Guid credentialId, CancellationToken ct = default);
    Task<CmdResponse> InviteMemberAsync(Guid threadId, Guid credentialId, CancellationToken ct = default);
    Task<CmdResponse> RespondInviteAsync(Guid threadId, Guid inviteId, bool accept, CancellationToken ct = default);
    Task<CmdResponse> UpdateMemberRoleAsync(Guid threadId, Guid memberId, string role, CancellationToken ct = default);

    Task<QueryResponse<CreateThreadMessageResponse>> SendMessageAsync(
        CreateThreadMessageRequest request,
        CancellationToken ct = default);

    Task<QueryResponse<CreateThreadMessageResponse>> SendMessageAsync(
        Guid threadId,
        string text,
        Guid? parentMessageId = null,
        IReadOnlyCollection<Guid>? mentionedCredentialIds = null,
        CancellationToken ct = default);

    Task<QueryResponse<GetThreadMessagesResponse>> GetMessagesAsync(
        Guid threadId,
        int pageIndex = 0,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<QueryResponse<SearchMessagesResponse>> SearchMessagesAsync(
        string query,
        Guid? threadId = null,
        int pageIndex = 0,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<CmdResponse> EditMessageAsync(Guid threadId, Guid messageId, string text, CancellationToken ct = default);
    Task<CmdResponse> DeleteMessageAsync(Guid threadId, Guid messageId, CancellationToken ct = default);
    Task<CmdResponse> MarkReadAsync(Guid threadId, IReadOnlyCollection<Guid> messageIds, CancellationToken ct = default);
    Task<CmdResponse> ReactAsync(Guid threadId, Guid messageId, Guid reactionTypeId, CancellationToken ct = default);
    Task<CmdResponse> DeleteReactionAsync(Guid threadId, Guid messageId, Guid reactionId, CancellationToken ct = default);
    Task<CmdResponse> AttachFileAsync(Guid threadId, Guid messageId, Guid storageFileId, CancellationToken ct = default);
    Task<QueryResponse<PaginatedResult<MessageFileResponse>>> GetFilesAsync(
        Guid threadId,
        Guid messageId,
        int pageIndex = 0,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<CmdResponse> DetachFileAsync(Guid threadId, Guid messageId, Guid fileId, CancellationToken ct = default);
    Task<CmdResponse> PinMessageAsync(Guid threadId, Guid messageId, CancellationToken ct = default);
    Task<CmdResponse> UnpinMessageAsync(Guid threadId, Guid messageId, CancellationToken ct = default);
    Task<CmdResponse> SaveMessageAsync(Guid threadId, Guid messageId, CancellationToken ct = default);
    Task<CmdResponse> UnsaveMessageAsync(Guid threadId, Guid messageId, CancellationToken ct = default);
    Task<CmdResponse> ReportMessageAsync(Guid threadId, Guid messageId, string reason, string? details = null, CancellationToken ct = default);
    Task<CmdResponse> BlockCredentialAsync(Guid credentialId, CancellationToken ct = default);
    Task<CmdResponse> UnblockCredentialAsync(Guid credentialId, CancellationToken ct = default);

    Task SubscribeThreadEventsAsync(
        Guid threadId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        CancellationToken ct = default);

    Task SubscribeUserEventsAsync(
        Func<CommunicationsRealtimeEvent, Task> handler,
        CancellationToken ct = default);

    Task SubscribeTypingAsync(
        Guid threadId,
        Func<CommunicationsTypingState, Task> handler,
        CancellationToken ct = default);

    Task SubscribePresenceAsync(
        Func<CommunicationsPresenceState, Task> handler,
        CancellationToken ct = default);

    Task PublishTypingAsync(Guid threadId, bool isTyping, CancellationToken ct = default);
    Task PublishPresenceAsync(bool isOnline, CancellationToken ct = default);
}

public sealed class CommunicationsChatClient(
    ICommunicationsServiceWrapper wrapper,
    IConfiguration configuration,
    ICommunicationsChatActorProvider actorProvider,
    IActorAccessTokenScope actorAccessTokenScope) : ICommunicationsChatClient
{
    public ICommunicationsChatSession For(Guid tenantId, Guid credentialId, string? deviceId = null)
    {
        if (!ExplicitActorSelectionAllowed())
        {
            throw new InvalidOperationException(
                "Explicit Communications chat actor selection is disabled. Configure an ICommunicationsChatActorProvider and call ForCurrentActorAsync, or enable CommunicationsChatClient:AllowExplicitActorSelection only in trusted backend services.");
        }

        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

        if (credentialId == Guid.Empty)
            throw new ArgumentException("Credential ID is required.", nameof(credentialId));

        return new CommunicationsChatSession(
            wrapper,
            actorAccessTokenScope,
            tenantId,
            credentialId,
            NormalizeDeviceId(deviceId));
    }

    public async ValueTask<ICommunicationsChatSession> ForCurrentActorAsync(string? deviceId = null, CancellationToken ct = default)
    {
        var actor = await actorProvider.GetCurrentActorAsync(ct);
        if (actor is null || actor.TenantId == Guid.Empty || actor.CredentialId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Communications chat actor context could not be resolved for the current user.");
        }

        return new CommunicationsChatSession(
            wrapper,
            actorAccessTokenScope,
            actor.TenantId,
            actor.CredentialId,
            NormalizeDeviceId(deviceId ?? actor.DeviceId),
            actor.AccessToken,
            async tokenCt =>
            {
                var refreshedActor = await actorProvider.GetCurrentActorAsync(tokenCt);
                return refreshedActor is not null &&
                       refreshedActor.TenantId == actor.TenantId &&
                       refreshedActor.CredentialId == actor.CredentialId
                    ? refreshedActor.AccessToken
                    : actor.AccessToken;
            });
    }

    private static string NormalizeDeviceId(string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId)
            ? Guid.NewGuid().ToString("N")
            : deviceId.Trim();

    private bool ExplicitActorSelectionAllowed() =>
        IsEnabled(configuration["CommunicationsChatClient:AllowExplicitActorSelection"]) ||
        IsEnabled(configuration["Communications:ChatClient:AllowExplicitActorSelection"]);

    private static bool IsEnabled(string? value) =>
        bool.TryParse(value, out var enabled) && enabled;
}

internal sealed class CommunicationsChatSession(
    ICommunicationsServiceWrapper wrapper,
    IActorAccessTokenScope actorAccessTokenScope,
    Guid tenantId,
    Guid credentialId,
    string deviceId,
    string? accessToken = null,
    Func<CancellationToken, ValueTask<string?>>? accessTokenProvider = null) : ICommunicationsChatSession
{
    public Guid TenantId { get; } = tenantId;
    public Guid CredentialId { get; } = credentialId;
    public string DeviceId { get; } = deviceId;

    public Task<QueryResponse<CreateThreadResponse>> CreateThreadAsync(
        CreateThreadRequest request,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.CreateThreadAsync(Prepare(request), callCt), ct);

    public Task<QueryResponse<CreateThreadResponse>> CreateDirectThreadAsync(
        Guid otherCredentialId,
        Guid? typeId = null,
        string? name = null,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.CreateDirectThreadAsync(Prepare(new CreateDirectThreadRequest
        {
            OtherCredentialId = otherCredentialId,
            TypeId = typeId,
            Name = name
        }), callCt), ct);

    public Task<QueryResponse<GetThreadListResponse>> GetThreadsAsync(
        int pageIndex = 0,
        int pageSize = 20,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.GetThreadListAsync(Prepare(new GetThreadListRequest
        {
            PageIndex = pageIndex,
            PageSize = pageSize
        }), callCt), ct);

    public Task<QueryResponse<GetThreadResponse>> GetThreadAsync(
        Guid threadId,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.GetThreadAsync(Prepare(new GetThreadRequest { Id = threadId }), callCt), ct);

    public Task<QueryResponse<GetUnreadCountsResponse>> GetUnreadCountsAsync(CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.GetUnreadCountsAsync(Prepare(new GetUnreadCountsRequest()), callCt), ct);

    public Task<CmdResponse> UpdateThreadAsync(UpdateThreadRequest request, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.UpdateThreadAsync(Prepare(request), callCt), ct);

    public Task<CmdResponse> LeaveThreadAsync(Guid threadId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.LeaveThreadAsync(Prepare(new LeaveThreadRequest { ThreadId = threadId }), callCt), ct);

    public Task<CmdResponse> MuteThreadAsync(Guid threadId, bool isMuted, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.MuteThreadAsync(Prepare(new MuteThreadRequest { ThreadId = threadId, IsMuted = isMuted }), callCt), ct);

    public Task<CmdResponse> ArchiveThreadAsync(Guid threadId, bool isArchived, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.ArchiveThreadAsync(Prepare(new ArchiveThreadRequest { ThreadId = threadId, IsArchived = isArchived }), callCt), ct);

    public Task<CmdResponse> AddThreadMemberAsync(Guid threadId, Guid credentialId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.AddThreadMemberAsync(Prepare(new AddThreadMemberRequest { ThreadId = threadId, CredentialId = credentialId }), callCt), ct);

    public Task<CmdResponse> RemoveThreadMemberAsync(Guid threadId, Guid credentialId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.RemoveThreadMemberAsync(Prepare(new RemoveThreadMemberRequest { ThreadId = threadId, CredentialId = credentialId }), callCt), ct);

    public Task<CmdResponse> InviteMemberAsync(Guid threadId, Guid credentialId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.CreateThreadInviteAsync(Prepare(new CreateThreadInviteRequest { ThreadId = threadId, CredentialId = credentialId }), callCt), ct);

    public Task<CmdResponse> RespondInviteAsync(Guid threadId, Guid inviteId, bool accept, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.RespondThreadInviteAsync(Prepare(new RespondThreadInviteRequest
        {
            ThreadId = threadId,
            InviteId = inviteId,
            Accept = accept
        }), callCt), ct);

    public Task<CmdResponse> UpdateMemberRoleAsync(Guid threadId, Guid memberId, string role, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.UpdateThreadMemberRoleAsync(Prepare(new UpdateThreadMemberRoleRequest
        {
            ThreadId = threadId,
            MemberId = memberId,
            Role = role
        }), callCt), ct);

    public Task<QueryResponse<CreateThreadMessageResponse>> SendMessageAsync(
        CreateThreadMessageRequest request,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.CreateThreadMessageAsync(Prepare(request), callCt), ct);

    public Task<QueryResponse<CreateThreadMessageResponse>> SendMessageAsync(
        Guid threadId,
        string text,
        Guid? parentMessageId = null,
        IReadOnlyCollection<Guid>? mentionedCredentialIds = null,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.CreateThreadMessageAsync(Prepare(new CreateThreadMessageRequest
        {
            ThreadId = threadId,
            Text = text,
            ParentMessageId = parentMessageId,
            MentionedCredentialIds = mentionedCredentialIds?.ToList() ?? []
        }), callCt), ct);

    public Task<QueryResponse<GetThreadMessagesResponse>> GetMessagesAsync(
        Guid threadId,
        int pageIndex = 0,
        int pageSize = 20,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.GetThreadMessagesAsync(Prepare(new GetThreadMessagesRequest
        {
            ThreadId = threadId,
            PageIndex = pageIndex,
            PageSize = pageSize
        }), callCt), ct);

    public Task<QueryResponse<SearchMessagesResponse>> SearchMessagesAsync(
        string query,
        Guid? threadId = null,
        int pageIndex = 0,
        int pageSize = 20,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.SearchMessagesAsync(Prepare(new SearchMessagesRequest
        {
            Query = query,
            ThreadId = threadId,
            PageIndex = pageIndex,
            PageSize = pageSize
        }), callCt), ct);

    public Task<CmdResponse> EditMessageAsync(Guid threadId, Guid messageId, string text, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.EditThreadMessageAsync(Prepare(new EditThreadMessageRequest
        {
            ThreadId = threadId,
            MessageId = messageId,
            Text = text
        }), callCt), ct);

    public Task<CmdResponse> DeleteMessageAsync(Guid threadId, Guid messageId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.DeleteThreadMessageAsync(Prepare(new DeleteThreadMessageRequest
        {
            ThreadId = threadId,
            MessageId = messageId
        }), callCt), ct);

    public Task<CmdResponse> MarkReadAsync(Guid threadId, IReadOnlyCollection<Guid> messageIds, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.MarkMessagesReadAsync(Prepare(new MarkMessagesReadRequest
        {
            ThreadId = threadId,
            MessageIds = messageIds.ToList()
        }), callCt), ct);

    public Task<CmdResponse> ReactAsync(Guid threadId, Guid messageId, Guid reactionTypeId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.CreateMessageReactionAsync(Prepare(new CreateMessageReactionRequest
        {
            ThreadId = threadId,
            MessageId = messageId,
            TypeId = reactionTypeId
        }), callCt), ct);

    public Task<CmdResponse> DeleteReactionAsync(Guid threadId, Guid messageId, Guid reactionId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.DeleteMessageReactionAsync(Prepare(new DeleteMessageReactionRequest
        {
            ThreadId = threadId,
            MessageId = messageId,
            ReactionId = reactionId
        }), callCt), ct);

    public Task<CmdResponse> AttachFileAsync(Guid threadId, Guid messageId, Guid storageFileId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.CreateMessageFileAsync(Prepare(new CreateMessageFileRequest
        {
            ThreadId = threadId,
            MessageId = messageId,
            StorageFileId = storageFileId
        }), callCt), ct);

    public Task<QueryResponse<PaginatedResult<MessageFileResponse>>> GetFilesAsync(
        Guid threadId,
        Guid messageId,
        int pageIndex = 0,
        int pageSize = 20,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.GetMessageFilesAsync(Prepare(new GetMessageFilesRequest
        {
            ThreadId = threadId,
            MessageId = messageId,
            PageIndex = pageIndex,
            PageSize = pageSize
        }), callCt), ct);

    public Task<CmdResponse> DetachFileAsync(Guid threadId, Guid messageId, Guid fileId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.DeleteMessageFileAsync(Prepare(new DeleteMessageFileRequest
        {
            ThreadId = threadId,
            MessageId = messageId,
            FileId = fileId
        }), callCt), ct);

    public Task<CmdResponse> PinMessageAsync(Guid threadId, Guid messageId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.PinMessageAsync(Prepare(new PinMessageRequest { ThreadId = threadId, MessageId = messageId }), callCt), ct);

    public Task<CmdResponse> UnpinMessageAsync(Guid threadId, Guid messageId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.UnpinMessageAsync(Prepare(new UnpinMessageRequest { ThreadId = threadId, MessageId = messageId }), callCt), ct);

    public Task<CmdResponse> SaveMessageAsync(Guid threadId, Guid messageId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.SaveMessageAsync(Prepare(new SaveMessageRequest { ThreadId = threadId, MessageId = messageId }), callCt), ct);

    public Task<CmdResponse> UnsaveMessageAsync(Guid threadId, Guid messageId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.UnsaveMessageAsync(Prepare(new UnsaveMessageRequest { ThreadId = threadId, MessageId = messageId }), callCt), ct);

    public Task<CmdResponse> ReportMessageAsync(Guid threadId, Guid messageId, string reason, string? details = null, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.ReportMessageAsync(Prepare(new ReportMessageRequest
        {
            ThreadId = threadId,
            MessageId = messageId,
            Reason = reason,
            Details = details
        }), callCt), ct);

    public Task<CmdResponse> BlockCredentialAsync(Guid credentialId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.BlockCredentialAsync(Prepare(new BlockCredentialRequest { CredentialId = credentialId }), callCt), ct);

    public Task<CmdResponse> UnblockCredentialAsync(Guid credentialId, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.DeleteCredentialBlockAsync(Prepare(new DeleteCredentialBlockRequest { CredentialId = credentialId }), callCt), ct);

    public Task SubscribeThreadEventsAsync(
        Guid threadId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.SubscribeThreadEventsForDeviceAsync(TenantId, CredentialId, threadId, DeviceId, handler, GetAccessTokenAsync, callCt), ct);

    public Task SubscribeUserEventsAsync(
        Func<CommunicationsRealtimeEvent, Task> handler,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.SubscribeUserCommunicationsEventsForDeviceAsync(TenantId, CredentialId, DeviceId, handler, GetAccessTokenAsync, callCt), ct);

    public Task SubscribeTypingAsync(
        Guid threadId,
        Func<CommunicationsTypingState, Task> handler,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.SubscribeTypingAsync(TenantId, threadId, handler, GetAccessTokenAsync, callCt), ct);

    public Task SubscribePresenceAsync(
        Func<CommunicationsPresenceState, Task> handler,
        CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.SubscribePresenceAsync(TenantId, handler, GetAccessTokenAsync, callCt), ct);

    public Task PublishTypingAsync(Guid threadId, bool isTyping, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.PublishCommunicationsTypingAsync(Prepare(new PublishCommunicationsTypingRequest
        {
            ThreadId = threadId,
            IsTyping = isTyping
        }), callCt), ct);

    public Task PublishPresenceAsync(bool isOnline, CancellationToken ct = default) =>
        InvokeAsync(callCt => wrapper.PublishCommunicationsPresenceAsync(Prepare(new PublishCommunicationsPresenceRequest
        {
            IsOnline = isOnline
        }), callCt), ct);

    private TRequest Prepare<TRequest>(TRequest request)
        where TRequest : RequestBase
    {
        request.Metadata ??= new RequestMetadata();
        request.Metadata.RequestedTenantId = TenantId;

        StampCredentialFields(request);
        return request;
    }

    private ValueTask<string?> GetAccessTokenAsync(CancellationToken ct) =>
        accessTokenProvider is not null
            ? accessTokenProvider(ct)
            : ValueTask.FromResult(accessToken);

    private async Task<T> InvokeAsync<T>(
        Func<CancellationToken, Task<T>> invocation,
        CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        using var actorScope = string.IsNullOrWhiteSpace(token)
            ? null
            : actorAccessTokenScope.Push(token);
        return await invocation(ct);
    }

    private async Task InvokeAsync(
        Func<CancellationToken, Task> invocation,
        CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        using var actorScope = string.IsNullOrWhiteSpace(token)
            ? null
            : actorAccessTokenScope.Push(token);
        await invocation(ct);
    }

    private void StampCredentialFields(RequestBase request)
    {
        switch (request)
        {
            case GetThreadListRequest getThreadList when getThreadList.CredentialId == Guid.Empty:
                getThreadList.CredentialId = CredentialId;
                break;
            case GetThreadRequest getThread when getThread.RequesterCredentialId == Guid.Empty:
                getThread.RequesterCredentialId = CredentialId;
                break;
            case UpdateThreadRequest updateThread when updateThread.RequesterCredentialId == Guid.Empty:
                updateThread.RequesterCredentialId = CredentialId;
                break;
            case CreateThreadMessageRequest createMessage when createMessage.SenderCredentialId == Guid.Empty:
                createMessage.SenderCredentialId = CredentialId;
                break;
            case GetThreadMessagesRequest getMessages when getMessages.RequesterCredentialId == Guid.Empty:
                getMessages.RequesterCredentialId = CredentialId;
                break;
            case DeleteThreadMessageRequest deleteMessage when deleteMessage.RequesterCredentialId == Guid.Empty:
                deleteMessage.RequesterCredentialId = CredentialId;
                break;
            case EditThreadMessageRequest editMessage when editMessage.RequesterCredentialId == Guid.Empty:
                editMessage.RequesterCredentialId = CredentialId;
                break;
            case MarkMessagesReadRequest markRead when markRead.RequesterCredentialId == Guid.Empty:
                markRead.RequesterCredentialId = CredentialId;
                break;
            case CreateMessageReactionRequest createReaction when createReaction.RequesterCredentialId == Guid.Empty:
                createReaction.RequesterCredentialId = CredentialId;
                break;
            case DeleteMessageReactionRequest deleteReaction when deleteReaction.RequesterCredentialId == Guid.Empty:
                deleteReaction.RequesterCredentialId = CredentialId;
                break;
            case CreateMessageFileRequest createFile when createFile.RequesterCredentialId == Guid.Empty:
                createFile.RequesterCredentialId = CredentialId;
                break;
            case GetMessageFilesRequest getFiles when getFiles.RequesterCredentialId == Guid.Empty:
                getFiles.RequesterCredentialId = CredentialId;
                break;
            case DeleteMessageFileRequest deleteFile when deleteFile.RequesterCredentialId == Guid.Empty:
                deleteFile.RequesterCredentialId = CredentialId;
                break;
        }
    }
}
