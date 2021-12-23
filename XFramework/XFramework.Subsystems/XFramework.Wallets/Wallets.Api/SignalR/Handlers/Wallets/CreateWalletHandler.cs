using System;
using System.Text.Json;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using Wallets.Domain.Generic.Contracts.Requests;
using Wallets.Domain.Generic.Contracts.Requests.Wallet;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Services.Helpers;

namespace Wallets.Api.SignalR.Handlers.Wallets
{
    public class CreateWalletHandler  : BaseSignalRHandler, ISignalREventHandler
    {
        public void Handle(HubConnection connection, IMediator mediator)
        {
            Console.WriteLine($"{GetType().Name} Initialized");
            connection.On<string, string, string>(GetType().Name.Replace("Handler", string.Empty),
                async (data, message, telemetryString) =>
                {
                    Task.Run(async () =>
                    {
                        StopWatch.Start();
                        try
                        {
                            var telemetry = JsonSerializer.Deserialize<StreamFlowTelemetryBO>(telemetryString);
                            var r = data.AsMediatorCmd<CreateWalletRequest, CreateWalletCmd>();
                            var result = await mediator.Send(r).ConfigureAwait(false);
                            StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' returned {result.HttpStatusCode.ToString()}");

                            await RespondToInvoke(connection, telemetry, result.Adapt<CmdResponseBO>());
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