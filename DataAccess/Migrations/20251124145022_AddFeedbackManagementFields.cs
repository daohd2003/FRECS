using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackManagementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BlockedAt",
                table: "Feedbacks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BlockedById",
                table: "Feedbacks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                table: "Feedbacks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFlagged",
                table: "Feedbacks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisible",
                table: "Feedbacks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ViolationReason",
                table: "Feedbacks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_BlockedById",
                table: "Feedbacks",
                column: "BlockedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Users_BlockedById",
                table: "Feedbacks",
                column: "BlockedById",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Users_BlockedById",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_BlockedById",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "BlockedAt",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "BlockedById",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "IsFlagged",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "IsVisible",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "ViolationReason",
                table: "Feedbacks");
        }
    }
}
