using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts.Realtime;
using Messaging.Domain.Shared.Contracts.Requests.Admin;
using Messaging.Domain.Shared.Contracts.Requests.Attachments;
using Messaging.Domain.Shared.Contracts.Requests.Create;
using Messaging.Domain.Shared.Contracts.Requests.Delete;
using Messaging.Domain.Shared.Contracts.Requests.Edit;
using Messaging.Domain.Shared.Contracts.Requests.Reactions;
using Messaging.Domain.Shared.Contracts.Requests.Realtime;
using Messaging.Domain.Shared.Contracts.Requests.Settings;
using Messaging.Domain.Shared.Contracts.Requests.Templates;
using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Domain.Shared.Contracts.Requests.Update;
using Messaging.Domain.Shared.Contracts.Responses;
using Messaging.Integration.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace Messaging.Integration.Drivers;

public interface IMessagingServiceWrapper : IServiceWrapper
{
    Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request);
    Task<CmdResponse> CreateDirectMessageAsync(
        CreateDirectMessageRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request);
    Task<CmdResponse> CreateVerificationMessageAsync(
        CreateVerificationMessageRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> UpdateMessageDirectAsync(
        UpdateMessageDirectRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<CreateThreadResponse>> CreateThreadAsync(
        CreateThreadRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<CreateThreadResponse>> CreateDirectThreadAsync(
        CreateDirectThreadRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<GetThreadListResponse>> GetThreadListAsync(
        GetThreadListRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<GetThreadResponse>> GetThreadAsync(
        GetThreadRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<GetUnreadCountsResponse>> GetUnreadCountsAsync(
        GetUnreadCountsRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> UpdateThreadAsync(
        UpdateThreadRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> LeaveThreadAsync(
        LeaveThreadRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> MuteThreadAsync(
        MuteThreadRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> ArchiveThreadAsync(
        ArchiveThreadRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> AddThreadMemberAsync(
        AddThreadMemberRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> RemoveThreadMemberAsync(
        RemoveThreadMemberRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> CreateThreadInviteAsync(
        CreateThreadInviteRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> RespondThreadInviteAsync(
        RespondThreadInviteRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> UpdateThreadMemberRoleAsync(
        UpdateThreadMemberRoleRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<CreateThreadMessageResponse>> CreateThreadMessageAsync(
        CreateThreadMessageRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<GetThreadMessagesResponse>> GetThreadMessagesAsync(
        GetThreadMessagesRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<SearchMessagesResponse>> SearchMessagesAsync(
        SearchMessagesRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> DeleteThreadMessageAsync(
        DeleteThreadMessageRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> EditThreadMessageAsync(
        EditThreadMessageRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> PinMessageAsync(
        PinMessageRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> UnpinMessageAsync(
        UnpinMessageRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> SaveMessageAsync(
        SaveMessageRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> UnsaveMessageAsync(
        UnsaveMessageRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> MarkMessagesReadAsync(
        MarkMessagesReadRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> CreateMessageReactionAsync(
        CreateMessageReactionRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> DeleteMessageReactionAsync(
        DeleteMessageReactionRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> CreateMessageFileAsync(
        CreateMessageFileRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<PaginatedResult<MessageFileResponse>>> GetMessageFilesAsync(
        GetMessageFilesRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> DeleteMessageFileAsync(
        DeleteMessageFileRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> ReportMessageAsync(
        ReportMessageRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> BlockCredentialAsync(
        BlockCredentialRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> DeleteCredentialBlockAsync(
        DeleteCredentialBlockRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<MessagingSettingsResponse>> GetMessagingSettingsAsync(
        GetMessagingSettingsRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<MessagingSettingsResponse>> UpdateMessagingSettingsAsync(
        UpdateMessagingSettingsRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<GetMessageTemplatesResponse>> GetMessageTemplatesAsync(
        GetMessageTemplatesRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<MessageTemplateResponse>> GetMessageTemplateAsync(
        GetMessageTemplateRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<MessageTemplateResponse>> CreateMessageTemplateAsync(
        CreateMessageTemplateRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<MessageTemplateResponse>> UpdateMessageTemplateAsync(
        UpdateMessageTemplateRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> DeleteMessageTemplateAsync(
        DeleteMessageTemplateRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<MessageTemplateResponse>> CloneMessageTemplateAsync(
        CloneMessageTemplateRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<RenderMessageTemplateResponse>> RenderMessageTemplateAsync(
        RenderMessageTemplateRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<MessagingAdminUsersResponse>> QueryMessagingAdminUsersAsync(
        QueryMessagingAdminUsersRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<MessagingAdminUserDetailResponse>> GetMessagingAdminUserDetailAsync(
        GetMessagingAdminUserDetailRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<MessagingAdminThreadsResponse>> QueryMessagingAdminThreadsAsync(
        QueryMessagingAdminThreadsRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<MessagingAdminThreadDetailResponse>> GetMessagingAdminThreadDetailAsync(
        GetMessagingAdminThreadDetailRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<MessagingAdminOperationsResponse>> GetMessagingAdminOperationsAsync(
        GetMessagingAdminOperationsRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<MessagingAdminModerationResponse>> GetMessagingAdminModerationAsync(
        GetMessagingAdminModerationRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<GetMessagingModerationRulesResponse>> GetMessagingModerationRulesAsync(
        GetMessagingModerationRulesRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<MessagingModerationRuleResponse>> CreateMessagingModerationRuleAsync(
        CreateMessagingModerationRuleRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<MessagingModerationRuleResponse>> UpdateMessagingModerationRuleAsync(
        UpdateMessagingModerationRuleRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> DeleteMessagingModerationRuleAsync(
        DeleteMessagingModerationRuleRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<MessagingReportWorkflowResponse>> ReviewMessageReportAsync(
        ReviewMessageReportRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> PublishMessagingTypingAsync(
        PublishMessagingTypingRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> PublishMessagingPresenceAsync(
        PublishMessagingPresenceRequest request,
        CancellationToken ct = default);
    Task SubscribeThreadEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default);
    Task SubscribeThreadEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default);
    Task SubscribeThreadEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default);
    Task SubscribeThreadEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default);
    Task SubscribeUserMessagingEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default);
    Task SubscribeUserMessagingEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default);
    Task SubscribeUserMessagingEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default);
    Task SubscribeUserMessagingEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default);
    Task SubscribeTypingAsync(
        Guid tenantId,
        Guid threadId,
        Func<MessagingTypingState, Task> handler,
        CancellationToken ct = default);
    Task SubscribeTypingAsync(
        Guid tenantId,
        Guid threadId,
        Func<MessagingTypingState, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default);
    Task SubscribeTypingAsync(
        Guid tenantId,
        Guid threadId,
        Func<MessagingTypingState, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default);
    Task SubscribePresenceAsync(
        Guid tenantId,
        Func<MessagingPresenceState, Task> handler,
        CancellationToken ct = default);
    Task SubscribePresenceAsync(
        Guid tenantId,
        Func<MessagingPresenceState, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default);
    Task SubscribePresenceAsync(
        Guid tenantId,
        Func<MessagingPresenceState, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default);
    Task PublishTypingAsync(MessagingTypingState state, CancellationToken ct = default);
    Task PublishPresenceAsync(MessagingPresenceState state, CancellationToken ct = default);
}

public sealed record MessagingServiceWrapper(
    IMessageBusWrapper messageBusDriver,
    IConfiguration configuration
) : DriverBase(messageBusDriver, configuration), IMessagingServiceWrapper
{
    public override void Initialize()
    {
        TargetClient = "XFramework.Messaging".ToSha256();
    }

    public async Task<CmdResponse> CreateDirectMessage(CreateDirectMessageRequest request)
    {
        return await SendVoidAsync(request);
    }

    public Task<CmdResponse> CreateDirectMessageAsync(
        CreateDirectMessageRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public async Task<CmdResponse> CreateVerificationMessage(CreateVerificationMessageRequest request)
    {
        return await SendVoidAsync(request);
    }

    public Task<CmdResponse> CreateVerificationMessageAsync(
        CreateVerificationMessageRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> UpdateMessageDirectAsync(
        UpdateMessageDirectRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<QueryResponse<CreateThreadResponse>> CreateThreadAsync(
        CreateThreadRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<CreateThreadRequest, CreateThreadResponse>(request);
    }

    public Task<QueryResponse<CreateThreadResponse>> CreateDirectThreadAsync(
        CreateDirectThreadRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<CreateDirectThreadRequest, CreateThreadResponse>(request);
    }

    public Task<QueryResponse<GetThreadListResponse>> GetThreadListAsync(
        GetThreadListRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetThreadListRequest, GetThreadListResponse>(request);
    }

    public Task<QueryResponse<GetThreadResponse>> GetThreadAsync(
        GetThreadRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetThreadRequest, GetThreadResponse>(request);
    }

    public Task<QueryResponse<GetUnreadCountsResponse>> GetUnreadCountsAsync(
        GetUnreadCountsRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetUnreadCountsRequest, GetUnreadCountsResponse>(request);
    }

    public Task<CmdResponse> UpdateThreadAsync(
        UpdateThreadRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> LeaveThreadAsync(
        LeaveThreadRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> MuteThreadAsync(
        MuteThreadRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> ArchiveThreadAsync(
        ArchiveThreadRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> AddThreadMemberAsync(
        AddThreadMemberRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> RemoveThreadMemberAsync(
        RemoveThreadMemberRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> CreateThreadInviteAsync(
        CreateThreadInviteRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> RespondThreadInviteAsync(
        RespondThreadInviteRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> UpdateThreadMemberRoleAsync(
        UpdateThreadMemberRoleRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<QueryResponse<CreateThreadMessageResponse>> CreateThreadMessageAsync(
        CreateThreadMessageRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<CreateThreadMessageRequest, CreateThreadMessageResponse>(request);
    }

    public Task<QueryResponse<GetThreadMessagesResponse>> GetThreadMessagesAsync(
        GetThreadMessagesRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetThreadMessagesRequest, GetThreadMessagesResponse>(request);
    }

    public Task<QueryResponse<SearchMessagesResponse>> SearchMessagesAsync(
        SearchMessagesRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<SearchMessagesRequest, SearchMessagesResponse>(request);
    }

    public Task<CmdResponse> DeleteThreadMessageAsync(
        DeleteThreadMessageRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> EditThreadMessageAsync(
        EditThreadMessageRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> PinMessageAsync(
        PinMessageRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> UnpinMessageAsync(
        UnpinMessageRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> SaveMessageAsync(
        SaveMessageRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> UnsaveMessageAsync(
        UnsaveMessageRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> MarkMessagesReadAsync(
        MarkMessagesReadRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> CreateMessageReactionAsync(
        CreateMessageReactionRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> DeleteMessageReactionAsync(
        DeleteMessageReactionRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> CreateMessageFileAsync(
        CreateMessageFileRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<QueryResponse<PaginatedResult<MessageFileResponse>>> GetMessageFilesAsync(
        GetMessageFilesRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetMessageFilesRequest, PaginatedResult<MessageFileResponse>>(request);
    }

    public Task<CmdResponse> DeleteMessageFileAsync(
        DeleteMessageFileRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> ReportMessageAsync(
        ReportMessageRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> BlockCredentialAsync(
        BlockCredentialRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> DeleteCredentialBlockAsync(
        DeleteCredentialBlockRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<QueryResponse<MessagingSettingsResponse>> GetMessagingSettingsAsync(
        GetMessagingSettingsRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetMessagingSettingsRequest, MessagingSettingsResponse>(request);
    }

    public Task<CmdResponse<MessagingSettingsResponse>> UpdateMessagingSettingsAsync(
        UpdateMessagingSettingsRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<UpdateMessagingSettingsRequest, MessagingSettingsResponse>(request);
    }

    public Task<QueryResponse<GetMessageTemplatesResponse>> GetMessageTemplatesAsync(
        GetMessageTemplatesRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetMessageTemplatesRequest, GetMessageTemplatesResponse>(request);
    }

    public Task<QueryResponse<MessageTemplateResponse>> GetMessageTemplateAsync(
        GetMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetMessageTemplateRequest, MessageTemplateResponse>(request);
    }

    public Task<CmdResponse<MessageTemplateResponse>> CreateMessageTemplateAsync(
        CreateMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<CreateMessageTemplateRequest, MessageTemplateResponse>(request);
    }

    public Task<CmdResponse<MessageTemplateResponse>> UpdateMessageTemplateAsync(
        UpdateMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<UpdateMessageTemplateRequest, MessageTemplateResponse>(request);
    }

    public Task<CmdResponse> DeleteMessageTemplateAsync(
        DeleteMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse<MessageTemplateResponse>> CloneMessageTemplateAsync(
        CloneMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<CloneMessageTemplateRequest, MessageTemplateResponse>(request);
    }

    public Task<QueryResponse<RenderMessageTemplateResponse>> RenderMessageTemplateAsync(
        RenderMessageTemplateRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<RenderMessageTemplateRequest, RenderMessageTemplateResponse>(request);
    }

    public Task<QueryResponse<MessagingAdminUsersResponse>> QueryMessagingAdminUsersAsync(
        QueryMessagingAdminUsersRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<QueryMessagingAdminUsersRequest, MessagingAdminUsersResponse>(request);
    }

    public Task<QueryResponse<MessagingAdminUserDetailResponse>> GetMessagingAdminUserDetailAsync(
        GetMessagingAdminUserDetailRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetMessagingAdminUserDetailRequest, MessagingAdminUserDetailResponse>(request);
    }

    public Task<QueryResponse<MessagingAdminThreadsResponse>> QueryMessagingAdminThreadsAsync(
        QueryMessagingAdminThreadsRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<QueryMessagingAdminThreadsRequest, MessagingAdminThreadsResponse>(request);
    }

    public Task<QueryResponse<MessagingAdminThreadDetailResponse>> GetMessagingAdminThreadDetailAsync(
        GetMessagingAdminThreadDetailRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetMessagingAdminThreadDetailRequest, MessagingAdminThreadDetailResponse>(request);
    }

    public Task<QueryResponse<MessagingAdminOperationsResponse>> GetMessagingAdminOperationsAsync(
        GetMessagingAdminOperationsRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetMessagingAdminOperationsRequest, MessagingAdminOperationsResponse>(request);
    }

    public Task<QueryResponse<MessagingAdminModerationResponse>> GetMessagingAdminModerationAsync(
        GetMessagingAdminModerationRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetMessagingAdminModerationRequest, MessagingAdminModerationResponse>(request);
    }

    public Task<QueryResponse<GetMessagingModerationRulesResponse>> GetMessagingModerationRulesAsync(
        GetMessagingModerationRulesRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetMessagingModerationRulesRequest, GetMessagingModerationRulesResponse>(request);
    }

    public Task<CmdResponse<MessagingModerationRuleResponse>> CreateMessagingModerationRuleAsync(
        CreateMessagingModerationRuleRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<CreateMessagingModerationRuleRequest, MessagingModerationRuleResponse>(request);
    }

    public Task<CmdResponse<MessagingModerationRuleResponse>> UpdateMessagingModerationRuleAsync(
        UpdateMessagingModerationRuleRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<UpdateMessagingModerationRuleRequest, MessagingModerationRuleResponse>(request);
    }

    public Task<CmdResponse> DeleteMessagingModerationRuleAsync(
        DeleteMessagingModerationRuleRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse<MessagingReportWorkflowResponse>> ReviewMessageReportAsync(
        ReviewMessageReportRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<ReviewMessageReportRequest, MessagingReportWorkflowResponse>(request);
    }

    public Task<CmdResponse> PublishMessagingTypingAsync(
        PublishMessagingTypingRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> PublishMessagingPresenceAsync(
        PublishMessagingPresenceRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task SubscribeThreadEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default)
    {
        var deviceId = $"thread:{threadId:N}";
        return SubscribeThreadEventsForDeviceAsync(tenantId, credentialId, threadId, deviceId, handler, ct);
    }

    public Task SubscribeThreadEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default) =>
        SubscribeThreadEventsForDeviceAsync(
            tenantId,
            credentialId,
            threadId,
            deviceId,
            handler,
            actorAccessToken: null,
            ct);

    public Task SubscribeThreadEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default)
    {
        return SubscribeThreadEventsForDeviceAsync(
            tenantId,
            credentialId,
            threadId,
            deviceId,
            handler,
            _ => ValueTask.FromResult(actorAccessToken),
            ct);
    }

    public Task SubscribeThreadEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default)
    {
        var topic = MessageRealtimeTopics.User(tenantId, credentialId);
        var subscriberId = $"messaging:{tenantId:N}:{credentialId:N}:device:{NormalizeSubscriberSegment(deviceId)}:thread:{threadId:N}";
        return Bus.SubscribeDurableAsync<MessagingRealtimeEvent>(
            topic,
            subscriberId,
            evt => evt.ThreadId == threadId ? handler(evt) : Task.CompletedTask,
            actorAccessTokenProvider,
            ct);
    }

    public Task SubscribeUserMessagingEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default)
    {
        return SubscribeUserMessagingEventsForDeviceAsync(tenantId, credentialId, "user", handler, ct);
    }

    public Task SubscribeUserMessagingEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        CancellationToken ct = default) =>
        SubscribeUserMessagingEventsForDeviceAsync(
            tenantId,
            credentialId,
            deviceId,
            handler,
            actorAccessToken: null,
            ct);

    public Task SubscribeUserMessagingEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default)
    {
        return SubscribeUserMessagingEventsForDeviceAsync(
            tenantId,
            credentialId,
            deviceId,
            handler,
            _ => ValueTask.FromResult(actorAccessToken),
            ct);
    }

    public Task SubscribeUserMessagingEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<MessagingRealtimeEvent, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default)
    {
        var topic = MessageRealtimeTopics.User(tenantId, credentialId);
        var subscriberId = $"messaging:{tenantId:N}:{credentialId:N}:device:{NormalizeSubscriberSegment(deviceId)}:user";
        return Bus.SubscribeDurableAsync(topic, subscriberId, handler, actorAccessTokenProvider, ct);
    }

    public Task SubscribeTypingAsync(
        Guid tenantId,
        Guid threadId,
        Func<MessagingTypingState, Task> handler,
        CancellationToken ct = default) =>
        SubscribeTypingAsync(tenantId, threadId, handler, actorAccessToken: null, ct);

    public Task SubscribeTypingAsync(
        Guid tenantId,
        Guid threadId,
        Func<MessagingTypingState, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default) =>
        SubscribeTypingAsync(
            tenantId,
            threadId,
            handler,
            _ => ValueTask.FromResult(actorAccessToken),
            ct);

    public Task SubscribeTypingAsync(
        Guid tenantId,
        Guid threadId,
        Func<MessagingTypingState, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default) =>
        Bus.SubscribeAsync(
            MessageRealtimeTopics.ThreadTyping(tenantId, threadId),
            handler,
            actorAccessTokenProvider,
            ct);

    public Task SubscribePresenceAsync(
        Guid tenantId,
        Func<MessagingPresenceState, Task> handler,
        CancellationToken ct = default) =>
        SubscribePresenceAsync(tenantId, handler, actorAccessToken: null, ct);

    public Task SubscribePresenceAsync(
        Guid tenantId,
        Func<MessagingPresenceState, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default) =>
        SubscribePresenceAsync(
            tenantId,
            handler,
            _ => ValueTask.FromResult(actorAccessToken),
            ct);

    public Task SubscribePresenceAsync(
        Guid tenantId,
        Func<MessagingPresenceState, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default) =>
        Bus.SubscribeAsync(
            MessageRealtimeTopics.Presence(tenantId),
            handler,
            actorAccessTokenProvider,
            ct);

    public Task PublishTypingAsync(MessagingTypingState state, CancellationToken ct = default) =>
        Bus.PublishAsync(
            MessageRealtimeTopics.TypingEventName,
            MessageRealtimeTopics.ThreadTyping(state.TenantId, state.ThreadId),
            state,
            durable: false);

    public Task PublishPresenceAsync(MessagingPresenceState state, CancellationToken ct = default) =>
        Bus.PublishAsync(
            MessageRealtimeTopics.PresenceEventName,
            MessageRealtimeTopics.Presence(state.TenantId),
            state,
            durable: false);

    private IMessageBusWrapper Bus =>
        MessageBusDriver ?? throw new InvalidOperationException(
            $"{nameof(MessagingServiceWrapper)} cannot use realtime helpers without an {nameof(IMessageBusWrapper)}.");

    private static string NormalizeSubscriberSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Guid.NewGuid().ToString("N");

        var chars = value.Trim()
            .Select(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_')
            .ToArray();

        return new string(chars);
    }
}

public static class MessagingServiceWrapperExtensions
{
    public static void AddMessagingWrapperServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IMessagingChatActorProvider, EmptyMessagingChatActorProvider>();
        services.AddSingleton<IMessagingServiceWrapper, MessagingServiceWrapper>();
        services.AddSingleton<IMessagingChatClient, MessagingChatClient>();
    }
}
