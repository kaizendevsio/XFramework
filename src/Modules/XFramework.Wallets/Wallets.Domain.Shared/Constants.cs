namespace Wallets.Domain.Shared;

public static class Constants
{
    public static class Currency
    {
        public static readonly Guid Php = new Guid("7ee3621a-5878-4c16-8112-eab11f29db95");
    }

    public static class WalletEvents
    {
        public static readonly string WalletUpdated = nameof(WalletUpdated);
        public static readonly string WalletCreated = nameof(WalletCreated);
    }
}