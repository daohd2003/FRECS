using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTryOnImageTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TryOnImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CloudinaryPublicId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PersonImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GarmentImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TryOnImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TryOnImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TryOnImages_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TryOnImages_CustomerId",
                table: "TryOnImages",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_TryOnImages_ExpiresAt",
                table: "TryOnImages",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_TryOnImages_IsDeleted_ExpiresAt",
                table: "TryOnImages",
                columns: new[] { "IsDeleted", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TryOnImages_ProductId",
                table: "TryOnImages",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TryOnImages");
        }
    }
}
