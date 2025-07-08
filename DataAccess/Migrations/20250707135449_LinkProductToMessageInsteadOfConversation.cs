using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class LinkProductToMessageInsteadOfConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Products_ProductId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ProductId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "Conversations");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ProductId",
                table: "Messages",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Products_ProductId",
                table: "Messages",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Products_ProductId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ProductId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "Messages");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ProductId",
                table: "Conversations",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Products_ProductId",
                table: "Conversations",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");
        }
    }
}
