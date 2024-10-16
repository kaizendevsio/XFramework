﻿using XFramework.Blazor.Core.Features.Wallet;

namespace XFramework.Blazor.Core.Features.Session;

public partial class SessionState
{
    public record Logout : StateAction
    {
        public string NavigateTo { get; set; }
        public bool ResetAllStates { get; set; } = true;
    }
    
    protected class LogoutHandler(
        HandlerServices handlerServices,
        IStore store)
        : StateActionHandler<Logout>(handlerServices, store)
    {
        public SessionState CurrentState => Store.GetState<SessionState>();

        public override async Task Handle(Logout action, CancellationToken aCancellationToken)
        {
            if (action.ResetAllStates)
            {
                //IndexedDbService.Database.StateCache.Clear();
                //await IndexedDbService.Database.SaveChanges();
                Store.Reset();
            }
            else
            {
                await IndexedDbService.RemoveItem("SessionState");
                await IndexedDbService.RemoveItem("WalletState");

                await Mediator.Send(new ClearState()
                {
                    ContactList = [],
                    Credential = new(),
                    Identity = new()
                });
                await Mediator.Send(new SetState()
                {
                    State = CurrentSessionState.Inactive,
                    LoginVm = new(),
                    RegisterVm = new(),
                    ResetPasswordVm = new()
                });
                
                await Mediator.Send(new WalletState.ClearState
                {
                    WalletList = []
                });
            }

            await Mediator.Send(new ApplicationState.SetState() {StateRestored = true});
            
            if (string.IsNullOrEmpty(action.NavigateTo))
            {
                action.NavigateTo = "/";
            }
            NavigationManager.NavigateTo(action.NavigateTo);
            return;
        }
    }
}