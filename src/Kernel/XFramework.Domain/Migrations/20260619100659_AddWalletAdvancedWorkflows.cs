using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletAdvancedWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Fee",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByCredentialId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedFee",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GatewayId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HoldOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderEventId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatus",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawRequestData",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawResponseData",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByCredentialId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedFee",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettledAt",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SettlementOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SettlementTransactionId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowStatus",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                schema: "Wallet",
                table: "WalletOutboxMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                schema: "Wallet",
                table: "WalletOutboxMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                schema: "Wallet",
                table: "WalletOutboxMessage",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntil",
                schema: "Wallet",
                table: "WalletOutboxMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                schema: "Wallet",
                table: "WalletOutboxMessage",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalId",
                schema: "Wallet",
                table: "WalletOperation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedFee",
                schema: "Wallet",
                table: "WalletOperation",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOperationId",
                schema: "Wallet",
                table: "WalletOperation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyDecisionJson",
                schema: "Wallet",
                table: "WalletOperation",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedFee",
                schema: "Wallet",
                table: "WalletOperation",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                schema: "Wallet",
                table: "WalletOperation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RiskScore",
                schema: "Wallet",
                table: "WalletOperation",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskTier",
                schema: "Wallet",
                table: "WalletOperation",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TransferableBalance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "MinTransferRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxTransferRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaintainingBalanceRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DebitOnHoldBalance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "CreditOnHoldBalance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "BondBalanceRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNo",
                schema: "Wallet",
                table: "DepositRequest",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(35)",
                oldMaxLength: 35,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalId",
                schema: "Wallet",
                table: "DepositRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                schema: "Wallet",
                table: "DepositRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByCredentialId",
                schema: "Wallet",
                table: "DepositRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedFee",
                schema: "Wallet",
                table: "DepositRequest",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                schema: "Wallet",
                table: "DepositRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                schema: "Wallet",
                table: "DepositRequest",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                schema: "Wallet",
                table: "DepositRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "Wallet",
                table: "DepositRequest",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderEventId",
                schema: "Wallet",
                table: "DepositRequest",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatus",
                schema: "Wallet",
                table: "DepositRequest",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionId",
                schema: "Wallet",
                table: "DepositRequest",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByCredentialId",
                schema: "Wallet",
                table: "DepositRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedFee",
                schema: "Wallet",
                table: "DepositRequest",
                type: "numeric(24,8)",
                precision: 24,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettledAt",
                schema: "Wallet",
                table: "DepositRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SettlementOperationId",
                schema: "Wallet",
                table: "DepositRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SettlementTransactionId",
                schema: "Wallet",
                table: "DepositRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WalletId",
                schema: "Wallet",
                table: "DepositRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowStatus",
                schema: "Wallet",
                table: "DepositRequest",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WalletApprovalRequest",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    OperationType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequesterCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AuditMetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WalletApprovalRequests_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "FK_WalletApprovalRequest_WalletOperation_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "Wallet",
                        principalTable: "WalletOperation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WalletApprovalRequest_Wallet_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "Wallet",
                        principalTable: "Wallet",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WalletCase",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    CaseType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettlementOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequesterCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeciderCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WalletCases_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "FK_WalletCase_WalletOperation_OriginalOperationId",
                        column: x => x.OriginalOperationId,
                        principalSchema: "Wallet",
                        principalTable: "WalletOperation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WalletCase_WalletOperation_SettlementOperationId",
                        column: x => x.SettlementOperationId,
                        principalSchema: "Wallet",
                        principalTable: "WalletOperation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WalletCase_WalletTransaction_OriginalTransactionId",
                        column: x => x.OriginalTransactionId,
                        principalSchema: "Wallet",
                        principalTable: "WalletTransaction",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WalletCase_Wallet_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "Wallet",
                        principalTable: "Wallet",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "WalletFeeSchedule",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OperationType = table.Column<int>(type: "integer", nullable: false),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: true),
                    FixedFee = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    PercentageFee = table.Column<decimal>(type: "numeric(18,10)", precision: 18, scale: 10, nullable: false),
                    MinimumFee = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    MaximumFee = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    AllowRequestedFeeOverride = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WalletFeeSchedules_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "FK_WalletFeeSchedule_CurrencyType_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "Finance",
                        principalTable: "CurrencyType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WalletFeeSchedule_WalletType_WalletTypeId",
                        column: x => x.WalletTypeId,
                        principalSchema: "Wallet",
                        principalTable: "WalletType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WalletPaymentWebhookEvent",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ProviderKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MappedWorkflowStatus = table.Column<int>(type: "integer", nullable: true),
                    ProcessingStatus = table.Column<int>(type: "integer", nullable: false),
                    SignatureValid = table.Column<bool>(type: "boolean", nullable: false),
                    SignatureScheme = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HeadersHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RawPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProcessingError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DepositRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    WithdrawalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WalletPaymentWebhookEvents_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "FK_WalletPaymentWebhookEvent_DepositRequest_DepositRequestId",
                        column: x => x.DepositRequestId,
                        principalSchema: "Wallet",
                        principalTable: "DepositRequest",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WalletPaymentWebhookEvent_WalletOperation_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "Wallet",
                        principalTable: "WalletOperation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WalletPaymentWebhookEvent_WithdrawalRequest_WithdrawalReque~",
                        column: x => x.WithdrawalRequestId,
                        principalSchema: "Wallet",
                        principalTable: "WithdrawalRequest",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WalletPolicyRule",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OperationType = table.Column<int>(type: "integer", nullable: true),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiredWalletStatus = table.Column<int>(type: "integer", nullable: true),
                    MaxSingleTransactionAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    DailyVelocityLimit = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    MonthlyVelocityLimit = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    ApprovalThreshold = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    DenyWhenMatched = table.Column<bool>(type: "boolean", nullable: false),
                    RiskTier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DecisionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WalletPolicyRules_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "FK_WalletPolicyRule_CurrencyType_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "Finance",
                        principalTable: "CurrencyType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WalletPolicyRule_WalletType_WalletTypeId",
                        column: x => x.WalletTypeId,
                        principalSchema: "Wallet",
                        principalTable: "WalletType",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WalletReconciliationRun",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedCount = table.Column<int>(type: "integer", nullable: false),
                    DriftCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WalletReconciliationRuns_pkey", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "WalletReconciliationItem",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    ActualAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    DriftAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: true),
                    RepairSuggestion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    MarkedReconciledByCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarkedReconciledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "false"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tbl_WalletReconciliationItems_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "FK_WalletReconciliationItem_WalletReconciliationRun_RunId",
                        column: x => x.RunId,
                        principalSchema: "Wallet",
                        principalTable: "WalletReconciliationRun",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WalletReconciliationItem_Wallet_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "Wallet",
                        principalTable: "Wallet",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_ApprovalId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "ApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_GatewayId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "GatewayId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_HoldOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "HoldOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_SettlementOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "SettlementOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_SettlementTransactionId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "SettlementTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_TenantId_ExternalReference",
                schema: "Wallet",
                table: "WithdrawalRequest",
                columns: new[] { "TenantId", "ExternalReference" },
                unique: true,
                filter: "\"ExternalReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_TenantId_ProviderEventId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                columns: new[] { "TenantId", "ProviderEventId" },
                unique: true,
                filter: "\"ProviderEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_TenantId_ReferenceNumber",
                schema: "Wallet",
                table: "WithdrawalRequest",
                columns: new[] { "TenantId", "ReferenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_TenantId_WorkflowStatus_CreatedAt",
                schema: "Wallet",
                table: "WithdrawalRequest",
                columns: new[] { "TenantId", "WorkflowStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletOutboxMessage_TenantId_OperationId_EventType",
                schema: "Wallet",
                table: "WalletOutboxMessage",
                columns: new[] { "TenantId", "OperationId", "EventType" },
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletOutboxMessage_TenantId_Status_LockedUntil_NextAttempt~",
                schema: "Wallet",
                table: "WalletOutboxMessage",
                columns: new[] { "TenantId", "Status", "LockedUntil", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletOperation_OriginalOperationId",
                schema: "Wallet",
                table: "WalletOperation",
                column: "OriginalOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletOperation_TenantId_ExternalReference",
                schema: "Wallet",
                table: "WalletOperation",
                columns: new[] { "TenantId", "ExternalReference" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletOperation_TenantId_Status_CreatedAt",
                schema: "Wallet",
                table: "WalletOperation",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Wallet_TenantId_AccountNumber",
                schema: "Wallet",
                table: "Wallet",
                columns: new[] { "TenantId", "AccountNumber" },
                unique: true,
                filter: "\"AccountNumber\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Wallet_TenantId_CredentialId_WalletTypeId",
                schema: "Wallet",
                table: "Wallet",
                columns: new[] { "TenantId", "CredentialId", "WalletTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_Wallet_TenantId_Status",
                schema: "Wallet",
                table: "Wallet",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_ApprovalId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "ApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_SettlementOperationId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "SettlementOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_SettlementTransactionId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "SettlementTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_TenantId_ExternalReference",
                schema: "Wallet",
                table: "DepositRequest",
                columns: new[] { "TenantId", "ExternalReference" },
                unique: true,
                filter: "\"ExternalReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_TenantId_ProviderEventId",
                schema: "Wallet",
                table: "DepositRequest",
                columns: new[] { "TenantId", "ProviderEventId" },
                unique: true,
                filter: "\"ProviderEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_TenantId_ReferenceNo",
                schema: "Wallet",
                table: "DepositRequest",
                columns: new[] { "TenantId", "ReferenceNo" });

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_TenantId_WorkflowStatus_CreatedAt",
                schema: "Wallet",
                table: "DepositRequest",
                columns: new[] { "TenantId", "WorkflowStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_WalletId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletApprovalRequest_OperationId",
                schema: "Wallet",
                table: "WalletApprovalRequest",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletApprovalRequest_TenantId_ApproverCredentialId",
                schema: "Wallet",
                table: "WalletApprovalRequest",
                columns: new[] { "TenantId", "ApproverCredentialId" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletApprovalRequest_TenantId_RequesterCredentialId",
                schema: "Wallet",
                table: "WalletApprovalRequest",
                columns: new[] { "TenantId", "RequesterCredentialId" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletApprovalRequest_TenantId_Status_OperationType_Request~",
                schema: "Wallet",
                table: "WalletApprovalRequest",
                columns: new[] { "TenantId", "Status", "OperationType", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletApprovalRequest_WalletId",
                schema: "Wallet",
                table: "WalletApprovalRequest",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletCase_OriginalOperationId",
                schema: "Wallet",
                table: "WalletCase",
                column: "OriginalOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletCase_OriginalTransactionId",
                schema: "Wallet",
                table: "WalletCase",
                column: "OriginalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletCase_SettlementOperationId",
                schema: "Wallet",
                table: "WalletCase",
                column: "SettlementOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletCase_TenantId_CaseType_Status_CreatedAt",
                schema: "Wallet",
                table: "WalletCase",
                columns: new[] { "TenantId", "CaseType", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletCase_TenantId_ExternalReference",
                schema: "Wallet",
                table: "WalletCase",
                columns: new[] { "TenantId", "ExternalReference" },
                unique: true,
                filter: "\"ExternalReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletCase_TenantId_WalletId_Status",
                schema: "Wallet",
                table: "WalletCase",
                columns: new[] { "TenantId", "WalletId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletCase_WalletId",
                schema: "Wallet",
                table: "WalletCase",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletFeeSchedule_CurrencyId",
                schema: "Wallet",
                table: "WalletFeeSchedule",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletFeeSchedule_TenantId_EffectiveAt_ExpiresAt",
                schema: "Wallet",
                table: "WalletFeeSchedule",
                columns: new[] { "TenantId", "EffectiveAt", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletFeeSchedule_TenantId_IsEnabled_OperationType_WalletTy~",
                schema: "Wallet",
                table: "WalletFeeSchedule",
                columns: new[] { "TenantId", "IsEnabled", "OperationType", "WalletTypeId", "CurrencyId" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletFeeSchedule_WalletTypeId",
                schema: "Wallet",
                table: "WalletFeeSchedule",
                column: "WalletTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPaymentWebhookEvent_DepositRequestId",
                schema: "Wallet",
                table: "WalletPaymentWebhookEvent",
                column: "DepositRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPaymentWebhookEvent_OperationId",
                schema: "Wallet",
                table: "WalletPaymentWebhookEvent",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPaymentWebhookEvent_TenantId_ExternalReference",
                schema: "Wallet",
                table: "WalletPaymentWebhookEvent",
                columns: new[] { "TenantId", "ExternalReference" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletPaymentWebhookEvent_TenantId_ProcessingStatus_Receive~",
                schema: "Wallet",
                table: "WalletPaymentWebhookEvent",
                columns: new[] { "TenantId", "ProcessingStatus", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletPaymentWebhookEvent_TenantId_ProviderKey_ExternalEven~",
                schema: "Wallet",
                table: "WalletPaymentWebhookEvent",
                columns: new[] { "TenantId", "ProviderKey", "ExternalEventId" },
                unique: true,
                filter: "\"ExternalEventId\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPaymentWebhookEvent_WithdrawalRequestId",
                schema: "Wallet",
                table: "WalletPaymentWebhookEvent",
                column: "WithdrawalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPolicyRule_CurrencyId",
                schema: "Wallet",
                table: "WalletPolicyRule",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPolicyRule_TenantId_EffectiveAt_ExpiresAt",
                schema: "Wallet",
                table: "WalletPolicyRule",
                columns: new[] { "TenantId", "EffectiveAt", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletPolicyRule_TenantId_IsEnabled_OperationType_WalletTyp~",
                schema: "Wallet",
                table: "WalletPolicyRule",
                columns: new[] { "TenantId", "IsEnabled", "OperationType", "WalletTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletPolicyRule_WalletTypeId",
                schema: "Wallet",
                table: "WalletPolicyRule",
                column: "WalletTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletReconciliationItem_RunId_Status",
                schema: "Wallet",
                table: "WalletReconciliationItem",
                columns: new[] { "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletReconciliationItem_TenantId_Status_CheckType",
                schema: "Wallet",
                table: "WalletReconciliationItem",
                columns: new[] { "TenantId", "Status", "CheckType" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletReconciliationItem_TenantId_WalletId_Status",
                schema: "Wallet",
                table: "WalletReconciliationItem",
                columns: new[] { "TenantId", "WalletId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletReconciliationItem_WalletId",
                schema: "Wallet",
                table: "WalletReconciliationItem",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletReconciliationRun_TenantId_Status_StartedAt",
                schema: "Wallet",
                table: "WalletReconciliationRun",
                columns: new[] { "TenantId", "Status", "StartedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_DepositRequest_WalletApprovalRequest_ApprovalId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "ApprovalId",
                principalSchema: "Wallet",
                principalTable: "WalletApprovalRequest",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DepositRequest_WalletOperation_SettlementOperationId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "SettlementOperationId",
                principalSchema: "Wallet",
                principalTable: "WalletOperation",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DepositRequest_WalletTransaction_SettlementTransactionId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "SettlementTransactionId",
                principalSchema: "Wallet",
                principalTable: "WalletTransaction",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DepositRequest_Wallet_WalletId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "WalletId",
                principalSchema: "Wallet",
                principalTable: "Wallet",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletOperation_WalletOperation_OriginalOperationId",
                schema: "Wallet",
                table: "WalletOperation",
                column: "OriginalOperationId",
                principalSchema: "Wallet",
                principalTable: "WalletOperation",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_Gateway_GatewayId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "GatewayId",
                principalSchema: "Integration.PaymentGateway",
                principalTable: "Gateway",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_WalletApprovalRequest_ApprovalId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "ApprovalId",
                principalSchema: "Wallet",
                principalTable: "WalletApprovalRequest",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_WalletOperation_HoldOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "HoldOperationId",
                principalSchema: "Wallet",
                principalTable: "WalletOperation",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_WalletOperation_SettlementOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "SettlementOperationId",
                principalSchema: "Wallet",
                principalTable: "WalletOperation",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_WalletTransaction_SettlementTransactionId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "SettlementTransactionId",
                principalSchema: "Wallet",
                principalTable: "WalletTransaction",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DepositRequest_WalletApprovalRequest_ApprovalId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_DepositRequest_WalletOperation_SettlementOperationId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_DepositRequest_WalletTransaction_SettlementTransactionId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_DepositRequest_Wallet_WalletId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletOperation_WalletOperation_OriginalOperationId",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_Gateway_GatewayId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_WalletApprovalRequest_ApprovalId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_WalletOperation_HoldOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_WalletOperation_SettlementOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_WalletTransaction_SettlementTransactionId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropTable(
                name: "WalletApprovalRequest",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WalletCase",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WalletFeeSchedule",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WalletPaymentWebhookEvent",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WalletPolicyRule",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WalletReconciliationItem",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WalletReconciliationRun",
                schema: "Wallet");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequest_ApprovalId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequest_GatewayId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequest_HoldOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequest_SettlementOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequest_SettlementTransactionId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequest_TenantId_ExternalReference",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequest_TenantId_ProviderEventId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequest_TenantId_ReferenceNumber",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequest_TenantId_WorkflowStatus_CreatedAt",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_WalletOutboxMessage_TenantId_OperationId_EventType",
                schema: "Wallet",
                table: "WalletOutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_WalletOutboxMessage_TenantId_Status_LockedUntil_NextAttempt~",
                schema: "Wallet",
                table: "WalletOutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_WalletOperation_OriginalOperationId",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropIndex(
                name: "IX_WalletOperation_TenantId_ExternalReference",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropIndex(
                name: "IX_WalletOperation_TenantId_Status_CreatedAt",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropIndex(
                name: "IX_Wallet_TenantId_AccountNumber",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropIndex(
                name: "IX_Wallet_TenantId_CredentialId_WalletTypeId",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropIndex(
                name: "IX_Wallet_TenantId_Status",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropIndex(
                name: "IX_DepositRequest_ApprovalId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropIndex(
                name: "IX_DepositRequest_SettlementOperationId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropIndex(
                name: "IX_DepositRequest_SettlementTransactionId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropIndex(
                name: "IX_DepositRequest_TenantId_ExternalReference",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropIndex(
                name: "IX_DepositRequest_TenantId_ProviderEventId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropIndex(
                name: "IX_DepositRequest_TenantId_ReferenceNo",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropIndex(
                name: "IX_DepositRequest_TenantId_WorkflowStatus_CreatedAt",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropIndex(
                name: "IX_DepositRequest_WalletId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "ApprovalId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "ApprovedByCredentialId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "CalculatedFee",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "GatewayId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "HoldOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "ProviderEventId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "ProviderStatus",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "RawRequestData",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "RawResponseData",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "RequestedByCredentialId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "RequestedFee",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "SettledAt",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "SettlementOperationId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "SettlementTransactionId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "WorkflowStatus",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                schema: "Wallet",
                table: "WalletOutboxMessage");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                schema: "Wallet",
                table: "WalletOutboxMessage");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                schema: "Wallet",
                table: "WalletOutboxMessage");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                schema: "Wallet",
                table: "WalletOutboxMessage");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                schema: "Wallet",
                table: "WalletOutboxMessage");

            migrationBuilder.DropColumn(
                name: "ApprovalId",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropColumn(
                name: "CalculatedFee",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropColumn(
                name: "OriginalOperationId",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropColumn(
                name: "PolicyDecisionJson",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropColumn(
                name: "RequestedFee",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropColumn(
                name: "RiskTier",
                schema: "Wallet",
                table: "WalletOperation");

            migrationBuilder.DropColumn(
                name: "ApprovalId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "ApprovedByCredentialId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "CalculatedFee",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "ProviderEventId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "ProviderStatus",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "RequestedByCredentialId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "RequestedFee",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "SettledAt",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "SettlementOperationId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "SettlementTransactionId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "WalletId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "WorkflowStatus",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.AlterColumn<decimal>(
                name: "Fee",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TransferableBalance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "MinTransferRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxTransferRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaintainingBalanceRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DebitOnHoldBalance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "CreditOnHoldBalance",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "BondBalanceRule",
                schema: "Wallet",
                table: "Wallet",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,8)",
                oldPrecision: 24,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNo",
                schema: "Wallet",
                table: "DepositRequest",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
