using Messaging.Domain.Shared.Contracts.Requests.Create;
using Messaging.Domain.Shared.Contracts.Requests.Update;
using XFramework.Core.Patterns;

namespace Messaging.Api.Services;

public interface IMessagingService
{
    /// <summary>
    /// Creates and sends a direct message (SMS/Email/etc)
    /// </summary>
    Task<Result<CmdResponse>> CreateDirectMessageAsync(CreateDirectMessageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates and sends a verification message.
    /// </summary>
    Task<Result<CmdResponse>> CreateVerificationMessageAsync(CreateVerificationMessageRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Updates a direct message status and delivery timestamps
    /// </summary>
    Task<Result<CmdResponse>> UpdateMessageDirectAsync(UpdateMessageDirectRequest request, CancellationToken ct = default);
}
