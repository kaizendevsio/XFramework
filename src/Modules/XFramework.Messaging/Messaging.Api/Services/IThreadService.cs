using Messaging.Domain.Shared.Contracts.Requests.Attachments;
using Messaging.Domain.Shared.Contracts.Requests.Delete;
using Messaging.Domain.Shared.Contracts.Requests.Edit;
using Messaging.Domain.Shared.Contracts.Requests.Reactions;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;

namespace Messaging.Api.Services;

public interface IThreadService
{
    /// <summary>
    /// Soft-deletes a message in a thread after verifying membership and ownership.
    /// </summary>
    Task<Result<CmdResponse>> DeleteThreadMessageAsync(DeleteThreadMessageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Edits the text of a message in a thread after verifying membership and ownership.
    /// </summary>
    Task<Result<CmdResponse>> EditThreadMessageAsync(EditThreadMessageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates a file attachment linking a message to a storage file.
    /// </summary>
    Task<Result<CmdResponse>> CreateMessageFileAsync(CreateMessageFileRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all file attachments for a given message.
    /// </summary>
    Task<Result<List<MessageFileResponse>>> GetMessageFilesAsync(GetMessageFilesRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates a reaction on a message, preventing duplicates of the same type per member.
    /// </summary>
    Task<Result<CmdResponse>> CreateMessageReactionAsync(CreateMessageReactionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a reaction after verifying the requester is a member of the thread.
    /// </summary>
    Task<Result<CmdResponse>> DeleteMessageReactionAsync(DeleteMessageReactionRequest request, CancellationToken ct = default);
}
