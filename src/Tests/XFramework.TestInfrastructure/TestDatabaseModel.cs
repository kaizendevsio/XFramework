namespace XFramework.TestInfrastructure;

/// <summary>
/// Loads the same module model assemblies referenced by the migration runner.
/// Integration fixtures that apply the shared migration set must call this before creating AppDbContext.
/// </summary>
public static class TestDatabaseModel
{
    private static readonly Type[] MigrationModelTypes =
    [
        typeof(XFramework.Domain.Shared.Contracts.MetaData),
        typeof(Bolt.Domain.Shared.Contracts.ServiceDiscovery.BoltServiceManifestRecord),
        typeof(Attendance.Domain.Shared.Contracts.AttendanceContext),
        typeof(Community.Domain.Shared.Contracts.CommunityConnection),
        typeof(Communications.Domain.Shared.Contracts.Message),
        typeof(IdentityServer.Domain.Shared.Contracts.Tenant),
        typeof(Inventario.Domain.Shared.Contracts.InventoryLocation),
        typeof(Notifications.Domain.Shared.Contracts.NotificationInboxItem),
        typeof(POS.Domain.Shared.Contracts.PosRegister),
        typeof(SmsGateway.Domain.Shared.Contracts.SmsOutboundJob),
        typeof(Storage.Domain.Shared.Configurations.StorageFileConfiguration),
        typeof(Wallets.Domain.Shared.Contracts.WalletType)
    ];

    public static void LoadMigrationAssemblies()
    {
        foreach (var modelType in MigrationModelTypes)
            _ = modelType.Assembly;
    }
}
