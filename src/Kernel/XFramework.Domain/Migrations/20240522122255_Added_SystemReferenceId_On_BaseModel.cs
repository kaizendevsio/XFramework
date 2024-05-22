using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Added_SystemReferenceId_On_BaseModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "WalletType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "WalletAddress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "Wallet",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Affiliate",
                table: "SubscriptionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Affiliate",
                table: "Subscription",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFileType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFileIdentifierGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFileIdentifier",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFile",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "SessionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "Session",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Registry",
                table: "RegistryFavoriteType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Registry",
                table: "RegistryConfigurationGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Registry",
                table: "RegistryConfiguration",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "MetaData",
                table: "MetaDataType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "MetaData",
                table: "MetaDataEntityGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "MetaData",
                table: "MetaData",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageThreadType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageThreadMemberGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageThread",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageReactionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageReaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageFiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageDeliveryType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageDelivery",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "Message",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityVerificationType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityVerification",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityRoleType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityRoleEntityGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityRole",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityInformation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityFavorite",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityCredential",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityContactType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityContactGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityContact",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityAddressType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseStatusType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Finance",
                table: "ExchangeRate",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "DepositRequest",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Finance",
                table: "CurrencyType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityIdentityType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityIdentityFileType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityIdentityFile",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityIdentity",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityContentType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityContentReactionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityContentReaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityContentFiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityContent",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityConnectionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityConnection",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Application",
                table: "Application",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "GeoLocation",
                table: "AddressRegion",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "GeoLocation",
                table: "AddressProvince",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "GeoLocation",
                table: "AddressCountry",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "GeoLocation",
                table: "AddressCity",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "GeoLocation",
                table: "AddressBarangay",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("23c13259-1e24-427d-ba89-a4d2506c7464"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("337ee33d-445f-4e6e-bc61-8709170b0ee4"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("4eec62eb-08ef-406c-9ea2-2ac2d6e0f206"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("54ab2c38-be75-4572-916b-72019d676162"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("66c8ab89-f24d-4aea-af1a-9ac6a8263575"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityAddressType",
                keyColumn: "ID",
                keyValue: new Guid("c9136227-f5dc-4147-984d-70aa855090e4"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityContactGroup",
                keyColumn: "ID",
                keyValue: new Guid("067b21a1-1cba-4c57-b357-43a6fab0a18b"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityContactGroup",
                keyColumn: "ID",
                keyValue: new Guid("08fb17f1-f4ae-4540-b7ae-03dad680f9ea"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityContactGroup",
                keyColumn: "ID",
                keyValue: new Guid("5d6f29ff-9779-44df-9900-40550bdf9d19"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityContactGroup",
                keyColumn: "ID",
                keyValue: new Guid("b4bda700-03c1-4a8a-bf6d-6043704cf767"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("03f26cc1-e4c2-424f-9d5b-b22d006ae45b"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("17583df0-c1b2-47a7-875b-2d9b44f55249"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("2fa27f70-d083-4327-b04e-74e1295cb4be"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("4e5edd0d-5c16-4955-9323-3c6e86b54f0b"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("cdc88887-c7e7-415e-9d43-cc0050d523d3"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityContactType",
                keyColumn: "ID",
                keyValue: new Guid("d89c4b4a-2077-44ea-958e-4327d191a14c"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("41b5d12c-ce50-4af6-b68f-79443bd5c489"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("45a7a8a7-3735-4a58-b93f-aa9e7b24a7c4"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                schema: "Identity",
                table: "IdentityVerificationType",
                keyColumn: "ID",
                keyValue: new Guid("fe1197ba-dfee-4a4e-b2d3-f8f8c48796be"),
                column: "SystemReferenceId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "WalletType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "WalletAddress");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Affiliate",
                table: "SubscriptionType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Affiliate",
                table: "Subscription");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFileType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFileIdentifierGroup");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFileIdentifier");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "SessionType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "Session");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Registry",
                table: "RegistryFavoriteType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Registry",
                table: "RegistryConfigurationGroup");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Registry",
                table: "RegistryConfiguration");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "MetaData",
                table: "MetaDataType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "MetaData",
                table: "MetaDataEntityGroup");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "MetaData",
                table: "MetaData");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageThreadType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageThreadMemberRole");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageThreadMemberGroup");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageThread");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageReactionType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageReaction");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageFiles");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageDeliveryType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageDelivery");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityVerificationType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityRoleType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityRoleEntityGroup");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityRole");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityInformation");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityFavorite");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityContactType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityContactGroup");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityContact");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityAddressType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseStatusType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Integration.PaymentGateway",
                table: "Gateway");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Finance",
                table: "ExchangeRate");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Finance",
                table: "CurrencyType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityIdentityType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityIdentityFileType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityIdentityFile");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityIdentity");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityContentType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityContentReactionType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityContentReaction");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityContentFiles");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityContent");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityConnectionType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityConnection");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Audit",
                table: "AuthorizationLog");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Application",
                table: "Application");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "GeoLocation",
                table: "AddressRegion");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "GeoLocation",
                table: "AddressProvince");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "GeoLocation",
                table: "AddressCity");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "GeoLocation",
                table: "AddressBarangay");
        }
    }
}
