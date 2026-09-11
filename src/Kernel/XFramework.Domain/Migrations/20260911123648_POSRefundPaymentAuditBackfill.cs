using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFramework.Domain.Migrations
{
    /// <inheritdoc />
    public partial class POSRefundPaymentAuditBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH completed_refunds AS (
                    SELECT "TenantId", "SaleId", SUM("TotalRefundAmount") AS "RefundedAmount"
                    FROM "POS"."PosReturn"
                    WHERE "Status" = 3 AND NOT "IsDeleted"
                    GROUP BY "TenantId", "SaleId"
                )
                UPDATE "POS"."PosPayment" AS payment
                SET "RefundedAmount" = LEAST(payment."Amount", completed."RefundedAmount"),
                    "Status" = CASE
                        WHEN completed."RefundedAmount" >= payment."Amount" THEN 3
                        ELSE 1
                    END
                FROM completed_refunds AS completed
                WHERE payment."TenantId" = completed."TenantId"
                  AND payment."SaleId" = completed."SaleId"
                  AND payment."Status" IN (1, 3)
                  AND NOT payment."IsDeleted";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // RefundedAmount is financial audit data and must not be erased during rollback.
        }
    }
}
