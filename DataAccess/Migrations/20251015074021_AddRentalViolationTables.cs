using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalViolationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RentalViolations",
                columns: table => new
                {
                    ViolationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ViolationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DamagePercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    PenaltyPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CustomerResponseAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalViolations", x => x.ViolationId);
                    table.ForeignKey(
                        name: "FK_RentalViolations_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RentalViolationImages",
                columns: table => new
                {
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ViolationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalViolationImages", x => x.ImageId);
                    table.ForeignKey(
                        name: "FK_RentalViolationImages_RentalViolations_ViolationId",
                        column: x => x.ViolationId,
                        principalTable: "RentalViolations",
                        principalColumn: "ViolationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RentalViolationImages_ViolationId",
                table: "RentalViolationImages",
                column: "ViolationId");

            migrationBuilder.CreateIndex(
                name: "IX_RentalViolations_OrderItemId",
                table: "RentalViolations",
                column: "OrderItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RentalViolationImages");

            migrationBuilder.DropTable(
                name: "RentalViolations");
        }
    }
}
