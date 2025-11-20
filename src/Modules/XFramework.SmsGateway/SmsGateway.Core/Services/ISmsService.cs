using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Core.Patterns;

namespace SmsGateway.Core.Services;

/// <summary>
/// Service for managing SMS gateway operations including sending, receiving, and tracking SMS messages
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Confirms that an SMS message has been sent successfully
    /// </summary>
    /// <param name="id">The message ID to confirm</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result<CmdResponse>> ConfirmMessageSentAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a record of a received SMS message
    /// </summary>
    /// <param name="request">The received message details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result<CmdResponse>> CreateMessageReceivedAsync(CreateMessageReceivedRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates a new SMS message to be sent
    /// </summary>
    /// <param name="request">The message creation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result<CmdResponse>> CreateSmsMessageAsync(CreateSmsMessageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets a list of pending SMS messages for a specific agent cluster
    /// </summary>
    /// <param name="request">The request containing filter criteria</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the list of pending messages</returns>
    Task<Result<List<SmsNodeJob>>> GetPendingSmsMessagesAsync(GetPendingSmsMessageListRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets a list of scheduled SMS messages for a specific agent cluster
    /// </summary>
    /// <param name="request">The request containing filter criteria</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the list of scheduled messages</returns>
    Task<Result<List<SmsNodeJob>>> GetScheduledSmsMessagesAsync(GetScheduledSmsMessageListRequest request, CancellationToken ct = default);
}