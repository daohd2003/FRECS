using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Products_ProductId",
                table: "Feedbacks");

            migrationBuilder.AddColumn<decimal>(
                name: "AverageRating",
                table: "Products",
                type: "decimal(2,1)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RatingCount",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "Feedbacks",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "Feedbacks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderItemId",
                table: "Feedbacks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                table: "Feedbacks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Feedbacks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_OrderId",
                table: "Feedbacks",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_OrderItemId",
                table: "Feedbacks",
                column: "OrderItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_OrderItems_OrderItemId",
                table: "Feedbacks",
                column: "OrderItemId",
                principalTable: "OrderItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Orders_OrderId",
                table: "Feedbacks",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Products_ProductId",
                table: "Feedbacks",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_OrderItems_OrderItemId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Orders_OrderId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Products_ProductId",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_OrderId",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_OrderItemId",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RatingCount",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "OrderItemId",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Feedbacks");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "Feedbacks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Products_ProductId",
                table: "Feedbacks",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
