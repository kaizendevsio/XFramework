using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Updated_IdentityAddress_Added_LatLong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdated",
                schema: "Identity",
                table: "IdentityAddress",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                schema: "Identity",
                table: "IdentityAddress",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                schema: "Identity",
                table: "IdentityAddress",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastUpdated",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropColumn(
                name: "Latitude",
                schema: "Identity",
                table: "IdentityAddress");

            migrationBuilder.DropColumn(
                name: "Longitude",
                schema: "Identity",
                table: "IdentityAddress");
        }
    }
}
