namespace XFramework.TestInfrastructure;

/// <summary>
/// Shared test data constants used across all integration test projects.
/// All GUIDs are deterministic so tests are reproducible and entities can reference each other.
/// </summary>
public class TestConstants
{
    // ── Tenant ──
    public static readonly Guid TenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");

    // ── Identity / Auth ──
    public static readonly Guid RoleTypeId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    public static readonly Guid RoleGroupId = Guid.Parse("b1b2c3d4-e5f6-7890-abcd-ef1234567890");
    public static readonly Guid RegistryGroupId = Guid.Parse("c1c2c3d4-e5f6-7890-abcd-ef1234567890");

    // ── Wallets ──
    public static readonly Guid WalletTypeId = Guid.Parse("e1e2e3e4-e5f6-7890-abcd-ef1234567890");
    public static readonly Guid WalletType2Id = Guid.Parse("e2e3e4e5-f6f7-8901-bcde-f12345678901");

    // ── SmsGateway ──
    public static readonly Guid SmsAgentClusterId = Guid.Parse("a1a2a3a4-b5b6-c7c8-d9d0-e1e2e3e4e5e6");

    // ── Inventario ──
    public static readonly Guid ProductCategoryId = Guid.Parse("f1f2f3f4-e5f6-7890-abcd-ef1234567890");

    // ── Verification ──
    public static readonly Guid ContactGroupId = Guid.Parse("d1d2d3d4-e5f6-7890-abcd-ef1234567890");

    // ── Ports (unique per module to allow parallel test runs) ──
    public static class Ports
    {
        // IdentityServer
        public const string IdentityBolt = "http://localhost:17000";
        public const string IdentityServer = "http://localhost:18261";
        public const string IdentityTestClient = "http://localhost:18262";

        // Wallets
        public const string WalletsBolt = "http://localhost:17100";
        public const string WalletsServer = "http://localhost:18361";
        public const string WalletsTestClient = "http://localhost:18362";

        // SmsGateway
        public const string SmsGatewayServer = "http://localhost:18561";

        // Inventario
        public const string InventarioBolt = "http://localhost:17200";
        public const string InventarioServer = "http://localhost:18461";
        public const string InventarioTestClient = "http://localhost:18462";

        // Attendance
        public const string AttendanceBolt = "http://localhost:17300";
        public const string AttendanceServer = "http://localhost:18581";
        public const string AttendanceTestClient = "http://localhost:18582";
    }
}
