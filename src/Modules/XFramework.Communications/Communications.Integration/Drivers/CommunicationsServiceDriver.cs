using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Admin;
using Communications.Domain.Shared.Contracts.Requests.Attachments;
using Communications.Domain.Shared.Contracts.Requests.Create;
using Communications.Domain.Shared.Contracts.Requests.Delete;
using Communications.Domain.Shared.Contracts.Requests.Edit;
using Communications.Domain.Shared.Contracts.Requests.Reactions;
using Communications.Domain.Shared.Contracts.Requests.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Settings;
using Communications.Domain.Shared.Contracts.Requests.Templates;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Requests.Update;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace Communications.Integration.Drivers;

public interface ICommunicationsServiceWrapper : IServiceWrapper
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
    Task<QueryResponse<CommunicationsSettingsResponse>> GetCommunicationsSettingsAsync(
        GetCommunicationsSettingsRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<CommunicationsSettingsResponse>> UpdateCommunicationsSettingsAsync(
        UpdateCommunicationsSettingsRequest request,
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
    Task<QueryResponse<CommunicationsAdminUsersResponse>> QueryCommunicationsAdminUsersAsync(
        QueryCommunicationsAdminUsersRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<CommunicationsAdminUserDetailResponse>> GetCommunicationsAdminUserDetailAsync(
        GetCommunicationsAdminUserDetailRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<CommunicationsAdminThreadsResponse>> QueryCommunicationsAdminThreadsAsync(
        QueryCommunicationsAdminThreadsRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<CommunicationsAdminThreadDetailResponse>> GetCommunicationsAdminThreadDetailAsync(
        GetCommunicationsAdminThreadDetailRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<CommunicationsAdminOperationsResponse>> GetCommunicationsAdminOperationsAsync(
        GetCommunicationsAdminOperationsRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<CommunicationsAdminModerationResponse>> GetCommunicationsAdminModerationAsync(
        GetCommunicationsAdminModerationRequest request,
        CancellationToken ct = default);
    Task<QueryResponse<GetCommunicationsModerationRulesResponse>> GetCommunicationsModerationRulesAsync(
        GetCommunicationsModerationRulesRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<CommunicationsModerationRuleResponse>> CreateCommunicationsModerationRuleAsync(
        CreateCommunicationsModerationRuleRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<CommunicationsModerationRuleResponse>> UpdateCommunicationsModerationRuleAsync(
        UpdateCommunicationsModerationRuleRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> DeleteCommunicationsModerationRuleAsync(
        DeleteCommunicationsModerationRuleRequest request,
        CancellationToken ct = default);
    Task<CmdResponse<CommunicationsReportWorkflowResponse>> ReviewMessageReportAsync(
        ReviewMessageReportRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> PublishCommunicationsTypingAsync(
        PublishCommunicationsTypingRequest request,
        CancellationToken ct = default);
    Task<CmdResponse> PublishCommunicationsPresenceAsync(
        PublishCommunicationsPresenceRequest request,
        CancellationToken ct = default);
    Task SubscribeThreadEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        CancellationToken ct = default);
    Task SubscribeThreadEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        string deviceId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        CancellationToken ct = default);
    Task SubscribeThreadEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        string deviceId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default);
    Task SubscribeThreadEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        string deviceId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default);
    Task SubscribeUserCommunicationsEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        CancellationToken ct = default);
    Task SubscribeUserCommunicationsEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        CancellationToken ct = default);
    Task SubscribeUserCommunicationsEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default);
    Task SubscribeUserCommunicationsEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default);
    Task SubscribeTypingAsync(
        Guid tenantId,
        Guid threadId,
        Func<CommunicationsTypingState, Task> handler,
        CancellationToken ct = default);
    Task SubscribeTypingAsync(
        Guid tenantId,
        Guid threadId,
        Func<CommunicationsTypingState, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default);
    Task SubscribeTypingAsync(
        Guid tenantId,
        Guid threadId,
        Func<CommunicationsTypingState, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default);
    Task SubscribePresenceAsync(
        Guid tenantId,
        Func<CommunicationsPresenceState, Task> handler,
        CancellationToken ct = default);
    Task SubscribePresenceAsync(
        Guid tenantId,
        Func<CommunicationsPresenceState, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default);
    Task SubscribePresenceAsync(
        Guid tenantId,
        Func<CommunicationsPresenceState, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default);
    Task PublishTypingAsync(CommunicationsTypingState state, CancellationToken ct = default);
    Task PublishPresenceAsync(CommunicationsPresenceState state, CancellationToken ct = default);
}

public sealed record CommunicationsServiceWrapper(
    IMessageBusWrapper messageBusDriver,
    IConfiguration configuration
) : DriverBase(messageBusDriver, configuration), ICommunicationsServiceWrapper
{
    public override void Initialize()
    {
        TargetClient = "XFramework.Communications".ToSha256();
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

    public Task<QueryResponse<CommunicationsSettingsResponse>> GetCommunicationsSettingsAsync(
        GetCommunicationsSettingsRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetCommunicationsSettingsRequest, CommunicationsSettingsResponse>(request);
    }

    public Task<CmdResponse<CommunicationsSettingsResponse>> UpdateCommunicationsSettingsAsync(
        UpdateCommunicationsSettingsRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<UpdateCommunicationsSettingsRequest, CommunicationsSettingsResponse>(request);
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

    public Task<QueryResponse<CommunicationsAdminUsersResponse>> QueryCommunicationsAdminUsersAsync(
        QueryCommunicationsAdminUsersRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<QueryCommunicationsAdminUsersRequest, CommunicationsAdminUsersResponse>(request);
    }

    public Task<QueryResponse<CommunicationsAdminUserDetailResponse>> GetCommunicationsAdminUserDetailAsync(
        GetCommunicationsAdminUserDetailRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetCommunicationsAdminUserDetailRequest, CommunicationsAdminUserDetailResponse>(request);
    }

    public Task<QueryResponse<CommunicationsAdminThreadsResponse>> QueryCommunicationsAdminThreadsAsync(
        QueryCommunicationsAdminThreadsRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<QueryCommunicationsAdminThreadsRequest, CommunicationsAdminThreadsResponse>(request);
    }

    public Task<QueryResponse<CommunicationsAdminThreadDetailResponse>> GetCommunicationsAdminThreadDetailAsync(
        GetCommunicationsAdminThreadDetailRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetCommunicationsAdminThreadDetailRequest, CommunicationsAdminThreadDetailResponse>(request);
    }

    public Task<QueryResponse<CommunicationsAdminOperationsResponse>> GetCommunicationsAdminOperationsAsync(
        GetCommunicationsAdminOperationsRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetCommunicationsAdminOperationsRequest, CommunicationsAdminOperationsResponse>(request);
    }

    public Task<QueryResponse<CommunicationsAdminModerationResponse>> GetCommunicationsAdminModerationAsync(
        GetCommunicationsAdminModerationRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetCommunicationsAdminModerationRequest, CommunicationsAdminModerationResponse>(request);
    }

    public Task<QueryResponse<GetCommunicationsModerationRulesResponse>> GetCommunicationsModerationRulesAsync(
        GetCommunicationsModerationRulesRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendAsync<GetCommunicationsModerationRulesRequest, GetCommunicationsModerationRulesResponse>(request);
    }

    public Task<CmdResponse<CommunicationsModerationRuleResponse>> CreateCommunicationsModerationRuleAsync(
        CreateCommunicationsModerationRuleRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<CreateCommunicationsModerationRuleRequest, CommunicationsModerationRuleResponse>(request);
    }

    public Task<CmdResponse<CommunicationsModerationRuleResponse>> UpdateCommunicationsModerationRuleAsync(
        UpdateCommunicationsModerationRuleRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<UpdateCommunicationsModerationRuleRequest, CommunicationsModerationRuleResponse>(request);
    }

    public Task<CmdResponse> DeleteCommunicationsModerationRuleAsync(
        DeleteCommunicationsModerationRuleRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse<CommunicationsReportWorkflowResponse>> ReviewMessageReportAsync(
        ReviewMessageReportRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync<ReviewMessageReportRequest, CommunicationsReportWorkflowResponse>(request);
    }

    public Task<CmdResponse> PublishCommunicationsTypingAsync(
        PublishCommunicationsTypingRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task<CmdResponse> PublishCommunicationsPresenceAsync(
        PublishCommunicationsPresenceRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return SendVoidAsync(request);
    }

    public Task SubscribeThreadEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Guid threadId,
        Func<CommunicationsRealtimeEvent, Task> handler,
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
        Func<CommunicationsRealtimeEvent, Task> handler,
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
        Func<CommunicationsRealtimeEvent, Task> handler,
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
        Func<CommunicationsRealtimeEvent, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default)
    {
        var topic = MessageRealtimeTopics.User(tenantId, credentialId);
        var subscriberId = $"communications:{tenantId:N}:{credentialId:N}:device:{NormalizeSubscriberSegment(deviceId)}:thread:{threadId:N}";
        return Bus.SubscribeDurableAsync<CommunicationsRealtimeEvent>(
            topic,
            subscriberId,
            evt => evt.ThreadId == threadId ? handler(evt) : Task.CompletedTask,
            actorAccessTokenProvider,
            ct);
    }

    public Task SubscribeUserCommunicationsEventsAsync(
        Guid tenantId,
        Guid credentialId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        CancellationToken ct = default)
    {
        return SubscribeUserCommunicationsEventsForDeviceAsync(tenantId, credentialId, "user", handler, ct);
    }

    public Task SubscribeUserCommunicationsEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        CancellationToken ct = default) =>
        SubscribeUserCommunicationsEventsForDeviceAsync(
            tenantId,
            credentialId,
            deviceId,
            handler,
            actorAccessToken: null,
            ct);

    public Task SubscribeUserCommunicationsEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default)
    {
        return SubscribeUserCommunicationsEventsForDeviceAsync(
            tenantId,
            credentialId,
            deviceId,
            handler,
            _ => ValueTask.FromResult(actorAccessToken),
            ct);
    }

    public Task SubscribeUserCommunicationsEventsForDeviceAsync(
        Guid tenantId,
        Guid credentialId,
        string deviceId,
        Func<CommunicationsRealtimeEvent, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default)
    {
        var topic = MessageRealtimeTopics.User(tenantId, credentialId);
        var subscriberId = $"communications:{tenantId:N}:{credentialId:N}:device:{NormalizeSubscriberSegment(deviceId)}:user";
        return Bus.SubscribeDurableAsync(topic, subscriberId, handler, actorAccessTokenProvider, ct);
    }

    public Task SubscribeTypingAsync(
        Guid tenantId,
        Guid threadId,
        Func<CommunicationsTypingState, Task> handler,
        CancellationToken ct = default) =>
        SubscribeTypingAsync(tenantId, threadId, handler, actorAccessToken: null, ct);

    public Task SubscribeTypingAsync(
        Guid tenantId,
        Guid threadId,
        Func<CommunicationsTypingState, Task> handler,
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
        Func<CommunicationsTypingState, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default) =>
        Bus.SubscribeAsync(
            MessageRealtimeTopics.ThreadTyping(tenantId, threadId),
            handler,
            actorAccessTokenProvider,
            ct);

    public Task SubscribePresenceAsync(
        Guid tenantId,
        Func<CommunicationsPresenceState, Task> handler,
        CancellationToken ct = default) =>
        SubscribePresenceAsync(tenantId, handler, actorAccessToken: null, ct);

    public Task SubscribePresenceAsync(
        Guid tenantId,
        Func<CommunicationsPresenceState, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default) =>
        SubscribePresenceAsync(
            tenantId,
            handler,
            _ => ValueTask.FromResult(actorAccessToken),
            ct);

    public Task SubscribePresenceAsync(
        Guid tenantId,
        Func<CommunicationsPresenceState, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default) =>
        Bus.SubscribeAsync(
            MessageRealtimeTopics.Presence(tenantId),
            handler,
            actorAccessTokenProvider,
            ct);

    public Task PublishTypingAsync(CommunicationsTypingState state, CancellationToken ct = default) =>
        Bus.PublishAsync(
            MessageRealtimeTopics.TypingEventName,
            MessageRealtimeTopics.ThreadTyping(state.TenantId, state.ThreadId),
            state,
            durable: false);

    public Task PublishPresenceAsync(CommunicationsPresenceState state, CancellationToken ct = default) =>
        Bus.PublishAsync(
            MessageRealtimeTopics.PresenceEventName,
            MessageRealtimeTopics.Presence(state.TenantId),
            state,
            durable: false);

    private IMessageBusWrapper Bus =>
        MessageBusDriver ?? throw new InvalidOperationException(
            $"{nameof(CommunicationsServiceWrapper)} cannot use realtime helpers without an {nameof(IMessageBusWrapper)}.");

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

public static class CommunicationsServiceWrapperExtensions
{
    public static void AddCommunicationsWrapperServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ICommunicationsChatActorProvider, EmptyCommunicationsChatActorProvider>();
        services.AddScoped<ICommunicationsServiceWrapper, CommunicationsServiceWrapper>();
        services.AddSingleton<ICommunicationsChatClient, CommunicationsChatClient>();
    }
}
