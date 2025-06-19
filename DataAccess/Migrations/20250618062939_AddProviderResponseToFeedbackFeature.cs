using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderResponseToFeedbackFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderResponse",
                table: "Feedbacks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderResponseAt",
                table: "Feedbacks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderResponseById",
                table: "Feedbacks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_ProviderResponseById",
                table: "Feedbacks",
                column: "ProviderResponseById");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Users_ProviderResponseById",
                table: "Feedbacks",
                column: "ProviderResponseById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Users_ProviderResponseById",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_ProviderResponseById",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "ProviderResponse",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "ProviderResponseAt",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "ProviderResponseById",
                table: "Feedbacks");
        }
    }
}
