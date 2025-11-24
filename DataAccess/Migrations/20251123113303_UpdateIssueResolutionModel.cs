using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIssueResolutionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequests_BankAccounts_BankAccountId",
                table: "WithdrawalRequests");

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequests_BankAccounts_BankAccountId",
                table: "WithdrawalRequests",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequests_BankAccounts_BankAccountId",
                table: "WithdrawalRequests");

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequests_BankAccounts_BankAccountId",
                table: "WithdrawalRequests",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
