using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Generic.Abstractions;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Contracts.Base;
using XFramework.Integration.Extensions;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Drivers;

public abstract class BaseSignalRHandler
{
    
    public async Task<HttpStatusCode> RespondToInvoke<TResult>(HubConnection connection, Guid requestId, Guid clientId, TResult data) 
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
        logger.LogInformation("[{Caller}] Request received, Invoking '{Request}'", nameof(StreamflowRequestHandler), GetType().Name);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var internalMediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var r = response.Data.AsMediatorCmd<TQuery, TResponse>();
            var result = await internalMediator.Send(r).ConfigureAwait(false);

            if (result.HttpStatusCode is HttpStatusCode.InternalServerError)
            {
                logger.LogInformation("[{Caller}] Invoking {Request}' resulted in exception: {Message}", nameof(StreamflowRequestHandler), GetType().Name, result.Message);
            }
            else
            {
                logger.LogInformation("[{Caller}] Invoking {Request}' returned {HttpStatusCode}", nameof(StreamflowRequestHandler), GetType().Name, result.HttpStatusCode);

            }

            await RespondToInvoke(connection, response.RequestId, response.ClientId, result);
        }
        catch (Exception e)
        {
            logger.LogInformation("[{Caller}] Invoking {Request}' resulted in exception: {Message}", nameof(StreamflowRequestHandler), GetType().Name, e.Message);
        }
    }
}