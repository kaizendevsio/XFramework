using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionAndWalletStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WalletId1",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyTypeId1",
                schema: "Wallet",
                table: "WalletType",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WalletId1",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "Wallet",
                table: "Wallet",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "WalletTypeId1",
                schema: "Wallet",
                table: "Wallet",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "Identity",
                table: "Session",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "MessageThreadMemberId1",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MessageThreadId1",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MessageThreadMemberGroupId",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MessageId1",
                schema: "Messaging",
                table: "MessageFiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyTypeId",
                schema: "Wallet",
                table: "DepositRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WalletTypeId1",
                schema: "Wallet",
                table: "DepositRequest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommunityIdentityFileTypeId",
                schema: "Community",
                table: "CommunityIdentityFile",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommunityIdentityId",
                schema: "Community",
                table: "CommunityIdentityFile",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommunityIdentityTypeId",
                schema: "Community",
                table: "CommunityIdentity",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommunityContentId",
                schema: "Community",
                table: "CommunityContentFiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyTypeId",
                schema: "GeoLocation",
                table: "AddressCountry",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequest_WalletId1",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "WalletId1");

            migrationBuilder.CreateIndex(
                name: "IX_WalletType_CurrencyTypeId1",
                schema: "Wallet",
                table: "WalletType",
                column: "CurrencyTypeId1");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransaction_WalletId1",
                schema: "Wallet",
                table: "WalletTransaction",
                column: "WalletId1");

            migrationBuilder.CreateIndex(
                name: "IX_Wallet_WalletTypeId1",
                schema: "Wallet",
                table: "Wallet",
                column: "WalletTypeId1");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMemberRole_MessageThreadMemberId1",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                column: "MessageThreadMemberId1");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMember_MessageThreadId1",
                schema: "Messaging",
                table: "MessageThreadMember",
                column: "MessageThreadId1");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreadMember_MessageThreadMemberGroupId",
                schema: "Messaging",
                table: "MessageThreadMember",
                column: "MessageThreadMemberGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageFiles_MessageId1",
                schema: "Messaging",
                table: "MessageFiles",
                column: "MessageId1");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_CurrencyTypeId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "CurrencyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRequest_WalletTypeId1",
                schema: "Wallet",
                table: "DepositRequest",
                column: "WalletTypeId1");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIdentityFile_CommunityIdentityFileTypeId",
                schema: "Community",
                table: "CommunityIdentityFile",
                column: "CommunityIdentityFileTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIdentityFile_CommunityIdentityId",
                schema: "Community",
                table: "CommunityIdentityFile",
                column: "CommunityIdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityIdentity_CommunityIdentityTypeId",
                schema: "Community",
                table: "CommunityIdentity",
                column: "CommunityIdentityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityContentFiles_CommunityContentId",
                schema: "Community",
                table: "CommunityContentFiles",
                column: "CommunityContentId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressCountry_CurrencyTypeId",
                schema: "GeoLocation",
                table: "AddressCountry",
                column: "CurrencyTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AddressCountry_CurrencyType_CurrencyTypeId",
                schema: "GeoLocation",
                table: "AddressCountry",
                column: "CurrencyTypeId",
                principalSchema: "Finance",
                principalTable: "CurrencyType",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityContentFiles_CommunityContent_CommunityContentId",
                schema: "Community",
                table: "CommunityContentFiles",
                column: "CommunityContentId",
                principalSchema: "Community",
                principalTable: "CommunityContent",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityIdentity_CommunityIdentityType_CommunityIdentityTy~",
                schema: "Community",
                table: "CommunityIdentity",
                column: "CommunityIdentityTypeId",
                principalSchema: "Community",
                principalTable: "CommunityIdentityType",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityIdentityFile_CommunityIdentityFileType_CommunityId~",
                schema: "Community",
                table: "CommunityIdentityFile",
                column: "CommunityIdentityFileTypeId",
                principalSchema: "Community",
                principalTable: "CommunityIdentityFileType",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_CommunityIdentityFile_CommunityIdentity_CommunityIdentityId",
                schema: "Community",
                table: "CommunityIdentityFile",
                column: "CommunityIdentityId",
                principalSchema: "Community",
                principalTable: "CommunityIdentity",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_DepositRequest_CurrencyType_CurrencyTypeId",
                schema: "Wallet",
                table: "DepositRequest",
                column: "CurrencyTypeId",
                principalSchema: "Finance",
                principalTable: "CurrencyType",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_DepositRequest_WalletType_WalletTypeId1",
                schema: "Wallet",
                table: "DepositRequest",
                column: "WalletTypeId1",
                principalSchema: "Wallet",
                principalTable: "WalletType",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageFiles_Message_MessageId1",
                schema: "Messaging",
                table: "MessageFiles",
                column: "MessageId1",
                principalSchema: "Messaging",
                principalTable: "Message",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageThreadMember_MessageThreadMemberGroup_MessageThreadM~",
                schema: "Messaging",
                table: "MessageThreadMember",
                column: "MessageThreadMemberGroupId",
                principalSchema: "Messaging",
                principalTable: "MessageThreadMemberGroup",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageThreadMember_MessageThread_MessageThreadId1",
                schema: "Messaging",
                table: "MessageThreadMember",
                column: "MessageThreadId1",
                principalSchema: "Messaging",
                principalTable: "MessageThread",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageThreadMemberRole_MessageThreadMember_MessageThreadMe~",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                column: "MessageThreadMemberId1",
                principalSchema: "Messaging",
                principalTable: "MessageThreadMember",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Wallet_WalletType_WalletTypeId1",
                schema: "Wallet",
                table: "Wallet",
                column: "WalletTypeId1",
                principalSchema: "Wallet",
                principalTable: "WalletType",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransaction_Wallet_WalletId1",
                schema: "Wallet",
                table: "WalletTransaction",
                column: "WalletId1",
                principalSchema: "Wallet",
                principalTable: "Wallet",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletType_CurrencyType_CurrencyTypeId1",
                schema: "Wallet",
                table: "WalletType",
                column: "CurrencyTypeId1",
                principalSchema: "Finance",
                principalTable: "CurrencyType",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_Wallet_WalletId1",
                schema: "Wallet",
                table: "WithdrawalRequest",
                column: "WalletId1",
                principalSchema: "Wallet",
                principalTable: "Wallet",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AddressCountry_CurrencyType_CurrencyTypeId",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunityContentFiles_CommunityContent_CommunityContentId",
                schema: "Community",
                table: "CommunityContentFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunityIdentity_CommunityIdentityType_CommunityIdentityTy~",
                schema: "Community",
                table: "CommunityIdentity");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunityIdentityFile_CommunityIdentityFileType_CommunityId~",
                schema: "Community",
                table: "CommunityIdentityFile");

            migrationBuilder.DropForeignKey(
                name: "FK_CommunityIdentityFile_CommunityIdentity_CommunityIdentityId",
                schema: "Community",
                table: "CommunityIdentityFile");

            migrationBuilder.DropForeignKey(
                name: "FK_DepositRequest_CurrencyType_CurrencyTypeId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_DepositRequest_WalletType_WalletTypeId1",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageFiles_Message_MessageId1",
                schema: "Messaging",
                table: "MessageFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageThreadMember_MessageThreadMemberGroup_MessageThreadM~",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageThreadMember_MessageThread_MessageThreadId1",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageThreadMemberRole_MessageThreadMember_MessageThreadMe~",
                schema: "Messaging",
                table: "MessageThreadMemberRole");

            migrationBuilder.DropForeignKey(
                name: "FK_Wallet_WalletType_WalletTypeId1",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransaction_Wallet_WalletId1",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletType_CurrencyType_CurrencyTypeId1",
                schema: "Wallet",
                table: "WalletType");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_Wallet_WalletId1",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_WithdrawalRequest_WalletId1",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropIndex(
                name: "IX_WalletType_CurrencyTypeId1",
                schema: "Wallet",
                table: "WalletType");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransaction_WalletId1",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropIndex(
                name: "IX_Wallet_WalletTypeId1",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropIndex(
                name: "IX_MessageThreadMemberRole_MessageThreadMemberId1",
                schema: "Messaging",
                table: "MessageThreadMemberRole");

            migrationBuilder.DropIndex(
                name: "IX_MessageThreadMember_MessageThreadId1",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropIndex(
                name: "IX_MessageThreadMember_MessageThreadMemberGroupId",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropIndex(
                name: "IX_MessageFiles_MessageId1",
                schema: "Messaging",
                table: "MessageFiles");

            migrationBuilder.DropIndex(
                name: "IX_DepositRequest_CurrencyTypeId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropIndex(
                name: "IX_DepositRequest_WalletTypeId1",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropIndex(
                name: "IX_CommunityIdentityFile_CommunityIdentityFileTypeId",
                schema: "Community",
                table: "CommunityIdentityFile");

            migrationBuilder.DropIndex(
                name: "IX_CommunityIdentityFile_CommunityIdentityId",
                schema: "Community",
                table: "CommunityIdentityFile");

            migrationBuilder.DropIndex(
                name: "IX_CommunityIdentity_CommunityIdentityTypeId",
                schema: "Community",
                table: "CommunityIdentity");

            migrationBuilder.DropIndex(
                name: "IX_CommunityContentFiles_CommunityContentId",
                schema: "Community",
                table: "CommunityContentFiles");

            migrationBuilder.DropIndex(
                name: "IX_AddressCountry_CurrencyTypeId",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.DropColumn(
                name: "WalletId1",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "CurrencyTypeId1",
                schema: "Wallet",
                table: "WalletType");

            migrationBuilder.DropColumn(
                name: "WalletId1",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "WalletTypeId1",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Identity",
                table: "Session");

            migrationBuilder.DropColumn(
                name: "MessageThreadMemberId1",
                schema: "Messaging",
                table: "MessageThreadMemberRole");

            migrationBuilder.DropColumn(
                name: "MessageThreadId1",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropColumn(
                name: "MessageThreadMemberGroupId",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropColumn(
                name: "MessageId1",
                schema: "Messaging",
                table: "MessageFiles");

            migrationBuilder.DropColumn(
                name: "CurrencyTypeId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "WalletTypeId1",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "CommunityIdentityFileTypeId",
                schema: "Community",
                table: "CommunityIdentityFile");

            migrationBuilder.DropColumn(
                name: "CommunityIdentityId",
                schema: "Community",
                table: "CommunityIdentityFile");

            migrationBuilder.DropColumn(
                name: "CommunityIdentityTypeId",
                schema: "Community",
                table: "CommunityIdentity");

            migrationBuilder.DropColumn(
                name: "CommunityContentId",
                schema: "Community",
                table: "CommunityContentFiles");

            migrationBuilder.DropColumn(
                name: "CurrencyTypeId",
                schema: "GeoLocation",
                table: "AddressCountry");
        }
    }
}
