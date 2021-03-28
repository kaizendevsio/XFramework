namespace XFramework.Identity.Shared.Entity.ViewModels.Wallets
{
    public class WalletVm
    {
        public int ID { get; set; }
        public string WalletName { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string Currency { get; set; }
        public string CreatedAt { get; set; }
        public string ModifiedAt { get; set; }
    }
}