using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTransactionReferenceCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundMethod",
                table: "DepositRefunds");

            migrationBuilder.DropColumn(
                name: "RefundReferenceCode",
                table: "DepositRefunds");

            migrationBuilder.AddColumn<Guid>(
                name: "RefundBankAccountId",
                table: "DepositRefunds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepositRefunds_RefundBankAccountId",
                table: "DepositRefunds",
                column: "RefundBankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_DepositRefunds_BankAccounts_RefundBankAccountId",
                table: "DepositRefunds",
                column: "RefundBankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DepositRefunds_BankAccounts_RefundBankAccountId",
                table: "DepositRefunds");

            migrationBuilder.DropIndex(
                name: "IX_DepositRefunds_RefundBankAccountId",
                table: "DepositRefunds");

            migrationBuilder.DropColumn(
                name: "RefundBankAccountId",
                table: "DepositRefunds");

            migrationBuilder.AddColumn<string>(
                name: "RefundMethod",
                table: "DepositRefunds",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundReferenceCode",
                table: "DepositRefunds",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
