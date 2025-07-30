using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ReporteeId",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "AdminResponse",
                table: "Reports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedAdminId",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Reports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_AssignedAdminId",
                table: "Reports",
                column: "AssignedAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Users_AssignedAdminId",
                table: "Reports",
                column: "AssignedAdminId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Users_AssignedAdminId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_AssignedAdminId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AdminResponse",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AssignedAdminId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Reports");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReporteeId",
                table: "Reports",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
