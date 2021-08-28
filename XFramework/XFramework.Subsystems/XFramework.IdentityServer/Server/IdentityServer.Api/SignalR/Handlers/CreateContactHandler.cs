using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Contacts;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Services.Helpers;

namespace IdentityServer.Api.SignalR.Handlers
{
    public class CreateContactHandler : BaseSignalRHandler, ISignalREventHandler
    {
        public void Handle(HubConnection connection, IMediator mediator)
        {
            Console.WriteLine($"{GetType().Name} Initialized");
            connection.On<string, string, string>(GetType().Name.Replace("Handler", string.Empty),
                async (data, message, telemetryString) =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            StopWatch.Start();
                            var telemetry = JsonSerializer.Deserialize<StreamFlowTelemetryBO>(telemetryString);
                    
                            var r = data.AsMediatorCmd<CreateContactRequest, CreateContactCmd>();
                            var result = await mediator.Send(r).ConfigureAwait(false);
                            StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' returned {result.HttpStatusCode.ToString()}");

                            await RespondToInvoke(connection, telemetry, result);
                        }
                        catch (Exception e)
                        {
                            StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception: [{e.Message}]");
                        }
                    });
                });
        }
    }
}