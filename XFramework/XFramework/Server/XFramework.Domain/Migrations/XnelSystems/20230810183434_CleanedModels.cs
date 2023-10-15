using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations.XnelSystems
{
    /// <inheritdoc />
    public partial class CleanedModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Wallet",
                table: "WalletType");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "WalletType");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Wallet",
                table: "WalletAddress");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "WalletAddress");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "SessionType");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Identity",
                table: "SessionType");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "SessionData");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Identity",
                table: "SessionData");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Income",
                table: "IncomeType");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Income",
                table: "IncomeType");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Income",
                table: "IncomeTransaction");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Income",
                table: "IncomeTransaction");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Income",
                table: "IncomePartition");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Income",
                table: "IncomePartition");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Income",
                table: "IncomeDistribution");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Income",
                table: "IncomeDistribution");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityVerificationType");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityRoleType");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityRoleType");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityRole");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityRole");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityInformation");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityInformation");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityContactType");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityContactType");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityContact");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityContact");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityAddressType");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityAddressType");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Finance",
                table: "ExchangeRate");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Finance",
                table: "ExchangeRate");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Application",
                table: "Enterprise");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Application",
                table: "Enterprise");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Finance",
                table: "CurrencyType");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Finance",
                table: "CurrencyType");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageType");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageType");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "BusinessPackage",
                table: "BusinessPackage");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "BusinessPackage",
                table: "BusinessPackage");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Affiliate",
                table: "BinaryMap");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Affiliate",
                table: "BinaryMap");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "Audit",
                table: "AuthorizationLog");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "Audit",
                table: "AuthorizationLog");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.RenameColumn(
                name: "LastChanged",
                schema: "Wallet",
                table: "WalletType",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.ELoad",
                table: "TelcoType",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.BillsPayment",
                table: "ProviderType",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "LastChanged",
                schema: "Income",
                table: "IncomeDistribution",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.PaymentGateway",
                table: "GatewayType",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "LastChanged",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "LastChanged",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "LastChanged",
                schema: "Affiliate",
                table: "BinaryMap",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.BillsPayment",
                table: "BillerCategory",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "Integration.BillsPayment",
                table: "Biller",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                schema: "GeoLocation",
                table: "AddressRegion",
                newName: "ModifiedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                schema: "GeoLocation",
                table: "AddressProvince",
                newName: "ModifiedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedOn",
                schema: "GeoLocation",
                table: "AddressCountry",
                newName: "ModifiedAt");

            migrationBuilder.RenameColumn(
                name: "LastChanged",
                schema: "GeoLocation",
                table: "AddressCountry",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                schema: "GeoLocation",
                table: "AddressCity",
                newName: "ModifiedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                schema: "GeoLocation",
                table: "AddressBarangay",
                newName: "ModifiedAt");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "WalletType",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Wallet",
                table: "WalletType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "WalletType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Wallet",
                table: "WalletAddress",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "WalletAddress",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "WalletAddress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Wallet",
                table: "WalletAddress",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Wallet",
                table: "WalletAddress",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Wallet",
                table: "Wallet",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "Wallet",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Wallet",
                table: "Wallet",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "Wallet",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Wallet",
                table: "Wallet",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.ELoad",
                table: "TelcoType",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.ELoad",
                table: "TelcoType",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.ELoad",
                table: "TelcoType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "TelcoType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                type: "boolean",
                nullable: false,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "SubscriptionType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "SubscriptionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Affiliate",
                table: "SubscriptionType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "Subscription",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "Subscription",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Affiliate",
                table: "Subscription",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Storage",
                table: "StorageFileType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Storage",
                table: "StorageFileType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Storage",
                table: "StorageFileType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Storage",
                table: "StorageFileIdentifierGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Storage",
                table: "StorageFileIdentifierGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Storage",
                table: "StorageFileIdentifierGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Storage",
                table: "StorageFileIdentifier",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Storage",
                table: "StorageFileIdentifier",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Storage",
                table: "StorageFileIdentifier",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Storage",
                table: "StorageFile",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Storage",
                table: "StorageFile",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Storage",
                table: "StorageFile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "SessionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "SessionType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "SessionData",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "SessionData",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Registry",
                table: "RegistryFavoriteType",
                type: "boolean",
                nullable: false,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Registry",
                table: "RegistryFavoriteType",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Registry",
                table: "RegistryFavoriteType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Registry",
                table: "RegistryFavoriteType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Registry",
                table: "RegistryFavoriteType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Registry",
                table: "RegistryConfigurationGroup",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Registry",
                table: "RegistryConfigurationGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Registry",
                table: "RegistryConfigurationGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Registry",
                table: "RegistryConfigurationGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "Registry",
                table: "RegistryConfigurationGroup",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Registry",
                table: "RegistryConfiguration",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Registry",
                table: "RegistryConfiguration",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Registry",
                table: "RegistryConfiguration",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "Registry",
                table: "RegistryConfiguration",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.BillsPayment",
                table: "ProviderType",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "ProviderType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "ProviderType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaDataType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaDataType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaDataType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaDataEntityGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaDataEntityGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaDataEntityGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaData",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaData",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaData",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageThreadType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageThreadType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageThreadType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageThreadMemberGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageThreadMemberGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageThreadMemberGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageThread",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageThread",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageThread",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageReactionType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageReactionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageReactionType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageReaction",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageReaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageReaction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageFiles",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageFiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageDirect",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageDirect",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageDirect",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageDeliveryType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageDeliveryType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageDeliveryType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageDelivery",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageDelivery",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageDelivery",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "Message",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "Message",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Messaging",
                table: "Message",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Audit",
                table: "Log",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Audit",
                table: "Log",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Audit",
                table: "Log",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "Audit",
                table: "Log",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Audit",
                table: "Log",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Income",
                table: "IncomeType",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Income",
                table: "IncomeType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Income",
                table: "IncomeType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Income",
                table: "IncomeType",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Income",
                table: "IncomeTransaction",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Income",
                table: "IncomeTransaction",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Income",
                table: "IncomeTransaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Income",
                table: "IncomeTransaction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Income",
                table: "IncomeTransaction",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Income",
                table: "IncomePartition",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Income",
                table: "IncomePartition",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Income",
                table: "IncomePartition",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Income",
                table: "IncomePartition",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Income",
                table: "IncomePartition",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Income",
                table: "IncomeDistribution",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Income",
                table: "IncomeDistribution",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Income",
                table: "IncomeDistribution",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Identity",
                table: "IdentityVerificationType",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "IdentityVerificationType",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Identity",
                table: "IdentityVerificationType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityVerificationType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityVerificationType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Identity",
                table: "IdentityVerification",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "IdentityVerification",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Identity",
                table: "IdentityVerification",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityVerification",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityVerification",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityRoleType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityRoleType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Identity",
                table: "IdentityRoleEntityGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityRoleEntityGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityRoleEntityGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityRole",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityRole",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityInformation",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityInformation",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Identity",
                table: "IdentityFavorite",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Identity",
                table: "IdentityFavorite",
                type: "boolean",
                nullable: false,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "IdentityFavorite",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityFavorite",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityFavorite",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityCredential",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityCredential",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityContactType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityContactType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Identity",
                table: "IdentityContactGroup",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityContactGroup",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityContactGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityContact",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityContact",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityAddressType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityAddressType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityAddress",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.PaymentGateway",
                table: "GatewayType",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseStatusType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseStatusType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseStatusType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                type: "boolean",
                nullable: false,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "isEnabled",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory",
                type: "boolean",
                nullable: false,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                type: "boolean",
                nullable: false,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Finance",
                table: "ExchangeRate",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Finance",
                table: "ExchangeRate",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Finance",
                table: "ExchangeRate",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Finance",
                table: "ExchangeRate",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Finance",
                table: "ExchangeRate",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Application",
                table: "Enterprise",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Application",
                table: "Enterprise",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "isEnabled",
                schema: "Integration.ELoad",
                table: "ELoadTransaction",
                type: "boolean",
                nullable: false,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.ELoad",
                table: "ELoadTransaction",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Integration.ELoad",
                table: "ELoadTransaction",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "ELoadTransaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Integration.ELoad",
                table: "ELoadTransaction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Wallet",
                table: "DepositRequest",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "DepositRequest",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "DepositRequest",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Wallet",
                table: "DepositRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Wallet",
                table: "DepositRequest",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Finance",
                table: "CurrencyType",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Finance",
                table: "CurrencyType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Finance",
                table: "CurrencyType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Finance",
                table: "CurrencyType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Finance",
                table: "CurrencyType",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityIdentityType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityIdentityType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityIdentityType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityIdentityFileType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityIdentityFileType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityIdentityFileType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityIdentityFile",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityIdentityFile",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityIdentityFile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityIdentity",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityIdentity",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityIdentity",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityContentType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityContentType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityContentType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityContentReactionType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityContentReactionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityContentReactionType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityContentReaction",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityContentReaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityContentReaction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityContentFiles",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityContentFiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityContentFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityContent",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityContent",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityContent",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityConnectionType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityConnectionType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityConnectionType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityConnection",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityConnection",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityConnection",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "CommissionDeductionRequest",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Affiliate",
                table: "CommissionDeductionRequest",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "CommissionDeductionRequest",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Affiliate",
                table: "CommissionDeductionRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageUpgradeTransaction",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "BusinessPackage",
                table: "BusinessPackageUpgradeTransaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageUpgradeTransaction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "BusinessPackage",
                table: "BusinessPackageType",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "BusinessPackage",
                table: "BusinessPackageType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageType",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "BusinessPackage",
                table: "BusinessPackageType",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "BinaryMap",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Affiliate",
                table: "BinaryMap",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "BinaryMap",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Affiliate",
                table: "BinaryMap",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "BinaryListMultiplex",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "BinaryListMultiplex",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Affiliate",
                table: "BinaryListMultiplex",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "Affiliate",
                table: "BinaryListMultiplex",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "BinaryList",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Affiliate",
                table: "BinaryList",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "BinaryList",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Affiliate",
                table: "BinaryList",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "Affiliate",
                table: "BinaryList",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "isEnabled",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "boolean",
                nullable: false,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

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

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "isEnabled",
                schema: "Integration.BillsPayment",
                table: "BillerCategory",
                type: "boolean",
                nullable: false,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.BillsPayment",
                table: "BillerCategory",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "BillerCategory",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "BillerCategory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.BillsPayment",
                table: "Biller",
                type: "boolean",
                nullable: false,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.BillsPayment",
                table: "Biller",
                type: "boolean",
                nullable: false,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true,
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "Biller",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "Biller",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Application",
                table: "Application",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "Application",
                table: "Application",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "Application",
                table: "Application",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Application",
                table: "Application",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "Application",
                table: "Application",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Application",
                table: "Application",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "GeoLocation",
                table: "AddressRegion",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "GeoLocation",
                table: "AddressRegion",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "GeoLocation",
                table: "AddressRegion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "GeoLocation",
                table: "AddressRegion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "GeoLocation",
                table: "AddressRegion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "GeoLocation",
                table: "AddressProvince",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "GeoLocation",
                table: "AddressProvince",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "GeoLocation",
                table: "AddressProvince",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "GeoLocation",
                table: "AddressProvince",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "GeoLocation",
                table: "AddressProvince",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "GeoLocation",
                table: "AddressCountry",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "GeoLocation",
                table: "AddressCountry",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "GeoLocation",
                table: "AddressCountry",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "GeoLocation",
                table: "AddressCountry",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "GeoLocation",
                table: "AddressCity",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "GeoLocation",
                table: "AddressCity",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "GeoLocation",
                table: "AddressCity",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "GeoLocation",
                table: "AddressCity",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "GeoLocation",
                table: "AddressCity",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "GeoLocation",
                table: "AddressBarangay",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                schema: "GeoLocation",
                table: "AddressBarangay",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "GeoLocation",
                table: "AddressBarangay",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "GeoLocation",
                table: "AddressBarangay",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "GeoLocation",
                table: "AddressBarangay",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Wallet",
                table: "WithdrawalRequest");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "WalletType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Wallet",
                table: "WalletTransaction");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "WalletAddress");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Wallet",
                table: "WalletAddress");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Wallet",
                table: "WalletAddress");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Wallet",
                table: "Wallet");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "TelcoType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "TelcoProductCode");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "TelcoPrefix");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "SubscriptionType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Affiliate",
                table: "SubscriptionType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "Subscription");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Affiliate",
                table: "Subscription");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Storage",
                table: "StorageFileType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Storage",
                table: "StorageFileType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Storage",
                table: "StorageFileIdentifierGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Storage",
                table: "StorageFileIdentifierGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Storage",
                table: "StorageFileIdentifier");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Storage",
                table: "StorageFileIdentifier");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Storage",
                table: "StorageFile");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "SessionType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "SessionType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "SessionData");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "SessionData");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Registry",
                table: "RegistryFavoriteType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Registry",
                table: "RegistryFavoriteType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Registry",
                table: "RegistryConfigurationGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Registry",
                table: "RegistryConfigurationGroup");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "Registry",
                table: "RegistryConfigurationGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Registry",
                table: "RegistryConfiguration");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Registry",
                table: "RegistryConfiguration");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "Registry",
                table: "RegistryConfiguration");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "ProviderType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaDataType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaDataType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaDataEntityGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaDataEntityGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "MetaData",
                table: "MetaData");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "MetaData",
                table: "MetaData");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageThreadType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageThreadType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageThreadMemberRole");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageThreadMemberRole");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageThreadMemberGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageThreadMemberGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageThreadMember");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageThread");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageThread");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageReactionType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageReactionType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageReaction");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageReaction");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageFiles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageFiles");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageDirect");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageDeliveryType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageDeliveryType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "MessageDelivery");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "MessageDelivery");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Messaging",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Audit",
                table: "Log");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Audit",
                table: "Log");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "Audit",
                table: "Log");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Audit",
                table: "Log");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Income",
                table: "IncomeType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Income",
                table: "IncomeType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Income",
                table: "IncomeType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Income",
                table: "IncomeTransaction");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Income",
                table: "IncomeTransaction");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Income",
                table: "IncomeTransaction");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Income",
                table: "IncomePartition");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Income",
                table: "IncomePartition");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Income",
                table: "IncomePartition");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Income",
                table: "IncomeDistribution");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Income",
                table: "IncomeDistribution");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityVerificationType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityVerificationType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityVerification");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityRoleType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityRoleType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityRoleEntityGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityRoleEntityGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityRole");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityRole");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityInformation");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityInformation");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityFavorite");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityFavorite");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityCredential");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityContactType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityContactType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityContactGroup");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityContactGroup");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityContact");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityContact");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityAddressType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityAddressType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseStatusType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseStatusType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "GatewayNumber");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.PaymentGateway",
                table: "Gateway");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Finance",
                table: "ExchangeRate");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Finance",
                table: "ExchangeRate");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Finance",
                table: "ExchangeRate");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Application",
                table: "Enterprise");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Application",
                table: "Enterprise");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.ELoad",
                table: "ELoadTransaction");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Integration.ELoad",
                table: "ELoadTransaction");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Wallet",
                table: "DepositRequest");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Finance",
                table: "CurrencyType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Finance",
                table: "CurrencyType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Finance",
                table: "CurrencyType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityIdentityType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityIdentityType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityIdentityFileType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityIdentityFileType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityIdentityFile");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityIdentityFile");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityIdentity");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityIdentity");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityContentType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityContentType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityContentReactionType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityContentReactionType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityContentReaction");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityContentReaction");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityContentFiles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityContentFiles");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityContent");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityContent");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityConnectionType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityConnectionType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Community",
                table: "CommunityConnection");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Community",
                table: "CommunityConnection");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "CommissionDeductionRequest");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Affiliate",
                table: "CommissionDeductionRequest");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "BusinessPackage",
                table: "BusinessPackageUpgradeTransaction");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageUpgradeTransaction");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "BusinessPackage",
                table: "BusinessPackageType");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "BusinessPackage",
                table: "BusinessPackageType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "BusinessPackage",
                table: "BusinessPackage");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "BusinessPackage",
                table: "BusinessPackage");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "BusinessPackage",
                table: "BusinessPackage");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "BinaryMap");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Affiliate",
                table: "BinaryMap");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "BinaryListMultiplex");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Affiliate",
                table: "BinaryListMultiplex");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "Affiliate",
                table: "BinaryListMultiplex");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Affiliate",
                table: "BinaryList");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Affiliate",
                table: "BinaryList");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "Affiliate",
                table: "BinaryList");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "BillerField");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "BillerCategory");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Integration.BillsPayment",
                table: "Biller");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Audit",
                table: "AuthorizationLog");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Audit",
                table: "AuthorizationLog");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Audit",
                table: "AuthorizationLog");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "Application",
                table: "Application");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "Application",
                table: "Application");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Application",
                table: "Application");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "Application",
                table: "Application");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Application",
                table: "Application");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "GeoLocation",
                table: "AddressRegion");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "GeoLocation",
                table: "AddressRegion");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "GeoLocation",
                table: "AddressRegion");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "GeoLocation",
                table: "AddressRegion");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "GeoLocation",
                table: "AddressProvince");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "GeoLocation",
                table: "AddressProvince");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "GeoLocation",
                table: "AddressProvince");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "GeoLocation",
                table: "AddressProvince");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "GeoLocation",
                table: "AddressCountry");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "GeoLocation",
                table: "AddressCity");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "GeoLocation",
                table: "AddressCity");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "GeoLocation",
                table: "AddressCity");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "GeoLocation",
                table: "AddressCity");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: "GeoLocation",
                table: "AddressBarangay");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "GeoLocation",
                table: "AddressBarangay");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "GeoLocation",
                table: "AddressBarangay");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "GeoLocation",
                table: "AddressBarangay");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Wallet",
                table: "WalletType",
                newName: "LastChanged");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.ELoad",
                table: "TelcoType",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.BillsPayment",
                table: "ProviderType",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Income",
                table: "IncomeDistribution",
                newName: "LastChanged");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayType",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType",
                newName: "LastChanged");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                newName: "LastChanged");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Affiliate",
                table: "BinaryMap",
                newName: "LastChanged");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.BillsPayment",
                table: "BillerCategory",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "Integration.BillsPayment",
                table: "Biller",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "ModifiedAt",
                schema: "GeoLocation",
                table: "AddressRegion",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedAt",
                schema: "GeoLocation",
                table: "AddressProvince",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedAt",
                schema: "GeoLocation",
                table: "AddressCountry",
                newName: "ModifiedOn");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                schema: "GeoLocation",
                table: "AddressCountry",
                newName: "LastChanged");

            migrationBuilder.RenameColumn(
                name: "ModifiedAt",
                schema: "GeoLocation",
                table: "AddressCity",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "ModifiedAt",
                schema: "GeoLocation",
                table: "AddressBarangay",
                newName: "UpdatedAt");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "WithdrawalRequest",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "WalletType",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Wallet",
                table: "WalletType",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Wallet",
                table: "WalletType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "WalletType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "WalletTransaction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Wallet",
                table: "WalletAddress",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "WalletAddress",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Wallet",
                table: "WalletAddress",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "WalletAddress",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Wallet",
                table: "Wallet",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "Wallet",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Wallet",
                table: "Wallet",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Wallet",
                table: "Wallet",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "Wallet",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.ELoad",
                table: "TelcoType",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.ELoad",
                table: "TelcoType",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.ELoad",
                table: "TelcoType",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                type: "boolean",
                nullable: true,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.ELoad",
                table: "TelcoProductCode",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.ELoad",
                table: "TelcoPrefix",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.ELoad",
                table: "TelcoCodePromo",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "SubscriptionType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "Subscription",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Storage",
                table: "StorageFileType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Storage",
                table: "StorageFileIdentifierGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Storage",
                table: "StorageFileIdentifier",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Storage",
                table: "StorageFile",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "SessionType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Identity",
                table: "SessionType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "SessionData",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Identity",
                table: "SessionData",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Registry",
                table: "RegistryFavoriteType",
                type: "boolean",
                nullable: true,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Registry",
                table: "RegistryFavoriteType",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Registry",
                table: "RegistryFavoriteType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Registry",
                table: "RegistryConfigurationGroup",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Registry",
                table: "RegistryConfigurationGroup",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Registry",
                table: "RegistryConfiguration",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.BillsPayment",
                table: "ProviderType",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "ProviderType",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "ProviderEndpoint",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaDataType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaDataEntityGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "MetaData",
                table: "MetaData",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageThreadType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageThreadMemberRole",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageThreadMemberGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageThreadMember",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageThread",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageReactionType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageReaction",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageFiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageDirect",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageDeliveryType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "MessageDelivery",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Messaging",
                table: "Message",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Audit",
                table: "Log",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Income",
                table: "IncomeType",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Income",
                table: "IncomeType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Income",
                table: "IncomeType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Income",
                table: "IncomeTransaction",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Income",
                table: "IncomeTransaction",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Income",
                table: "IncomeTransaction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Income",
                table: "IncomeTransaction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Income",
                table: "IncomePartition",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Income",
                table: "IncomePartition",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Income",
                table: "IncomePartition",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Income",
                table: "IncomePartition",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Income",
                table: "IncomeDistribution",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Income",
                table: "IncomeDistribution",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Income",
                table: "IncomeDistribution",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Identity",
                table: "IdentityVerificationType",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "IdentityVerificationType",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Identity",
                table: "IdentityVerificationType",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityVerificationType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Identity",
                table: "IdentityVerification",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "IdentityVerification",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Identity",
                table: "IdentityVerification",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityVerification",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityVerification",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityRoleType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityRoleType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Identity",
                table: "IdentityRoleEntityGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityRole",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityRole",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityInformation",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityInformation",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Identity",
                table: "IdentityFavorite",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Identity",
                table: "IdentityFavorite",
                type: "boolean",
                nullable: true,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Identity",
                table: "IdentityFavorite",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityCredential",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityCredential",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityContactType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityContactType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Identity",
                table: "IdentityContactGroup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityContact",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityContact",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityAddressType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityAddressType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Identity",
                table: "IdentityAddress",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Identity",
                table: "IdentityAddress",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.PaymentGateway",
                table: "GatewayType",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayType",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponseStatusType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayResponse",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                type: "boolean",
                nullable: true,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.ELoad",
                table: "GatewayNumber",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayInstructions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayEndpoint",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "isEnabled",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory",
                type: "boolean",
                nullable: true,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.PaymentGateway",
                table: "GatewayCategory",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                type: "boolean",
                nullable: true,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.PaymentGateway",
                table: "Gateway",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Finance",
                table: "ExchangeRate",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Finance",
                table: "ExchangeRate",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Finance",
                table: "ExchangeRate",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Finance",
                table: "ExchangeRate",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Application",
                table: "Enterprise",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Application",
                table: "Enterprise",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "isEnabled",
                schema: "Integration.ELoad",
                table: "ELoadTransaction",
                type: "boolean",
                nullable: true,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.ELoad",
                table: "ELoadTransaction",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Integration.ELoad",
                table: "ELoadTransaction",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Wallet",
                table: "DepositRequest",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Wallet",
                table: "DepositRequest",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Wallet",
                table: "DepositRequest",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Wallet",
                table: "DepositRequest",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Finance",
                table: "CurrencyType",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Finance",
                table: "CurrencyType",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Finance",
                table: "CurrencyType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Finance",
                table: "CurrencyType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityIdentityType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityIdentityFileType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityIdentityFile",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityIdentity",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityContentType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityContentReactionType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityContentReaction",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityContentFiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityContent",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityConnectionType",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Community",
                table: "CommunityConnection",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "CommissionDeductionRequest",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Affiliate",
                table: "CommissionDeductionRequest",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageUpgradeTransaction",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "BusinessPackage",
                table: "BusinessPackageType",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageType",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusionsType",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "BusinessPackage",
                table: "BusinessPackageInclusion",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "BusinessPackage",
                table: "BusinessPackage",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "BinaryMap",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Affiliate",
                table: "BinaryMap",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Affiliate",
                table: "BinaryMap",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Affiliate",
                table: "BinaryMap",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "BinaryListMultiplex",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Affiliate",
                table: "BinaryList",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Affiliate",
                table: "BinaryList",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "BillsPaymentTransaction",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "isEnabled",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "boolean",
                nullable: true,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

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

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "BillerField",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "isEnabled",
                schema: "Integration.BillsPayment",
                table: "BillerCategory",
                type: "boolean",
                nullable: true,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "isDeleted",
                schema: "Integration.BillsPayment",
                table: "BillerCategory",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "BillerCategory",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Integration.BillsPayment",
                table: "Biller",
                type: "boolean",
                nullable: true,
                defaultValueSql: "true",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "true");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "Integration.BillsPayment",
                table: "Biller",
                type: "boolean",
                nullable: true,
                defaultValueSql: "false",
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValueSql: "false");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Integration.BillsPayment",
                table: "Biller",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "Audit",
                table: "AuthorizationLog",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "Application",
                table: "Application",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "GeoLocation",
                table: "AddressRegion",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "GeoLocation",
                table: "AddressProvince",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                schema: "GeoLocation",
                table: "AddressCountry",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<long>(
                name: "CreatedBy",
                schema: "GeoLocation",
                table: "AddressCountry",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                schema: "GeoLocation",
                table: "AddressCountry",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ModifiedBy",
                schema: "GeoLocation",
                table: "AddressCountry",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "GeoLocation",
                table: "AddressCity",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "GeoLocation",
                table: "AddressBarangay",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }
    }
}
