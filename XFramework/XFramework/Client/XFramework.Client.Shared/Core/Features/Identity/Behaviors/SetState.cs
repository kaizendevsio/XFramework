﻿using XFramework.Domain.Generic.Contracts;
using XFramework.Domain.Generic.Contracts.Responses;

namespace XFramework.Client.Shared.Core.Features.Identity;

public partial class IdentityState
{
    public record SetState : StateAction
    {
        public IdentityCredential? Credential { get; set; }
        public IdentityInformation? Identity { get; set; }
        public List<IdentityAddress>? Addresses { get; set; }
        public List<IdentityRole>? Roles { get; set; }
        public List<IdentityContact>? Contacts { get; set; }
        public List<Domain.Generic.Contracts.Wallet>? Wallets { get; set; }
        public IdentityContact? SelectedContact { get; set; }
        public PaginatedResult<IdentityCredential>? CredentialList { get; set; }
    }
    
    protected class SetStateHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<SetState>(handlerServices, store)
    {
        private SessionState CurrentState => Store.GetState<SessionState>();
        
        public override async Task Handle(SetState state, CancellationToken aCancellationToken)
        {
            try
            {
                StateHelper.SetProperties(state, CurrentState);
                Persist(CurrentState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return;
        }
    }
} 
