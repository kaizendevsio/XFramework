using Microsoft.Extensions.Logging;

namespace XFramework.Core.Loggers;

/// <summary>
/// Centralized structured logging messages using LoggerMessage source generators.
/// Provides zero-allocation, high-performance logging methods.
/// </summary>
/// <remarks>
/// EventId Ranges:
/// - 1000-1999: CRUD Operations
/// - 2000-2999: Cache Operations
/// - 3000-3999: Performance Metrics
/// - 4000-4999: Security Events
/// - 5000-5999: Errors/Exceptions
/// - 6000-6999: Wallet/Financial Operations
/// - 7000-7999: Communications Operations
/// - 8000-8999: Integration/External Service Operations
/// </remarks>
public static partial class LogMessages
{
    #region CRUD Operations (1000-1999)

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Creating {EntityType} with ID {EntityId} for tenant {TenantId}")]
    public static partial void EntityCreating(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, Guid entityId, Guid? tenantId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Successfully created {EntityType} with ID {EntityId}")]
    public static partial void EntityCreated(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, Guid entityId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Retrieving {EntityType} with ID {EntityId}")]
    public static partial void EntityRetrieving(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, Guid entityId);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Successfully retrieved {EntityType} with ID {EntityId}")]
    public static partial void EntityRetrieved(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, Guid entityId);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Updating {EntityType} with ID {EntityId}")]
    public static partial void EntityUpdating(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, Guid entityId);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Successfully updated {EntityType} with ID {EntityId}")]
    public static partial void EntityUpdated(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, Guid entityId);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Deleting {EntityType} with ID {EntityId}")]
    public static partial void EntityDeleting(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, Guid entityId);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Information,
        Message = "Successfully deleted {EntityType} with ID {EntityId}")]
    public static partial void EntityDeleted(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, Guid entityId);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Information,
        Message = "Querying {EntityType} with filter: {FilterDescription}")]
    public static partial void EntityQuerying(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, string filterDescription);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Information,
        Message = "Query returned {Count} {EntityType} records")]
    public static partial void EntityQueryCompleted(
        this Microsoft.Extensions.Logging.ILogger logger, int count, string entityType);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Warning,
        Message = "{EntityType} with ID {EntityId} not found")]
    public static partial void EntityNotFound(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, Guid entityId);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Information,
        Message = "Listing {EntityType} - Page {Page}, Size {PageSize}")]
    public static partial void EntityListing(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, int page, int pageSize);

    #endregion

    #region Cache Operations (2000-2999)

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Cache hit for key: {CacheKey}")]
    public static partial void CacheHit(
        this Microsoft.Extensions.Logging.ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Cache miss for key: {CacheKey}")]
    public static partial void CacheMiss(
        this Microsoft.Extensions.Logging.ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "Setting cache key: {CacheKey} with expiration {ExpirationMinutes} minutes")]
    public static partial void CacheSetting(
        this Microsoft.Extensions.Logging.ILogger logger, string cacheKey, int expirationMinutes);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Cache invalidated for key: {CacheKey}")]
    public static partial void CacheInvalidated(
        this Microsoft.Extensions.Logging.ILogger logger, string cacheKey);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Cache cleared for pattern: {Pattern}")]
    public static partial void CacheCleared(
        this Microsoft.Extensions.Logging.ILogger logger, string pattern);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Warning,
        Message = "Cache operation failed for key: {CacheKey}")]
    public static partial void CacheOperationFailed(
        this Microsoft.Extensions.Logging.ILogger logger, string cacheKey, Exception? exception = null);

    #endregion

    #region Performance Metrics (3000-3999)

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Operation {Operation} completed in {DurationMs}ms")]
    public static partial void OperationCompleted(
        this Microsoft.Extensions.Logging.ILogger logger, string operation, long durationMs);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Operation {Operation} took {DurationMs}ms (threshold: {ThresholdMs}ms)")]
    public static partial void OperationSlow(
        this Microsoft.Extensions.Logging.ILogger logger, string operation, long durationMs, long thresholdMs);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Debug,
        Message = "Database query executed in {DurationMs}ms: {QueryName}")]
    public static partial void DatabaseQueryExecuted(
        this Microsoft.Extensions.Logging.ILogger logger, long durationMs, string queryName);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Information,
        Message = "Batch operation processed {ItemCount} items in {DurationMs}ms")]
    public static partial void BatchOperationCompleted(
        this Microsoft.Extensions.Logging.ILogger logger, int itemCount, long durationMs);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Warning,
        Message = "High memory usage detected: {MemoryMb}MB")]
    public static partial void HighMemoryUsage(
        this Microsoft.Extensions.Logging.ILogger logger, long memoryMb);

    #endregion

    #region Security Events (4000-4999)

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Credential {CredentialId} authenticated successfully")]
    public static partial void UserAuthenticated(
        this Microsoft.Extensions.Logging.ILogger logger, Guid credentialId);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Warning,
        Message = "Failed login attempt")]
    public static partial void LoginFailed(
        this Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Warning,
        Message = "Unauthorized access attempt to {Resource} by user {UserId}")]
    public static partial void UnauthorizedAccess(
        this Microsoft.Extensions.Logging.ILogger logger, string resource, Guid? userId);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Information,
        Message = "User {UserId} logged out")]
    public static partial void UserLoggedOut(
        this Microsoft.Extensions.Logging.ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Warning,
        Message = "Token validation failed for user {UserId}: {Reason}")]
    public static partial void TokenValidationFailed(
        this Microsoft.Extensions.Logging.ILogger logger, Guid? userId, string reason);

    [LoggerMessage(
        EventId = 4006,
        Level = LogLevel.Information,
        Message = "Password changed for user {UserId}")]
    public static partial void PasswordChanged(
        this Microsoft.Extensions.Logging.ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 4007,
        Level = LogLevel.Warning,
        Message = "Multiple failed login attempts detected for credential {CredentialId} - Count: {AttemptCount}")]
    public static partial void MultipleFailedLogins(
        this Microsoft.Extensions.Logging.ILogger logger, Guid credentialId, int attemptCount);

    [LoggerMessage(
        EventId = 4008,
        Level = LogLevel.Information,
        Message = "API key validated for application {ApplicationId}")]
    public static partial void ApiKeyValidated(
        this Microsoft.Extensions.Logging.ILogger logger, Guid applicationId);

    #endregion

    #region Errors/Exceptions (5000-5999)

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Error,
        Message = "Operation {Operation} failed for {EntityType} {EntityId}: {ErrorMessage}")]
    public static partial void OperationFailed(
        this Microsoft.Extensions.Logging.ILogger logger, string operation, string entityType,
        Guid entityId, string errorMessage, Exception? exception = null);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Error,
        Message = "Validation failed for {EntityType}: {ValidationErrors}")]
    public static partial void ValidationFailed(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, string validationErrors);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Error,
        Message = "Database operation failed: {Operation}")]
    public static partial void DatabaseOperationFailed(
        this Microsoft.Extensions.Logging.ILogger logger, string operation, Exception? exception = null);

    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Error,
        Message = "External service call failed: {ServiceName} - {Endpoint}")]
    public static partial void ExternalServiceFailed(
        this Microsoft.Extensions.Logging.ILogger logger, string serviceName, string endpoint, Exception? exception = null);

    [LoggerMessage(
        EventId = 5005,
        Level = LogLevel.Critical,
        Message = "Critical error in {Component}: {ErrorMessage}")]
    public static partial void CriticalError(
        this Microsoft.Extensions.Logging.ILogger logger, string component, string errorMessage, Exception? exception = null);

    [LoggerMessage(
        EventId = 5006,
        Level = LogLevel.Error,
        Message = "Concurrency conflict detected for {EntityType} {EntityId}")]
    public static partial void ConcurrencyConflict(
        this Microsoft.Extensions.Logging.ILogger logger, string entityType, Guid entityId);

    [LoggerMessage(
        EventId = 5007,
        Level = LogLevel.Error,
        Message = "Business rule violation in {Operation}: {RuleDescription}")]
    public static partial void BusinessRuleViolation(
        this Microsoft.Extensions.Logging.ILogger logger, string operation, string ruleDescription);

    [LoggerMessage(
        EventId = 5008,
        Level = LogLevel.Error,
        Message = "Unhandled exception in request pipeline")]
    public static partial void UnhandledException(
        this Microsoft.Extensions.Logging.ILogger logger, Exception exception);

    #endregion

    #region Wallet/Financial Operations (6000-6999)

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "Wallet {WalletId} balance increment: {Amount} {Currency} - New balance: {NewBalance}")]
    public static partial void WalletIncremented(
        this Microsoft.Extensions.Logging.ILogger logger, Guid walletId, decimal amount, string currency, decimal newBalance);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "Wallet {WalletId} balance decrement: {Amount} {Currency} - New balance: {NewBalance}")]
    public static partial void WalletDecremented(
        this Microsoft.Extensions.Logging.ILogger logger, Guid walletId, decimal amount, string currency, decimal newBalance);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Information,
        Message = "Wallet transfer from {SourceWalletId} to {DestinationWalletId}: {Amount} {Currency}")]
    public static partial void WalletTransfer(
        this Microsoft.Extensions.Logging.ILogger logger, Guid sourceWalletId, Guid destinationWalletId, decimal amount, string currency);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Warning,
        Message = "Insufficient balance in wallet {WalletId}: Required {RequiredAmount}, Available {AvailableBalance}")]
    public static partial void InsufficientBalance(
        this Microsoft.Extensions.Logging.ILogger logger, Guid walletId, decimal requiredAmount, decimal availableBalance);

    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Information,
        Message = "Batch wallet operation started: {OperationType} - {ItemCount} items")]
    public static partial void BatchWalletOperationStarted(
        this Microsoft.Extensions.Logging.ILogger logger, string operationType, int itemCount);

    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Information,
        Message = "Batch wallet operation completed: {OperationType} - {SuccessCount}/{TotalCount} successful")]
    public static partial void BatchWalletOperationCompleted(
        this Microsoft.Extensions.Logging.ILogger logger, string operationType, int successCount, int totalCount);

    [LoggerMessage(
        EventId = 6007,
        Level = LogLevel.Information,
        Message = "Transaction {TransactionId} created for wallet {WalletId}: {TransactionType} {Amount}")]
    public static partial void TransactionCreated(
        this Microsoft.Extensions.Logging.ILogger logger, Guid transactionId, Guid walletId, string transactionType, decimal amount);

    #endregion

    #region Communications Operations (7000-7999)

    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Information,
        Message = "Message {MessageId} sent from {SenderId} to {RecipientId}")]
    public static partial void MessageSent(
        this Microsoft.Extensions.Logging.ILogger logger, Guid messageId, Guid senderId, Guid recipientId);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Information,
        Message = "Message {MessageId} delivered to {RecipientId}")]
    public static partial void MessageDelivered(
        this Microsoft.Extensions.Logging.ILogger logger, Guid messageId, Guid recipientId);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Warning,
        Message = "Message {MessageId} delivery failed to {RecipientId}: {Reason}")]
    public static partial void MessageDeliveryFailed(
        this Microsoft.Extensions.Logging.ILogger logger, Guid messageId, Guid recipientId, string reason);

    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Information,
        Message = "SMS sent to {PhoneNumber}: {MessageType}")]
    public static partial void SmsSent(
        this Microsoft.Extensions.Logging.ILogger logger, string phoneNumber, string messageType);

    [LoggerMessage(
        EventId = 7005,
        Level = LogLevel.Warning,
        Message = "SMS delivery failed to {PhoneNumber}: {ErrorCode}")]
    public static partial void SmsDeliveryFailed(
        this Microsoft.Extensions.Logging.ILogger logger, string phoneNumber, string errorCode);

    [LoggerMessage(
        EventId = 7006,
        Level = LogLevel.Information,
        Message = "Verification message sent to {Recipient}: Type {VerificationType}")]
    public static partial void VerificationMessageSent(
        this Microsoft.Extensions.Logging.ILogger logger, string recipient, string verificationType);

    #endregion

    #region Integration/External Service Operations (8000-8999)

    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Information,
        Message = "External API call: {Method} {Endpoint} - Status: {StatusCode}")]
    public static partial void ExternalApiCallCompleted(
        this Microsoft.Extensions.Logging.ILogger logger, string method, string endpoint, int statusCode);

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Warning,
        Message = "External API call retry {Attempt}/{MaxAttempts}: {Endpoint}")]
    public static partial void ExternalApiRetry(
        this Microsoft.Extensions.Logging.ILogger logger, int attempt, int maxAttempts, string endpoint);

    [LoggerMessage(
        EventId = 8003,
        Level = LogLevel.Error,
        Message = "External API call failed after {Attempts} attempts: {Endpoint}")]
    public static partial void ExternalApiCallFailed(
        this Microsoft.Extensions.Logging.ILogger logger, int attempts, string endpoint, Exception? exception = null);

    [LoggerMessage(
        EventId = 8004,
        Level = LogLevel.Information,
        Message = "Blockchain transaction initiated: {TransactionHash}")]
    public static partial void BlockchainTransactionInitiated(
        this Microsoft.Extensions.Logging.ILogger logger, string transactionHash);

    [LoggerMessage(
        EventId = 8005,
        Level = LogLevel.Information,
        Message = "Blockchain transaction confirmed: {TransactionHash} - Block: {BlockNumber}")]
    public static partial void BlockchainTransactionConfirmed(
        this Microsoft.Extensions.Logging.ILogger logger, string transactionHash, long blockNumber);

    [LoggerMessage(
        EventId = 8006,
        Level = LogLevel.Information,
        Message = "WebSocket connection established: ClientId {ClientId}")]
    public static partial void WebSocketConnected(
        this Microsoft.Extensions.Logging.ILogger logger, string clientId);

    [LoggerMessage(
        EventId = 8007,
        Level = LogLevel.Information,
        Message = "WebSocket connection closed: ClientId {ClientId} - Reason: {Reason}")]
    public static partial void WebSocketDisconnected(
        this Microsoft.Extensions.Logging.ILogger logger, string clientId, string reason);

    #endregion

    #region Bolt Operations (9000-9999)

    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Warning,
        Message = "Unknown or unauthorized client detected. ConnectionId: {ConnectionId}")]
    public static partial void BoltClientUnauthorized(
        this Microsoft.Extensions.Logging.ILogger logger, string connectionId);

    [LoggerMessage(
        EventId = 9002,
        Level = LogLevel.Information,
        Message = "FanOut message sent. RequestId: {RequestId}, Sender: {SenderName}")]
    public static partial void BoltFanOutSent(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId, string senderName);

    [LoggerMessage(
        EventId = 9003,
        Level = LogLevel.Information,
        Message = "Topic message sent. RequestId: {RequestId}, Topic: {Topic}, Sender: {SenderName}")]
    public static partial void BoltTopicSent(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId, string topic, string senderName);

    [LoggerMessage(
        EventId = 9004,
        Level = LogLevel.Information,
        Message = "Direct message sent. ExchangeType: {ExchangeType}, RequestId: {RequestId}, Sender: {SenderName} -> Recipient: {RecipientName}, Status: {StatusCode}")]
    public static partial void BoltDirectSent(
        this Microsoft.Extensions.Logging.ILogger logger, string exchangeType, string requestId,
        string senderName, string recipientName, int statusCode);

    [LoggerMessage(
        EventId = 9005,
        Level = LogLevel.Error,
        Message = "Error pushing message. RequestId: {RequestId}")]
    public static partial void BoltPushError(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId, Exception exception);

    [LoggerMessage(
        EventId = 9006,
        Level = LogLevel.Error,
        Message = "Failed to register client after {MaxAttempts} attempts. ConnectionId: {ConnectionId}, ClientId: {ClientId}")]
    public static partial void BoltClientRegistrationFailed(
        this Microsoft.Extensions.Logging.ILogger logger, int maxAttempts, string connectionId, string clientId);

    [LoggerMessage(
        EventId = 9007,
        Level = LogLevel.Information,
        Message = "Client registered. ConnectionId: {ConnectionId}, ClientId: {ClientId}, Transport: {TransportType}, Name: {ClientName}")]
    public static partial void BoltClientRegistered(
        this Microsoft.Extensions.Logging.ILogger logger, string connectionId, string clientId,
        string transportType, string clientName);

    [LoggerMessage(
        EventId = 9008,
        Level = LogLevel.Warning,
        Message = "Failed to queue method call. RequestId: {RequestId}")]
    public static partial void BoltMethodCallQueueFailed(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId);

    [LoggerMessage(
        EventId = 9009,
        Level = LogLevel.Warning,
        Message = "Method invocation timed out. RequestId: {RequestId}")]
    public static partial void BoltMethodInvocationTimeout(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId);

    [LoggerMessage(
        EventId = 9010,
        Level = LogLevel.Error,
        Message = "Error invoking method. RequestId: {RequestId}")]
    public static partial void BoltMethodInvocationError(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId, Exception exception);

    [LoggerMessage(
        EventId = 9011,
        Level = LogLevel.Information,
        Message = "Method response received. RequestId: {RequestId}")]
    public static partial void BoltMethodResponseReceived(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId);

    [LoggerMessage(
        EventId = 9012,
        Level = LogLevel.Error,
        Message = "Error processing method response. RequestId: {RequestId}")]
    public static partial void BoltMethodResponseError(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId, Exception exception);

    [LoggerMessage(
        EventId = 9013,
        Level = LogLevel.Debug,
        Message = "Dequeue request for client. ClientId: {ClientId}")]
    public static partial void BoltDequeueRequest(
        this Microsoft.Extensions.Logging.ILogger logger, string clientId);

    [LoggerMessage(
        EventId = 9014,
        Level = LogLevel.Warning,
        Message = "Invalid recipient for message. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}")]
    public static partial void BoltInvalidRecipient(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId, string senderName, string recipientId);

    [LoggerMessage(
        EventId = 9015,
        Level = LogLevel.Information,
        Message = "Message queueing disabled. Message dropped. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}")]
    public static partial void BoltMessageQueuingDisabled(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId, string senderName, string recipientId);

    [LoggerMessage(
        EventId = 9016,
        Level = LogLevel.Information,
        Message = "Message queued for offline recipient. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}")]
    public static partial void BoltMessageQueued(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId, string senderName, string recipientId);

    [LoggerMessage(
        EventId = 9017,
        Level = LogLevel.Warning,
        Message = "Failed to queue message (channel closed). RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}")]
    public static partial void BoltMessageQueueFailed(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId, string senderName, string recipientId);

    [LoggerMessage(
        EventId = 9018,
        Level = LogLevel.Information,
        Message = "Message queueing cancelled. RequestId: {RequestId}, Sender: {SenderName}, RecipientId: {RecipientId}")]
    public static partial void BoltMessageQueueCancelled(
        this Microsoft.Extensions.Logging.ILogger logger, string requestId, string senderName, string recipientId);

    [LoggerMessage(
        EventId = 9019,
        Level = LogLevel.Warning,
        Message = "Failed to cache latest client after {MaxAttempts} attempts. ClientId: {ClientId}")]
    public static partial void BoltClientCacheFailed(
        this Microsoft.Extensions.Logging.ILogger logger, int maxAttempts, string clientId);

    [LoggerMessage(
        EventId = 9020,
        Level = LogLevel.Warning,
        Message = "Failed to update latest client cache after {MaxAttempts} attempts. ClientId: {ClientId}")]
    public static partial void BoltClientCacheUpdateFailed(
        this Microsoft.Extensions.Logging.ILogger logger, int maxAttempts, string clientId);

    [LoggerMessage(
        EventId = 9021,
        Level = LogLevel.Debug,
        Message = "Client added to absolute clients. ClientId: {ClientId}")]
    public static partial void BoltClientAddedToAbsolute(
        this Microsoft.Extensions.Logging.ILogger logger, string clientId);

    [LoggerMessage(
        EventId = 9022,
        Level = LogLevel.Warning,
        Message = "Failed to add client to absolute clients after {MaxAttempts} attempts. ClientId: {ClientId}")]
    public static partial void BoltAbsoluteClientAddFailed(
        this Microsoft.Extensions.Logging.ILogger logger, int maxAttempts, string clientId);

    [LoggerMessage(
        EventId = 9023,
        Level = LogLevel.Debug,
        Message = "Updated connection ID for existing client. ClientId: {ClientId}")]
    public static partial void BoltClientConnectionUpdated(
        this Microsoft.Extensions.Logging.ILogger logger, string clientId);

    #endregion

    #region SMS Gateway Operations (10000-10999)

    [LoggerMessage(
        EventId = 10001,
        Level = LogLevel.Error,
        Message = "Failed to confirm message sent after {RetryCount} attempts, reason: {Reason}")]
    public static partial void SmsConfirmationFailed(
        this Microsoft.Extensions.Logging.ILogger logger, int retryCount, string reason);

    [LoggerMessage(
        EventId = 10002,
        Level = LogLevel.Warning,
        Message = "Failed to confirm message sent, reason: {Reason}, retry count: {RetryCount}")]
    public static partial void SmsConfirmationRetrying(
        this Microsoft.Extensions.Logging.ILogger logger, string reason, int retryCount);

    [LoggerMessage(
        EventId = 10003,
        Level = LogLevel.Error,
        Message = "Error confirming message sent for ID: {Id}")]
    public static partial void SmsConfirmationError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid id, Exception exception);

    [LoggerMessage(
        EventId = 10004,
        Level = LogLevel.Warning,
        Message = "Failed to create message received record, reason: {Reason}")]
    public static partial void SmsMessageReceivedCreationFailed(
        this Microsoft.Extensions.Logging.ILogger logger, string reason);

    [LoggerMessage(
        EventId = 10005,
        Level = LogLevel.Information,
        Message = "Message received record created successfully for AgentClusterId: {AgentClusterId}")]
    public static partial void SmsMessageReceivedCreated(
        this Microsoft.Extensions.Logging.ILogger logger, Guid agentClusterId);

    [LoggerMessage(
        EventId = 10006,
        Level = LogLevel.Error,
        Message = "Error in background task creating message received for AgentClusterId: {AgentClusterId}")]
    public static partial void SmsMessageReceivedBackgroundError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid agentClusterId, Exception exception);

    [LoggerMessage(
        EventId = 10007,
        Level = LogLevel.Error,
        Message = "Error creating message received for AgentClusterId: {AgentClusterId}")]
    public static partial void SmsCreateMessageReceivedError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid agentClusterId, Exception exception);

    [LoggerMessage(
        EventId = 10008,
        Level = LogLevel.Error,
        Message = "Error creating SMS message for AgentClusterId: {AgentClusterId}")]
    public static partial void SmsCreateMessageError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid agentClusterId, Exception exception);

    [LoggerMessage(
        EventId = 10009,
        Level = LogLevel.Error,
        Message = "Error getting pending SMS messages for AgentClusterId: {AgentClusterId}")]
    public static partial void SmsGetPendingError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid agentClusterId, Exception exception);

    [LoggerMessage(
        EventId = 10010,
        Level = LogLevel.Error,
        Message = "Error getting scheduled SMS messages for AgentClusterId: {AgentClusterId}")]
    public static partial void SmsGetScheduledError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid agentClusterId, Exception exception);

    #endregion

    #region Communications Service Operations (11000-11999)

    [LoggerMessage(
        EventId = 11001,
        Level = LogLevel.Error,
        Message = "Agent cluster id not found for tenant {TenantId}")]
    public static partial void CommunicationsAgentClusterNotFound(
        this Microsoft.Extensions.Logging.ILogger logger, Guid tenantId);

    [LoggerMessage(
        EventId = 11002,
        Level = LogLevel.Warning,
        Message = "Message transport type {Type} not implemented")]
    public static partial void CommunicationsTransportNotImplemented(
        this Microsoft.Extensions.Logging.ILogger logger, string type);

    [LoggerMessage(
        EventId = 11003,
        Level = LogLevel.Error,
        Message = "Unknown message transport type: {Type}")]
    public static partial void CommunicationsUnknownTransportType(
        this Microsoft.Extensions.Logging.ILogger logger, string type);

    [LoggerMessage(
        EventId = 11004,
        Level = LogLevel.Error,
        Message = "Error creating direct message ({ErrorType})")]
    public static partial void CommunicationsCreateDirectError(
        this Microsoft.Extensions.Logging.ILogger logger, string errorType);

    [LoggerMessage(
        EventId = 11005,
        Level = LogLevel.Warning,
        Message = "Agent cluster id {AgentClusterId} not found")]
    public static partial void CommunicationsAgentClusterIdNotFound(
        this Microsoft.Extensions.Logging.ILogger logger, Guid agentClusterId);

    [LoggerMessage(
        EventId = 11006,
        Level = LogLevel.Warning,
        Message = "Message {MessageId} not found for agent {AgentClusterId}")]
    public static partial void CommunicationsMessageNotFound(
        this Microsoft.Extensions.Logging.ILogger logger, Guid messageId, Guid agentClusterId);

    [LoggerMessage(
        EventId = 11007,
        Level = LogLevel.Error,
        Message = "Error updating message {MessageId}")]
    public static partial void CommunicationsUpdateError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid messageId, Exception exception);

    #endregion

    #region Community Operations (12000-12999)

    [LoggerMessage(
        EventId = 12001,
        Level = LogLevel.Warning,
        Message = "Credential with Id {CredentialId} not found")]
    public static partial void CommunityCredentialNotFound(
        this Microsoft.Extensions.Logging.ILogger logger, Guid credentialId);

    [LoggerMessage(
        EventId = 12002,
        Level = LogLevel.Warning,
        Message = "Community identity type with Id {TypeId} not found")]
    public static partial void CommunityIdentityTypeNotFound(
        this Microsoft.Extensions.Logging.ILogger logger, Guid typeId);

    [LoggerMessage(
        EventId = 12003,
        Level = LogLevel.Information,
        Message = "Community identity created successfully for Credential {CredentialId}")]
    public static partial void CommunityIdentityCreated(
        this Microsoft.Extensions.Logging.ILogger logger, Guid credentialId);

    [LoggerMessage(
        EventId = 12004,
        Level = LogLevel.Error,
        Message = "Error creating community identity for Credential {CredentialId}")]
    public static partial void CommunityIdentityCreationError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid credentialId, Exception exception);

    [LoggerMessage(
        EventId = 12005,
        Level = LogLevel.Warning,
        Message = "Community identity with Id {Id} not found")]
    public static partial void CommunityIdentityNotFound(
        this Microsoft.Extensions.Logging.ILogger logger, Guid id);

    [LoggerMessage(
        EventId = 12006,
        Level = LogLevel.Information,
        Message = "Community identity {Id} updated successfully")]
    public static partial void CommunityIdentityUpdated(
        this Microsoft.Extensions.Logging.ILogger logger, Guid id);

    [LoggerMessage(
        EventId = 12007,
        Level = LogLevel.Error,
        Message = "Error updating community identity {Id}")]
    public static partial void CommunityIdentityUpdateError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid id, Exception exception);

    [LoggerMessage(
        EventId = 12008,
        Level = LogLevel.Warning,
        Message = "Connection type with Id {TypeId} not found")]
    public static partial void CommunityConnectionTypeNotFound(
        this Microsoft.Extensions.Logging.ILogger logger, Guid typeId);

    [LoggerMessage(
        EventId = 12009,
        Level = LogLevel.Information,
        Message = "No connections found for community identity {IdentityId}")]
    public static partial void CommunityNoConnectionsFound(
        this Microsoft.Extensions.Logging.ILogger logger, Guid identityId);

    [LoggerMessage(
        EventId = 12010,
        Level = LogLevel.Information,
        Message = "Retrieved {Count} connections for community identity {IdentityId}")]
    public static partial void CommunityConnectionsRetrieved(
        this Microsoft.Extensions.Logging.ILogger logger, int count, Guid identityId);

    [LoggerMessage(
        EventId = 12011,
        Level = LogLevel.Error,
        Message = "Error retrieving connections for community identity {IdentityId}")]
    public static partial void CommunityConnectionsError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid identityId, Exception exception);

    [LoggerMessage(
        EventId = 12012,
        Level = LogLevel.Information,
        Message = "Connection {ConnectionId} created between source {SourceId} and target {TargetId}")]
    public static partial void CommunityConnectionCreated(
        this Microsoft.Extensions.Logging.ILogger logger, Guid connectionId, Guid sourceId, Guid targetId);

    [LoggerMessage(
        EventId = 12013,
        Level = LogLevel.Error,
        Message = "Error creating connection between source {SourceId} and target {TargetId}")]
    public static partial void CommunityConnectionCreateError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid sourceId, Guid targetId, Exception exception);

    [LoggerMessage(
        EventId = 12014,
        Level = LogLevel.Information,
        Message = "Connection {ConnectionId} deleted successfully")]
    public static partial void CommunityConnectionDeleted(
        this Microsoft.Extensions.Logging.ILogger logger, Guid connectionId);

    [LoggerMessage(
        EventId = 12015,
        Level = LogLevel.Error,
        Message = "Error deleting connection {ConnectionId}")]
    public static partial void CommunityConnectionDeleteError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid connectionId, Exception exception);

    [LoggerMessage(
        EventId = 12016,
        Level = LogLevel.Information,
        Message = "Retrieved {Count} feed items for identity {IdentityId}")]
    public static partial void CommunityFeedRetrieved(
        this Microsoft.Extensions.Logging.ILogger logger, int count, Guid identityId);

    [LoggerMessage(
        EventId = 12017,
        Level = LogLevel.Error,
        Message = "Error generating feed for identity {IdentityId}")]
    public static partial void CommunityFeedError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid identityId, Exception exception);

    [LoggerMessage(
        EventId = 12018,
        Level = LogLevel.Information,
        Message = "Notification {NotificationId} created for recipient {RecipientId}")]
    public static partial void CommunityNotificationCreated(
        this Microsoft.Extensions.Logging.ILogger logger, Guid notificationId, Guid recipientId);

    [LoggerMessage(
        EventId = 12019,
        Level = LogLevel.Error,
        Message = "Error creating notification for recipient {RecipientId}")]
    public static partial void CommunityNotificationCreateError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid recipientId, Exception exception);

    [LoggerMessage(
        EventId = 12020,
        Level = LogLevel.Information,
        Message = "{Count} notifications marked as read")]
    public static partial void CommunityNotificationsMarkedRead(
        this Microsoft.Extensions.Logging.ILogger logger, int count);

    [LoggerMessage(
        EventId = 12021,
        Level = LogLevel.Error,
        Message = "Error marking notifications as read")]
    public static partial void CommunityNotificationsMarkReadError(
        this Microsoft.Extensions.Logging.ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 12022,
        Level = LogLevel.Information,
        Message = "Retrieved {Count} notifications for identity {IdentityId}")]
    public static partial void CommunityNotificationsRetrieved(
        this Microsoft.Extensions.Logging.ILogger logger, int count, Guid identityId);

    [LoggerMessage(
        EventId = 12023,
        Level = LogLevel.Error,
        Message = "Error retrieving notifications for identity {IdentityId}")]
    public static partial void CommunityNotificationsError(
        this Microsoft.Extensions.Logging.ILogger logger, Guid identityId, Exception exception);

    #endregion
}
