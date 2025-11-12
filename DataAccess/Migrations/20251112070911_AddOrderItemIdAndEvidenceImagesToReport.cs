using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemIdAndEvidenceImagesToReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceImages",
                table: "Reports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderItemId",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_OrderItemId",
                table: "Reports",
                column: "OrderItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_OrderItems_OrderItemId",
                table: "Reports",
                column: "OrderItemId",
                principalTable: "OrderItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reports_OrderItems_OrderItemId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_OrderItemId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "EvidenceImages",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "OrderItemId",
                table: "Reports");
        }
    }
}
