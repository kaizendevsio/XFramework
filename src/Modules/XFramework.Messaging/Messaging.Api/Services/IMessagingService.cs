using Messaging.Domain.Shared.Contracts.Requests.Create;
using Messaging.Domain.Shared.Contracts.Requests.Update;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;

namespace Messaging.Api.Services;

public interface IMessagingService
{
    /// <summary>
    /// Creates and sends a direct message (SMS/Email/etc)
    /// </summary>
    Task<Result<CmdResponse>> CreateDirectMessageAsync(CreateDirectMessageRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Updates a direct message status and delivery timestamps
    /// </summary>
    Task<Result<CmdResponse>> UpdateMessageDirectAsync(UpdateMessageDirectRequest request, CancellationToken ct = default);
}