using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueResolutionAndUpdateViolationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IssueResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ViolationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerFineAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ProviderCompensationAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ResolutionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    ResolutionStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueResolutions_RentalViolations_ViolationId",
                        column: x => x.ViolationId,
                        principalTable: "RentalViolations",
                        principalColumn: "ViolationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IssueResolutions_Users_ProcessedByAdminId",
                        column: x => x.ProcessedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssueResolutions_ProcessedByAdminId",
                table: "IssueResolutions",
                column: "ProcessedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueResolutions_ViolationId",
                table: "IssueResolutions",
                column: "ViolationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueResolutions");
        }
    }
}
