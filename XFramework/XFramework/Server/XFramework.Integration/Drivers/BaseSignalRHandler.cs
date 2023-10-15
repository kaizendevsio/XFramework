using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Contracts.Base;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Drivers;

public class BaseSignalRHandler
{
    public ILogger<BaseSignalRHandler> Logger { get; }
    public MetricsMonitor MetricsMonitor { get; }
    public Guid? Id { get; set; }

    public BaseSignalRHandler(ILogger<BaseSignalRHandler> logger, MetricsMonitor metricsMonitor)
    {
        Logger = logger;
        MetricsMonitor = metricsMonitor;
    }
    
    public async Task<HttpStatusCode> RespondToInvoke<TResult>(HubConnection connection, Guid requestId, Guid clientId, TResult data) 
        where TResult : IBaseResponse, new()
    {
        var request = new StreamFlowMessage<TResult>(data)
        {
            RequestId = requestId,
            RecipientId = clientId,
            ExchangeType = MessageExchangeType.Direct,
            ResponseStatusCode = data.HttpStatusCode,
            CommandName = "InvokeResponseHandler"
        };
        
        return await connection.InvokeAsync<HttpStatusCode>("Push", request);
    }

    protected virtual void HandleRequestQuery<TRequest, TQuery, TResponse>(HubConnection connection, IMediator mediator)
        where TRequest : class, new() 
        where TResponse : new() 
        where TQuery : class, IRequest<QueryResponse<TResponse>>
    {
        connection.On(HandlerName(), (StreamFlowMessage<TRequest> response) => Handler<TRequest, TQuery, QueryResponse<TResponse>>(response, connection, mediator).ConfigureAwait(false));
    }

    protected virtual void HandleRequestCmd<TRequest, TQuery>(HubConnection connection, IMediator mediator)
        where TRequest : class, new() 
        where TQuery : class, IRequest<CmdResponse<TQuery>>
    {
        connection.On(HandlerName(), (StreamFlowMessage<TRequest> response) => Handler<TRequest, TQuery, CmdResponse<TQuery>>(response, connection, mediator).ConfigureAwait(false));
    }
    
    protected virtual void HandleRequestCmd<TRequest, TQuery, TResponse>(HubConnection connection, IMediator mediator) 
        where TRequest : class, new() 
        where TQuery : class, IRequest<CmdResponse<TResponse>>
    {
        connection.On(HandlerName(), (StreamFlowMessage<TRequest> response) => Handler<TRequest, TQuery, CmdResponse<TResponse>>(response, connection, mediator).ConfigureAwait(false));
    }

    protected virtual void HandleVoidRequestCmd<TRequest, TQuery>(HubConnection connection, IMediator mediator) 
        where TRequest : class, new() 
        where TQuery : class, IRequest<CmdResponse>
    {
        connection.On(HandlerName(), (StreamFlowMessage<TRequest> response) => Handler<TRequest, TQuery, CmdResponse>(response, connection, mediator).ConfigureAwait(false));
    }

    private async Task Handler<TRequest, TQuery, TResponse>(StreamFlowMessage<TRequest> response, HubConnection connection, IMediator mediator) 
        where TRequest : class, new() 
        where TQuery : class, IRequest<TResponse> 
        where TResponse : IBaseResponse, new()
    {
        using var metricLogger = MetricsMonitor.Start();
        try
        {
            var r = response.Data.AsMediatorCmd<TRequest, TQuery, TResponse>();
            var result = await mediator.Send(r).ConfigureAwait(false);

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

    private string HandlerName()
    {
        return GetType().Name.Replace("Handler", string.Empty);
    }
}