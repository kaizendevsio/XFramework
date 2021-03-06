﻿using System;
using System.Text.Json;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;
using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Services.Helpers;

namespace IdentityServer.Api.SignalR.Handlers
{
    public class AuthenticateHandler : BaseSignalRHandler, ISignalREventHandler
    {
        public void Handle(HubConnection connection, IMediator mediator)
        {
            Console.WriteLine($"{GetType().Name} Initialized");
          connection.On<string,string,string>(GetType().Name.Replace("Handler", string.Empty),
                async (data,message,telemetryString) =>
                {
                    StopWatch.Start();
                    try
                    {
                        var telemetry = JsonSerializer.Deserialize<StreamFlowTelemetryBO>(telemetryString);
                        
                        var r = data.AsMediatorCmd<AuthenticateCredentialRequest, AuthenticateCredentialQuery>();
                        var result = await mediator.Send(r).ConfigureAwait(false);
                        StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' returned {result.HttpStatusCode.ToString()}"); 
                        
                        await RespondToInvoke(connection, telemetry, result.Adapt<QueryResponseBO<AuthorizeIdentityContract>>());
                    }
                    catch (Exception e)
                    {
                        StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception: [{e.Message}]"); 
                    }
                 
                });
        }
    }
}