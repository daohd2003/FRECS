using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RenameRentalDaysDiscountToItemRentalCountDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RentalDaysDiscountPercent",
                table: "Orders",
                newName: "ItemRentalCountDiscountPercent");

            migrationBuilder.RenameColumn(
                name: "RentalDaysDiscount",
                table: "Orders",
                newName: "ItemRentalCountDiscount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ItemRentalCountDiscountPercent",
                table: "Orders",
                newName: "RentalDaysDiscountPercent");

            migrationBuilder.RenameColumn(
                name: "ItemRentalCountDiscount",
                table: "Orders",
                newName: "RentalDaysDiscount");
        }
    }
}
