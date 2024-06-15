using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Updated_IdentityAddress_Removed_Required_Fk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "tbl_identityaddresses__id_fk",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropForeignKey(
                name: "tbl_identityaddresses__id_fk_brgy",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropForeignKey(
                name: "tbl_identityaddresses__id_fk_city",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropForeignKey(
                name: "tbl_identityaddresses__id_fk_province",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropForeignKey(
                name: "tbl_identityaddresses_tbl_addresscountry__fk",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.AlterColumn<Guid>(
                name: "RegionId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProvinceId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "CountryId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "CityId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "BarangayId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "tbl_identityaddresses__id_fk",
                schema: "Identity",
                table: "IdentityAddress",
                column: "RegionId",
                principalSchema: "GeoLocation",
                principalTable: "AddressRegion",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "tbl_identityaddresses__id_fk_brgy",
                schema: "Identity",
                table: "IdentityAddress",
                column: "BarangayId",
                principalSchema: "GeoLocation",
                principalTable: "AddressBarangay",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "tbl_identityaddresses__id_fk_city",
                schema: "Identity",
                table: "IdentityAddress",
                column: "CityId",
                principalSchema: "GeoLocation",
                principalTable: "AddressCity",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "tbl_identityaddresses__id_fk_province",
                schema: "Identity",
                table: "IdentityAddress",
                column: "ProvinceId",
                principalSchema: "GeoLocation",
                principalTable: "AddressProvince",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "tbl_identityaddresses_tbl_addresscountry__fk",
                schema: "Identity",
                table: "IdentityAddress",
                column: "CountryId",
                principalSchema: "GeoLocation",
                principalTable: "AddressCountry",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "tbl_identityaddresses__id_fk",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropForeignKey(
                name: "tbl_identityaddresses__id_fk_brgy",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropForeignKey(
                name: "tbl_identityaddresses__id_fk_city",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropForeignKey(
                name: "tbl_identityaddresses__id_fk_province",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropForeignKey(
                name: "tbl_identityaddresses_tbl_addresscountry__fk",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.AlterColumn<Guid>(
                name: "RegionId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProvinceId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CountryId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CityId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BarangayId",
                schema: "Identity",
                table: "IdentityAddress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "tbl_identityaddresses__id_fk",
                schema: "Identity",
                table: "IdentityAddress",
                column: "RegionId",
                principalSchema: "GeoLocation",
                principalTable: "AddressRegion",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "tbl_identityaddresses__id_fk_brgy",
                schema: "Identity",
                table: "IdentityAddress",
                column: "BarangayId",
                principalSchema: "GeoLocation",
                principalTable: "AddressBarangay",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "tbl_identityaddresses__id_fk_city",
                schema: "Identity",
                table: "IdentityAddress",
                column: "CityId",
                principalSchema: "GeoLocation",
                principalTable: "AddressCity",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "tbl_identityaddresses__id_fk_province",
                schema: "Identity",
                table: "IdentityAddress",
                column: "ProvinceId",
                principalSchema: "GeoLocation",
                principalTable: "AddressProvince",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "tbl_identityaddresses_tbl_addresscountry__fk",
                schema: "Identity",
                table: "IdentityAddress",
                column: "CountryId",
                principalSchema: "GeoLocation",
                principalTable: "AddressCountry",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
