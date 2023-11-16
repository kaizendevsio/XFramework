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

    protected virtual void HandleRequestQuery<TQuery, TResponse>(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, MetricsMonitor metricsMonitor, IServiceScopeFactory scopeFactory)
        where TResponse : class
        where TQuery : class, IRequest<QueryResponse<TResponse>>, IHasRequestServer
    {
        logger.LogInformation("Registering streamflow handler for {HandlerName}", typeof(TQuery).GetTypeFullName());
        connection.On(typeof(TQuery).GetTypeFullName(), (StreamFlowMessage<TQuery> response) => Handler<TQuery, QueryResponse<TResponse>>(response, connection, mediator, metricsMonitor, scopeFactory).ConfigureAwait(false));
    }
    
    protected virtual void HandleRequestCmd<TCmd, TResponse>(HubConnection connection, IMediator mediator, ILogger<BaseSignalRHandler> logger, MetricsMonitor metricsMonitor, IServiceScopeFactory scopeFactory) 
        where TResponse : class
        where TCmd : class, IRequest<CmdResponse<TResponse>>, IHasRequestServer
    {
        logger.LogInformation("Registering streamflow handler for {HandlerName}", typeof(TCmd).GetTypeFullName());
        connection.On(typeof(TCmd).GetTypeFullName(), (StreamFlowMessage<TCmd> response) => Handler<TCmd, CmdResponse<TResponse>>(response, connection, mediator, metricsMonitor, scopeFactory).ConfigureAwait(false));
    }

    private async Task Handler<TQuery, TResponse>(StreamFlowMessage<TQuery> response, HubConnection connection, IMediator mediator, MetricsMonitor metricsMonitor, IServiceScopeFactory scopeFactory) 
        where TQuery : class, IRequest<TResponse>, IHasRequestServer
        where TResponse : class, IBaseResponse, IHasRequestServer
    {
        using var metricLogger = metricsMonitor.Start();
        try
        {
            using var scope = scopeFactory.CreateScope();
            var internalMediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var r = response.Data.AsMediatorCmd<TQuery, TResponse>();
            var result = await internalMediator.Send(r).ConfigureAwait(false);

            if (result.HttpStatusCode is HttpStatusCode.InternalServerError)
            {
                metricLogger.Failed($"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception: [{result.Message}]");
            }
            else
            {
                metricLogger.Completed($"[{DateTime.Now}] Invoked '{GetType().Name}' returned {result.HttpStatusCode}");
            }

            await RespondToInvoke(connection, response.RequestId, response.ClientId, result);
        }
        catch (Exception e)
        {
            metricLogger.Failed($"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception: [{e.Message}]");
        }
    }
}