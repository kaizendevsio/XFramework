using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using StreamFlow.Domain.Generic.Abstractions;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Contracts.Base;
using XFramework.Integration.Extensions;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Drivers;

public abstract class BaseSignalRHandler
{
    
    public async Task<HttpStatusCode> RespondToInvoke<TResult>(HubConnection connection, Guid requestId, string clientId, TResult data) 
        where TResult : class, IBaseResponse, IHasRequestServer
    {
        var request = new StreamFlowMessage<TResult>(data)
        {
            RequestId = requestId,
            RecipientId = clientId,
            ExchangeType = MessageExchangeType.Direct,
            ResponseStatusCode = data.HttpStatusCode,
            CommandName = nameof(IStreamFlow.InvokeResponseHandler)
        };
        
        return await connection.InvokeAsync<HttpStatusCode>(nameof(IStreamFlow.Push), request);
    }

    protected virtual void HandleRequestQuery<TQuery, TResponse>(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory)
        where TResponse : class
        where TQuery : class, IRequest<QueryResponse<TResponse>>, IHasRequestServer
    {
        logger.LogInformation("Registering streamflow handler for {HandlerName}", typeof(TQuery).GetTypeFullName());
        connection.On(typeof(TQuery).GetTypeFullName(), (StreamFlowMessage<TQuery> response) => StreamflowRequestHandler<TQuery, QueryResponse<TResponse>>(response, connection, mediator, logger, scopeFactory).ConfigureAwait(false));
    }
   
    protected virtual void HandleRequestCmd<TCmd>(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory) 
        where TCmd : class, IRequest<CmdResponse>, IHasRequestServer
    {
        logger.LogInformation("Registering streamflow handler for {HandlerName}", typeof(TCmd).GetTypeFullName());
        connection.On(typeof(TCmd).GetTypeFullName(), (StreamFlowMessage<TCmd> response) => StreamflowRequestHandler<TCmd, CmdResponse>(response, connection, mediator, logger, scopeFactory).ConfigureAwait(false));
    }
    
    protected virtual void HandleRequestCmd<TCmd, TResponse>(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory) 
        where TResponse : class
        where TCmd : class, IRequest<CmdResponse<TResponse>>, IHasRequestServer
    {
        logger.LogInformation("Registering streamflow handler for {HandlerName}", typeof(TCmd).GetTypeFullName());
        connection.On(typeof(TCmd).GetTypeFullName(), (StreamFlowMessage<TCmd> response) => StreamflowRequestHandler<TCmd, CmdResponse<TResponse>>(response, connection, mediator, logger, scopeFactory).ConfigureAwait(false));
    }

    private async Task StreamflowRequestHandler<TQuery, TResponse>(StreamFlowMessage<TQuery> response, HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory) 
        where TQuery : class, IRequest<TResponse>, IHasRequestServer
        where TResponse : class, IBaseResponse, IHasRequestServer
    {

        try
        {
            using var scope = scopeFactory.CreateScope();
            var internalMediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var r = response.Data.AsMediatorCmd<TQuery, TResponse>();

            using (LogContext.PushProperty("TenantId", r.Metadata?.TenantId))
            {
                using (LogContext.PushProperty("RequestId", r.Metadata?.RequestId))
                {
                    // Offload logging to a background task
                    _ = Task.Run(() =>
                    {
                        if (!logger.IsEnabled(LogLevel.Information)) return;

                        var requestData = JsonSerializer.Serialize(r, new JsonSerializerOptions {ReferenceHandler = ReferenceHandler.IgnoreCycles});
                        logger.LogInformation("[{Caller}] Request received, Invoking '{Request}' with data: {Data}", nameof(StreamflowRequestHandler), GetType().Name, requestData);
                    });

                    var result = await internalMediator.Send(r).ConfigureAwait(false);

                    // Log result in a background task
                    _ = Task.Run(() =>
                    {
                        if (!logger.IsEnabled(LogLevel.Information)) return;

                        var resultData = JsonSerializer.Serialize(result, new JsonSerializerOptions {ReferenceHandler = ReferenceHandler.IgnoreCycles});

                        if (result.HttpStatusCode is HttpStatusCode.InternalServerError)
                        {
                            logger.LogInformation("[{Caller}] Invoking {Request}' resulted in exception: {Message}; {Data}", nameof(StreamflowRequestHandler), GetType().Name, result.Message, resultData);
                        }
                        else
                        {
                            logger.LogInformation("[{Caller}] Invoking {Request}' returned {HttpStatusCode}; {Data}", nameof(StreamflowRequestHandler), GetType().Name, result.HttpStatusCode, resultData);
                        }
                    });

                    await RespondToInvoke(connection, response.RequestId, response.ClientId, result);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogInformation("[{Caller}] Invoking {Request}' resulted in exception: {Message}; {StackTrace}",
                nameof(StreamflowRequestHandler), GetType().Name, e.Message, e.StackTrace);
        }
    }
}