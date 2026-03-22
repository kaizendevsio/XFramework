using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using StreamFlow.Domain.Shared.Abstractions;
using StreamFlow.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Extensions;
using XFramework.Integration.Services;
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
        
        return await connection.InvokeAsync<HttpStatusCode>(nameof(IStreamFlow.InvokeResponse), request);
    }

    protected virtual void HandleRequestQuery<TQuery, TResponse>(HubConnection connection, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory)
        where TResponse : class
        where TQuery : class, IQuery<QueryResponse<TResponse>>, IHasRequestServer
    {
        logger.LogInformation("Registering streamflow handler for {HandlerName}", typeof(TQuery).GetTypeFullName());
        connection.On(typeof(TQuery).GetTypeFullName(), (StreamFlowMessage<TQuery> response) => StreamflowRequestHandler<TQuery, QueryResponse<TResponse>>(response, connection, logger, scopeFactory).ConfigureAwait(false));
    }
   
    protected virtual void HandleRequestCmd<TCmd>(HubConnection connection, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory)
        where TCmd : class, ICommand<CmdResponse>, IHasRequestServer
    {
        logger.LogInformation("Registering streamflow handler for {HandlerName}", typeof(TCmd).GetTypeFullName());
        connection.On(typeof(TCmd).GetTypeFullName(), (StreamFlowMessage<TCmd> response) => StreamflowRequestHandler<TCmd, CmdResponse>(response, connection, logger, scopeFactory).ConfigureAwait(false));
    }
    
    protected virtual void HandleRequestCmd<TCmd, TResponse>(HubConnection connection, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory)
        where TCmd : class, ICommand<CmdResponse<TResponse>>, IHasRequestServer
    {
        logger.LogInformation("Registering streamflow handler for {HandlerName}", typeof(TCmd).GetTypeFullName());
        connection.On(typeof(TCmd).GetTypeFullName(), (StreamFlowMessage<TCmd> response) => StreamflowRequestHandler<TCmd, CmdResponse<TResponse>>(response, connection, logger, scopeFactory).ConfigureAwait(false));
    }

    private async Task StreamflowRequestHandler<TRequest, TResponse>(StreamFlowMessage<TRequest> response, HubConnection connection, ILogger<BaseSignalRHandler> logger, IServiceScopeFactory scopeFactory)
        where TRequest : class, IHasRequestServer
        where TResponse : class, IBaseResponse, IHasRequestServer
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            // NOTE: CQRS infrastructure removed - this handler is deprecated
            // Direct service injection should be used instead in VSA
            var r = response.Data.AsCommandQuery<TRequest>();

            using (LogContext.PushProperty(nameof(RequestMetadata.SessionId), r.Metadata?.SessionId))
            {
                using (LogContext.PushProperty(nameof(RequestMetadata.TenantId), r.Metadata?.TenantId))
                {
                    using (LogContext.PushProperty(nameof(RequestMetadata.RequestId), r.Metadata?.RequestId))
                    {
                        logger.LogWarning("[{Caller}] CQRS dispatcher removed - handler is deprecated. Request type: {RequestType}",
                            nameof(StreamflowRequestHandler), typeof(TRequest).Name);
                        
                        // Return not implemented response
                        var result = Activator.CreateInstance<TResponse>();
                        if (result is IBaseResponse baseResponse)
                        {
                            baseResponse.HttpStatusCode = HttpStatusCode.NotImplemented;
                            baseResponse.Message = "CQRS infrastructure has been removed. Please use direct service injection.";
                        }

                        await RespondToInvoke(connection, response.RequestId, response.ClientId, result);
                        response.Dispose();
                    }
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