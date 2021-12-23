namespace XFramework.Client.Shared.Core.Wrappers.Interfaces
{
    public interface IIdentityWalletWrapper
    {
        public Task<List<GetIdentityWalletContract>> GetIdentityWalletList();
        
        public Task<CmdResponseBO> Create(CreateIdentityWalletRequest request, GetIdentityWalletContract wallet);
        public Task<QueryResponseBO<GetIdentityWalletContract>> Get(GetIdentityWalletRequest request, GetIdentityWalletContract wallet);
        public Task<QueryResponseBO<List<GetIdentityWalletContract>>> GetList(GetAllIdentityWalletRequest request, GetIdentityWalletContract wallet);
        public Task<CmdResponseBO> Update(UpdateIdentityWalletRequest request, GetIdentityWalletContract wallet);
        public Task<CmdResponseBO> Delete(DeleteIdentityWalletRequest request, GetIdentityWalletContract wallet);
        public Task<CmdResponseBO> Transfer(TransferIdentityWalletRequest request, GetIdentityWalletContract wallet);
        public Task<CmdResponseBO> Increment(IncrementIdentityWalletRequest request, GetIdentityWalletContract wallet);
        public Task<CmdResponseBO> Decrement(DecrementIdentityWalletRequest request, GetIdentityWalletContract wallet);

    }
}