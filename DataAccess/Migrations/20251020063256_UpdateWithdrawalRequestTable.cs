using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateWithdrawalRequestTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_BankAccounts_BankAccountId",
                table: "WithdrawalRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_Users_ProcessedByAdminId",
                table: "WithdrawalRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequest_Users_ProviderId",
                table: "WithdrawalRequest");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WithdrawalRequest",
                table: "WithdrawalRequest");

            migrationBuilder.RenameTable(
                name: "WithdrawalRequest",
                newName: "WithdrawalRequests");

            migrationBuilder.RenameIndex(
                name: "IX_WithdrawalRequest_ProviderId",
                table: "WithdrawalRequests",
                newName: "IX_WithdrawalRequests_ProviderId");

            migrationBuilder.RenameIndex(
                name: "IX_WithdrawalRequest_ProcessedByAdminId",
                table: "WithdrawalRequests",
                newName: "IX_WithdrawalRequests_ProcessedByAdminId");

            migrationBuilder.RenameIndex(
                name: "IX_WithdrawalRequest_BankAccountId",
                table: "WithdrawalRequests",
                newName: "IX_WithdrawalRequests_BankAccountId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WithdrawalRequests",
                table: "WithdrawalRequests",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequests_BankAccounts_BankAccountId",
                table: "WithdrawalRequests",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequests_Users_ProcessedByAdminId",
                table: "WithdrawalRequests",
                column: "ProcessedByAdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequests_Users_ProviderId",
                table: "WithdrawalRequests",
                column: "ProviderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequests_BankAccounts_BankAccountId",
                table: "WithdrawalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequests_Users_ProcessedByAdminId",
                table: "WithdrawalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequests_Users_ProviderId",
                table: "WithdrawalRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WithdrawalRequests",
                table: "WithdrawalRequests");

            migrationBuilder.RenameTable(
                name: "WithdrawalRequests",
                newName: "WithdrawalRequest");

            migrationBuilder.RenameIndex(
                name: "IX_WithdrawalRequests_ProviderId",
                table: "WithdrawalRequest",
                newName: "IX_WithdrawalRequest_ProviderId");

            migrationBuilder.RenameIndex(
                name: "IX_WithdrawalRequests_ProcessedByAdminId",
                table: "WithdrawalRequest",
                newName: "IX_WithdrawalRequest_ProcessedByAdminId");

            migrationBuilder.RenameIndex(
                name: "IX_WithdrawalRequests_BankAccountId",
                table: "WithdrawalRequest",
                newName: "IX_WithdrawalRequest_BankAccountId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WithdrawalRequest",
                table: "WithdrawalRequest",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_BankAccounts_BankAccountId",
                table: "WithdrawalRequest",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_Users_ProcessedByAdminId",
                table: "WithdrawalRequest",
                column: "ProcessedByAdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequest_Users_ProviderId",
                table: "WithdrawalRequest",
                column: "ProviderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
