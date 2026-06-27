using Communications.Domain.Shared.Contracts.Requests.Attachments;
using Communications.Domain.Shared.Contracts.Requests.Delete;
using Communications.Domain.Shared.Contracts.Requests.Edit;
using Communications.Domain.Shared.Contracts.Requests.Reactions;
using Communications.Domain.Shared.Contracts.Requests.Realtime;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Responses;

namespace Communications.Api.Services;

public interface IThreadService
{
    // Round 1: Thread CRUD + Members + Messages
    Task<Result<CreateThreadResponse>> CreateThreadAsync(CreateThreadRequest request, CancellationToken ct = default);
    Task<Result<CreateThreadResponse>> CreateDirectThreadAsync(CreateDirectThreadRequest request, CancellationToken ct = default);
    Task<Result<GetThreadListResponse>> GetThreadListAsync(GetThreadListRequest request, CancellationToken ct = default);
    Task<Result<GetThreadResponse>> GetThreadAsync(GetThreadRequest request, CancellationToken ct = default);
    Task<Result<GetUnreadCountsResponse>> GetUnreadCountsAsync(GetUnreadCountsRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> UpdateThreadAsync(UpdateThreadRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> LeaveThreadAsync(LeaveThreadRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> MuteThreadAsync(MuteThreadRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> ArchiveThreadAsync(ArchiveThreadRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> AddThreadMemberAsync(AddThreadMemberRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> RemoveThreadMemberAsync(RemoveThreadMemberRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> CreateThreadInviteAsync(CreateThreadInviteRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> RespondThreadInviteAsync(RespondThreadInviteRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> UpdateThreadMemberRoleAsync(UpdateThreadMemberRoleRequest request, CancellationToken ct = default);
    Task<Result<CreateThreadMessageResponse>> CreateThreadMessageAsync(CreateThreadMessageRequest request, CancellationToken ct = default);
    Task<Result<GetThreadMessagesResponse>> GetThreadMessagesAsync(GetThreadMessagesRequest request, CancellationToken ct = default);
    Task<Result<SearchMessagesResponse>> SearchMessagesAsync(SearchMessagesRequest request, CancellationToken ct = default);

    // Round 2: Delete, Edit, Attachments, Reactions
    Task<Result<CmdResponse>> DeleteThreadMessageAsync(DeleteThreadMessageRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> EditThreadMessageAsync(EditThreadMessageRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> PinMessageAsync(PinMessageRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> SaveMessageAsync(SaveMessageRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> ReportMessageAsync(ReportMessageRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> BlockCredentialAsync(BlockCredentialRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> DeleteCredentialBlockAsync(DeleteCredentialBlockRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> CreateMessageFileAsync(CreateMessageFileRequest request, CancellationToken ct = default);
    Task<Result<PaginatedResult<MessageFileResponse>>> GetMessageFilesAsync(GetMessageFilesRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> DeleteMessageFileAsync(DeleteMessageFileRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> CreateMessageReactionAsync(CreateMessageReactionRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> DeleteMessageReactionAsync(DeleteMessageReactionRequest request, CancellationToken ct = default);

    // Round 3: Read Receipts
    Task<Result<CmdResponse>> MarkMessagesReadAsync(MarkMessagesReadRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> PublishTypingAsync(PublishCommunicationsTypingRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> PublishPresenceAsync(PublishCommunicationsPresenceRequest request, CancellationToken ct = default);
}
