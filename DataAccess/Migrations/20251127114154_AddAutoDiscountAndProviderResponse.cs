using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoDiscountAndProviderResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderResponseAt",
                table: "RentalViolations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderResponseToCustomer",
                table: "RentalViolations",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyDiscount",
                table: "Orders",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyDiscountPercent",
                table: "Orders",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RentalDaysDiscount",
                table: "Orders",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RentalDaysDiscountPercent",
                table: "Orders",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderResponseAt",
                table: "RentalViolations");

            migrationBuilder.DropColumn(
                name: "ProviderResponseToCustomer",
                table: "RentalViolations");

            migrationBuilder.DropColumn(
                name: "LoyaltyDiscount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "LoyaltyDiscountPercent",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RentalDaysDiscount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RentalDaysDiscountPercent",
                table: "Orders");
        }
    }
}
