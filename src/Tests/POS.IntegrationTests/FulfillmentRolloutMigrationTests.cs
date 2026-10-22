using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using XFramework.Domain.Migrations;

namespace POS.IntegrationTests;

[TestFixture, NonParallelizable]
[Category("Kind:Integration"), Category("Area:FeatureGates")]
public sealed class FulfillmentRolloutMigrationTests
{
    private PostgreSqlContainer postgres = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            CREATE SCHEMA "Identity";
            CREATE SCHEMA "Application";
            CREATE TABLE "Application"."Application" (
                "ID" uuid PRIMARY KEY, "ConcurrencyStamp" uuid NOT NULL, "ModifiedAt" timestamptz,
                "IsEnabled" boolean NOT NULL, "IsDeleted" boolean NOT NULL);
            CREATE TABLE "Identity"."TenantModuleFeature" (
                "ID" uuid PRIMARY KEY, "TenantId" uuid NOT NULL REFERENCES "Application"."Application"("ID"),
                "ModuleKey" varchar NOT NULL, "SubFeatureKey" varchar NOT NULL,
                "DisplayName" varchar, "Description" varchar, "IsEnabled" boolean NOT NULL,
                "IsDeleted" boolean NOT NULL, "ConcurrencyStamp" uuid NOT NULL,
                "CreatedAt" timestamptz, "ModifiedAt" timestamptz, "DeletedAt" timestamptz,
                UNIQUE ("TenantId", "ModuleKey", "SubFeatureKey"));
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    [OneTimeTearDown]
    public async Task Stop() { if (postgres is not null) await postgres.DisposeAsync(); }

    [TestCase(true, true, "missing", true)]
    [TestCase(true, true, "disabled", true)]
    [TestCase(true, true, "deleted", false)]
    [TestCase(true, true, "enabled", true)]
    [TestCase(false, true, "missing", false)]
    [TestCase(true, false, "missing", false)]
    [TestCase(false, false, "disabled", false)]
    [TestCase(false, true, "enabled", true)]
    [TestCase(true, true, "disabled", false, "inactive-tenant")]
    [TestCase(true, true, "disabled", false, "deleted-tenant")]
    [TestCase(true, true, "missing", false, "deleted-root")]
    [TestCase(true, true, "missing", false, "deleted-reservations")]
    [TestCase(false, false, "missing", false, "fresh-tenant")]
    public async Task Up_PreservesExistingShippingAccess_WithoutEnablingUnrelatedTenants(
        bool rootEnabled, bool reservationsEnabled, string previous, bool expectedEnabled, string exclusion = "none")
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var tenant = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO "Application"."Application" VALUES (@tenant, @stamp, now(), @active, @deleted);
            INSERT INTO "Identity"."TenantModuleFeature"
                ("ID", "TenantId", "ModuleKey", "SubFeatureKey", "IsEnabled", "IsDeleted", "ConcurrencyStamp")
            SELECT gen_random_uuid(), @tenant, 'inventario', feature, enabled, deleted, gen_random_uuid()
            FROM (VALUES ('', @root, @rootDeleted), ('reservations', @reservations, @reservationsDeleted)) AS features(feature, enabled, deleted)
            WHERE NOT @fresh;
            INSERT INTO "Identity"."TenantModuleFeature"
                ("ID", "TenantId", "ModuleKey", "SubFeatureKey", "IsEnabled", "IsDeleted", "ConcurrencyStamp")
            VALUES (gen_random_uuid(), @tenant, 'inventario', 'negative_stock', false, false, gen_random_uuid());
            """, connection, transaction))
        {
            seed.Parameters.AddWithValue("tenant", tenant);
            seed.Parameters.AddWithValue("stamp", stamp);
            seed.Parameters.AddWithValue("root", rootEnabled);
            seed.Parameters.AddWithValue("reservations", reservationsEnabled);
            seed.Parameters.AddWithValue("active", exclusion != "inactive-tenant");
            seed.Parameters.AddWithValue("deleted", exclusion == "deleted-tenant");
            seed.Parameters.AddWithValue("rootDeleted", exclusion == "deleted-root");
            seed.Parameters.AddWithValue("reservationsDeleted", exclusion == "deleted-reservations");
            seed.Parameters.AddWithValue("fresh", exclusion == "fresh-tenant");
            await seed.ExecuteNonQueryAsync();
        }
        if (previous != "missing")
        {
            await using var seed = new NpgsqlCommand("""
                INSERT INTO "Identity"."TenantModuleFeature"
                    ("ID", "TenantId", "ModuleKey", "SubFeatureKey", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "DisplayName", "Description")
                VALUES (gen_random_uuid(), @tenant, 'inventario', 'fulfillment', @enabled, @deleted, gen_random_uuid(), 'Custom label', 'Custom description');
                """, connection, transaction);
            seed.Parameters.AddWithValue("tenant", tenant);
            seed.Parameters.AddWithValue("enabled", previous == "enabled");
            seed.Parameters.AddWithValue("deleted", previous == "deleted");
            await seed.ExecuteNonQueryAsync();
        }

        var migration = new EnableExistingReservationFulfillment();
        foreach (var operation in migration.UpOperations.Cast<SqlOperation>())
        {
            await using var command = new NpgsqlCommand(operation.Sql, connection, transaction);
            await command.ExecuteNonQueryAsync();
        }

        await using var read = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM "Identity"."TenantModuleFeature"
                           WHERE "TenantId" = @tenant AND "SubFeatureKey" = 'fulfillment'
                             AND "IsEnabled" AND NOT "IsDeleted"),
                   "ConcurrencyStamp",
                   (SELECT "IsEnabled" FROM "Identity"."TenantModuleFeature"
                    WHERE "TenantId" = @tenant AND "SubFeatureKey" = 'negative_stock'),
                   (SELECT "DisplayName" FROM "Identity"."TenantModuleFeature"
                    WHERE "TenantId" = @tenant AND "SubFeatureKey" = 'fulfillment'),
                   (SELECT "Description" FROM "Identity"."TenantModuleFeature"
                    WHERE "TenantId" = @tenant AND "SubFeatureKey" = 'fulfillment')
            FROM "Application"."Application" WHERE "ID" = @tenant;
            """, connection, transaction);
        read.Parameters.AddWithValue("tenant", tenant);
        await using (var reader = await read.ExecuteReaderAsync())
        {
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetBoolean(0).Should().Be(expectedEnabled);
            (reader.GetGuid(1) != stamp).Should().Be(rootEnabled && reservationsEnabled &&
                previous is not ("enabled" or "deleted") && exclusion == "none");
            reader.GetBoolean(2).Should().BeFalse();
            if (previous != "missing")
            {
                reader.GetString(3).Should().Be("Custom label");
                reader.GetString(4).Should().Be("Custom description");
            }
        }
        migration.DownOperations.Should().BeEmpty("rollback must not revoke access or overwrite subsequent administrator decisions");
        await transaction.RollbackAsync();
    }
}
