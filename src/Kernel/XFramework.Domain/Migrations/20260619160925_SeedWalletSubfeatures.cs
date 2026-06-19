using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class SeedWalletSubfeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "Identity"."TenantModuleFeature"
                    ("ID", "ModuleKey", "SubFeatureKey", "DisplayName", "Description", "IsEnabled", "IsDeleted", "ConcurrencyStamp", "CreatedAt", "ModifiedAt", "TenantId")
                SELECT
                    uuid_generate_v4(),
                    'wallets',
                    definitions."SubFeatureKey",
                    definitions."DisplayName",
                    definitions."Description",
                    true,
                    false,
                    uuid_generate_v4(),
                    now(),
                    now(),
                    tenants."TenantId"
                FROM (
                    SELECT DISTINCT base_feature."TenantId"
                    FROM "Identity"."TenantModuleFeature" base_feature
                    WHERE base_feature."ModuleKey" = 'wallets'
                      AND base_feature."SubFeatureKey" = ''
                      AND base_feature."IsEnabled" = true
                      AND base_feature."IsDeleted" = false
                ) tenants
                CROSS JOIN (VALUES
                    ('transfers', 'Wallet Transfers', 'Wallet transfer and conversion operations.'),
                    ('deposits', 'Wallet Deposits', 'Deposit requests, approvals, provider callbacks, and settlement.'),
                    ('withdrawals', 'Wallet Withdrawals', 'Withdrawal requests, holds, approvals, payout settlement, and failures.'),
                    ('batch', 'Wallet Batch', 'Batch wallet balance and transfer operations.'),
                    ('reconciliation', 'Wallet Reconciliation', 'Ledger, balance, transaction, and provider reconciliation.'),
                    ('policy', 'Wallet Policy', 'Wallet policy, risk, fee, approval, refund, dispute, and chargeback workflows.'),
                    ('webhooks', 'Wallet Webhooks', 'Payment provider webhook ingestion and outbox delivery.'),
                    ('reporting', 'Wallet Reporting', 'Statements, operation history, settlement, and failure reports.')
                ) AS definitions("SubFeatureKey", "DisplayName", "Description")
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "Identity"."TenantModuleFeature" existing
                    WHERE existing."TenantId" = tenants."TenantId"
                      AND existing."ModuleKey" = 'wallets'
                      AND existing."SubFeatureKey" = definitions."SubFeatureKey"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "Identity"."TenantModuleFeature"
                WHERE "ModuleKey" = 'wallets'
                  AND "SubFeatureKey" IN (
                      'transfers',
                      'deposits',
                      'withdrawals',
                      'batch',
                      'reconciliation',
                      'policy',
                      'webhooks',
                      'reporting'
                  );
                """);
        }
    }
}
