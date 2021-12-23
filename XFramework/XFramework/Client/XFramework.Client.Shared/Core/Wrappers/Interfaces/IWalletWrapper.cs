namespace XFramework.Client.Shared.Core.Wrappers.Interfaces
{
    public interface IWalletWrapper
    {
        
        public Task<List<GetWalletContract>> GetWalletList();
        
        public Task<CmdResponseBO> Create(CreateWalletRequest request, GetWalletContract wallet);
        public Task<QueryResponseBO<GetWalletContract>> Get(GetWalletRequest request, GetWalletContract wallet );
        public Task<QueryResponseBO<List<GetWalletContract>>> GetList(GetAllWalletRequest request, GetWalletContract wallet);
        public Task<CmdResponseBO> Update(UpdateWalletRequest request, GetWalletContract wallet);
        public Task<CmdResponseBO> Delete(DeleteWalletRequest request, GetWalletContract wallet);
    }
}