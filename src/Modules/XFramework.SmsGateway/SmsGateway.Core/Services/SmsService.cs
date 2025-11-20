using Messaging.Integration.Drivers;
using Microsoft.Extensions.Logging;
using SmsGateway.Core.Interfaces;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Abstractions.Wrappers;

namespace SmsGateway.Core.Services;

/// <summary>
/// Service for managing SMS gateway operations including sending, receiving, and tracking SMS messages
/// </summary>
public class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;
    private readonly ICachingService _cachingService;
    private readonly IMessagingServiceWrapper _messagingServiceWrapper;
    private readonly IMessageBusWrapper _messageBusWrapper;

    public SmsService(
        ILogger<SmsService> logger,
        ICachingService cachingService,
        IMessagingServiceWrapper messagingServiceWrapper,
        IMessageBusWrapper messageBusWrapper)
    {
        _logger = logger;
        _cachingService = cachingService;
        _messagingServiceWrapper = messagingServiceWrapper;
        _messageBusWrapper = messageBusWrapper;
    }

    /// <summary>
    /// Confirms that an SMS message has been sent successfully
    /// </summary>
    public async Task<Result<CmdResponse>> ConfirmMessageSentAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var item = _cachingService.PendingMessageList
                .FirstOrDefault(i => i.Value.Id == id);

            if (item.Value is null)
            {
                return Result<CmdResponse>.Failure("Message not found in pending list", 404);
            }

            var retryCount = 0;
            var maxRetries = 10; // Add maximum retry limit to prevent infinite loop

            retry:
            var result = await _messagingServiceWrapper.ConfirmMessageSent(new()
            {
                Metadata = new RequestMetadata(), // Using default metadata since it's not available in this context
                Id = item.Value.Id,
                AgentClusterId = item.Value.AgentClusterId,
                SentAt = DateTime.Now.ToUniversalTime()
            });

            if (result.IsSuccess is false)
            {
                retryCount++;
                
                if (retryCount >= maxRetries)
                {
                    _logger.LogError("Failed to confirm message sent after {RetryCount} attempts, reason: {Reason}",
                        retryCount, result.Message);
                    return Result<CmdResponse>.Failure(
                        $"Failed to confirm message after {maxRetries} retry attempts",
                        500
                    );
                }

                await Task.Delay(1500, CancellationToken.None);
                _logger.LogWarning("Failed to confirm message sent, reason: {Reason}, retry count: {RetryCount}", 
                    result.Message, retryCount);
                goto retry;
            }

            _cachingService.PendingMessageList.Remove(item.Key, out _);

            return Result<CmdResponse>.Success(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming message sent for ID: {Id}", id);
            return Result<CmdResponse>.Failure($"Error confirming message: {ex.Message}", 500);
        }
    }

    /// <summary>
    /// Creates a record of a received SMS message
    /// </summary>
    public async Task<Result<CmdResponse>> CreateMessageReceivedAsync(CreateMessageReceivedRequest request, CancellationToken ct = default)
    {
        try
        {
            // Fire and forget - matches original handler behavior
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _messagingServiceWrapper.MessageDirect
                        .Create(new()
                        {
                            ExternalSender = request.Sender,
                            Message = request.Message,
                            SubscriptionId = request.SubscriptionId,
                            RecievedAt = string.IsNullOrEmpty(request.ReceivedAt)
                                ? null
                                : DateTime.Parse(request.ReceivedAt).ToUniversalTime(),
                            AgentClusterId = request.AgentClusterId
                        });

                    if (result.IsSuccess is false)
                    {
                        _logger.LogWarning("Failed to create message received record, reason: {Reason}", result.Message);
                    }
                    else
                    {
                        _logger.LogInformation("Message received record created successfully for AgentClusterId: {AgentClusterId}",
                            request.AgentClusterId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background task creating message received for AgentClusterId: {AgentClusterId}", 
                        request.AgentClusterId);
                }
            }, CancellationToken.None);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Success"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating message received for AgentClusterId: {AgentClusterId}",
                request.AgentClusterId);
            return Result<CmdResponse>.Failure($"Error creating message received: {ex.Message}", 500);
        }
    }

    /// <summary>
    /// Creates a new SMS message to be sent
    /// </summary>
    public async Task<Result<CmdResponse>> CreateSmsMessageAsync(CreateSmsMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            Retry:
            var data = new SmsNodeJob()
            {
                Id = request.Id,
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now,
                IsDeleted = false,
                AgentClusterId = request.AgentClusterId,
                Recipient = request.Recipient,
                Message = request.Message
            };

            if (_cachingService.PendingMessageList.TryAdd(Guid.NewGuid(), data) is false)
            {
                goto Retry;
            }

            return Result<CmdResponse>.Success(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SMS message for AgentClusterId: {AgentClusterId}",
                request.AgentClusterId);
            return Result<CmdResponse>.Failure($"Error creating SMS message: {ex.Message}", 500);
        }
    }

    /// <summary>
    /// Gets a list of pending SMS messages for a specific agent cluster
    /// </summary>
    public async Task<Result<List<SmsNodeJob>>> GetPendingSmsMessagesAsync(GetPendingSmsMessageListRequest request, CancellationToken ct = default)
    {
        try
        {
            var messageDirectResponses = _cachingService.PendingMessageList
                .Where(x => x.Value.AgentClusterId == request.AgentClusterId)
                .Select(x => x.Value)
                .ToList();

            return Result<List<SmsNodeJob>>.Success(messageDirectResponses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending SMS messages for AgentClusterId: {AgentClusterId}",
                request.AgentClusterId);
            return Result<List<SmsNodeJob>>.Failure($"Error getting pending SMS messages: {ex.Message}", 500);
        }
    }

    /// <summary>
    /// Gets a list of scheduled SMS messages for a specific agent cluster
    /// </summary>
    public async Task<Result<List<SmsNodeJob>>> GetScheduledSmsMessagesAsync(GetScheduledSmsMessageListRequest request, CancellationToken ct = default)
    {
        try
        {
            var messageDirectResponses = _cachingService.ScheduledMessageList
                .Where(x => x.Value.AgentClusterId == request.AgentClusterId)
                .Select(x => x.Value)
                .ToList();

            return Result<List<SmsNodeJob>>.Success(messageDirectResponses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduled SMS messages for AgentClusterId: {AgentClusterId}",
                request.AgentClusterId);
            return Result<List<SmsNodeJob>>.Failure($"Error getting scheduled SMS messages: {ex.Message}", 500);
        }
    }
}