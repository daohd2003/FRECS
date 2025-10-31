using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOrderDepositRefundToOneToOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Clean up duplicate DepositRefunds - keep only the most recent one per Order
            migrationBuilder.Sql(@"
                WITH CTE AS (
                    SELECT 
                        Id,
                        OrderId,
                        CreatedAt,
                        ROW_NUMBER() OVER (PARTITION BY OrderId ORDER BY CreatedAt DESC) AS RowNum
                    FROM DepositRefunds
                )
                DELETE FROM CTE WHERE RowNum > 1;
            ");

            // Step 2: Drop old index
            migrationBuilder.DropIndex(
                name: "IX_DepositRefunds_OrderId",
                table: "DepositRefunds");

            // Step 3: Create unique index for 1-1 relationship
            migrationBuilder.CreateIndex(
                name: "IX_DepositRefunds_OrderId",
                table: "DepositRefunds",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DepositRefunds_OrderId",
                table: "DepositRefunds");

            migrationBuilder.CreateIndex(
                name: "IX_DepositRefunds_OrderId",
                table: "DepositRefunds",
                column: "OrderId");
        }
    }
}
