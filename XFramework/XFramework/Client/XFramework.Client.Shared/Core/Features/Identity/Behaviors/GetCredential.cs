﻿using IdentityServer.Integration.Drivers;
using XFramework.Domain.Generic.Contracts;

namespace XFramework.Client.Shared.Core.Features.Identity;

public partial class IdentityState
{
    public record GetCredential(Guid Id) : StateAction;

    protected class GetCredentialHandler(
        IIdentityServerServiceWrapper identityServerServiceWrapper,
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<GetCredential>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();

        public override async Task Handle(GetCredential request, CancellationToken cancellationToken)
        {
            var response = await identityServerServiceWrapper.IdentityCredential.Get(request.Id);
            if (await HandleFailure(response, request)) return;
            await HandleSuccess(response, request, true);
            
            CurrentState.Credential = response.Response;
            Store.SetState(CurrentState);
        }
    }
}
