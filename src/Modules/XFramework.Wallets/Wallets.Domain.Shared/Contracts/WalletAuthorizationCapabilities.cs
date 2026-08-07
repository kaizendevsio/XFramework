namespace Wallets.Domain.Shared.Contracts;

public static class WalletAuthorizationCapabilities
{
    public const string View = "wallets:view";
    public const string Update = "wallets:update";
    public const string Manage = "wallets:manage";
    public const string ReportingView = "wallets.reporting:view";
    public const string PolicyManage = "wallets.policy:manage";
    public const string ReconciliationManage = "wallets.reconciliation:manage";
    public const string WebhooksManage = "wallets.webhooks:manage";

    public static IReadOnlyList<string> All { get; } =
    [
        View,
        Update,
        Manage,
        ReportingView,
        PolicyManage,
        ReconciliationManage,
        WebhooksManage
    ];
}
