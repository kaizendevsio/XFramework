using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using XFramework.Domain.Contexts;
using XFramework.TestInfrastructure;

namespace IdentityServer.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.IdentityServer)]
public sealed class IdentityServerMigrationUpgradeTests
{
    private const string PreRemediationMigration = "20260705084341_AddIdentityRoleCapabilities";

    [Test]
    public async Task RemediationMigrations_LegacyIdentityData_UpgradeAndEnforceLatestConstraints()
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithDatabase("IdentityServer_Migration_Upgrade")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();
        await postgres.StartAsync();

        TestDatabaseModel.LoadMigrationAssemblies();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreRemediationMigration);
        await db.Database.ExecuteSqlRawAsync(LegacySeedSql);

        await migrator.MigrateAsync();

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "__EFMigrationsHistory"
            WHERE "MigrationId" IN (
                '20260730170511_IdentityServerBackendHardening',
                '20260730192030_IdentityServerAuditCompletion',
                '20260730205009_IdentityServerPersistenceRemediation',
                '20260730211054_IdentityServerAuthenticationSecurityCompletion',
                '20260730223513_IdentityServerFinalAuditCompletion')
            """)).Should().Be(5);
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "__EFMigrationsHistory"
            WHERE "MigrationId" IN (
                '20260731023000_RegistryConfigurationActiveKeyUniqueness',
                '20260731040421_IdentityServerDeliveryOutboxes',
                '20260731040823_IdentityStorageCleanupOutbox',
                '20260731052105_IdentityServerOutboxRetryAndDueIndexes',
                '20260731055950_CrossModuleDurabilityCompletion')
            """)).Should().Be(5, "hand-authored and generated remediation migrations must all be discoverable");

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM information_schema.tables
            WHERE table_schema = 'Identity'
              AND table_name IN (
                  'PasswordResetOutboxMessage',
                  'VerificationDeliveryOutboxMessage',
                  'StorageCleanupOutboxMessage',
                  'StorageClaimOutboxMessage')
            """)).Should().Be(4, "all durable IdentityServer delivery boundaries must be migrated");

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'Identity'
              AND indexname IN (
                  'IX_PasswordResetOutbox_Global_Due',
                  'IX_VerificationDeliveryOutbox_Global_Due',
                  'IX_StorageCleanupOutbox_Global_Due',
                  'IX_StorageClaimOutbox_Global_Due')
            """)).Should().Be(4, "global outbox pollers must have indexes matching their due-work scans");

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM information_schema.columns
            WHERE (table_schema = 'Communications' AND table_name = 'MessageDirect' AND column_name = 'IdempotencyRequestId')
               OR (table_schema = 'Storage' AND table_name = 'StorageFile' AND column_name = 'UnclaimedUntil')
            """)).Should().Be(2, "cross-module durability state must be added without backfilling existing files");
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM pg_indexes
            WHERE (schemaname = 'Communications' AND indexname = 'UX_MessageDirect_Tenant_IdempotencyRequest')
               OR (schemaname = 'Storage' AND indexname IN (
                    'ix_storagefile_global_unclaimed_due',
                    'ix_storageuploadsession_global_expired_due'))
            """)).Should().Be(3, "idempotency and bounded maintenance scans must use their dedicated indexes");
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = 'Identity'
              AND (
                  (table_name = 'PasswordResetOutboxMessage' AND column_name = 'DispatchStartedAt') OR
                  (table_name = 'VerificationDeliveryOutboxMessage' AND column_name IN ('DispatchStartedAt', 'NextAttemptAt')))
            """)).Should().Be(3, "retry state must distinguish a safe claimed lease from an ambiguous started dispatch");

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Identity"."Session"
            WHERE "ID" = '00000000-0000-0000-0000-000000000501'
              AND "SessionData" IS NULL
              AND "RefreshTokenHash" IS NULL
              AND "RefreshTokenExpiresAt" IS NULL
            """)).Should().Be(1, "legacy session secrets must be expired during the upgrade");

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Identity"."IdentityVerification"
            WHERE "ID" = '00000000-0000-0000-0000-000000000601'
              AND "Token" IS NULL
              AND "StatusUpdatedOn" IS NULL
              AND "Purpose" = 'contact-verification'
              AND "FailedAttempts" = 0
            """)).Should().Be(1, "legacy verification secrets and incompatible timestamps must be retired");

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = 'Identity'
              AND table_name = 'IdentityVerification'
              AND column_name = 'StatusUpdatedOn'
              AND data_type = 'timestamp with time zone'
            """)).Should().Be(1);

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Identity"."ServiceSigningKey"
            """)).Should().Be(0, "database-held private signing keys must not survive the upgrade");

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Storage"."StorageFileType"
            WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'
              AND "Name" = 'legacy-avatar'
              AND "IsDeleted" = false
            """)).Should().Be(1);
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Storage"."StorageFileIdentifierGroup"
            WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'
              AND "Name" = 'legacy-avatar-group'
              AND "IsDeleted" = false
            """)).Should().Be(1);
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Storage"."StorageFileIdentifier"
            WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'
              AND "Name" = 'legacy-avatar-identifier'
              AND "IsDeleted" = false
            """)).Should().Be(1);
        (await ScalarAsync<long>(db, """
            SELECT count(*)
            FROM (
                SELECT "IsDeleted" FROM "Storage"."StorageFileType"
                WHERE "TenantId" = '00000000-0000-0000-0000-000000000001' AND "Name" = 'legacy-avatar'
                UNION ALL
                SELECT "IsDeleted" FROM "Storage"."StorageFileIdentifierGroup"
                WHERE "TenantId" = '00000000-0000-0000-0000-000000000001' AND "Name" = 'legacy-avatar-group'
                UNION ALL
                SELECT "IsDeleted" FROM "Storage"."StorageFileIdentifier"
                WHERE "TenantId" = '00000000-0000-0000-0000-000000000001' AND "Name" = 'legacy-avatar-identifier'
            ) AS metadata
            WHERE "IsDeleted" = true
            """)).Should().Be(3, "deleted metadata must not displace the active canonical rows");
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Storage"."StorageFile"
            WHERE "ID" = '00000000-0000-0000-0000-000000000401'
              AND "TypeId" = '00000000-0000-0000-0000-000000000302'
              AND "StorageFileIdentifierId" = '00000000-0000-0000-0000-000000000202'
            """)).Should().Be(1, "duplicate metadata references must be rewritten to the canonical rows");

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Identity"."IdentityRole"
            WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'
              AND "UserCredID" = '00000000-0000-0000-0000-000000000102'
              AND "RoleTypeID" = '00000000-0000-0000-0000-000000000104'
              AND "IsDeleted" = false
            """)).Should().Be(1);
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Identity"."IdentityRole"
            WHERE "ID" = '00000000-0000-0000-0000-000000000105'
              AND "IsDeleted" = true
              AND "IsEnabled" = false
              AND "DeletedAt" IS NOT NULL
            """)).Should().Be(1, "the older duplicate role assignment must be disabled");

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Registry"."RegistryConfiguration"
            WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'
              AND "Key" = 'Legacy.Duplicate'
            """)).Should().Be(1);
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Registry"."RegistryConfiguration"
            WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'
              AND "Key" = 'Legacy.Duplicate'
              AND "Value" = 'new'
            """)).Should().Be(1, "the newest registry value must win deduplication");
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Registry"."RegistryConfigurationGroup"
            WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'
              AND "SystemReferenceId" = '00000000-0000-0000-0000-000000000801'
              AND "IsDeleted" = false
            """)).Should().Be(1, "only the newest active registry group may retain a tenant/system identity");
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Registry"."RegistryConfiguration"
            WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'
              AND "GroupId" = '00000000-0000-0000-0000-000000000804'
            """)).Should().Be(1, "configuration references must move to the canonical registry group");

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Identity"."IdentityContact"
            WHERE "TenantId" = '00000000-0000-0000-0000-000000000001'
              AND "TypeId" = '03f26cc1-e4c2-424f-9d5b-b22d006ae45b'
              AND "Value" = 'legacy@example.test'
              AND "IsDeleted" = false
              AND "IsEnabled" = true
            """)).Should().Be(1);
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Identity"."IdentityContact"
            WHERE "ID" = '00000000-0000-0000-0000-000000000701'
              AND "IsDeleted" = true
              AND "IsEnabled" = false
            """)).Should().Be(1, "the older duplicate authentication contact must be retired");

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'Registry'
              AND indexname = 'UX_RegistryConfiguration_Tenant_Key'
              AND indexdef LIKE 'CREATE UNIQUE INDEX%'
              AND indexdef LIKE '%WHERE ("IsDeleted" = false)%'
            """)).Should().Be(1, "only active registry keys must participate in uniqueness");
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'Registry'
              AND indexname = 'IX_RegistryConfigurationGroup_TenantId_SystemReferenceId'
              AND indexdef LIKE 'CREATE UNIQUE INDEX%'
              AND indexdef LIKE '%WHERE ("IsDeleted" = false)%'
            """)).Should().Be(1, "the final remediation migration must enforce one active system group per tenant");
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'Identity'
              AND indexname IN (
                  'IX_IdentityRole_Tenant_Credential_Type',
                  'UX_IdentityContact_ActiveAuthenticationContact')
              AND indexdef LIKE 'CREATE UNIQUE INDEX%'
            """)).Should().Be(2);

        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM pg_constraint
            WHERE conname = 'tbl_identitycredentials_avatar_storagefile_fk'
            """)).Should().Be(0, "IdentityServer must no longer own Storage lifecycle through a cross-schema FK");
        (await ScalarAsync<long>(db, """
            SELECT count(*) FROM "Identity"."IdentityCredential"
            WHERE "ID" = '00000000-0000-0000-0000-000000000102'
              AND "AvatarStorageFileId" = '00000000-0000-0000-0000-000000000401'
            """)).Should().Be(1, "the migration must preserve the external Storage pointer");
    }

    private static async Task<T> ScalarAsync<T>(DbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(value!, typeof(T));
    }

    private const string LegacySeedSql = """
        INSERT INTO "Application"."Application"
            ("ID", "Name", "Version", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000001', 'Legacy tenant', 1, true, false,
             '00000000-0000-0000-0000-000000000011', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Identity"."IdentityInformation"
            ("ID", "IdentityName", "IsVerified", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000101', 'Legacy identity', false, true, false,
             '00000000-0000-0000-0000-000000000111', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Identity"."IdentityCredential"
            ("ID", "IdentityInfoID", "UserName", "IsOnline", "LastSeen", "IsEnabled", "IsDeleted",
             "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-000000000101',
             'legacy-user', false, '2026-01-01T00:00:00Z', true, false,
             '00000000-0000-0000-0000-000000000112', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Identity"."IdentityRoleEntityGroup"
            ("ID", "Name", "Description", "SystemReferenceId", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000103', 'Legacy roles', 'Legacy role group',
             '00000000-0000-0000-0000-000000000103', true, false,
             '00000000-0000-0000-0000-000000000113', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Identity"."IdentityRoleType"
            ("ID", "Name", "GroupId", "SystemReferenceId", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000104', 'Legacy role',
             '00000000-0000-0000-0000-000000000103', '00000000-0000-0000-0000-000000000104',
             true, false, '00000000-0000-0000-0000-000000000114', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Identity"."IdentityRole"
            ("ID", "UserCredID", "RoleTypeID", "RoleExpiration", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000105', '00000000-0000-0000-0000-000000000102',
             '00000000-0000-0000-0000-000000000104', '2030-01-01T00:00:00Z', true, false,
             '00000000-0000-0000-0000-000000000115', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001'),
            ('00000000-0000-0000-0000-000000000106', '00000000-0000-0000-0000-000000000102',
             '00000000-0000-0000-0000-000000000104', '2030-01-01T00:00:00Z', true, false,
             '00000000-0000-0000-0000-000000000116', '2026-01-02T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Identity"."IdentityContactGroup"
            ("ID", "Name", "SystemReferenceId", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000107', 'Legacy contacts',
             '00000000-0000-0000-0000-000000000107', true, false,
             '00000000-0000-0000-0000-000000000117', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Identity"."IdentityContact"
            ("ID", "TypeId", "Value", "CredentialID", "GroupId", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000701', '03f26cc1-e4c2-424f-9d5b-b22d006ae45b',
             'legacy@example.test', '00000000-0000-0000-0000-000000000102',
             '00000000-0000-0000-0000-000000000107', true, false,
             '00000000-0000-0000-0000-000000000711', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001'),
            ('00000000-0000-0000-0000-000000000702', '03f26cc1-e4c2-424f-9d5b-b22d006ae45b',
             'legacy@example.test', '00000000-0000-0000-0000-000000000102',
             '00000000-0000-0000-0000-000000000107', true, false,
             '00000000-0000-0000-0000-000000000712', '2026-01-02T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Registry"."RegistryConfigurationGroup"
            ("ID", "Name", "SystemReferenceId", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000801', 'Legacy registry',
             '00000000-0000-0000-0000-000000000801', true, false,
             '00000000-0000-0000-0000-000000000811', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001'),
            ('00000000-0000-0000-0000-000000000804', 'Legacy registry replacement',
             '00000000-0000-0000-0000-000000000801', true, false,
             '00000000-0000-0000-0000-000000000814', '2026-01-02T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Registry"."RegistryConfiguration"
            ("ID", "Key", "Value", "GroupId", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000802', 'Legacy.Duplicate', 'old',
             '00000000-0000-0000-0000-000000000801', true, false,
             '00000000-0000-0000-0000-000000000812', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001'),
            ('00000000-0000-0000-0000-000000000803', 'Legacy.Duplicate', 'new',
             '00000000-0000-0000-0000-000000000801', true, false,
             '00000000-0000-0000-0000-000000000813', '2026-01-02T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Storage"."StorageFileIdentifierGroup"
            ("ID", "Name", "SystemReferenceId", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000101', 'legacy-avatar-group',
             '00000000-0000-0000-0000-000000000121', false, true,
             '00000000-0000-0000-0000-000000000131', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001'),
            ('00000000-0000-0000-0000-000000000102', 'legacy-avatar-group',
             '00000000-0000-0000-0000-000000000122', true, false,
             '00000000-0000-0000-0000-000000000132', '2026-01-02T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Storage"."StorageFileIdentifier"
            ("ID", "Name", "GroupId", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000201', 'legacy-avatar-identifier',
             '00000000-0000-0000-0000-000000000101', false, true,
             '00000000-0000-0000-0000-000000000231', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001'),
            ('00000000-0000-0000-0000-000000000202', 'legacy-avatar-identifier',
             '00000000-0000-0000-0000-000000000102', true, false,
             '00000000-0000-0000-0000-000000000232', '2026-01-02T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Storage"."StorageFileType"
            ("ID", "Name", "SystemReferenceId", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000301', 'legacy-avatar',
             '00000000-0000-0000-0000-000000000321', false, true,
             '00000000-0000-0000-0000-000000000331', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001'),
            ('00000000-0000-0000-0000-000000000302', 'legacy-avatar',
             '00000000-0000-0000-0000-000000000322', true, false,
             '00000000-0000-0000-0000-000000000332', '2026-01-02T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Storage"."StorageFile"
            ("ID", "ContentPath", "TypeId", "Identifier", "StorageFileIdentifierId", "IsEnabled", "IsDeleted",
             "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000401', '/legacy/avatar.webp',
             '00000000-0000-0000-0000-000000000302', '00000000-0000-0000-0000-000000000401',
             '00000000-0000-0000-0000-000000000202', true, false,
             '00000000-0000-0000-0000-000000000431', '2026-01-02T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        UPDATE "Identity"."IdentityCredential"
        SET "AvatarStorageFileId" = '00000000-0000-0000-0000-000000000401',
            "AvatarUrl" = '/legacy/avatar.webp'
        WHERE "ID" = '00000000-0000-0000-0000-000000000102';

        INSERT INTO "Identity"."Session"
            ("ID", "CredentialID", "SessionData", "Status", "ExpiresAt", "IsEnabled", "IsDeleted",
             "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000501', '00000000-0000-0000-0000-000000000102',
             'plaintext-refresh-token', 1, '2027-01-01T00:00:00Z', true, false,
             '00000000-0000-0000-0000-000000000511', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Identity"."IdentityVerification"
            ("ID", "CredentialID", "Status", "StatusUpdatedOn", "Token", "Expiry", "IsEnabled", "IsDeleted",
             "ConcurrencyStamp", "CreatedAt", "TenantId")
        VALUES
            ('00000000-0000-0000-0000-000000000601', '00000000-0000-0000-0000-000000000102',
             1, '12:34:56+00'::timetz, 'plaintext-verification-code', '2027-01-01T00:00:00Z', true, false,
             '00000000-0000-0000-0000-000000000611', '2026-01-01T00:00:00Z',
             '00000000-0000-0000-0000-000000000001');

        INSERT INTO "Identity"."ServiceSigningKey"
            ("Id", "KeyId", "Algorithm", "PrivateKeyPem", "PublicKeyPem", "CreatedAtUtc", "IsActive")
        VALUES
            ('00000000-0000-0000-0000-000000000901', 'legacy-key', 'RS256',
             'plaintext-private-key', 'public-key', '2026-01-01T00:00:00Z', true);
        """;
}
