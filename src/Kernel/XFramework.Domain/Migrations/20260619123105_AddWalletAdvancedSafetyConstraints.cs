using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletAdvancedSafetyConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_WithdrawalRequest_NonNegativeFees",
                schema: "Wallet",
                table: "WithdrawalRequest",
                sql: "(\"Fee\" IS NULL OR \"Fee\" >= 0) AND (\"RequestedFee\" IS NULL OR \"RequestedFee\" >= 0) AND (\"CalculatedFee\" IS NULL OR \"CalculatedFee\" >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WithdrawalRequest_PositiveAmount",
                schema: "Wallet",
                table: "WithdrawalRequest",
                sql: "\"Amount\" IS NULL OR \"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletOperation_NonNegativeFees",
                schema: "Wallet",
                table: "WalletOperation",
                sql: "(\"RequestedFee\" IS NULL OR \"RequestedFee\" >= 0) AND (\"CalculatedFee\" IS NULL OR \"CalculatedFee\" >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletOperation_RiskScoreRange",
                schema: "Wallet",
                table: "WalletOperation",
                sql: "\"RiskScore\" IS NULL OR (\"RiskScore\" >= 0 AND \"RiskScore\" <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletLedgerEntry_NonNegativeSequence",
                schema: "Wallet",
                table: "WalletLedgerEntry",
                sql: "\"Sequence\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletLedgerEntry_PositiveAmount",
                schema: "Wallet",
                table: "WalletLedgerEntry",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletFeeSchedule_MinMaxFee",
                schema: "Wallet",
                table: "WalletFeeSchedule",
                sql: "\"MinimumFee\" IS NULL OR \"MaximumFee\" IS NULL OR \"MaximumFee\" >= \"MinimumFee\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletFeeSchedule_NonNegativeFees",
                schema: "Wallet",
                table: "WalletFeeSchedule",
                sql: "\"FixedFee\" >= 0 AND \"PercentageFee\" >= 0 AND (\"MinimumFee\" IS NULL OR \"MinimumFee\" >= 0) AND (\"MaximumFee\" IS NULL OR \"MaximumFee\" >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Wallet_NonNegativeBalances",
                schema: "Wallet",
                table: "Wallet",
                sql: "\"Balance\" >= 0 AND \"TransferableBalance\" >= 0 AND \"DebitOnHoldBalance\" >= 0 AND \"CreditOnHoldBalance\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Wallet_TransferRules",
                schema: "Wallet",
                table: "Wallet",
                sql: "(\"MinTransferRule\" IS NULL OR \"MinTransferRule\" >= 0) AND (\"MaxTransferRule\" IS NULL OR \"MaxTransferRule\" >= 0) AND (\"MinTransferRule\" IS NULL OR \"MaxTransferRule\" IS NULL OR \"MaxTransferRule\" >= \"MinTransferRule\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DepositRequest_NonNegativeFees",
                schema: "Wallet",
                table: "DepositRequest",
                sql: "(\"ConvenienceFee\" IS NULL OR \"ConvenienceFee\" >= 0) AND (\"SystemFee\" IS NULL OR \"SystemFee\" >= 0) AND (\"Discount\" IS NULL OR \"Discount\" >= 0) AND (\"RequestedFee\" IS NULL OR \"RequestedFee\" >= 0) AND (\"CalculatedFee\" IS NULL OR \"CalculatedFee\" >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DepositRequest_PositiveAmount",
                schema: "Wallet",
                table: "DepositRequest",
                sql: "\"Amount\" IS NULL OR \"Amount\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WithdrawalRequest_NonNegativeFees",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WithdrawalRequest_PositiveAmount",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletOperation_NonNegativeFees",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletOperation_RiskScoreRange",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletLedgerEntry_NonNegativeSequence",
                schema: "Wallet",
                table: "WalletLedgerEntry");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletLedgerEntry_PositiveAmount",
                schema: "Wallet",
                table: "WalletLedgerEntry");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletFeeSchedule_MinMaxFee",
                schema: "Wallet",
                table: "WalletFeeSchedule");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletFeeSchedule_NonNegativeFees",
                schema: "Wallet",
                table: "WalletFeeSchedule");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Wallet_NonNegativeBalances",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Wallet_TransferRules",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DepositRequest_NonNegativeFees",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DepositRequest_PositiveAmount",
                schema: "Wallet",
                table: "DepositRequest");
        }
    }
}
