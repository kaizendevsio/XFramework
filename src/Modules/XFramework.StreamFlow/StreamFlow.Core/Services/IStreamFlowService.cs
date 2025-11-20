using Microsoft.AspNetCore.SignalR;
using StreamFlow.Domain.Shared.Contracts.Requests;
using StreamFlow.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace StreamFlow.Core.Services;

/// <summary>
/// Service interface for StreamFlow SignalR messaging operations.
/// Provides methods for client registration, message pushing, method invocation, and message dequeuing.
/// Replaces MediatR handlers with direct service calls for better performance and clarity.
/// </summary>
public interface IStreamFlowService
{
    /// <summary>
    /// Pushes a message to StreamFlow clients based on exchange type (FanOut, Direct, Topic).
    /// Handles load balancing, offline queueing, and message delivery.
    /// </summary>
    /// <param name="message">The message to push</param>
    /// <param name="context">SignalR hub caller context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure with appropriate status code</returns>
    Task<Result> PushMessageAsync(
        StreamFlowMessage message,
        HubCallerContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a StreamFlow client for message delivery.
    /// Adds client to tracking dictionaries and remembers client for reconnection.
    /// </summary>
    /// <param name="client">The client information to register</param>
    /// <param name="context">SignalR hub caller context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure with appropriate status code</returns>
    Task<Result> RegisterClientAsync(
        StreamFlowClient client,
        HubCallerContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a method on a target client and waits for response.
    /// Uses method call channel for request-response pattern.
    /// </summary>
    /// <param name="message">The method invocation message</param>
    /// <param name="context">SignalR hub caller context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the invoke response or failure</returns>
    Task<Result<StreamFlowInvokeResponse>> InvokeMethodAsync(
        StreamFlowMessage message,
        HubCallerContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the response to a method invocation.
    /// Completes the waiting TaskCompletionSource for the original invoke call.
    /// </summary>
    /// <param name="message">The method response message</param>
    /// <param name="context">SignalR hub caller context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure with appropriate status code</returns>
    Task<Result> InvokeResponseAsync(
        StreamFlowMessage message,
        HubCallerContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues and delivers pending messages for a newly connected client.
    /// Processes messages from the channel-based queue.
    /// </summary>
    /// <param name="client">The client to dequeue messages for</param>
    /// <param name="context">SignalR hub caller context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task DequeueMessagesAsync(
        StreamFlowClient client,
        HubCallerContext context,
        CancellationToken cancellationToken = default);
}