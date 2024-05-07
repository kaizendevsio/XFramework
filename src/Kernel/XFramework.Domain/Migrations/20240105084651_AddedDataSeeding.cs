using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace XFramework.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class AddedDataSeeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "tbl_applications_tbl_enterprises_id_fk",
                schema: "Application",
                table: "Application");

            migrationBuilder.DropTable(
                name: "Enterprise",
                schema: "Application");

            migrationBuilder.DropIndex(
                name: "IX_Application_EnterpriseID",
                schema: "Application",
                table: "Application");

            migrationBuilder.RenameColumn(
                name: "ApplicationId",
                schema: "Wallet",
                table: "WalletType",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletType_ApplicationId",
                schema: "Wallet",
                table: "WalletType",
                newName: "IX_WalletType_TenantId");

            migrationBuilder.RenameColumn(
                name: "ApplicationID",
                schema: "Registry",
                table: "RegistryConfiguration",
                newName: "ApplicationId");

            migrationBuilder.RenameIndex(
                name: "IX_RegistryConfiguration_ApplicationID",
                schema: "Registry",
                table: "RegistryConfiguration",
                newName: "IX_RegistryConfiguration_ApplicationId");

            migrationBuilder.RenameColumn(
                name: "ApplicationID",
                schema: "Audit",
                table: "Log",
                newName: "ApplicationId");

            migrationBuilder.RenameColumn(
                name: "ApplicationId",
                schema: "Identity",
                table: "IdentityRoleType",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityRoleType_ApplicationId",
                schema: "Identity",
                table: "IdentityRoleType",
                newName: "IX_IdentityRoleType_TenantId");

            migrationBuilder.RenameColumn(
                name: "ApplicationID",
                schema: "Identity",
                table: "IdentityInformation",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityInformation_ApplicationID",
                schema: "Identity",
                table: "IdentityInformation",
                newName: "IX_IdentityInformation_TenantId");

            migrationBuilder.RenameColumn(
                name: "ApplicationID",
                schema: "Identity",
                table: "IdentityCredential",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityCredential_ApplicationID",
                schema: "Identity",
                table: "IdentityCredential",
                newName: "IX_IdentityCredential_TenantId");

            migrationBuilder.RenameColumn(
                name: "EnterpriseID",
                schema: "Application",
                table: "Application",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "AppName",
                schema: "Application",
                table: "Application",
                newName: "Name");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Wallet",
                table: "WalletAddress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Wallet",
                table: "Wallet",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "TelcoType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Affiliate",
                table: "SubscriptionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Affiliate",
                table: "Subscription",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Storage",
                table: "StorageFileType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Storage",
                table: "StorageFileIdentifierGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Storage",
                table: "StorageFileIdentifier",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "BlobContainer",
                schema: "Storage",
                table: "StorageFile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Storage",
                table: "StorageFile",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "SessionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "SessionData",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Registry",
                table: "RegistryFavoriteType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Registry",
                table: "RegistryConfigurationGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "ProviderType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "MetaData",
                table: "MetaDataType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "MetaData",
                table: "MetaDataEntityGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "MetaData",
                table: "MetaData",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageThreadType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageThreadMemberGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageThread",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageReactionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageReaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageFiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageDeliveryType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageDelivery",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Messaging",
                table: "Message",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Income",
                table: "IncomeType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Income",
                table: "IncomeTransaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Income",
                table: "IncomePartition",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Income",
                table: "IncomeDistribution",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityVerificationType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityVerification",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityRoleEntityGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityRole",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityFavorite",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityContactType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityContactGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityContact",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityAddressType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "Identity",
                table: "IdentityAddress",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseStatusType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Finance",
                table: "ExchangeRate",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "ELoadTransaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Wallet",
                table: "DepositRequest",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Finance",
                table: "CurrencyType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Community",
                table: "CommunityIdentityType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Community",
                table: "CommunityIdentityFileType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Community",
                table: "CommunityIdentityFile",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Community",
                table: "CommunityIdentity",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Community",
                table: "CommunityContentType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Community",
                table: "CommunityContentReactionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Community",
                table: "CommunityContentReaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Community",
                table: "CommunityContentFiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Community",
                table: "CommunityContent",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Community",
                table: "CommunityConnectionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Community",
                table: "CommunityConnection",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Affiliate",
                table: "CommissionDeductionRequest",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "BusinessPackage",
                table: "BusinessPackageUpgradeTransaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "BusinessPackage",
                table: "BusinessPackageType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Affiliate",
                table: "BinaryMap",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Affiliate",
                table: "BinaryListMultiplex",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Affiliate",
                table: "BinaryList",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<decimal>(
                name: "Width",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "numeric(2)",
                precision: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(2,0)",
                oldPrecision: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "BillerCategory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "Biller",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "GeoLocation",
                table: "AddressRegion",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "GeoLocation",
                table: "AddressProvince",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "GeoLocation",
                table: "AddressCountry",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "GeoLocation",
                table: "AddressCity",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "GeoLocation",
                table: "AddressBarangay",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.InsertData(
                schema: "Identity",
                table: "IdentityAddressType",
                columns: new[] { "ID", "ConcurrencyStamp", "CreatedAt", "DeletedAt", "IsDeleted", "IsEnabled", "ModifiedAt", "Name", "TenantId" },
                values: new object[,]
                {
                    { new Guid("067b21a1-1cba-4c57-b357-43a6fab0a18b"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "BUSINESS", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("08fb17f1-f4ae-4540-b7ae-03dad680f9ea"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3afe8862-9881-49b7-885c-fbe96544e07d"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK_RIDER", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5d6f29ff-9779-44df-9900-40550bdf9d19"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "HOME", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("67ab57af-d5ce-4562-8797-3b53e0b42221"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK_DOCTOR", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9f6cdc72-8ee3-4934-88c0-6aba12c69bbf"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK_LABORATORY", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b4bda700-03c1-4a8a-bf6d-6043704cf767"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "PERSONAL", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f9da3fec-e466-413a-b88e-adbbc26cff74"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "WORK_PHARMACY", new Guid("00000000-0000-0000-0000-000000000000") }
                });

            migrationBuilder.InsertData(
                schema: "Identity",
                table: "IdentityContactType",
                columns: new[] { "ID", "ConcurrencyStamp", "CreatedAt", "DeletedAt", "IsDeleted", "IsEnabled", "ModifiedAt", "Name", "TenantId" },
                values: new object[,]
                {
                    { new Guid("03f26cc1-e4c2-424f-9d5b-b22d006ae45b"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "Email", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("17583df0-c1b2-47a7-875b-2d9b44f55249"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "Twitter", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2fa27f70-d083-4327-b04e-74e1295cb4be"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "LinkedIn", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4e5edd0d-5c16-4955-9323-3c6e86b54f0b"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "Instagram", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("cdc88887-c7e7-415e-9d43-cc0050d523d3"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "Phone", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d89c4b4a-2077-44ea-958e-4327d191a14c"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, false, true, null, "Facebook", new Guid("00000000-0000-0000-0000-000000000000") }
                });

            migrationBuilder.InsertData(
                schema: "Identity",
                table: "IdentityVerificationType",
                columns: new[] { "ID", "ConcurrencyStamp", "CreatedAt", "DefaultExpiry", "DeletedAt", "IsDeleted", "IsEnabled", "ModifiedAt", "Name", "Priority", "TenantId" },
                values: new object[,]
                {
                    { new Guid("41b5d12c-ce50-4af6-b68f-79443bd5c489"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1051200L, null, false, false, null, "KYC", null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 10L, null, false, false, null, "SMS", null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fe1197ba-dfee-4a4e-b2d3-f8f8c48796be"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 120L, null, false, false, null, "Email", null, new Guid("00000000-0000-0000-0000-000000000000") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("067b21a1-1cba-4c57-b357-43a6fab0a18b"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("08fb17f1-f4ae-4540-b7ae-03dad680f9ea"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("3afe8862-9881-49b7-885c-fbe96544e07d"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("5d6f29ff-9779-44df-9900-40550bdf9d19"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("67ab57af-d5ce-4562-8797-3b53e0b42221"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("9f6cdc72-8ee3-4934-88c0-6aba12c69bbf"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("b4bda700-03c1-4a8a-bf6d-6043704cf767"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("f9da3fec-e466-413a-b88e-adbbc26cff74"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("03f26cc1-e4c2-424f-9d5b-b22d006ae45b"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("17583df0-c1b2-47a7-875b-2d9b44f55249"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("2fa27f70-d083-4327-b04e-74e1295cb4be"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("4e5edd0d-5c16-4955-9323-3c6e86b54f0b"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("cdc88887-c7e7-415e-9d43-cc0050d523d3"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("d89c4b4a-2077-44ea-958e-4327d191a14c"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("41b5d12c-ce50-4af6-b68f-79443bd5c489"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4"));

            migrationBuilder.DeleteData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("fe1197ba-dfee-4a4e-b2d3-f8f8c48796be"));

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Wallet",
                table: "WalletAddress");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "TelcoType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "TelcoProductCode");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "TelcoPrefix");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Affiliate",
                table: "SubscriptionType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Affiliate",
                table: "Subscription");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Storage",
                table: "StorageFileType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Storage",
                table: "StorageFileIdentifierGroup");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Storage",
                table: "StorageFileIdentifier");

            migrationBuilder.DropColumn(
                name: "BlobContainer",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "SessionType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "SessionData");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Registry",
                table: "RegistryFavoriteType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Registry",
                table: "RegistryConfigurationGroup");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "ProviderType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "MetaData",
                table: "MetaDataType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "MetaData",
                table: "MetaDataEntityGroup");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "MetaData",
                table: "MetaData");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageThreadType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageThreadMemberRole");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageThreadMemberGroup");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageThread");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageReactionType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageReaction");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageFiles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageDeliveryType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "MessageDelivery");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Income",
                table: "IncomeType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Income",
                table: "IncomeTransaction");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Income",
                table: "IncomePartition");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Income",
                table: "IncomeDistribution");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityVerificationType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityRoleEntityGroup");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityRole");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityFavorite");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityContactType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityContactGroup");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityContact");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityAddressType");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseStatusType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "GatewayNumber");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.PaymentGateway",
                table: "Gateway");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Finance",
                table: "ExchangeRate");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.ELoad",
                table: "ELoadTransaction");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Finance",
                table: "CurrencyType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Community",
                table: "CommunityIdentityType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Community",
                table: "CommunityIdentityFileType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Community",
                table: "CommunityIdentityFile");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Community",
                table: "CommunityIdentity");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Community",
                table: "CommunityContentType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Community",
                table: "CommunityContentReactionType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Community",
                table: "CommunityContentReaction");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Community",
                table: "CommunityContentFiles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Community",
                table: "CommunityContent");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Community",
                table: "CommunityConnectionType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Community",
                table: "CommunityConnection");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Affiliate",
                table: "CommissionDeductionRequest");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "BusinessPackage",
                table: "BusinessPackageUpgradeTransaction");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "BusinessPackage",
                table: "BusinessPackageType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "BusinessPackage",
                table: "BusinessPackage");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Affiliate",
                table: "BinaryMap");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Affiliate",
                table: "BinaryListMultiplex");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Affiliate",
                table: "BinaryList");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "BillerField");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "BillerCategory");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Integration.BillsPayment",
                table: "Biller");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Audit",
                table: "AuthorizationLog");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "GeoLocation",
                table: "AddressRegion");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "GeoLocation",
                table: "AddressProvince");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "GeoLocation",
                table: "AddressCity");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "GeoLocation",
                table: "AddressBarangay");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                schema: "Wallet",
                table: "WalletType",
                newName: "ApplicationId");

            migrationBuilder.RenameIndex(
                name: "IX_WalletType_TenantId",
                schema: "Wallet",
                table: "WalletType",
                newName: "IX_WalletType_ApplicationId");

            migrationBuilder.RenameColumn(
                name: "ApplicationId",
                schema: "Registry",
                table: "RegistryConfiguration",
                newName: "ApplicationID");

            migrationBuilder.RenameIndex(
                name: "IX_RegistryConfiguration_ApplicationId",
                schema: "Registry",
                table: "RegistryConfiguration",
                newName: "IX_RegistryConfiguration_ApplicationID");

            migrationBuilder.RenameColumn(
                name: "ApplicationId",
                schema: "Audit",
                table: "Log",
                newName: "ApplicationID");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityRoleType",
                newName: "ApplicationId");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityRoleType_TenantId",
                schema: "Identity",
                table: "IdentityRoleType",
                newName: "IX_IdentityRoleType_ApplicationId");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityInformation",
                newName: "ApplicationID");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityInformation_TenantId",
                schema: "Identity",
                table: "IdentityInformation",
                newName: "IX_IdentityInformation_ApplicationID");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                schema: "Identity",
                table: "IdentityCredential",
                newName: "ApplicationID");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityCredential_TenantId",
                schema: "Identity",
                table: "IdentityCredential",
                newName: "IX_IdentityCredential_ApplicationID");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                schema: "Application",
                table: "Application",
                newName: "EnterpriseID");

            migrationBuilder.RenameColumn(
                name: "Name",
                schema: "Application",
                table: "Application",
                newName: "AppName");

            migrationBuilder.AlterColumn<decimal>(
                name: "Width",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "numeric(2,0)",
                precision: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(2)",
                oldPrecision: 2,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Enterprise",
                schema: "Application",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "(uuid_generate_v4())"),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_tbl_Enterprises", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Application_EnterpriseID",
                schema: "Application",
                table: "Application",
                column: "EnterpriseID");

            migrationBuilder.AddForeignKey(
                name: "tbl_applications_tbl_enterprises_id_fk",
                schema: "Application",
                table: "Application",
                column: "EnterpriseID",
                principalSchema: "Application",
                principalTable: "Enterprise",
                principalColumn: "ID");
        }
    }
}
