using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Responses;

namespace XFramework.Blazor.Core.Features.Identity;

public partial class IdentityState
{
    public record SetState : StateAction
    {
        public IdentityCredential? Credential { get; set; }
        public IdentityInformation? Identity { get; set; }
        public List<IdentityAddress>? Addresses { get; set; }
        public List<IdentityRole>? Roles { get; set; }
        public List<IdentityContact>? Contacts { get; set; }
        public List<Domain.Shared.Contracts.Wallet>? Wallets { get; set; }
        public IdentityContact? SelectedContact { get; set; }
        public PaginatedResult<IdentityCredential>? CredentialList { get; set; }
    }
    
    protected class SetStateHandler(HandlerServices handlerServices, IStore store)
        : StateActionHandler<SetState>(handlerServices, store)
    {
        private IdentityState CurrentState => Store.GetState<IdentityState>();
        
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
