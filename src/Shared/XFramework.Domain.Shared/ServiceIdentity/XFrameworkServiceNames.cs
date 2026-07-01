namespace XFramework.Domain.Shared.ServiceIdentity;

public static class XFrameworkServiceNames
{
    public const string Attendance = "XFramework.Attendance";
    public const string BoltHub = "XFramework.Bolt.Hub";
    public const string Coins = "XFramework.Coins";
    public const string Communications = "XFramework.Communications";
    public const string Community = "XFramework.Community";
    public const string ControlPanel = "XFramework.ControlPanel";
    public const string Gateway = "XFramework.Gateway";
    public const string IdentityServer = "XFramework.IdentityServer";
    public const string Inventario = "XFramework.Inventario";
    public const string Notifications = "XFramework.Notifications";
    public const string OperationsDashboard = "XFramework.Operations.Dashboard";
    public const string Payments = "XFramework.Payments";
    public const string Pos = "XFramework.POS";
    public const string SmsGateway = "XFramework.SmsGateway";
    public const string Storage = "XFramework.Storage";
    public const string Wallets = "XFramework.Wallets";

    public static readonly IReadOnlyList<string> All =
    [
        Attendance,
        BoltHub,
        Coins,
        Communications,
        Community,
        ControlPanel,
        Gateway,
        IdentityServer,
        Inventario,
        Notifications,
        OperationsDashboard,
        Payments,
        Pos,
        SmsGateway,
        Storage,
        Wallets
    ];
}
