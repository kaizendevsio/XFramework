using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Added_SystemReferenceId_On_Types : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SystemReferenceId",
                schema: "Wallet",
                table: "WalletType",
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
                schema: "Identity",
                table: "SessionType",
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
                table: "MessageThreadMemberGroup",
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
                table: "MessageDeliveryType",
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
                table: "IdentityAddressType",
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
                table: "CommunityConnectionType",
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
                table: "WalletType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Affiliate",
                table: "SubscriptionType");

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
                schema: "Identity",
                table: "SessionType");

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
                schema: "MetaData",
                table: "MetaDataType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "MetaData",
                table: "MetaDataEntityGroup");

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
                table: "MessageThreadMemberGroup");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageReactionType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Messaging",
                table: "MessageDeliveryType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityVerificationType");

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
                table: "IdentityContactType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityContactGroup");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Identity",
                table: "IdentityAddressType");

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
                table: "CommunityContentType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityContentReactionType");

            migrationBuilder.DropColumn(
                name: "SystemReferenceId",
                schema: "Community",
                table: "CommunityConnectionType");
        }
    }
}
