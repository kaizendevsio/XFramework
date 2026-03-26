using Messaging.Domain.Shared.Contracts.Requests.Threads;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;

namespace Messaging.Api.Services;

public interface IThreadService
{
    /// <summary>
    /// Creates a new message thread with initial members
    /// </summary>
    Task<Result<CreateThreadResponse>> CreateThreadAsync(CreateThreadRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets a paginated list of threads where the credential is a member
    /// </summary>
    Task<Result<GetThreadListResponse>> GetThreadListAsync(GetThreadListRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets a thread by ID with its members
    /// </summary>
    Task<Result<GetThreadResponse>> GetThreadAsync(GetThreadRequest request, CancellationToken ct = default);

    /// <summary>
    /// Adds a member to a thread
    /// </summary>
    Task<Result<CmdResponse>> AddThreadMemberAsync(AddThreadMemberRequest request, CancellationToken ct = default);

    /// <summary>
    /// Removes a member from a thread
    /// </summary>
    Task<Result<CmdResponse>> RemoveThreadMemberAsync(RemoveThreadMemberRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates a new message in a thread
    /// </summary>
    Task<Result<CreateThreadMessageResponse>> CreateThreadMessageAsync(CreateThreadMessageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets paginated messages for a thread
    /// </summary>
    Task<Result<GetThreadMessagesResponse>> GetThreadMessagesAsync(GetThreadMessagesRequest request, CancellationToken ct = default);
}
