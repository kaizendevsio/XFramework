﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;
using Wallets.Domain.Generic.Contracts.Requests;
using Wallets.Domain.Generic.Contracts.Requests.Wallet.Identity;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Services.Helpers;

namespace Wallets.Api.SignalR.Handlers.Wallets.Identity
{
    public class GetIdentityWalletHandler  : BaseSignalRHandler, ISignalREventHandler
    {
        public void Handle(HubConnection connection, IMediator mediator)
        {
            Console.WriteLine($"{GetType().Name} Initialized");
            connection.On<string, string, string>(GetType().Name.Replace("Handler", string.Empty),
                async (data, message, telemetryString) =>
                {
                    StopWatch.Start();
                    try
                    {
                        var telemetry = JsonSerializer.Deserialize<StreamFlowTelemetryBO>(telemetryString);
                        var r = data.AsMediatorCmd<GetIdentityWalletRequest, GetIdentityWalletQuery>();
                        var result = await mediator.Send(r).ConfigureAwait(false);
                        StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' returned {result.HttpStatusCode.ToString()}");

                        await RespondToInvoke(connection, telemetry, result.Adapt<QueryResponseBO<GetIdentityWalletContract>>());
                    }
                    catch (Exception e)
                    {
                        StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception: [{e.Message}]");
                    }

                });
        }
    }
}