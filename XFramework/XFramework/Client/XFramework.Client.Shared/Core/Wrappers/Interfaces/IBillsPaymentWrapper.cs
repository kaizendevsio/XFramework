namespace XFramework.Client.Shared.Core.Wrappers.Interfaces
{
    public interface IBillsPaymentWrapper
    {
        public Task<QueryResponseBO<List<BillsPaymentTransactionContract>>> TransactionHistory(GetTransactionsRequest request);
        public Task<CmdResponseBO> Transact(BillsTransactRequest request);
        public Task<List<BillerCategoryContract>> GetMerchantList();
        public Task<BillerCategoryContract> GetMerchant();
    }
}