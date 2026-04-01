using Messaging.Domain.Shared.Contracts.Requests.Attachments;
using Messaging.Domain.Shared.Contracts.Requests.Delete;
using Messaging.Domain.Shared.Contracts.Requests.Edit;
using Messaging.Domain.Shared.Contracts.Requests.Reactions;
using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;

namespace Messaging.Api.Services;

public interface IThreadService
{
    // Round 1: Thread CRUD + Members + Messages
    Task<Result<CreateThreadResponse>> CreateThreadAsync(CreateThreadRequest request, CancellationToken ct = default);
    Task<Result<GetThreadListResponse>> GetThreadListAsync(GetThreadListRequest request, CancellationToken ct = default);
    Task<Result<GetThreadResponse>> GetThreadAsync(GetThreadRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> UpdateThreadAsync(UpdateThreadRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> AddThreadMemberAsync(AddThreadMemberRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> RemoveThreadMemberAsync(RemoveThreadMemberRequest request, CancellationToken ct = default);
    Task<Result<CreateThreadMessageResponse>> CreateThreadMessageAsync(CreateThreadMessageRequest request, CancellationToken ct = default);
    Task<Result<GetThreadMessagesResponse>> GetThreadMessagesAsync(GetThreadMessagesRequest request, CancellationToken ct = default);

    // Round 2: Delete, Edit, Attachments, Reactions
    Task<Result<CmdResponse>> DeleteThreadMessageAsync(DeleteThreadMessageRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> EditThreadMessageAsync(EditThreadMessageRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> CreateMessageFileAsync(CreateMessageFileRequest request, CancellationToken ct = default);
    Task<Result<List<MessageFileResponse>>> GetMessageFilesAsync(GetMessageFilesRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> CreateMessageReactionAsync(CreateMessageReactionRequest request, CancellationToken ct = default);
    Task<Result<CmdResponse>> DeleteMessageReactionAsync(DeleteMessageReactionRequest request, CancellationToken ct = default);

    // Round 3: Read Receipts
    Task<Result<CmdResponse>> MarkMessagesReadAsync(MarkMessagesReadRequest request, CancellationToken ct = default);
}
