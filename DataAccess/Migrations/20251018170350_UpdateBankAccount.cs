using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBankAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccounts_Users_ProviderId",
                table: "BankAccounts");

            migrationBuilder.RenameColumn(
                name: "ProviderId",
                table: "BankAccounts",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_BankAccounts_ProviderId",
                table: "BankAccounts",
                newName: "IX_BankAccounts_UserId");

            migrationBuilder.AddColumn<string>(
                name: "AccountHolderName",
                table: "BankAccounts",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_Users_UserId",
                table: "BankAccounts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccounts_Users_UserId",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "AccountHolderName",
                table: "BankAccounts");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "BankAccounts",
                newName: "ProviderId");

            migrationBuilder.RenameIndex(
                name: "IX_BankAccounts_UserId",
                table: "BankAccounts",
                newName: "IX_BankAccounts_ProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_Users_ProviderId",
                table: "BankAccounts",
                column: "ProviderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
