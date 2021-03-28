namespace XFramework.Identity.Shared.Entity.ViewModels.Wallets
{
    public class WalletTransactionVm
    {
        public string ID { get; set; }
        public string User { get; set; }
        public string Amount { get; set; }
        public string Remarks { get; set; }
        public string RunningBalance { get; set; }
        public string Description { get; set; }
        public string CreatedAt { get; set; }
        public string ModifiedAt { get; set; }
    }
}