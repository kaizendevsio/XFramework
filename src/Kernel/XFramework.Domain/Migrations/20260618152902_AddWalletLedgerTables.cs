using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletLedgerTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "CurrencyID",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.DropIndex(
                name: "IX_MessageThreadMemberRole_MessageThreadMemberId",
                schema: "Messaging",
                table: "MessageThreadMemberRole");

            migrationBuilder.DropIndex(
                name: "IX_MessageThreadMember_CredentialId",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropIndex(
                name: "IX_MessageThreadMember_MessageThreadId",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropIndex(
                name: "IX_MessageReaction_MessageId",
                schema: "Messaging",
                table: "MessageReaction");

            migrationBuilder.DropIndex(
                name: "IX_MessageDelivery_MessageId",
                schema: "Messaging",
                table: "MessageDelivery");

            migrationBuilder.DropIndex(
                name: "IX_MessageDelivery_MessageThreadMemberId",
                schema: "Messaging",
                table: "MessageDelivery");

            migrationBuilder.DropIndex(
                name: "IX_Message_MessageThreadId",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropIndex(
                name: "IX_Message_MessageThreadMemberId",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropIndex(
                name: "IX_AddressCountry_CurrencyID",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.AddColumn<Guid>(
                name: "MessageThreadMemberId",
                schema: "Messaging",
                table: "MessageReaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "CommunityNotification",
                schema: "Community",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    RecipientIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "character varying", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("communitynotification_pk", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CommunityNotification_CommunityContent_ContentId",
                        column: x => x.ContentId,
                        principalSchema: "Community",
                        principalTable: "CommunityContent",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "communitynotification_actoridentity_id_fk",
                        column: x => x.ActorIdentityId,
                        principalSchema: "Community",
                        principalTable: "CommunityIdentity",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "communitynotification_recipientidentity_id_fk",
                        column: x => x.RecipientIdentityId,
                        principalSchema: "Community",
                        principalTable: "CommunityIdentity",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "MessageOutboxEvent",
                schema: "Messaging",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    EventType = table.Column<string>(type: "character varying", maxLength: 128, nullable: false),
                    AggregateType = table.Column<string>(type: "character varying", maxLength: 128, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValueSql: "true"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("messageoutboxevent_pk", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "WalletOperation",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    OperationType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActorCredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RiskDecision = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PolicyDecision = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("tbl_WalletOperations_pkey", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "WalletLedgerEntry",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    WalletTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: true),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    BalanceBucket = table.Column<int>(type: "integer", nullable: false),
                    EntryKind = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CounterpartyType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CounterpartyReference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PreviousBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    PreviousAvailableBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    PreviousDebitOnHoldBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    PreviousCreditOnHoldBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    RunningBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    RunningAvailableBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    RunningDebitOnHoldBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
                    RunningCreditOnHoldBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: true),
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
                    table.PrimaryKey("tbl_WalletLedgerEntries_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_WalletLedgerEntries_OperationId_fkey",
                        column: x => x.OperationId,
                        principalSchema: "Wallet",
                        principalTable: "WalletOperation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "tbl_WalletLedgerEntries_WalletId_fkey",
                        column: x => x.WalletId,
                        principalSchema: "Wallet",
                        principalTable: "Wallet",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "tbl_WalletLedgerEntries_WalletTransactionId_fkey",
                        column: x => x.WalletTransactionId,
                        principalSchema: "Wallet",
                        principalTable: "WalletTransaction",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WalletOutboxMessage",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    HeadersJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("tbl_WalletOutboxMessages_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_WalletOutboxMessages_OperationId_fkey",
                        column: x => x.OperationId,
                        principalSchema: "Wallet",
                        principalTable: "WalletOperation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WalletBalanceSnapshot",
                schema: "Wallet",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Balance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    TransferableBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    DebitOnHoldBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    CreditOnHoldBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    TotalBalance = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    LastOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsReconciled = table.Column<bool>(type: "boolean", nullable: false),
                    DriftAmount = table.Column<decimal>(type: "numeric(24,8)", precision: 24, scale: 8, nullable: false),
                    ReconciledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("tbl_WalletBalanceSnapshots_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "tbl_WalletBalanceSnapshots_LastLedgerEntryId_fkey",
                        column: x => x.LastLedgerEntryId,
                        principalSchema: "Wallet",
                        principalTable: "WalletLedgerEntry",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "tbl_WalletBalanceSnapshots_LastOperationId_fkey",
                        column: x => x.LastOperationId,
                        principalSchema: "Wallet",
                        principalTable: "WalletOperation",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "tbl_WalletBalanceSnapshots_WalletId_fkey",
                        column: x => x.WalletId,
                        principalSchema: "Wallet",
                        principalTable: "Wallet",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MessageThreadMemberRole_Member_Role_Active",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                columns: new[] { "MessageThreadMemberId", "RoleId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMember_Credential_Thread",
                schema: "Messaging",
                table: "MessageThreadMember",
                columns: new[] { "CredentialId", "MessageThreadId" });

            migrationBuilder.CreateIndex(
                name: "UX_MessageThreadMember_Thread_Credential_Active",
                schema: "Messaging",
                table: "MessageThreadMember",
                columns: new[] { "MessageThreadId", "CredentialId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThread_Tenant_CreatedAt",
                schema: "Messaging",
                table: "MessageThread",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageReaction_Member_Message",
                schema: "Messaging",
                table: "MessageReaction",
                columns: new[] { "MessageThreadMemberId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "UX_MessageReaction_Message_Type_Member_Active",
                schema: "Messaging",
                table: "MessageReaction",
                columns: new[] { "MessageId", "TypeId", "MessageThreadMemberId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDelivery_Message_Type",
                schema: "Messaging",
                table: "MessageDelivery",
                columns: new[] { "MessageId", "TypeId" });

            migrationBuilder.CreateIndex(
                name: "UX_MessageDelivery_Member_Message_Active",
                schema: "Messaging",
                table: "MessageDelivery",
                columns: new[] { "MessageThreadMemberId", "MessageId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Message_Member_CreatedAt",
                schema: "Messaging",
                table: "Message",
                columns: new[] { "MessageThreadMemberId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Message_Thread_CreatedAt_Id",
                schema: "Messaging",
                table: "Message",
                columns: new[] { "MessageThreadId", "CreatedAt", "ID" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityNotification_ActorIdentityId",
                schema: "Community",
                table: "CommunityNotification",
                column: "ActorIdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityNotification_ContentId",
                schema: "Community",
                table: "CommunityNotification",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityNotification_RecipientIdentityId",
                schema: "Community",
                table: "CommunityNotification",
                column: "RecipientIdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageOutboxEvent_EventType_Occurred",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                columns: new[] { "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageOutboxEvent_Tenant_Processed_Occurred",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                columns: new[] { "TenantId", "ProcessedAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageOutboxEvent_Thread_Occurred",
                schema: "Messaging",
                table: "MessageOutboxEvent",
                columns: new[] { "ThreadId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletBalanceSnapshot_LastLedgerEntryId",
                schema: "Wallet",
                table: "WalletBalanceSnapshot",
                column: "LastLedgerEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletBalanceSnapshot_LastOperationId",
                schema: "Wallet",
                table: "WalletBalanceSnapshot",
                column: "LastOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletBalanceSnapshot_TenantId_IsReconciled",
                schema: "Wallet",
                table: "WalletBalanceSnapshot",
                columns: new[] { "TenantId", "IsReconciled" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletBalanceSnapshot_TenantId_WalletId",
                schema: "Wallet",
                table: "WalletBalanceSnapshot",
                columns: new[] { "TenantId", "WalletId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletBalanceSnapshot_WalletId",
                schema: "Wallet",
                table: "WalletBalanceSnapshot",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedgerEntry_OperationId",
                schema: "Wallet",
                table: "WalletLedgerEntry",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedgerEntry_TenantId_OperationId_Sequence",
                schema: "Wallet",
                table: "WalletLedgerEntry",
                columns: new[] { "TenantId", "OperationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedgerEntry_TenantId_ReferenceNumber",
                schema: "Wallet",
                table: "WalletLedgerEntry",
                columns: new[] { "TenantId", "ReferenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedgerEntry_TenantId_WalletId_CreatedAt",
                schema: "Wallet",
                table: "WalletLedgerEntry",
                columns: new[] { "TenantId", "WalletId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedgerEntry_TenantId_WalletTransactionId",
                schema: "Wallet",
                table: "WalletLedgerEntry",
                columns: new[] { "TenantId", "WalletTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedgerEntry_WalletId",
                schema: "Wallet",
                table: "WalletLedgerEntry",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedgerEntry_WalletTransactionId",
                schema: "Wallet",
                table: "WalletLedgerEntry",
                column: "WalletTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletOperation_TenantId_ActorCredentialId",
                schema: "Wallet",
                table: "WalletOperation",
                columns: new[] { "TenantId", "ActorCredentialId" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletOperation_TenantId_IdempotencyKey",
                schema: "Wallet",
                table: "WalletOperation",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletOperation_TenantId_OperationType_Status",
                schema: "Wallet",
                table: "WalletOperation",
                columns: new[] { "TenantId", "OperationType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletOperation_TenantId_ReferenceNumber",
                schema: "Wallet",
                table: "WalletOperation",
                columns: new[] { "TenantId", "ReferenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletOutboxMessage_OperationId",
                schema: "Wallet",
                table: "WalletOutboxMessage",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletOutboxMessage_TenantId_AggregateType_AggregateId",
                schema: "Wallet",
                table: "WalletOutboxMessage",
                columns: new[] { "TenantId", "AggregateType", "AggregateId" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletOutboxMessage_TenantId_OperationId",
                schema: "Wallet",
                table: "WalletOutboxMessage",
                columns: new[] { "TenantId", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_WalletOutboxMessage_TenantId_Status_NextAttemptAt",
                schema: "Wallet",
                table: "WalletOutboxMessage",
                columns: new[] { "TenantId", "Status", "NextAttemptAt" });

            migrationBuilder.AddForeignKey(
                name: "messagereaction_messagethreadmember_id_fk",
                schema: "Messaging",
                table: "MessageReaction",
                column: "MessageThreadMemberId",
                principalSchema: "Messaging",
                principalTable: "MessageThreadMember",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "messagereaction_messagethreadmember_id_fk",
                schema: "Messaging",
                table: "MessageReaction");

            migrationBuilder.DropTable(
                name: "CommunityNotification",
                schema: "Community");

            migrationBuilder.DropTable(
                name: "MessageOutboxEvent",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "WalletBalanceSnapshot",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WalletOutboxMessage",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WalletLedgerEntry",
                schema: "Wallet");

            migrationBuilder.DropTable(
                name: "WalletOperation",
                schema: "Wallet");

            migrationBuilder.DropIndex(
                name: "UX_MessageThreadMemberRole_Member_Role_Active",
                schema: "Messaging",
                table: "MessageThreadMemberRole");

            migrationBuilder.DropIndex(
                name: "IX_MessageThreadMember_Credential_Thread",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropIndex(
                name: "UX_MessageThreadMember_Thread_Credential_Active",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropIndex(
                name: "IX_MessageThread_Tenant_CreatedAt",
                schema: "Messaging",
                table: "MessageThread");

            migrationBuilder.DropIndex(
                name: "IX_MessageReaction_Member_Message",
                schema: "Messaging",
                table: "MessageReaction");

            migrationBuilder.DropIndex(
                name: "UX_MessageReaction_Message_Type_Member_Active",
                schema: "Messaging",
                table: "MessageReaction");

            migrationBuilder.DropIndex(
                name: "IX_MessageDelivery_Message_Type",
                schema: "Messaging",
                table: "MessageDelivery");

            migrationBuilder.DropIndex(
                name: "UX_MessageDelivery_Member_Message_Active",
                schema: "Messaging",
                table: "MessageDelivery");

            migrationBuilder.DropIndex(
                name: "IX_Message_Member_CreatedAt",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropIndex(
                name: "IX_Message_Thread_CreatedAt_Id",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "MessageThreadMemberId",
                schema: "Messaging",
                table: "MessageReaction");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMemberRole_MessageThreadMemberId",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                column: "MessageThreadMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMember_CredentialId",
                schema: "Messaging",
                table: "MessageThreadMember",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMember_MessageThreadId",
                schema: "Messaging",
                table: "MessageThreadMember",
                column: "MessageThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReaction_MessageId",
                schema: "Messaging",
                table: "MessageReaction",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDelivery_MessageId",
                schema: "Messaging",
                table: "MessageDelivery",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDelivery_MessageThreadMemberId",
                schema: "Messaging",
                table: "MessageDelivery",
                column: "MessageThreadMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Message_MessageThreadId",
                schema: "Messaging",
                table: "Message",
                column: "MessageThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_Message_MessageThreadMemberId",
                schema: "Messaging",
                table: "Message",
                column: "MessageThreadMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressCountry_CurrencyID",
                schema: "GeoLocation",
                table: "AddressCountry",
                column: "CurrencyID");

            migrationBuilder.AddForeignKey(
                name: "CurrencyID",
                schema: "GeoLocation",
                table: "AddressCountry",
                column: "CurrencyID",
                principalSchema: "Finance",
                principalTable: "CurrencyType",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
