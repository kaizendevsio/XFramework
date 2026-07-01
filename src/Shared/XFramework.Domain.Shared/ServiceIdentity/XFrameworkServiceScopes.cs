namespace XFramework.Domain.Shared.ServiceIdentity;

public static class XFrameworkServiceScopes
{
    public const string BoltService = "bolt.service";
    public const string DataContextQuery = "datacontext.query";
    public const string DataContextMutate = "datacontext.mutate";

    public const string AttendanceAdmin = "attendance.admin";
    public const string CommunicationsAdmin = "communications.admin";
    public const string CommunicationsChat = "communications.chat";
    public const string CommunityAdmin = "community.admin";
    public const string IdentityAdmin = "identity.admin";
    public const string InventarioAdmin = "inventario.admin";
    public const string NotificationsSend = "notifications.send";
    public const string SmsGatewaySend = "smsgateway.send";
    public const string StorageRead = "storage.read";
    public const string StorageWrite = "storage.write";
    public const string WalletsAdmin = "wallets.admin";

    public static readonly IReadOnlyList<string> AdminDefaults =
    [
        BoltService,
        DataContextQuery,
        DataContextMutate,
        AttendanceAdmin,
        CommunicationsAdmin,
        CommunicationsChat,
        CommunityAdmin,
        IdentityAdmin,
        InventarioAdmin,
        NotificationsSend,
        SmsGatewaySend,
        StorageRead,
        StorageWrite,
        WalletsAdmin
    ];
}
