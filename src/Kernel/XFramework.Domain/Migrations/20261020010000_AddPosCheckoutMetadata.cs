using Microsoft.EntityFrameworkCore.Migrations;

namespace XFramework.Domain.Migrations;

public sealed partial class AddPosCheckoutMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "CheckoutMetadataJson", schema: "POS",
            table: "PosCart", type: "jsonb", nullable: true);
        migrationBuilder.AddColumn<string>(name: "CheckoutMetadataJson", schema: "POS",
            table: "PosSale", type: "jsonb", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CheckoutMetadataJson", schema: "POS", table: "PosCart");
        migrationBuilder.DropColumn(name: "CheckoutMetadataJson", schema: "POS", table: "PosSale");
    }
}
