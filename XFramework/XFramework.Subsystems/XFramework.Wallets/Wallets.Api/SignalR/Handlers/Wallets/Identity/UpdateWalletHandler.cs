using System;
using System.Text.Json;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using Wallets.Domain.Generic.Contracts.Requests;
using Wallets.Domain.Generic.Contracts.Requests.Update;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Services.Helpers;

namespace Wallets.Api.SignalR.Handlers.Wallets.Identity;

public class UpdateWalletHandler  : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequest<UpdateWalletRequest, UpdateWalletCmd>(connection, mediator);
    }
}