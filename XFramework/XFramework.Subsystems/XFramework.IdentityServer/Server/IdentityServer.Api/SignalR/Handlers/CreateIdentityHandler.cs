using System;
using System.Text.Json;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using XFramework.Integration.Services.Helpers;

namespace IdentityServer.Api.SignalR.Handlers
{
    public class CreateIdentityHandler : BaseSignalRHandler, ISignalREventHandler
    {
       public void Handle(HubConnection connection, IMediator mediator)
        {
            connection.On<string,string,StreamFlowTelemetryBO>(GetType().Name.Replace("Handler", string.Empty),
                async (data,message,telemetry) =>
                {
                    StopWatch.Start();
                    try
                    {
                        var r = data.AsMediatorCmd<CreateIdentityRequest, CreateIdentityCmd>();
                        var result = await mediator.Send(r).ConfigureAwait(false);
                        StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' returned {result.HttpStatusCode.ToString()}"); }
                    catch (Exception e)
                    {
                        StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception {e.Message}"); 
                    }
                 
                });
        }
    }
}