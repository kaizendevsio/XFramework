using Microsoft.Extensions.Logging;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Core.Loggers;
using XFramework.Domain.Shared.Enums;

namespace SmsGateway.Api.Services;

/// <summary>
/// Service for managing SMS gateway operations including sending, receiving, and tracking SMS messages.
/// This is the SMS Gateway Node service that manages local message queues.
/// </summary>
public sealed class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;
    private readonly ICachingService _cachingService;

    public SmsService(
        ILogger<SmsService> logger,
        ICachingService cachingService)
    {
        _logger = logger;
        _cachingService = cachingService;
    }

    /// <summary>
    /// Confirms that an SMS message has been sent successfully by removing it from the pending list
    /// </summary>
    public Task<Result<CmdResponse>> ConfirmMessageSentAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var item = _cachingService.PendingMessageList
                .FirstOrDefault(i => i.Value.Id == id);

            if (item.Value is null)
            {
                return Task.FromResult(Result<CmdResponse>.Failure("Message not found in pending list", 404));
            }

            // Remove from pending list - message has been sent
            _cachingService.PendingMessageList.Remove(item.Key, out _);

            _logger.LogInformation("SMS message {MessageId} confirmed as sent for agent cluster {AgentClusterId}",
                id, item.Value.AgentClusterId);

            return Task.FromResult(Result<CmdResponse>.Success(new CmdResponse { HttpStatusCode = HttpStatusCode.OK }));
        }
        catch (Exception ex)
        {
            _logger.SmsConfirmationError(id, ex);
            return Task.FromResult(Result<CmdResponse>.Failure($"Error confirming message: {ex.Message}", 500));
        }
    }

    /// <summary>
    /// Creates a record of a received SMS message (logs the receipt for now)
    /// </summary>
    public Task<Result<CmdResponse>> CreateMessageReceivedAsync(CreateMessageReceivedRequest request, CancellationToken ct = default)
    {
        try
        {
            // Log the received message
            _logger.LogInformation(
                "SMS message received from {Sender} for agent cluster {AgentClusterId}: {Message}",
                request.Sender,
                request.AgentClusterId,
                request.Message?.Substring(0, Math.Min(50, request.Message?.Length ?? 0)));

            return Task.FromResult(Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Message received recorded"
            }));
        }
        catch (Exception ex)
        {
            _logger.SmsCreateMessageReceivedError(request.AgentClusterId, ex);
            return Task.FromResult(Result<CmdResponse>.Failure($"Error creating message received: {ex.Message}", 500));
        }
    }

    /// <summary>
    /// Creates a new SMS message to be sent
    /// </summary>
    public Task<Result<CmdResponse>> CreateSmsMessageAsync(CreateSmsMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var data = new SmsNodeJob()
            {
                Id = request.Id,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                IsDeleted = false,
                AgentClusterId = request.AgentClusterId,
                Recipient = request.Recipient,
                Message = request.Message
            };

            // ConcurrentDictionary.TryAdd with a new GUID key will virtually always succeed,
            // but loop defensively in case of the astronomically unlikely GUID collision
            while (!_cachingService.PendingMessageList.TryAdd(Guid.NewGuid(), data))
            {
            }

            return Task.FromResult(Result<CmdResponse>.Success(new CmdResponse { HttpStatusCode = HttpStatusCode.OK }));
        }
        catch (Exception ex)
        {
            _logger.SmsCreateMessageError(request.AgentClusterId, ex);
            return Task.FromResult(Result<CmdResponse>.Failure($"Error creating SMS message: {ex.Message}", 500));
        }
    }

    /// <summary>
    /// Gets a list of pending SMS messages for a specific agent cluster
    /// </summary>
    public Task<Result<List<SmsNodeJob>>> GetPendingSmsMessagesAsync(GetPendingSmsMessageListRequest request, CancellationToken ct = default)
    {
        try
        {
            var messageDirectResponses = _cachingService.PendingMessageList
                .Where(x => x.Value.AgentClusterId == request.AgentClusterId)
                .Select(x => x.Value)
                .ToList();

            return Task.FromResult(Result<List<SmsNodeJob>>.Success(messageDirectResponses));
        }
        catch (Exception ex)
        {
            _logger.SmsGetPendingError(request.AgentClusterId, ex);
            return Task.FromResult(Result<List<SmsNodeJob>>.Failure($"Error getting pending SMS messages: {ex.Message}", 500));
        }
    }

    /// <summary>
    /// Gets a list of scheduled SMS messages for a specific agent cluster
    /// </summary>
    public Task<Result<List<SmsNodeJob>>> GetScheduledSmsMessagesAsync(GetScheduledSmsMessageListRequest request, CancellationToken ct = default)
    {
        try
        {
            var messageDirectResponses = _cachingService.ScheduledMessageList
                .Where(x => x.Value.AgentClusterId == request.AgentClusterId)
                .Select(x => x.Value)
                .ToList();

            return Task.FromResult(Result<List<SmsNodeJob>>.Success(messageDirectResponses));
        }
        catch (Exception ex)
        {
            _logger.SmsGetScheduledError(request.AgentClusterId, ex);
            return Task.FromResult(Result<List<SmsNodeJob>>.Failure($"Error getting scheduled SMS messages: {ex.Message}", 500));
        }
    }

    /// <summary>
    /// Gets pending SMS messages and updates their status to Processing (replaces legacy controller List endpoint)
    /// </summary>
    public Task<Result<List<SmsNodeJob>>> GetPendingWithStatusUpdateAsync(Guid agentClusterId, CancellationToken ct = default)
    {
        try
        {
            var itemList = _cachingService.PendingMessageList
                .Where(x => x.Value.AgentClusterId == agentClusterId)
                .Where(x => x.Value.Status is MessageStatus.Queued)
                .Select(i => i.Value)
                .ToList();

            foreach (var current in itemList)
            {
                current.Status = MessageStatus.Processing;
            }

            return Task.FromResult(Result<List<SmsNodeJob>>.Success(itemList));
        }
        catch (Exception ex)
        {
            _logger.SmsGetPendingError(agentClusterId, ex);
            return Task.FromResult(Result<List<SmsNodeJob>>.Failure($"Error getting pending SMS messages with status update: {ex.Message}", 500));
        }
    }
}
